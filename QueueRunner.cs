
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BiliLearn.CSharp.Plugin.Models;
using BiliLearn.CSharp.Plugin.Orchestrator;
using BiliLearn.CSharp.Plugin.Services;
using Microsoft.Extensions.Logging;

namespace BiliLearn.CSharp.Plugin;

/// <summary>
/// 队列调度核心：预下载并行 + 分析串行，支持批量入队、结构化状态、取消
/// </summary>
public class QueueRunner : IDisposable
{
    private readonly object _lock = new();
    private readonly List<VideoStatus> _queue = new();
    private readonly DownloadStage _downloadStage;
    private readonly ILogger _logger;
    private readonly Func<string, CancellationToken, Task<ProcessingResult>> _analyzeFunc;
    private readonly Func<string, Task>? _pokeFunc;
    private readonly SemaphoreSlim _analyzeSemaphore = new(1, 1);
    private readonly CancellationTokenSource _cts = new();
    private Task? _loopTask;
    private bool _running;
    private const int MaxQueued = 5;

    /// <summary>当前所有视频状态快照</summary>
    public IReadOnlyList<VideoStatus> Snapshot
    {
        get
        {
            lock (_lock)
            {
                return _queue.ToList();
            }
        }
    }

    public QueueRunner(
        DownloadStage downloadStage,
        ILogger logger,
        Func<string, CancellationToken, Task<ProcessingResult>> analyzeFunc,
        Func<string, Task>? pokeFunc = null)
    {
        _downloadStage = downloadStage;
        _logger = logger;
        _analyzeFunc = analyzeFunc;
        _pokeFunc = pokeFunc;
    }

    /// <summary>启动后台调度循环</summary>
    public void Start()
    {
        if (_running) return;
        _running = true;
        _loopTask = Task.Run(LoopAsync);
        _logger.LogInformation("[QueueRunner] 队列循环已启动");
    }

    private async Task LoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                // 1. 取一个排队中的 -> 交下载
                VideoStatus? toDownload = null;
                lock (_lock)
                {
                    toDownload = _queue.FirstOrDefault(v => v.Stage == VideoStage.Queued);
                    if (toDownload != null)
                        toDownload.Stage = VideoStage.Downloading;
                }

                if (toDownload != null)
                {
                    await _downloadStage.DownloadAsync(toDownload.Bvid,
                        p => { toDownload.Progress = p; return Task.CompletedTask; },
                        _cts.Token)
                        .ContinueWith(t =>
                        {
                            var r = t.IsCompletedSuccessfully ? t.Result : null;
                            lock (_lock)
                            {
                                if (r != null && r.Success)
                                {
                                    toDownload.Stage = VideoStage.Downloaded;
                                    toDownload.Progress = 100;
                                    toDownload.Error = null;
                                }
                                else if (r != null && r.Canceled)
                                {
                                    toDownload.Stage = VideoStage.Canceled;
                                    _queue.Remove(toDownload);
                                }
                                else
                                {
                                    toDownload.Stage = VideoStage.Failed;
                                    toDownload.Error = r?.Message ?? "下载失败";
                                }
                            }
                            PokeStatus();
                        }, TaskScheduler.Default);
                }

                // 2. 等待分析信号量，串行分析
                var toAnalyze = GetNextForAnalysis();
                if (toAnalyze != null)
                {
                    await _analyzeSemaphore.WaitAsync();
                    try
                    {
                        toAnalyze.Stage = VideoStage.Analyzing;
                        PokeStatus();
                        var result = await _analyzeFunc(toAnalyze.Bvid, _cts.Token);
                        lock (_lock)
                        {
                            if (result.Success)
                            {
                                toAnalyze.Stage = VideoStage.Completed;
                                toAnalyze.Progress = 100;
                                toAnalyze.Error = null;
                            }
                            else if (_cts.IsCancellationRequested)
                            {
                                toAnalyze.Stage = VideoStage.Canceled;
                                _queue.Remove(toAnalyze);
                            }
                            else
                            {
                                toAnalyze.Stage = VideoStage.Failed;
                                toAnalyze.Error = result.Message;
                            }
                        }
                        PokeStatus();
                    }
                    finally
                    {
                        _analyzeSemaphore.Release();
                    }
                }

                if (NoPendingWork())
                {
                    await Task.Delay(2000, _cts.Token);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[QueueRunner] 循环异常");
                try { await Task.Delay(2000, _cts.Token); } catch { break; }
            }
        }
    }

    private VideoStatus? GetNextForAnalysis()
    {
        lock (_lock)
        {
            return _queue.FirstOrDefault(v => v.Stage == VideoStage.Downloaded);
        }
    }

    private bool NoPendingWork()
    {
        lock (_lock)
        {
            return !_queue.Any(v => v.Stage == VideoStage.Queued || v.Stage == VideoStage.Downloading || v.Stage == VideoStage.Downloaded || v.Stage == VideoStage.Analyzing);
        }
    }

    /// <summary>入队（已去重检查）</summary>
    /// <returns>0=成功, 1=已在队列中, 2=队列已满, 3=已在其他阶段</returns>
    public int Enqueue(string bvid, string? title = null)
    {
        lock (_lock)
        {
            var existing = _queue.FirstOrDefault(v => v.Bvid == bvid);
            if (existing != null)
                return existing.Stage == VideoStage.Completed || existing.Stage == VideoStage.Failed || existing.Stage == VideoStage.Canceled ? 3 : 1;

            var queuedCount = _queue.Count(v => v.Stage == VideoStage.Queued);
            if (queuedCount >= MaxQueued)
                return 2;

            _queue.Add(new VideoStatus
            {
                Bvid = bvid,
                Title = title,
                Stage = VideoStage.Queued,
                QueuedAt = DateTime.Now,
                Progress = 0
            });
            PokeStatus();
            return 0;
        }
    }

    /// <summary>批量入队</summary>
    public (int success, int skipped) EnqueueBatch(IEnumerable<string> bvids)
    {
        int success = 0, skipped = 0;
        foreach (var bvid in bvids)
        {
            var r = Enqueue(bvid);
            if (r == 0) success++; else skipped++;
        }
        PokeStatus();
        return (success, skipped);
    }

    /// <summary>取消（分析中CTS取消 / 排队下载中移除）</summary>
    public bool Cancel(string bvid)
    {
        lock (_lock)
        {
            var item = _queue.FirstOrDefault(v => v.Bvid == bvid);
            if (item == null) return false;

            if (item.Stage == VideoStage.Analyzing)
            {
                item.Stage = VideoStage.Canceled;
                _queue.Remove(item);
                return true;
            }

            if (item.Stage == VideoStage.Queued || item.Stage == VideoStage.Downloading)
            {
                item.Stage = VideoStage.Canceled;
                _queue.Remove(item);
                return true;
            }

            return false;
        }
    }

    private void PokeStatus()
    {
        var snap = Snapshot;
        if (snap.Count == 0) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"📋 B站学习队列（共{snap.Count}个）");
        int idx = 1;
        foreach (var v in snap.Take(5))
        {
            string icon = v.Stage switch
            {
                VideoStage.Queued => "⏳",
                VideoStage.Downloading => "⬇",
                VideoStage.Downloaded => "⬇️",
                VideoStage.Analyzing => "▶",
                VideoStage.Completed => "✅",
                VideoStage.Failed => "❌",
                VideoStage.Canceled => "🚫",
                _ => "❓"
            };
            string detail = v.Stage switch
            {
                VideoStage.Queued => "排队中",
                VideoStage.Downloading => $"下载中 {v.Progress}%",
                VideoStage.Downloaded => "等待分析",
                VideoStage.Analyzing => $"分析中 {v.Progress}%",
                VideoStage.Completed => "完成",
                VideoStage.Failed => $"失败: {v.Error ?? "未知"}",
                VideoStage.Canceled => "已取消",
                _ => ""
            };
            sb.AppendLine($"{icon} ({idx++}/{snap.Count}) {v.Bvid} 【{v.Title ?? "未命名"}】 {detail}");
        }
        int done = snap.Count(v => v.Stage == VideoStage.Completed);
        int failed = snap.Count(v => v.Stage == VideoStage.Failed);
        sb.AppendLine($"✅ 完成{done} | ❌ 失败{failed} | 剩余{snap.Count - done - failed}");

        _pokeFunc?.Invoke(sb.ToString());
    }

    public void Dispose()
    {
        _running = false;
        _cts.Cancel();
        _loopTask?.Wait(2000);
        _cts.Dispose();
        _analyzeSemaphore.Dispose();
        _downloadStage.Dispose();
    }
}


using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BiliLearn.CSharp.Plugin.Models;
using Microsoft.Extensions.Logging;

namespace BiliLearn.CSharp.Plugin.Capabilities.Learn;

/// <summary>
/// 学习队列实现（迁绑自 V1 QueueRunner：预下载并行 + 分析串行）
/// </summary>
public class LearnQueue : ILearnQueue
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

    public LearnQueue(
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
        _logger.LogInformation("[LearnQueue] 队列循环已启动");
    }

    /// <summary>停止调度循环</summary>
    public void Stop()
    {
        _cts.Cancel();
        _running = false;
        _logger.LogInformation("[LearnQueue] 队列循环已停止");
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
                _logger.LogError(ex, "[LearnQueue] 循环异常");
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

            switch (item.Stage)
            {
                case VideoStage.Queued:
                    _queue.Remove(item);
                    PokeStatus();
                    return true;
                case VideoStage.Downloading:
                case VideoStage.Analyzing:
                    // 标记为取消，循环中会处理（下载/分析中的会在完成后判断Canceled）
                    // 无法中断正在进行的下载/分析，但标记后会在结束时移除
                    item.Stage = VideoStage.Canceled;
                    PokeStatus();
                    // 尝试取消当前分析（如果正在等待）
                    return true;
                default:
                    return false; // Completed/Failed/Downloaded 不可取消
            }
        }
    }

    /// <summary>取消所有</summary>
    public void CancelAll()
    {
        lock (_lock)
        {
            var active = _queue.Where(v => v.Stage == VideoStage.Queued || v.Stage == VideoStage.Downloading || v.Stage == VideoStage.Downloaded).ToList();
            foreach (var item in active)
            {
                _queue.Remove(item);
            }
            // 分析中的标记取消，循环结束后移除
            var analyzing = _queue.FirstOrDefault(v => v.Stage == VideoStage.Analyzing);
            if (analyzing != null)
                analyzing.Stage = VideoStage.Canceled;
        }
        PokeStatus();
    }

    private void PokeStatus()
    {
        try
        {
            _pokeFunc?.Invoke(FormatStatus());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[LearnQueue] Poke 失败");
        }
    }

    private string FormatStatus()
    {
        lock (_lock)
        {
            if (_queue.Count == 0) return "📋 B站学习队列为空";
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"📋 B站学习队列（共{_queue.Count}个）");
            foreach (var v in _queue)
            {
                string icon = v.Stage switch
                {
                    VideoStage.Queued => "⏳",
                    VideoStage.Downloading => "⬇",
                    VideoStage.Downloaded => "⏸",
                    VideoStage.Analyzing => "▶",
                    VideoStage.Completed => "✅",
                    VideoStage.Failed => "❌",
                    VideoStage.Canceled => "🚫",
                    _ => "❓"
                };
                sb.AppendLine($"{icon} {v.Bvid} {v.Title ?? ""} [{v.Stage}] {v.Progress}%");
            }
            return sb.ToString();
        }
    }

    public void Dispose()
    {
        Stop();
        _cts.Dispose();
        _analyzeSemaphore.Dispose();
    }
}

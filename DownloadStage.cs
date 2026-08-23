
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BiliLearn.CSharp.Plugin.Domain.Interfaces;
using BiliLearn.CSharp.Plugin.Models;
using BiliLearn.CSharp.Plugin.Services;
using Microsoft.Extensions.Logging;

namespace BiliLearn.CSharp.Plugin;

/// <summary>
/// 下载阶段管理器：控制并行下载数（默认2），自含取地址+落盘+状态流转+风控退避
/// </summary>
public class DownloadStage : IDisposable
{
    private readonly IBilibiliFetcher _biliApi;
    private readonly MediaDownloader _downloader;
    private readonly ILogger _logger;
    private readonly string _tempDir;
    private readonly SemaphoreSlim _downloadSemaphore;

    /// <summary>并行下载并发数</summary>
    public int MaxConcurrentDownloads { get; }

    public DownloadStage(
        IBilibiliFetcher biliApi,
        MediaDownloader downloader,
        ILogger logger,
        string workDir,
        int maxConcurrent = 2)
    {
        _biliApi = biliApi;
        _downloader = downloader;
        _logger = logger;
        _tempDir = Path.Combine(workDir, "temp");
        Directory.CreateDirectory(_tempDir);
        MaxConcurrentDownloads = maxConcurrent;
        _downloadSemaphore = new SemaphoreSlim(maxConcurrent, maxConcurrent);
    }

    /// <summary>
    /// 下载视频和音频（并行受控），返回下载结果
    /// </summary>
    /// <param name="bvid">BV号</param>
    /// <param name="onProgress">进度回调（percent 0-100）</param>
    /// <param name="ct">取消令牌</param>
    public async Task<VideoDownloadResult> DownloadAsync(
        string bvid,
        Func<int, Task>? onProgress = null,
        CancellationToken ct = default)
    {
        var result = new VideoDownloadResult { Bvid = bvid };

        await _downloadSemaphore.WaitAsync(ct);
        try
        {
            ct.ThrowIfCancellationRequested();
            _logger.LogInformation("[BiliLearn][DownloadStage] 开始下载: {Bvid}", bvid);

            // 1. 获取视频信息（含播放地址）
            var infoResult = await _biliApi.GetVideoInfoAsync(bvid, ct);
            if (!infoResult.Success || infoResult.Data == null)
            {
                result.Success = false;
                result.Message = infoResult.Message;
                return result;
            }

            var info = infoResult.Data;
            result.Title = info.Title;
            result.DurationSeconds = info.DurationSeconds;

            // 2. 并行下载视频+音频（内部两个任务各自独立失败不互相阻塞）
            var videoPath = Path.Combine(_tempDir, $"{bvid}_video.mp4");
            var audioPath = Path.Combine(_tempDir, $"{bvid}_audio.m4a");

            var tasks = new System.Collections.Generic.List<Task>();

            if (!string.IsNullOrEmpty(info.VideoUrl))
            {
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        result.VideoFilePath = await _downloader.DownloadFileAsync(info.VideoUrl, videoPath, ct);
                        _logger.LogInformation("[BiliLearn][DownloadStage] 视频下载完成: {Path}", result.VideoFilePath);
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[BiliLearn][DownloadStage] 视频下载失败");
                        result.VideoFilePath = null;
                        result.VideoError = ex.Message;
                    }
                }, ct));
            }
            else
            {
                result.VideoError = "未获取到视频流地址";
            }

            if (!string.IsNullOrEmpty(info.AudioUrl))
            {
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        result.AudioFilePath = await _downloader.DownloadFileAsync(info.AudioUrl, audioPath, ct);
                        _logger.LogInformation("[BiliLearn][DownloadStage] 音频下载完成: {Path}", result.AudioFilePath);
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[BiliLearn][DownloadStage] 音频下载失败");
                        result.AudioFilePath = null;
                        result.AudioError = ex.Message;
                    }
                }, ct));
            }
            else
            {
                result.AudioError = "未获取到音频流地址";
            }

            await Task.WhenAll(tasks);

            // 3. 判定结果
            result.Success = result.VideoFilePath != null || result.AudioFilePath != null;
            if (result.Success)
                result.Message = "下载完成";
            else
                result.Message = result.VideoError ?? result.AudioError ?? "下载失败";

            if (onProgress != null)
                await onProgress(100);

            _logger.LogInformation("[BiliLearn][DownloadStage] 下载结束: {Bvid} 成功={Success}", bvid, result.Success);
            return result;
        }
        catch (OperationCanceledException)
        {
            result.Success = false;
            result.Message = "已取消";
            result.Canceled = true;
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = ex.Message;
            _logger.LogError(ex, "[BiliLearn][DownloadStage] 下载异常: {Bvid}", bvid);
            return result;
        }
        finally
        {
            _downloadSemaphore.Release();
        }
    }

    public void Dispose()
    {
        _downloadSemaphore?.Dispose();
    }
}

/// <summary>视频下载结果</summary>
public class VideoDownloadResult
{
    public string Bvid { get; set; } = "";
    public bool Success { get; set; }
    public bool Canceled { get; set; }
    public string Message { get; set; } = "";
    public string? Title { get; set; }
    public int DurationSeconds { get; set; }
    public string? VideoFilePath { get; set; }
    public string? AudioFilePath { get; set; }
    public string? VideoError { get; set; }
    public string? AudioError { get; set; }
}

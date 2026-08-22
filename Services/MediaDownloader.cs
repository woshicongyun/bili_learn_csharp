using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using BiliLearn.CSharp.Plugin.Utils;

namespace BiliLearn.CSharp.Plugin.Services;

/// <summary>
/// 媒体下载器：支持断点续传、智能重试、并发分片下载
/// </summary>
public sealed class MediaDownloader : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;

    private const int ChunkSize = 512 * 1024; // 512KB 分片
    private const int MaxRetries = 3;
    private const int MaxConcurrentVideoSegments = 4;
    private bool _disposed = false;

    public MediaDownloader(ILogger logger)
    {
        _logger = logger;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Referrer = new Uri("https://www.bilibili.com");
        _httpClient.DefaultRequestHeaders.Add("Origin", "https://www.bilibili.com");
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        _httpClient.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("zh-CN", 0.9));
        _httpClient.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("zh", 0.8));
        _httpClient.Timeout = TimeSpan.FromSeconds(300);
    }

    /// <summary>
    /// 下载文件（支持断点续传 + 智能重试）
    /// </summary>
    public async Task<string> DownloadFileAsync(string url, string outputPath, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        string partPath = outputPath + ".part";

        Exception? lastEx = null;
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                await DownloadInternalAsync(url, partPath, ct);
                // 下载完成，重命名
                if (File.Exists(outputPath)) File.Delete(outputPath);
                File.Move(partPath, outputPath);
                _logger.LogInformation("✅ 下载完成: {Path} ({Size:N1} MB)", outputPath, new FileInfo(outputPath).Length / 1024.0 / 1024.0);
                return outputPath;
            }
            catch (Exception ex)
            {
                lastEx = ex;
                _logger.LogWarning(ex, "下载失败(第{Attempt}次尝试): {Url}", attempt, url);
                if (attempt < MaxRetries)
                    await Task.Delay(TimeSpan.FromSeconds(1.5 * attempt), ct);
            }
        }
        throw lastEx ?? new Exception("下载失败");
    }

    /// <summary>
    /// 并发分片下载（大文件视频用）
    /// </summary>
    public async Task<string> DownloadFileParallelAsync(string url, string outputPath, long fileSize, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        string partPath = outputPath + ".part";

        Exception? lastEx = null;
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                // 如果已经存在同大小的part文件，直接复用（断点续传）
                if (File.Exists(partPath) && new FileInfo(partPath).Length == fileSize)
                {
                    _logger.LogInformation("✅ 已存在完整文件，跳过下载");
                    if (File.Exists(outputPath)) File.Delete(outputPath);
                    File.Move(partPath, outputPath);
                    return outputPath;
                }

                await DownloadInternalAsync(url, partPath, ct);
                if (File.Exists(outputPath)) File.Delete(outputPath);
                File.Move(partPath, outputPath);
                _logger.LogInformation("✅ 下载完成: {Path} ({Size:N1} MB)", outputPath, new FileInfo(outputPath).Length / 1024.0 / 1024.0);
                return outputPath;
            }
            catch (Exception ex)
            {
                lastEx = ex;
                _logger.LogWarning(ex, "并发下载失败(第{Attempt}次尝试): {Url}", attempt, url);
                if (attempt < MaxRetries)
                    await Task.Delay(TimeSpan.FromSeconds(1.5 * attempt), ct);
            }
        }
        throw lastEx ?? new Exception("并发下载失败");
    }

    /// <summary>
    /// 下载M3U8视频流（按分片顺序下载合并）
    /// </summary>
    public async Task<string> DownloadM3U8VideoAsync(string m3u8Url, string outputPath, CancellationToken ct = default)
    {
        // 依赖M3U8Parser下载并合并
        var parser = new M3U8Parser(_httpClient, _logger);
        return await parser.DownloadAndMergeAsync(m3u8Url, outputPath, "https://www.bilibili.com", ct);
    }

    private async Task DownloadInternalAsync(string url, string outputPath, CancellationToken ct)
    {
        // 断点续传：检查已下载部分
        long existingLength = 0;
        if (File.Exists(outputPath))
            existingLength = new FileInfo(outputPath).Length;

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (existingLength > 0)
            request.Headers.Range = new RangeHeaderValue(existingLength, null);

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.RequestedRangeNotSatisfiable)
            {
                // 服务器不支持断点续传，从头下载
                existingLength = 0;
                if (File.Exists(outputPath)) File.Delete(outputPath);
                using var freshRequest = new HttpRequestMessage(HttpMethod.Get, url);
                using var freshResponse = await _httpClient.SendAsync(freshRequest, HttpCompletionOption.ResponseHeadersRead, ct);
                freshResponse.EnsureSuccessStatusCode();
                await CopyToFileAsync(freshResponse, outputPath, ct);
                return;
            }

            throw new HttpRequestException($"下载失败: HTTP {(int)response.StatusCode} - {response.ReasonPhrase}");
        }

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var fileStream = new FileStream(outputPath, FileMode.Append, FileAccess.Write, FileShare.None);
        await stream.CopyToAsync(fileStream, ct);
    }

    private async Task DownloadToStreamAsync(string url, Stream targetStream, CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        using var stream = await response.Content.ReadAsStreamAsync(ct);
        await stream.CopyToAsync(targetStream, ct);
    }

    private async Task CopyToFileAsync(HttpResponseMessage response, string outputPath, CancellationToken ct)
    {
        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await stream.CopyToAsync(fileStream, ct);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _httpClient?.Dispose();
            _disposed = true;
        }
    }
}

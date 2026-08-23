
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace BiliLearn.CSharp.Plugin.Utils;

/// <summary>
/// M3U8 解析与分片下载工具
/// 处理B站高清视频（大会员/高码率）返回的M3U8索引文件
/// </summary>
public class M3U8Parser
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;

    public M3U8Parser(HttpClient httpClient, ILogger logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// 解析M3U8内容获取所有.ts分片URL
    /// </summary>
    public List<string> ParsePlaylist(string m3u8Content, string baseUrl)
    {
        var segments = new List<string>();
        var lines = m3u8Content.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim()).ToList();

        // 确认是M3U8
        if (!lines.Any(l => l.StartsWith("#EXTM3U")))
        {
            _logger.LogWarning("不是有效的M3U8文件");
            return segments;
        }

        bool isEncrypted = false;
        string? keyUri = null;

        foreach (var line in lines)
        {
            if (line.StartsWith("#EXT-X-KEY"))
            {
                // 可能有AES-128加密
                var uriPart = line.Split(',').FirstOrDefault(p => p.Trim().StartsWith("URI="));
                if (uriPart != null)
                {
                    keyUri = uriPart.Replace("URI=", "").Trim('"');
                    isEncrypted = true;
                    _logger.LogWarning("M3U8带AES加密（{KeyUri}），暂不支持加密视频", keyUri);
                    return segments;
                }
            }
            else if (!line.StartsWith("#") && !string.IsNullOrWhiteSpace(line))
            {
                string fullUrl = line;
                // .ts分片URL可能是相对路径
                if (!line.StartsWith("http"))
                {
                    fullUrl = ResolveRelativeUrl(baseUrl, line);
                }
                segments.Add(fullUrl);
            }
        }

        _logger.LogInformation("M3U8解析: {Count}个分片 (加密: {IsEncrypted})", segments.Count, isEncrypted);
        return segments;
    }

    /// <summary>
    /// 并发下载所有分片并按顺序合并
    /// </summary>
    public async Task<string> DownloadAndMergeAsync(string m3u8Url, string outputPath, string referer, CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(outputPath)!;
        Directory.CreateDirectory(dir);

        // 1. 下载M3U8索引
        var m3u8Content = await DownloadWithRetryAsync(m3u8Url, referer, ct);
        if (string.IsNullOrEmpty(m3u8Content))
            throw new InvalidOperationException("M3U8索引下载为空");

        // 2. 解析分片
        var segments = ParsePlaylist(m3u8Content, m3u8Url);
        if (segments.Count == 0)
        {
            throw new InvalidOperationException("M3U8中没有解析到分片");
        }

        // 3. 并发下载分片（限制并发数）
        const int maxConcurrent = 8;
        using var semaphore = new SemaphoreSlim(maxConcurrent);

        var segmentPaths = new string[segments.Count];
        var tasks = new Task[segments.Count];
        for (int i = 0; i < segments.Count; i++)
        {
            int idx = i;
            tasks[i] = Task.Run(async () =>
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    var tmpPath = Path.Combine(dir, $"seg_{idx:D4}.ts");
                    await DownloadWithRetryAsync(segments[idx], referer, ct, tmpPath);
                    segmentPaths[idx] = tmpPath;
                }
                finally { semaphore.Release(); }
            }, ct);
        }
        await Task.WhenAll(tasks);

        // 4. 按顺序合并
        await using var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        foreach (var segPath in segmentPaths)
        {
            if (string.IsNullOrEmpty(segPath)) continue;
            await using var segStream = File.OpenRead(segPath);
            await segStream.CopyToAsync(output, ct);
            File.Delete(segPath); // 合并后删除临时分片
        }

        _logger.LogInformation("M3U8下载完成: {Count}分片 → {Path}", segments.Count, outputPath);
        return outputPath;
    }

    private async Task<string?> DownloadWithRetryAsync(string url, string referer, CancellationToken ct, string? outputPath = null)
    {
        const int maxRetries = 3;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (!string.IsNullOrEmpty(referer))
                    request.Headers.Referrer = new Uri(referer);
                request.Headers.UserAgent.ParseAdd(
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");

                using var resp = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                resp.EnsureSuccessStatusCode();

                if (outputPath != null)
                {
                    await using var fs = File.Create(outputPath);
                    await resp.Content.CopyToAsync(fs, ct);
                    return outputPath;
                }

                // 读取到内存（用于M3U8索引）
                return await resp.Content.ReadAsStringAsync(ct);
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                _logger.LogWarning("下载失败(第{Attempt}次): {Url}, 错误: {Err}", attempt, url, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(1.5 * attempt), ct);
            }
        }
        throw new HttpRequestException($"下载失败(重试{maxRetries}次): {url}");
    }

    private static string ResolveRelativeUrl(string baseUrl, string relative)
    {
        var baseUri = new Uri(baseUrl);
        var relativeUri = new Uri(baseUri, relative);
        return relativeUri.ToString();
    }
}

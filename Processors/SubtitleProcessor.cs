using System;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using BiliLearn.CSharp.Plugin.Domain.Interfaces;
using BiliLearn.CSharp.Plugin.Models;
using Microsoft.Extensions.Logging;

namespace BiliLearn.CSharp.Plugin.Processors;

/// <summary>
/// 字幕处理器：解析B站字幕JSON格式
/// </summary>
public class SubtitleProcessor : IMediaAnalyzer
{
    private readonly ILogger _logger;

    public SubtitleProcessor(ILogger logger) => _logger = logger;

    public Task<List<StructuredSubtitle>> ParseSubtitleAsync(string jsonContent)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;
            var items = new List<SubtitleItem>();

            // 格式1: conclusion接口返回的 subtitle[].part_subtitle[] 结构
            if (root.TryGetProperty("subtitle", out var subtitleArr) && subtitleArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var part in subtitleArr.EnumerateArray())
                {
                    if (!part.TryGetProperty("part_subtitle", out var partSubs) || partSubs.ValueKind != JsonValueKind.Array)
                        continue;
                    foreach (var sub in partSubs.EnumerateArray())
                    {
                        items.Add(new SubtitleItem
                        {
                            From = sub.TryGetProperty("start_timestamp", out var st) ? st.GetDouble() : 0,
                            To = sub.TryGetProperty("end_timestamp", out var et) ? et.GetDouble() : 0,
                            Text = sub.TryGetProperty("content", out var content) ? content.GetString() ?? "" : ""
                        });
                    }
                }
                if (items.Count > 0)
                {
                    _logger.LogInformation("✅ 字幕解析完成(conclusion格式): {Count}条", items.Count);
                    return Task.FromResult(new List<StructuredSubtitle> { new StructuredSubtitle { Items = items } });
                }
            }

            // 格式2: player/v2下载的字幕文件 body[] 结构
            if (root.TryGetProperty("body", out var body) && body.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in body.EnumerateArray())
                {
                    items.Add(new SubtitleItem
                    {
                        From = item.TryGetProperty("from", out var from) ? from.GetDouble() : 0,
                        To = item.TryGetProperty("to", out var to) ? to.GetDouble() : 0,
                        Text = item.TryGetProperty("content", out var content) ? content.GetString() ?? "" : ""
                    });
                }
                _logger.LogInformation("✅ 字幕解析完成(body格式): {Count}条", items.Count);
                return Task.FromResult(new List<StructuredSubtitle> { new StructuredSubtitle { Items = items } });
            }

            _logger.LogWarning("字幕解析失败: 未知JSON结构");
            return Task.FromResult(new List<StructuredSubtitle>());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "字幕解析失败");
            return Task.FromResult(new List<StructuredSubtitle>());
        }
    }

    public Task<string?> TranscribeAsync(string audioPath, CancellationToken ct = default)
        => Task.FromResult(null as string);

    public Task<List<FrameDescription>> AnalyzeVisualAsync(string videoPath, string workDir, int durationSeconds, int intervalSeconds, int maxFrames, ILogger logger, CancellationToken ct = default)
        => Task.FromResult(new List<FrameDescription>());

    public void Dispose() { }
}

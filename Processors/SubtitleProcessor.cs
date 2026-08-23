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

            // 方式1: body[] 结构（player/v2 接口）
            if (root.TryGetProperty("body", out var body) && body.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in body.EnumerateArray())
                {
                    items.Add(new SubtitleItem
                    {
                        From = item.TryGetProperty("from", out var from) ? from.GetDouble() : 0,
                        To = item.TryGetProperty("to", out var to) ? to.GetDouble() : 0,
                        Text = item.TryGetProperty("content", out var c1) ? c1.GetString() ?? "" : ""
                    });
                }
            }
            // 方式2: subtitle[].part_subtitle[] 结构（conclusion 接口）
            else if (root.TryGetProperty("subtitle", out var subtitle) && subtitle.ValueKind == JsonValueKind.Array)
            {
                foreach (var part in subtitle.EnumerateArray())
                {
                    if (!part.TryGetProperty("part_subtitle", out var partSub) || partSub.ValueKind != JsonValueKind.Array)
                        continue;
                    foreach (var item in partSub.EnumerateArray())
                    {
                        items.Add(new SubtitleItem
                        {
                            From = item.TryGetProperty("start_timestamp", out var st) ? st.GetDouble() : 0,
                            To = item.TryGetProperty("end_timestamp", out var et) ? et.GetDouble() : 0,
                            Text = item.TryGetProperty("content", out var c2) ? c2.GetString() ?? "" : ""
                        });
                    }
                }
            }
            else
            {
                _logger.LogInformation("字幕数据中没有可识别的结构");
                return Task.FromResult(new List<StructuredSubtitle>());
            }

            _logger.LogInformation("✅ 字幕解析完成: {Count}条", items.Count);
            return Task.FromResult(new List<StructuredSubtitle> { new StructuredSubtitle { Items = items } });
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

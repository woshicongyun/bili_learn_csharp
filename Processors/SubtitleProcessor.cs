
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using BiliLearn.CSharp.Plugin.Models;
using Microsoft.Extensions.Logging;

namespace BiliLearn.CSharp.Plugin.Processors;

/// <summary>
/// 字幕处理器：解析B站字幕JSON格式
/// </summary>
public class SubtitleProcessor
{
    private readonly ILogger _logger;

    public SubtitleProcessor(ILogger logger) => _logger = logger;

    public Task<List<SubtitleItem>> ParseJsonAsync(string jsonContent)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;
            if (!root.TryGetProperty("body", out var body) || body.ValueKind != JsonValueKind.Array)
                return Task.FromResult(new List<SubtitleItem>());

            var result = new List<SubtitleItem>();
            foreach (var item in body.EnumerateArray())
            {
                result.Add(new SubtitleItem
                {
                    From = item.TryGetProperty("from", out var from) ? from.GetDouble() : 0,
                    To = item.TryGetProperty("to", out var to) ? to.GetDouble() : 0,
                    Text = item.TryGetProperty("content", out var content) ? content.GetString() ?? "" : ""
                });
            }
            _logger.LogInformation("✅ 字幕解析完成: {Count}条", result.Count);
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "字幕解析失败");
            return Task.FromResult(new List<SubtitleItem>());
        }
    }

    public string ToPlainText(List<SubtitleItem> items)
    {
        return string.Join(" ", items.Select(i => i.Text));
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BiliLearn.CSharp.Plugin.Models;
using BiliLearn.CSharp.Plugin.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace BiliLearn.CSharp.Plugin.Services;

/// <summary>
/// LLM整合器：将多源信息合并，调用LLM生成结构化总结并归档
/// </summary>
public class LLMIntegrator : IDisposable
{
    private readonly ILLMService _llm;
    private readonly KnowledgeBaseService _knowledgeBase;
    private readonly ILogger _logger;
    private bool _disposed = false;

    public LLMIntegrator(ILLMService llm, KnowledgeBaseService knowledgeBase, ILogger logger)
    {
        _llm = llm;
        _knowledgeBase = knowledgeBase;
        _logger = logger;
    }

    /// <summary>
    /// 生成摘要和分类
    /// </summary>
    public async Task<string> GenerateSummaryAndCategoryAsync(VideoProcessingContext ctx, CancellationToken ct = default)
    {
        try
        {
            var subtitles = ctx.SubtitleItems?.Select(s => s.Text).ToList() ?? new List<string>();
            var frames = ctx.KeyFrameDescriptions ?? new List<FrameDescription>();
            var audioTranscript = ctx.AsrTranscription != null ? new List<string> { ctx.AsrTranscription } : new List<string>();
            var videoTitle = ctx.VideoTitle;

            // 检查信息源
            var sources = new Dictionary<string, bool>
            {
                { "字幕", subtitles.Any() },
                { "视觉", frames.Any() },
                { "语音", audioTranscript.Any() }
            };
            
            var missingSources = sources.Where(s => !s.Value).Select(s => s.Key).ToList();
            var sourceNote = missingSources.Any() 
                ? $"\n⚠️ 缺失信息源: {string.Join(", ", missingSources)}\n" 
                : "";

            var prompt = @$"分析B站视频并生成结构化知识摘要。

视频信息:
- BV号: {ctx.Bvid}
- 标题: {videoTitle}
- 时长: {ctx.DurationSeconds}秒
- UP主: {ctx.UploaderName ?? "未知"}
- 描述: {(ctx.VideoDescription ?? "无")}

{sourceNote}
字幕内容（共{subtitles.Count}条）:
{string.Join("\n", subtitles.Take(50))}

关键帧描述（共{frames.Count}帧）:
{string.Join("\n", frames.Select(f => $"{f.StartTime}s-{f.EndTime}s: {f.Description}").Take(30))}

语音转录（共{audioTranscript.Count}条）:
{string.Join("\n", audioTranscript.Take(50))}

请以JSON格式输出，包含以下字段：
1. summary: 视频核心内容摘要（200字以内）
2. category: 分类（技术教程/游戏/生活/娱乐/学习/其他）
3. tags: 关键词标签列表（5-10个）
4. keyPoints: 关键要点列表（3-5条）

只输出JSON，不要有其他内容。";

            var result = await _llm.ChatAsync(prompt, maxTokens: 2000, ct: ct);
            if (string.IsNullOrEmpty(result))
            {
                _logger.LogWarning("[LLMIntegrator] LLM返回空结果");
                return "";
            }

            // 解析并保存到上下文
            var entry = ParseLLMResult(result, ctx);
            if (entry != null)
            {
                ctx.FinalSummary = entry.Summary;
                ctx.Category = entry.Category;
                ctx.Tags = entry.Tags;
            }

            _logger.LogInformation("[LLMIntegrator] 生成知识条目: {Title}", entry?.Title ?? "解析失败");
            return entry?.Summary ?? "";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[LLMIntegrator] 整合失败");
            return "";
        }
    }

    /// <summary>
    /// 保存到知识库
    /// </summary>
    public async Task<KnowledgeEntry> SaveToKnowledgeBaseAsync(VideoProcessingContext ctx, CancellationToken ct = default)
    {
        try
        {
            var entry = new KnowledgeEntry
            {
                Bvid = ctx.Bvid,
                Title = ctx.VideoTitle ?? "",
                Summary = ctx.FinalSummary ?? "",
                Category = ctx.Category ?? "其他",
                Uploader = ctx.UploaderName,
                Duration = ctx.DurationSeconds,
                Description = ctx.VideoDescription,
                Tags = ctx.Tags ?? new List<string>(),
                CreatedAt = DateTime.Now
            };

            await _knowledgeBase.SaveAsync(entry, ct);
            return entry;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[LLMIntegrator] 保存知识库失败");
            throw;
        }
    }

    private KnowledgeEntry? ParseLLMResult(string json, VideoProcessingContext ctx)
    {
        try
        {
            json = json.Trim();
            if (json.StartsWith("```"))
            {
                json = json.Substring(3);
                if (json.StartsWith("json")) json = json.Substring(4);
                json = json.Trim();
            }
            if (json.EndsWith("```"))
            {
                json = json.Substring(0, json.Length - 3);
                json = json.Trim();
            }

            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            return new KnowledgeEntry
            {
                Bvid = ctx.Bvid,
                Title = ctx.VideoTitle ?? "",
                Summary = root.TryGetProperty("summary", out var s) ? s.GetString() ?? "" : "",
                Category = root.TryGetProperty("category", out var c) ? c.GetString() ?? "其他" : "其他",
                Uploader = ctx.UploaderName,
                Duration = ctx.DurationSeconds,
                Description = ctx.VideoDescription,
                Tags = root.TryGetProperty("tags", out var tags) 
                    ? tags.EnumerateArray().Select(t => t.GetString() ?? "").ToList() 
                    : new List<string>(),
                CreatedAt = DateTime.Now
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[LLMIntegrator] 解析JSON失败");
            return null;
        }
    }
    
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}

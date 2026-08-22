using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BiliLearn.CSharp.Plugin.Domain.Interfaces;
using BiliLearn.CSharp.Plugin.Models;
using BiliLearn.CSharp.Plugin.Processors;
using BiliLearn.CSharp.Plugin.Services;
using BiliLearn.CSharp.Plugin.Utils;
using Microsoft.Extensions.Logging;

namespace BiliLearn.CSharp.Plugin.Orchestrator;

public class VideoProcessingOrchestrator : IDisposable
{
    private readonly BilibiliApiService _biliApi;
    private readonly MediaDownloader _downloader;
    private readonly IMediaAnalyzer _visionProcessor;
    private readonly IMediaAnalyzer _audioProcessor;
    private readonly IMediaAnalyzer _subtitleProcessor;
    private readonly LLMIntegrator _llmIntegrator;
    private readonly ILogger _logger;
    private readonly string _workDir;
    private readonly IProgressReporter _progressReporter;

    private readonly KnowledgeBaseService _kbService;
    private readonly int _frameExtractInterval;
    private readonly int _maxFrames;

    public VideoProcessingOrchestrator(
        BilibiliApiService biliApi,
        MediaDownloader downloader,
        IMediaAnalyzer visionProcessor,
        IMediaAnalyzer audioProcessor,
        IMediaAnalyzer subtitleProcessor,
        LLMIntegrator llmIntegrator,
        ILogger logger,
        string workDir,
        BiliLearnConfig config,
        IProgressReporter progressReporter)
    {
        _biliApi = biliApi;
        _downloader = downloader;
        _visionProcessor = visionProcessor;
        _audioProcessor = audioProcessor;
        _subtitleProcessor = subtitleProcessor;
        _llmIntegrator = llmIntegrator;
        _logger = logger;
        _workDir = workDir;
        _progressReporter = progressReporter;

        _frameExtractInterval = config.FrameExtractInterval;
        _maxFrames = config.MaxFrames;
        _kbService = new KnowledgeBaseService(_logger, workDir);
    }

    public async Task<ProcessingResult> ProcessAsync(string bvid, CancellationToken cancellationToken = default)
    {
        var ctx = new VideoProcessingContext { Bvid = bvid };

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            await _progressReporter.ReportAsync($"📥 获取视频信息...", ProgressLevel.LogAndPush);
            var infoResult = await _biliApi.GetVideoInfoAsync(bvid, cancellationToken);
            if (!infoResult.Success || infoResult.Data == null)
            {
                await _progressReporter.ReportAsync($"📥 获取视频信息失败: {infoResult.Message}", ProgressLevel.LogAndPush);
                return Fail(infoResult.Message);
            }

            var info = infoResult.Data;
            ctx.VideoTitle = info.Title;
            ctx.DurationSeconds = info.DurationSeconds;
            ctx.VideoDescription = info.Description;
            ctx.UploaderName = info.Owner;
            ctx.VideoUrl = info.VideoUrl;
            ctx.AudioUrl = info.AudioUrl;
            ctx.Category = info.Category;
            ctx.Tags = info.Tags;
            ctx.Cid = info.Cid;
            await _progressReporter.ReportAsync($"✅ 视频信息: {info.Title} ({info.DurationSeconds}s)", ProgressLevel.LogAndPush);

            int estimatedFrames = Math.Max(1, Math.Min(12, info.DurationSeconds / 15));
            int estimatedSeconds = estimatedFrames * 15 + 30;
            string tagsText = info.Tags != null && info.Tags.Count > 0 ? string.Join(", ", info.Tags.Take(5)) : "无";
            await _progressReporter.ReportAsync($"🎬 **开始分析视频**\n" +
                $"**标题**: {info.Title}\n" +
                $"**UP主**: {info.Owner}\n" +
                $"**时长**: {FormatDuration(info.DurationSeconds)}\n" +
                $"**标签**: {tagsText}\n" +
                $"**简介**: {(info.Description?.Length > 100 ? info.Description.Substring(0, 100) + "..." : info.Description)}\n" +
                $"⏱ 预计提取{(estimatedFrames == 1 ? "1帧" : $"{estimatedFrames}帧")}，总分析耗时{(estimatedSeconds < 60 ? $"约{estimatedSeconds}秒" : $"约{estimatedSeconds / 60}分{estimatedSeconds % 60}秒")}",
                ProgressLevel.LogAndPush);

            cancellationToken.ThrowIfCancellationRequested();
            var downloadTasks = new List<Task>();
            var videoPath = Path.Combine(_workDir, $"{bvid}_video.mp4");
            var audioPath = Path.Combine(_workDir, $"{bvid}_audio.m4a");

            if (!string.IsNullOrEmpty(info.VideoUrl))
            {
                downloadTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        ctx.VideoFilePath = await _downloader.DownloadFileAsync(info.VideoUrl, videoPath, cancellationToken);
                        await _progressReporter.ReportAsync($"✅ 视频下载完成: {ctx.VideoFilePath}", ProgressLevel.LogAndPush);
                    }
                    catch (Exception ex)
                    {
                        await _progressReporter.ReportAsync($"❌ 视频下载失败: {ex.Message}", ProgressLevel.LogAndPush);
                        ctx.VideoFilePath = null;
                    }
                }));
            }

            if (!string.IsNullOrEmpty(info.AudioUrl))
            {
                downloadTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        ctx.AudioFilePath = await _downloader.DownloadFileAsync(info.AudioUrl, audioPath, cancellationToken);
                        await _progressReporter.ReportAsync($"✅ 音频下载完成: {ctx.AudioFilePath}", ProgressLevel.LogAndPush);
                    }
                    catch (Exception ex)
                    {
                        await _progressReporter.ReportAsync($"❌ 音频下载失败: {ex.Message}", ProgressLevel.LogAndPush);
                        ctx.AudioFilePath = null;
                    }
                }));
            }

            await Task.WhenAll(downloadTasks);
            await _progressReporter.ReportAsync($"📥 **下载完成**\n" +
                $"🎬 视频: {(ctx.VideoFilePath != null && File.Exists(ctx.VideoFilePath) ? "✅" : "❌")}\n" +
                $"🎵 音频: {(ctx.AudioFilePath != null && File.Exists(ctx.AudioFilePath) ? "✅" : "❌")}\n" +
                $"正在并行解析字幕、语音和视觉画面...", 
                ProgressLevel.LogAndPush);

            cancellationToken.ThrowIfCancellationRequested();
            var analysisTasks = new List<Task>();
            var sourceStatus = new Dictionary<string, bool> { ["subtitle"] = false, ["asr"] = false, ["visual"] = false };

            analysisTasks.Add(GetSubtitleAsync(ctx, sourceStatus, cancellationToken));

            if (!string.IsNullOrEmpty(ctx.AudioFilePath))
            {
                analysisTasks.Add(GetAsrAsync(ctx, sourceStatus, cancellationToken));
            }
            else
            {
                await _progressReporter.ReportAsync("⚠️ 未获取到音频，ASR跳过", ProgressLevel.LogAndPush);
            }

            if (!string.IsNullOrEmpty(ctx.VideoFilePath))
            {
                analysisTasks.Add(GetVisualAsync(ctx, sourceStatus, cancellationToken));
            }
            else
            {
                await _progressReporter.ReportAsync("⚠️ 未获取到视频，视觉分析跳过", ProgressLevel.LogAndPush);
            }

            await Task.WhenAll(analysisTasks);
            await _progressReporter.ReportAsync($"✅ **三源解析完成**\n" +
                $"📝 字幕: {(sourceStatus["subtitle"] ? "✅" : "❌")}\n" +
                $"🎵 ASR: {(sourceStatus["asr"] ? "✅" : "❌")}\n" +
                $"🖼 视觉: {(sourceStatus["visual"] ? "✅" : "❌")}\n" +
                $"正在调用语言模型进行综合分析...", ProgressLevel.LogAndPush);

            await _progressReporter.ReportAsync("🧠 LLM整合分析中...", ProgressLevel.LogAndPush);
            ctx.FinalSummary = await _llmIntegrator.GenerateSummaryAndCategoryAsync(ctx);
            if (string.IsNullOrEmpty(ctx.FinalSummary))
            {
                await _progressReporter.ReportAsync("❌ LLM整合失败", ProgressLevel.LogAndPush);
                return Fail("LLM整合失败");
            }
            await _progressReporter.ReportAsync($"✅ 整合完成: {ctx.FinalSummary.Length}字符", ProgressLevel.LogAndPush);
            await _progressReporter.ReportAsync($"   信息源状态: 字幕={sourceStatus["subtitle"]} ASR={sourceStatus["asr"]} 视觉={sourceStatus["visual"]}", ProgressLevel.LogAndPush);
            await _progressReporter.ReportAsync($"🧠 **分析完成**\n" +
                $"**标题**: {ctx.VideoTitle}\n" +
                $"**信息源**: 字幕={(sourceStatus["subtitle"] ? "✅" : "❌")} ASR={(sourceStatus["asr"] ? "✅" : "❌")} 视觉={(sourceStatus["visual"] ? "✅" : "❌")}\n" +
                $"正在保存到知识库...");

            var entry = await _llmIntegrator.SaveToKnowledgeBaseAsync(ctx);
            await _progressReporter.ReportAsync($"📚 已归档: {entry.Title} → {entry.Category}", ProgressLevel.LogAndPush);

            return new ProcessingResult
            {
                Success = true,
                Bvid = bvid,
                Title = ctx.VideoTitle,
                Summary = ctx.FinalSummary,
                Category = entry.Category,
                SourceStatus = sourceStatus,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理视频失败: {Bvid}", bvid);
            await _progressReporter.ReportAsync($"❌ 异常: {ex.Message}", ProgressLevel.LogAndPush);
            return Fail(ex.Message);
        }
    }

    private async Task GetSubtitleAsync(VideoProcessingContext ctx, Dictionary<string, bool> sourceStatus, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _progressReporter.ReportAsync("📝 获取字幕...", ProgressLevel.LogOnly);
            var subtitleJson = await _biliApi.GetSubtitleAsync(ctx.Bvid, ctx.Cid, cancellationToken);
            if (string.IsNullOrEmpty(subtitleJson))
            {
                await _progressReporter.ReportAsync("⚠️ 无字幕", ProgressLevel.LogAndPush);
                ctx.SubtitleText = null;
                return;
            }
            var structuredList = await _subtitleProcessor.ParseSubtitleAsync(subtitleJson);
            if (structuredList.Count > 0)
            {
                var structured = structuredList[0];
                ctx.SubtitleItems = structured.Items;
                ctx.SubtitleText = structured.FullText;
                sourceStatus["subtitle"] = true;
                await _progressReporter.ReportAsync($"✅ 字幕: {structured.Items.Count}条 / {ctx.SubtitleText.Length}字", ProgressLevel.LogOnly);
            }
            else
            {
                await _progressReporter.ReportAsync("⚠️ 字幕解析为空", ProgressLevel.LogAndPush);
                ctx.SubtitleText = null;
            }
        }
        catch (Exception ex)
        {
            await _progressReporter.ReportAsync($"❌ 字幕获取失败: {ex.Message}", ProgressLevel.LogAndPush);
            ctx.SubtitleText = null;
        }
    }

    private async Task GetAsrAsync(VideoProcessingContext ctx, Dictionary<string, bool> sourceStatus, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _progressReporter.ReportAsync("🎙 ASR转写中...", ProgressLevel.LogOnly);
            var text = await _audioProcessor.TranscribeAsync(ctx.AudioFilePath!, cancellationToken);
            if (!string.IsNullOrEmpty(text))
            {
                ctx.AsrTranscription = text;
                sourceStatus["asr"] = true;
                await _progressReporter.ReportAsync($"✅ ASR: {text.Length}字", ProgressLevel.LogOnly);
            }
            else
            {
                await _progressReporter.ReportAsync("⚠️ ASR转写为空", ProgressLevel.LogAndPush);
                ctx.AsrTranscription = null;
            }
        }
        catch (Exception ex)
        {
            await _progressReporter.ReportAsync($"❌ ASR失败: {ex.Message}", ProgressLevel.LogAndPush);
            ctx.AsrTranscription = null;
        }
    }

    private async Task GetVisualAsync(VideoProcessingContext ctx, Dictionary<string, bool> sourceStatus, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _progressReporter.ReportAsync("🖼 视觉分析中...", ProgressLevel.LogOnly);

            var descriptions = await _visionProcessor.AnalyzeVisualAsync(
                ctx.VideoFilePath!, _workDir, ctx.DurationSeconds,
                _frameExtractInterval, _maxFrames, _logger, cancellationToken);

            if (descriptions.Count > 0)
            {
                ctx.FramePaths = descriptions.Select(d => d.FramePath).ToList();
                ctx.KeyFrameDescriptions = descriptions;
                sourceStatus["visual"] = true;
                await _progressReporter.ReportAsync($"✅ 视觉: {descriptions.Count}帧描述完成", ProgressLevel.LogAndPush);
            }
            else
            {
                await _progressReporter.ReportAsync("⚠️ 无关键帧", ProgressLevel.LogAndPush);
            }
        }
        catch (Exception ex)
        {
            await _progressReporter.ReportAsync($"❌ 视觉失败: {ex.Message}", ProgressLevel.LogAndPush);
        }
    }

    public async Task CheckLoginAsync()
    {
        try
        {
            var status = await _biliApi.VerifyLoginAsync();
            if (status.Valid)
            {
                var msg = $"✅ 登录有效\n" +
                    $"👤 用户: {status.UserName}\n" +
                    $"🆔 UID: {status.Uid}\n" +
                    (status.IsVip ? $"👑 {status.VipLabel}" : "");
                await _progressReporter.ReportAsync(msg, ProgressLevel.LogAndPush);
            }
            else
            {
                await _progressReporter.ReportAsync($"❌ {status.Message}", ProgressLevel.LogAndPush);
            }
        }
        catch (Exception ex)
        {
            await _progressReporter.ReportAsync($"❌ 登录检查异常: {ex.Message}", ProgressLevel.LogAndPush);
        }
    }

    public async Task<string> SearchKnowledgeAsync(string keyword)
    {
        var results = _kbService.Search(keyword);
        if (results.Count == 0)
        {
            await _progressReporter.ReportAsync($"📭 知识库中未找到与「{keyword}」相关的内容", ProgressLevel.LogAndPush);
            return $"📭 知识库中未找到与「{keyword}」相关的内容";
        }
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"📚 找到 {results.Count} 条与「{keyword}」相关的知识：");
        foreach (var entry in results.Take(5))
        {
            sb.AppendLine($"\n【{entry.Category}】{entry.Title}");
            sb.AppendLine($"  {entry.Summary}");
        }
        await _progressReporter.ReportAsync($"📚 知识库搜索完成，找到 {results.Count} 条结果", ProgressLevel.LogAndPush);
        await _progressReporter.ReportAsync(sb.ToString(), ProgressLevel.LogAndPush);
        return sb.ToString();
    }

    private ProcessingResult Fail(string msg)
    {
        _ = _progressReporter.ReportAsync($"❌ {msg}", ProgressLevel.LogAndPush);
        _logger.LogError("[BiliLearn] 处理失败: {Message}", msg);
        return new ProcessingResult { Success = false, Message = msg};
    }
    
    private static string FormatDuration(int seconds)
    {
        if (seconds < 60) return $"{seconds}秒";
        int minutes = seconds / 60;
        int secs = seconds % 60;
        return $"{minutes}分{secs}秒";
    }

    public void Dispose()
    {
        _downloader?.Dispose();
        _biliApi?.Dispose();
        _llmIntegrator?.Dispose();
        _visionProcessor?.Dispose();
        _audioProcessor?.Dispose();
        _subtitleProcessor?.Dispose();
    }
}

public class ProcessingResult
{
    public bool Success { get; set; }
    public string Bvid { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Message { get; set; }
    public string? Summary { get; set; }
    public string Category { get; set; } = "";
    public Dictionary<string, bool> SourceStatus { get; set; } = new();    
}

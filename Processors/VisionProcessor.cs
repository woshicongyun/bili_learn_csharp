using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Alife.Function.AIModelUtility;
using BiliLearn.CSharp.Plugin.Domain.Interfaces;
using BiliLearn.CSharp.Plugin.Models;
using BiliLearn.CSharp.Plugin.Services;
using Microsoft.Extensions.Logging;

namespace BiliLearn.CSharp.Plugin.Processors;

/// <summary>
/// 视觉处理器：调用Alife内置视觉模型分析关键帧
/// </summary>
public class VisionProcessor : IMediaAnalyzer
{
    private readonly IVisionModel _visionModel;
    private readonly ILogger _logger;

    public VisionProcessor(IVisionModel visionModel, ILogger logger)
    {
        _visionModel = visionModel;
        _logger = logger;
    }

    public async Task<List<FrameDescription>> AnalyzeVisualAsync(
        string videoPath, string workDir, int durationSeconds,
        int intervalSeconds, int maxFrames, ILogger logger, CancellationToken ct = default)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            logger.LogInformation("🖼 视觉分析中...");
            var framePaths = await FFmpegHelper.ExtractFramesAsync(
                videoPath, workDir, durationSeconds, intervalSeconds, maxFrames, logger, ct);

            var results = new List<FrameDescription>();
            if (framePaths.Count > 0)
            {
                foreach (var framePath in framePaths)
                {
                    ct.ThrowIfCancellationRequested();
                    var descs = await AnalyzeFrameAsync(framePath, 0, intervalSeconds, ct);
                    results.AddRange(descs);
                }
                logger.LogInformation("✅ 视觉分析完成: {Count}帧描述", results.Count);
            }
            else
            {
                logger.LogWarning("⚠️ 无关键帧");
            }
            return results;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "视觉分析失败");
            return new List<FrameDescription>();
        }
    }

    public async Task<List<FrameDescription>> AnalyzeFrameAsync(string framePath, double startTime, double endTime, CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(framePath))
            {
                _logger.LogWarning("帧文件不存在: {Path}", framePath);
                return new();
            }
            var question = "请描述这张视频截图中发生的场景、人物、动作、文字内容（如有）。用中文简洁回答。";
            var description = await _visionModel.QueryAsync(framePath, question, 300, ct);
            _logger.LogInformation("✅ 帧分析完成: {Path} → {Desc}", framePath, description[..Math.Min(80, description.Length)]);
            return new List<FrameDescription>
            {
                new FrameDescription
                {
                    FramePath = framePath,
                    Description = description,
                    StartTime = startTime,
                    EndTime = endTime
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "帧分析失败: {Path}", framePath);
            return new();
        }
    }

    public Task<string?> TranscribeAsync(string audioPath, CancellationToken ct = default)
        => Task.FromResult(null as string);

    public Task<StructuredSubtitle> ParseSubtitleAsync(string subtitleJson)
        => Task.FromResult(new StructuredSubtitle());

    public void Dispose() { }
}

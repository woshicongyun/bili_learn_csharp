
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Alife.Function.AIModelUtility;
using BiliLearn.CSharp.Plugin.Models;
using Microsoft.Extensions.Logging;

namespace BiliLearn.CSharp.Plugin.Processors;

/// <summary>
/// 视觉处理器：调用Alife内置视觉模型分析关键帧
/// </summary>
public class VisionProcessor
{
    private readonly IVisionModel _visionModel;
    private readonly ILogger _logger;

    public VisionProcessor(IVisionModel visionModel, ILogger logger)
    {
        _visionModel = visionModel;
        _logger = logger;
    }

    /// <summary>
    /// 分析一组关键帧图片
    /// </summary>
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

    /// <summary>
    /// 批量分析帧（逐张串行避免显存冲突）
    /// </summary>
    public async Task<List<FrameDescription>> AnalyzeFramesAsync(List<(string Path, double Start, double End)> frames, CancellationToken ct = default)
    {
        var results = new List<FrameDescription>();
        foreach (var frame in frames)
        {
            ct.ThrowIfCancellationRequested();
            var descs = await AnalyzeFrameAsync(frame.Path, frame.Start, frame.End, ct);
            results.AddRange(descs);
        }
        return results;
    }
}

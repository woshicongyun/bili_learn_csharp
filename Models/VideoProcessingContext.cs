using System;
using System.Collections.Generic;

namespace BiliLearn.CSharp.Plugin.Models;

/// <summary>
/// 视频处理上下文对象，贯穿整个工作流
/// </summary>
public class VideoProcessingContext
{
    public string Bvid { get; set; } = "";
    public long Cid { get; set; }
    public string? VideoTitle { get; set; }
    public int DurationSeconds { get; set; }
    public bool IsExclusiveForVip { get; set; }
    public string? VideoDescription { get; set; }
    public string? UploaderName { get; set; }
    public string? VideoCoverUrl { get; set; }

    // 下载结果
    public string? VideoUrl { get; set; }
    public string? AudioUrl { get; set; }
    public string? VideoFilePath { get; set; }
    public string? AudioFilePath { get; set; }

    // 视觉
    public List<string> FramePaths { get; set; } = new();
    public List<FrameDescription> KeyFrameDescriptions { get; set; } = new();

    // 字幕
    public List<SubtitleItem> SubtitleItems { get; set; } = new();

    // ASR
    public string? AsrTranscription { get; set; }

    // 评论
    public List<CommentItem> Comments { get; set; } = new();
    public string? CommentInsights { get; set; }

    // LLM结果
    public string? FinalSummary { get; set; }
    public string? Category { get; set; }
    public List<string> Tags { get; set; } = new();

    // 兼容旧字段：取FinalSummary
    public string? SubtitleText
    {
        get => _subtitleText;
        set => _subtitleText = value;
    }
    private string? _subtitleText;
}

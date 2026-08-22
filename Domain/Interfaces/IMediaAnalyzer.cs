using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BiliLearn.CSharp.Plugin.Models;
using Microsoft.Extensions.Logging;

namespace BiliLearn.CSharp.Plugin.Domain.Interfaces;

/// <summary>
/// 媒体分析抽象接口
/// 对应现有: AudioProcessor + SubtitleProcessor + VisionProcessor + FFmpegHelper
/// </summary>
public interface IMediaAnalyzer : IDisposable
{
    /// <summary>
    /// 提取关键帧并分析
    /// </summary>
    Task<List<FrameDescription>> AnalyzeVisualAsync(
        string videoPath,
        string workDir,
        int durationSeconds,
        int intervalSeconds,
        int maxFrames,
        ILogger logger,
        CancellationToken ct = default);
    
    /// <summary>
    /// ASR转写音频
    /// </summary>
    Task<string?> TranscribeAsync(string audioPath, CancellationToken ct = default);
    
    /// <summary>
    /// 解析字幕JSON为结构化列表
    /// </summary>
    Task<List<StructuredSubtitle>> ParseSubtitleAsync(string subtitleJson);
}

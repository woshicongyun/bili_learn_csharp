
using System;

namespace BiliLearn.CSharp.Plugin.Models;

/// <summary>视频生命周期阶段</summary>
public enum VideoStage
{
    Queued,      // 排队中（等待下载）
    Downloading, // 下载中
    Downloaded,  // 已下载（等待分析）
    Analyzing,   // 分析中
    Completed,   // 已完成
    Failed,      // 失败
    Canceled     // 已取消
}

/// <summary>每个视频的状态模型（供队列调度与状态Poke使用）</summary>
public class VideoStatus
{
    public string Bvid { get; set; } = "";
    public VideoStage Stage { get; set; } = VideoStage.Queued;
    public string? Title { get; set; }          // 视频标题（入队时可先取）
    public int Progress { get; set; }           // 0-100 进度
    public string? Error { get; set; }          // 失败原因
    public DateTime QueuedAt { get; set; }      // 入队时间
}

using System;

namespace BiliLearn.CSharp.Plugin.Models;

/// <summary>
/// 队列任务数据模型
/// </summary>
public record QueueItem
{
    public int Id { get; init; }
    public string Bvid { get; init; } = "";
    public string Title { get; init; } = "";
    public string Status { get; init; } = "Queued";
    public string? Stage { get; init; }
    public string? Error { get; init; }
    public DateTime EnqueuedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

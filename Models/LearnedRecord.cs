using System;

namespace BiliLearn.CSharp.Plugin.Models;

/// <summary>
/// 已学习记录数据模型
/// </summary>
public record LearnedRecord
{
    public string Bvid { get; init; } = "";
    public string Title { get; init; } = "";
    public string Summary { get; init; } = "";
    public string Category { get; init; } = "其他";
    public DateTime LearnedAt { get; init; }
}

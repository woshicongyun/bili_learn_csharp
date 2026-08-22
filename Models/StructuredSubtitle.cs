
using System;
using System.Linq;
using System.Collections.Generic;

namespace BiliLearn.CSharp.Plugin.Models;

/// <summary>
/// 结构化字幕条目
/// </summary>
public class SubtitleItem
{
    public double From { get; set; }
    public double To { get; set; }
    public string Text { get; set; } = "";
}

/// <summary>
/// 结构化字幕（含全文）
/// </summary>
public class StructuredSubtitle
{
    public List<SubtitleItem> Items { get; set; } = new();
    public string FullText => string.Join(" ", Items.Select(i => i.Text));
}

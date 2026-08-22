using System;
using System.Collections.Generic;
namespace BiliLearn.CSharp.Plugin.Models;

public class VideoInfo
{
    public string Bvid { get; set; } = "";
    public long Cid { get; set; }
    public string Title { get; set; } = "";
    public int DurationSeconds { get; set; }
    public string? Description { get; set; }
    public string? Pic { get; set; }
    public string? Owner { get; set; }
    public bool IsExclusiveForVip { get; set; }
    public bool NeedCharge { get; set; }
    public bool NeedVip { get; set; }
    public string? AudioUrl { get; set; }
    public long AudioSize { get; set; }
    public string? VideoUrl { get; set; }
    public long VideoSize { get; set; }
    public List<string> Tags { get; set; } = new();
    public string? Category { get; set; }
}

public class VideoInfoResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public bool NeedVip { get; set; }
    public bool NeedCharge { get; set; }
    public VideoInfo? Data { get; set; }
}

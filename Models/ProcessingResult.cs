using System.Collections.Generic;

namespace BiliLearn.CSharp.Plugin.Models;

/// <summary>视频处理结果</summary>
public class ProcessingResult
{
    public bool Success { get; set; }
    public string Bvid { get; set; } = "";
    public string? Title { get; set; }
    public string? Summary { get; set; }
    public string? Category { get; set; }
    public string? Message { get; set; }
    public Dictionary<string, bool>? SourceStatus { get; set; }
}

/// <summary>视频下载结果</summary>
public class VideoDownloadResult
{
    public bool Success { get; set; }
    public bool Canceled { get; set; }
    public string? FilePath { get; set; }
    public string? Message { get; set; }
}

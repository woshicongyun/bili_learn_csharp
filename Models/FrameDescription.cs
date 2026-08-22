
using System;
using System.Collections.Generic;
namespace BiliLearn.CSharp.Plugin.Models;

/// <summary>
/// 关键帧描述
/// </summary>
public class FrameDescription
{
    public string FramePath { get; set; } = "";
    public string Description { get; set; } = "";
    public double StartTime { get; set; }
    public double EndTime { get; set; }
}

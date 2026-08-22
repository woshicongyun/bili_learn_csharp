using System;
using System.Collections.Generic;

namespace BiliLearn.CSharp.Plugin.Models;

/// <summary>
/// 登录状态
/// </summary>
public class LoginStatus
{
    public bool Valid { get; set; }
    public bool IsLogin { get; set; }
    public long? Mid { get; set; }
    public string Uid { get; set; } = "";
    public string? Uname { get; set; }
    public string? UserName { get; set; }
    public int Level { get; set; }
    public string? Message { get; set; }
    public bool IsVip { get; set; }
    public string? VipLabel { get; set; }
}

/// <summary>
/// 推荐视频项
/// </summary>
public class RecommendItem
{
    public string Bvid { get; set; } = "";
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public string Duration { get; set; } = "";
    public string Pic { get; set; } = "";
}



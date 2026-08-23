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



/// <summary>
/// 视频搜索结果项
/// </summary>
public class VideoSearchResult
{
    public string Bvid { get; set; } = "";
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public string Duration { get; set; } = "";
    public string Pic { get; set; } = "";
    public string Description { get; set; } = "";
    public long PlayCount { get; set; }
    public string PublishTime { get; set; } = "";
}

/// <summary>
/// 扫码登录 - 二维码信息
/// </summary>
public class QrCodeInfo
{
    public bool Success { get; set; }
    public string QrCodeKey { get; set; } = "";
    public string QrCodeUrl { get; set; } = "";
    public string Message { get; set; } = "";
}

/// <summary>
/// 扫码登录 - 轮询结果
/// </summary>
public class QrCodePollResult
{
    /// <summary>
    /// 0=已扫码未确认, 1=扫码成功(含Cookie), 2=二维码过期, 3=未扫码
    /// </summary>
    public int Status { get; set; }
    public string Message { get; set; } = "";
    public string Cookie { get; set; } = "";
    public string UserName { get; set; } = "";
    public long Mid { get; set; }
}

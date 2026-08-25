
using System;
using System.Collections.Generic;

namespace BiliLearn.CSharp.Plugin.Models;

/// <summary>
/// B站评论数据模型
/// </summary>
public class CommentItem
{
    /// <summary>
    /// 评论ID
    /// </summary>
    public long Rpid { get; set; }
    
    /// <summary>
    /// 用户ID
    /// </summary>
    public string MemberMid { get; set; } = "";
    
    /// <summary>
    /// 用户名
    /// </summary>
    public string Author { get; set; } = "";
    
    /// <summary>
    /// 评论内容
    /// </summary>
    public string Message { get; set; } = "";
    
    /// <summary>
    /// 点赞数
    /// </summary>
    public int LikeCount { get; set; }
    
    /// <summary>
    /// 回复数
    /// </summary>
    public int ReplyCount { get; set; }
    
    /// <summary>
    /// 评论时间（Unix时间戳）
    /// </summary>
    public long Ctime { get; set; }
    
    /// <summary>
    /// 评论时间（可读格式）
    /// </summary>
    public DateTime CtimeFormatted => DateTimeOffset.FromUnixTimeSeconds(Ctime).LocalDateTime;
}

/// <summary>
/// 评论获取结果
/// </summary>
public class CommentResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public List<CommentItem> Comments { get; set; } = new();
}

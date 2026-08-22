using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BiliLearn.CSharp.Plugin.Models;

namespace BiliLearn.CSharp.Plugin.Domain.Interfaces;

/// <summary>
/// B站API抽象接口
/// 对应现有 BilibiliApiService
/// </summary>
public interface IBilibiliFetcher : IDisposable
{
    /// <summary>
    /// 验证登录状态
    /// </summary>
    Task<LoginStatus> VerifyLoginAsync(CancellationToken ct = default);
    
    /// <summary>
    /// 获取视频信息（含播放地址）
    /// </summary>
    Task<VideoInfoResult> GetVideoInfoAsync(string bvid, CancellationToken ct = default);
    
    /// <summary>
    /// 获取字幕JSON字符串
    /// </summary>
    Task<string?> GetSubtitleAsync(string bvid, long cid, CancellationToken ct = default);
    
    /// <summary>
    /// 获取推荐视频列表
    /// </summary>
    Task<List<RecommendItem>> GetRecommendAsync(int count = 10, CancellationToken ct = default);
    
    /// <summary>
    /// 按关键词搜索视频
    /// </summary>
    Task<List<VideoSearchResult>> SearchVideosAsync(string keyword, int count = 10, CancellationToken ct = default);
}

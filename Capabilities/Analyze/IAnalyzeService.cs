
using System;
using System.Threading;
using System.Threading.Tasks;
using BiliLearn.CSharp.Plugin.Domain.Interfaces;
using BiliLearn.CSharp.Plugin.Models;
using BiliLearn.CSharp.Plugin.Services;

namespace BiliLearn.CSharp.Plugin.Capabilities.Analyze;

/// <summary>
/// 分析服务接口：视频分析（元信息+下载+三源解析+LLM整合+归档）
/// </summary>
public interface IAnalyzeService : IDisposable
{
    /// <summary>
    /// 分析单个视频，返回结构化结果
    /// </summary>
    Task<ProcessingResult> ProcessAsync(string bvid, CancellationToken cancellationToken = default);

    /// <summary>
    /// 底层B站API（供外部调用搜索等方法），暂保留
    /// </summary>
    IBilibiliFetcher BiliApi { get; }
}

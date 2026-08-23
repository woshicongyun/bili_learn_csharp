
using System;
using BiliLearn.CSharp.Plugin.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BiliLearn.CSharp.Plugin.Capabilities.Learn;

/// <summary>
/// 学习队列接口：预下载并行 + 分析串行调度
/// </summary>
public interface ILearnQueue : IDisposable
{
    /// <summary>
    /// 加入单个视频（已去重检查）
    /// </summary>
    /// <returns>0=成功, 1=已在队列中, 2=队列已满, 3=已完成/失败/取消</returns>
    int Enqueue(string bvid, string? title = null);

    /// <summary>
    /// 批量加入视频
    /// </summary>
    (int success, int skipped) EnqueueBatch(IEnumerable<string> bvids);

    /// <summary>
    /// 取消指定视频（分析中则CTS取消 / 排队下载中移除）
    /// </summary>
    bool Cancel(string bvid);

    /// <summary>
    /// 取消队列中所有视频
    /// </summary>
    void CancelAll();

    /// <summary>
    /// 当前所有视频状态快照
    /// </summary>
    IReadOnlyList<VideoStatus> Snapshot { get; }

    /// <summary>
    /// 启动后台调度循环
    /// </summary>
    void Start();

    /// <summary>
    /// 停止调度循环
    /// </summary>
    void Stop();
}

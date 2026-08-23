
using System;
using System.Threading.Tasks;

namespace BiliLearn.CSharp.Plugin.Capabilities.Learn;

/// <summary>
/// 服务接口：单视频学习流程
/// </summary>
public interface ILearnService : IDisposable
{
    /// <summary>
    /// 学习单个视频（含去重检查、入队，返回结果消息）
    /// </summary>
    Task<string> LearnAsync(string bvid);

    /// <summary>
    /// 学习多个视频（批量入队，返回结果消息）
    /// </summary>
    Task<string> LearnBatchAsync(string bvidsCsv);

    /// <summary>
    /// 取消指定的视频学习
    /// </summary>
    Task<string> CancelLearnAsync(string bvid);

    /// <summary>
    /// 获取当前学习队列的状态信息
    /// </summary>
    Task<string> GetQueueStatusAsync();
}

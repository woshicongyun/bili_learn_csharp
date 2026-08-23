
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BiliLearn.CSharp.Plugin.Domain.Interfaces;
using BiliLearn.CSharp.Plugin.Models;
using Microsoft.Extensions.Logging;

namespace BiliLearn.CSharp.Plugin.Capabilities.Learn;

/// <summary>
/// 单视频学习流程实现（迁绑自 V1 BiliLearnService 学习相关方法）
/// </summary>
public class LearnService : ILearnService
{
    private readonly BiliLearnServices _services;
    private readonly ConfirmationService _confirmationService;
    private readonly ILogger _logger;
    private readonly Func<string, Task> _poke;

    public LearnService(
        BiliLearnServices services,
        ConfirmationService confirmationService,
        ILogger logger,
        Func<string, Task> poke)
    {
        _services = services;
        _confirmationService = confirmationService;
        _logger = logger;
        _poke = poke;
    }

    /// <summary>分析B站视频：入队处理</summary>
    public async Task<string> LearnAsync(string bvid)
    {
        _logger.LogInformation("[LearnService] LearnAsync called, LearnQueue is null: {IsNull}", _services.LearnQueue == null);
        // 检查是否已学习过
        var existingEntry = await _services.KnowledgeRepo.GetByBvidAsync(bvid);
        if (existingEntry != null)
            return await _confirmationService.HandleExistingVideoAsync(bvid, existingEntry, _poke);

        // 先获取视频标题再入队
        string? title = null;
        try
        {
            var info = await _services.BiliApi.GetVideoInfoAsync(bvid);
            title = info?.Data?.Title;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[LearnService] 获取视频信息失败，将使用BV号作为标题");
        }

        // 检查队列中是否已有
        if (_services.LearnQueue == null)
        {
            _logger.LogError("[LearnService] LearnQueue is null! Cannot enqueue bvid: {Bvid}", bvid);
            return "❌ LearnQueue未初始化";
        }
        var enqueueResult = _services.LearnQueue.Enqueue(bvid, title);
        return enqueueResult switch
        {
            0 => $"✅ 已加入学习队列：{title ?? bvid}，进度实时推送中...",
            1 => $"⚠️ 该视频已在队列中（{title ?? bvid}）",
            2 => "⚠️ 队列已满（最多5个排队中），请稍后再试",
            3 => $"⚠️ 该视频已完成过或已取消：{title ?? bvid}，如需重新学习请回复确认",
            _ => "❌ 入队失败"
        };
    }

    /// <summary>批量入队</summary>
    public async Task<string> LearnBatchAsync(string bvidsCsv)
    {
        var bvids = bvidsCsv.Split(',', '，', ' ', '\t')
            .Select(b => b.Trim())
            .Where(b => b.StartsWith("BV", StringComparison.OrdinalIgnoreCase) || b.Length == 12)
            .ToList();

        if (bvids.Count == 0)
            return "❌ 未识别到有效的BV号，请用逗号分隔多个视频";

        // 检查已学过的
        var newBvids = new System.Collections.Generic.List<string>();
        foreach (var bvid in bvids)
        {
            var existing = await _services.KnowledgeRepo.GetByBvidAsync(bvid);
            if (existing != null)
                await _confirmationService.HandleExistingVideoAsync(bvid, existing, _poke);
            else
                newBvids.Add(bvid);
        }

        if (newBvids.Count == 0)
            return "✅ 所有视频均已学习过，已发送确认消息等待回复";

        var (success, skipped) = _services.LearnQueue.EnqueueBatch(newBvids);
        if (success > 0)
            return $"✅ 已加入队列 {success} 个视频，跳过 {skipped} 个（队列满或重复），进度实时推送中...";
        return $"⚠️ 入队失败：成功 {success} 个，跳过 {skipped} 个";
    }

    /// <summary>取消分析</summary>
    public Task<string> CancelLearnAsync(string bvid)
    {
        if (_services.LearnQueue == null)
            return Task.FromResult("❌ LearnQueue未初始化");
        if (_services.LearnQueue.Cancel(bvid))
        {
            _poke($"🛑 已取消: {bvid}");
            return Task.FromResult($"✅ 已取消: {bvid}");
        }
        return Task.FromResult($"⚠️ 未找到该视频（可能已完成/失败/不在队列中）: {bvid}");
    }

    /// <summary>获取队列状态</summary>
    public Task<string> GetQueueStatusAsync()
    {
        _logger.LogInformation("[LearnService] GetQueueStatusAsync called, LearnQueue is null: {IsNull}, _services is null: {ServicesNull}", _services.LearnQueue == null, _services == null);
        if (_services == null)
            return Task.FromResult("❌ Services未初始化");
        if (_services.LearnQueue == null)
            return Task.FromResult("❌ LearnQueue未初始化");
        
        try
        {
            var snapshot = _services.LearnQueue.Snapshot;
            _logger.LogInformation("[LearnService] Snapshot count: {Count}", snapshot.Count);
            if (snapshot.Count == 0)
                return Task.FromResult("📋 B站学习队列为空");

            var sb = new StringBuilder();
            sb.AppendLine($"📋 B站学习队列（共{snapshot.Count}个）");
            for (int i = 0; i < snapshot.Count; i++)
            {
                var v = snapshot[i];
                string icon = v.Stage switch
                {
                    VideoStage.Queued => "⏳",
                    VideoStage.Downloading => "⬇",
                    VideoStage.Downloaded => "⏸",
                    VideoStage.Analyzing => "▶",
                    VideoStage.Completed => "✅",
                    VideoStage.Failed => "❌",
                    VideoStage.Canceled => "🚫",
                    _ => "❓"
                };
                string detail = v.Stage switch
                {
                    VideoStage.Queued => "排队中",
                    VideoStage.Downloading => $"下载中 {v.Progress}%",
                    VideoStage.Downloaded => "等待分析",
                    VideoStage.Analyzing => $"分析中 {v.Progress}%",
                    VideoStage.Completed => "完成",
                    VideoStage.Failed => $"失败: {v.Error ?? "未知"}",
                    VideoStage.Canceled => "已取消",
                    _ => ""
                };
                sb.AppendLine($"{icon} ({i + 1}/{snapshot.Count}) {v.Bvid} 【{v.Title ?? "未命名"}】 {detail}");
            }
            int done = snapshot.Count(v => v.Stage == VideoStage.Completed);
            int failed = snapshot.Count(v => v.Stage == VideoStage.Failed);
            sb.AppendLine($"✅ 完成{done} | ❌ 失败{failed} | 剩余{snapshot.Count - done - failed}");
            var result = sb.ToString();
            _poke(result);
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[LearnService] Error getting queue status");
            return Task.FromResult($"❌ 获取队列状态失败: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _services.LearnQueue?.Dispose();
        _services.AnalyzeService?.Dispose();
    }
}

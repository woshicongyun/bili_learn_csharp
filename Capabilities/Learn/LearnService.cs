using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BiliLearn.CSharp.Plugin.Capabilities.Learn;
using BiliLearn.CSharp.Plugin.Domain.Interfaces;
using BiliLearn.CSharp.Plugin.Models;
using BiliLearn.CSharp.Plugin.Services;
using Microsoft.Extensions.Logging;

namespace BiliLearn.CSharp.Plugin.Capabilities.Learn;

public class LearnService : ILearnService
{
    private readonly BiliLearnServices _services;
    private readonly ConfirmationService _confirmationService;
    private readonly ILogger _logger;
    private readonly Func<string, Task> _poke;
    private readonly IBiliLearnStore _store;

    public LearnService(
        BiliLearnServices services,
        ConfirmationService confirmationService,
        ILogger logger,
        Func<string, Task> poke,
        IBiliLearnStore store)
    {
        _services = services;
        _confirmationService = confirmationService;
        _logger = logger;
        _poke = poke;
        _store = store;
    }

    public async Task LearnAsync(string bvid)
    {
        _logger.LogInformation("[LearnService] LearnAsync called");

        var isLearned = await _store.IsLearnedAsync(bvid);
        if (isLearned)
        {
            var history = await _store.GetHistoryAsync(limit: 1, offset: 0);
            var record = history.FirstOrDefault(h => h.Bvid == bvid);
            if (record != null)
            {
                var entry = new KnowledgeEntry 
                { 
                    Bvid = record.Bvid, 
                    Title = record.Title, 
                    Category = record.Category, 
                    Summary = record.Summary, 
                    CreatedAt = record.LearnedAt 
                };
                await _confirmationService.HandleExistingVideoAsync(bvid, entry, _poke);
                return;
            }
        }

        string? title = null;
        try
        {
            var info = await _services.BiliApi.GetVideoInfoAsync(bvid);
            title = info?.Data?.Title;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[LearnService] 获取视频信息失败");
        }

        if (_services.LearnQueue == null)
        {
            _logger.LogError("[LearnService] LearnQueue is null!");
            await _poke("❌ LearnQueue未初始化");
            return;
        }

        var enqueueResult = await _services.LearnQueue.Enqueue(bvid, title);
        switch (enqueueResult)
        {
            case 0:
                await _poke($"✅ 已加入学习队列：{title ?? bvid}，进度实时推送中...");
                break;
            case 1:
                await _poke($"⚠️ 该视频已在队列中（{title ?? bvid}）");
                break;
            case 2:
                await _poke("⚠️ 队列已满（最多5个排队中），请稍后再试");
                break;
            case 3:
                await _poke($"⚠️ 该视频已完成过或已取消：{title ?? bvid}，如需重新学习请回复确认");
                break;
            default:
                await _poke("❌ 入队失败");
                break;
        }
    }

    public async Task LearnBatchAsync(string bvidsCsv)
    {
        var bvids = bvidsCsv.Split(',', '，', ' ', '\t')
            .Select(b => b.Trim())
            .Where(b => b.StartsWith("BV", StringComparison.OrdinalIgnoreCase) || b.Length == 12)
            .ToList();

        if (bvids.Count == 0)
        {
            await _poke("❌ 未识别到有效的BV号");
            return;
        }

        var newBvids = new System.Collections.Generic.List<string>();
        foreach (var bvid in bvids)
        {
            var isLearned = await _store.IsLearnedAsync(bvid);
            if (isLearned)
            {
                var history = await _store.GetHistoryAsync(limit: 1, offset: 0);
                var record = history.FirstOrDefault(h => h.Bvid == bvid);
                if (record != null)
                {
                    var entry = new KnowledgeEntry 
                    { 
                        Bvid = record.Bvid, 
                        Title = record.Title, 
                        Category = record.Category, 
                        Summary = record.Summary, 
                        CreatedAt = record.LearnedAt 
                    };
                    await _confirmationService.HandleExistingVideoAsync(bvid, entry, _poke);
                }
            }
            else
            {
                newBvids.Add(bvid);
            }
        }

        if (newBvids.Count == 0)
        {
            await _poke("✅ 所有视频均已学习过");
            return;
        }

        var (success, skipped) = await _services.LearnQueue.EnqueueBatch(newBvids);
        if (success > 0)
            await _poke($"✅ 已加入队列 {success} 个视频，跳过 {skipped} 个，进度实时推送中...");
        else
            await _poke($"⚠️ 入队失败：成功 {success} 个，跳过 {skipped} 个");
    }

    public async Task CancelLearnAsync(string bvid)
    {
        if (_services.LearnQueue == null)
        {
            await _poke("❌ LearnQueue未初始化");
            return;
        }
        if (_services.LearnQueue.Cancel(bvid))
        {
            await _poke($"🛑 已取消: {bvid}");
        }
        else
        {
            await _poke($"⚠️ 未找到该视频（可能已完成/失败/不在队列中）: {bvid}");
        }
    }

    public async Task GetQueueStatusAsync()
    {
        if (_services == null)
        {
            await _poke("❌ Services未初始化");
            return;
        }
        if (_services.LearnQueue == null)
        {
            await _poke("❌ LearnQueue未初始化");
            return;
        }

        var snapshot = _services.LearnQueue.Snapshot;
        if (snapshot.Count == 0)
        {
            await _poke("📋 B站学习队列为空");
            return;
        }

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
        await _poke(sb.ToString());
    }

    public Task AnalyzeAsync(string bvid)
    {
        // AnalyzeAsync 由 AnalyzeService 处理，此处仅占位实现
        _logger.LogInformation("[LearnService] AnalyzeAsync called, bvid={Bvid}", bvid);
        return Task.CompletedTask;
    }
}

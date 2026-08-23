
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BiliLearn.CSharp.Plugin.Models;
using BiliLearn.CSharp.Plugin.Orchestrator;
using BiliLearn.CSharp.Plugin.Services;
using Microsoft.Extensions.Logging;

namespace BiliLearn.CSharp.Plugin;

/// <summary>
/// 待确认/重学确认服务：从 BiliLearnModule 中剥离的确认逻辑
/// </summary>
public class ConfirmationService
{
    private readonly ConcurrentDictionary<string, PendingConfirmation> _pendingConfirmations = new();
    private readonly ILogger _logger;
    private readonly Func<string, Task> _poke;
    private readonly Func<string, CancellationToken, Task<ProcessingResult>> _processFunc;

    public ConfirmationService(
        ILogger logger,
        Func<string, Task> poke,
        Func<string, CancellationToken, Task<ProcessingResult>> processFunc)
    {
        _logger = logger;
        _poke = poke;
        _processFunc = processFunc;
    }

    /// <summary>处理已学习过的视频，生成确认消息</summary>
    public async Task<string> HandleExistingVideoAsync(string bvid, KnowledgeEntry entry, Func<string, Task> poke)
    {
        var pending = new PendingConfirmation
        {
            Bvid = bvid,
            OldEntry = entry,
            UserQuery = $"该视频已于 {entry.CreatedAt:yyyy-MM-dd HH:mm} 学习过。" + Environment.NewLine +
               $"📝 摘要：{entry.Summary.Substring(0, Math.Min(150, entry.Summary.Length))}..." + Environment.NewLine +
               $"🔗 链接：https://www.bilibili.com/video/{bvid}" + Environment.NewLine + Environment.NewLine +
               "是否需要重新学习？回复yes重新学习，回复no取消。",
            Timestamp = DateTime.Now
        };

        _pendingConfirmations[bvid] = pending;
        await poke($"📚 {pending.UserQuery}");
        return "已学习过该视频，等待确认...";
    }

    /// <summary>处理用户消息，检测确认回复</summary>
    public async Task OnMessageReceivedAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        var entries = _pendingConfirmations.ToArray();
        if (entries.Length == 0) return;

        var lowerMsg = message.ToLower().Trim();
        var confirmPatterns = new[] { "是", "好的", "重新学习", "重新学", "学", "y", "yes" };
        var denyPatterns = new[] { "否", "不用了", "取消", "不学", "n", "no" };

        foreach (var (bvid, pending) in entries)
        {
            if (confirmPatterns.Any(p => lowerMsg.Contains(p)))
            {
                _logger.LogInformation("[BiliLearn] 确认重新学习: {Bvid}", bvid);
                _ = Task.Run(async () =>
                {
                    await _poke($"✅ 开始重新学习: {bvid}");
                    try
                    {
                        await _processFunc(bvid, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        await _poke($"❌ 重新学习失败: {ex.Message}");
                    }
                });
                _pendingConfirmations.TryRemove(bvid, out _);
            }
            else if (denyPatterns.Any(p => lowerMsg.Contains(p)))
            {
                await _poke("已取消重新学习。");
                _pendingConfirmations.TryRemove(bvid, out _);
            }
        }
    }

    public void Dispose() => _pendingConfirmations.Clear();
}

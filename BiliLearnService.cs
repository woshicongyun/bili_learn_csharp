
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BiliLearn.CSharp.Plugin.Domain.Interfaces;
using BiliLearn.CSharp.Plugin.Models;
using BiliLearn.CSharp.Plugin.Utils;
using BiliLearn.CSharp.Plugin.Services;
using Microsoft.Extensions.Logging;

namespace BiliLearn.CSharp.Plugin;

/// <summary>
/// 业务服务层：从 BiliLearnModule 中下沉的业务方法
/// </summary>
public class BiliLearnService : IDisposable
{
    private readonly BiliLearnServices _services;
    private readonly ConfirmationService _confirmationService;
    private readonly BiliLearnConfig _config;
    private readonly ILogger _logger;
    private readonly Func<string, Task> _poke;
    private readonly Action? _onConfigChanged;

    public BiliLearnService(
        BiliLearnServices services,
        ConfirmationService confirmationService,
        BiliLearnConfig config,
        ILogger logger,
        Func<string, Task> poke,
        Action? onConfigChanged = null)
    {
        _services = services;
        _confirmationService = confirmationService;
        _config = config;
        _logger = logger;
        _poke = poke;
        _onConfigChanged = onConfigChanged;
    }

    /// <summary>分析B站视频：入队处理</summary>
    public async Task<string> LearnAsync(string bvid)
    {
        // 检查是否已学习过
        var existingEntry = await _services.KnowledgeRepo.GetByBvidAsync(bvid);
        if (existingEntry != null)
            return await _confirmationService.HandleExistingVideoAsync(bvid, existingEntry, _poke);

        // 检查队列中是否已有
        var enqueueResult = _services.QueueRunner.Enqueue(bvid);
        return enqueueResult switch
        {
            0 => "✅ 已加入学习队列，进度实时推送中...",
            1 => "⚠️ 该视频已在队列中（下载/分析中或排队等待）",
            2 => "⚠️ 队列已满（最多5个排队中），请稍后再试",
            3 => "⚠️ 该视频已完成过或已取消，如需重新学习请回复确认",
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

        var (success, skipped) = _services.QueueRunner.EnqueueBatch(newBvids);
        if (success > 0)
            return $"✅ 已加入队列 {success} 个视频，跳过 {skipped} 个（队列满或重复），进度实时推送中...";
        return $"⚠️ 入队失败：成功 {success} 个，跳过 {skipped} 个";
    }

    /// <summary>取消分析</summary>
    public Task<string> CancelLearnAsync(string bvid)
    {
        if (_services.QueueRunner.Cancel(bvid))
        {
            _poke($"🛑 已取消: {bvid}");
            return Task.FromResult($"✅ 已取消: {bvid}");
        }
        return Task.FromResult($"⚠️ 未找到该视频（可能已完成/失败/不在队列中）: {bvid}");
    }

    /// <summary>获取队列状态</summary>
    public Task<string> GetQueueStatusAsync()
    {
        var snapshot = _services.QueueRunner.Snapshot;
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

    /// <summary>检查登录状态</summary>
    public async Task CheckLoginAsync()
    {
        try
        {
            var result = await _services.BiliApi.VerifyLoginAsync();
            if (result.Valid && result.IsLogin)
                _poke($"✅ 登录有效，用户: {result.UserName ?? result.Uname ?? "未知"} (UID: {result.Mid})");
            else
                _poke($"❌ 未登录或登录已失效: {result.Message}");
        }
        catch (Exception ex)
        {
            _poke($"❌ 检查登录失败: {ex.Message}");
        }
    }

    /// <summary>搜索B站视频</summary>
    public async Task<string> SearchBiliVideoAsync(string keyword, int count = 10)
    {
        try
        {
            var results = await _services.BiliApi.SearchVideosAsync(keyword, count);
            if (results.Count == 0)
                return "未找到相关视频";

            var sb = new StringBuilder();
            sb.AppendLine($"🔍 搜索 \"{keyword}\" 找到 {results.Count} 个视频：");
            for (int i = 0; i < results.Count; i++)
            {
                var v = results[i];
                sb.AppendLine($"{i + 1}. 【{v.Bvid}】{v.Title} - UP:{v.Author} | 播放:{v.PlayCount} | 时长:{v.Duration}");
            }
            _poke(sb.ToString());
            return sb.ToString();
        }
        catch (Exception ex)
        {
            _poke($"❌ 搜索失败: {ex.Message}");
            return $"❌ 搜索失败: {ex.Message}";
        }
    }

    /// <summary>扫码登录</summary>
    public async Task<string> QrVerifyAsync()
    {
        var qrInfo = await _services.BiliApi.GenerateQrCodeAsync();
        if (!qrInfo.Success)
            return $"❌ 生成二维码失败: {qrInfo.Message}";

        var workDir = _services.WorkDir;
        var qrDir = Path.Combine(workDir, "temp");
        var qrPng = QrCodeGenerator.GeneratePng(qrInfo.QrCodeUrl, qrDir, _logger);
        var qrBase64 = Convert.ToBase64String(File.ReadAllBytes(qrPng));

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(qrPng) { UseShellExecute = true });
            _logger.LogInformation("[BiliLearn] 已自动打开二维码: {0}", qrPng);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[BiliLearn] 自动打开二维码失败: {0}", ex.Message);
        }

        _poke($"📱 **请用B站APP扫码登录**\n\n![QR](data:image/png;base64,{qrBase64})\n\n二维码已自动打开，有效期2分钟！");

        // 后台轮询
        _ = Task.Run(async () =>
        {
            try
            {
                var deadline = DateTime.Now.AddMinutes(2);
                var lastStatus = -1;
                while (DateTime.Now < deadline)
                {
                    await Task.Delay(3000);
                    var poll = await _services.BiliApi.PollQrCodeStatusAsync(qrInfo.QrCodeKey);
                    if (poll.Status == 1)
                    {
                        _poke($"✅ 登录成功！欢迎 **{poll.UserName ?? "主人"}** (UID: {poll.Mid})！");
                        if (!string.IsNullOrEmpty(poll.Cookie))
                        {
                            _services.BiliApi.SetCookie(poll.Cookie);
                            _config.Cookie = poll.Cookie;
                            _onConfigChanged?.Invoke();
                            _poke("✅ Cookie已持久化，重启后仍保持登录状态");
                        }
                        return;
                    }
                    if (poll.Status == 0 && lastStatus != 0)
                    {
                        _poke("✅ 已扫码，请在手机上确认登录...");
                        lastStatus = 0;
                    }
                    else if (poll.Status == 2)
                    {
                        _poke("⚠️ 二维码已过期，请重新生成");
                        return;
                    }
                }
                _poke("⏰ 二维码已过期，请重试");
            }
            catch (Exception ex)
            {
                _poke($"❌ 登录过程异常: {ex.Message}");
            }
        });

        return "二维码已生成并自动打开，等待扫码结果推送...";
    }

    /// <summary>退出登录</summary>
    public Task<string> LogoutAsync()
    {
        _services.BiliApi.ClearCookie();
        _config.Cookie = "";
        _onConfigChanged?.Invoke();
        _poke("👋 已退出B站登录");
        return Task.FromResult("✅ 已退出登录");
    }

    /// <summary>清理临时文件</summary>
    public Task<string> CleanTempAsync()
    {
        var tempDir = Path.Combine(_services.WorkDir, "temp");
        if (!Directory.Exists(tempDir))
            return Task.FromResult("✅ 临时目录不存在，无需清理");

        int deleted = 0;
        foreach (var f in Directory.GetFiles(tempDir))
        {
            File.Delete(f);
            deleted++;
        }
        foreach (var d in Directory.GetDirectories(tempDir))
        {
            Directory.Delete(d, true);
            deleted++;
        }
        _poke($"🧹 已清理 {deleted} 个临时文件");
        return Task.FromResult($"✅ 清理完成，删除 {deleted} 项");
    }

    public void Dispose()
    {
        _services.QueueRunner?.Dispose();
        _services.Orchestrator?.Dispose();
    }
}

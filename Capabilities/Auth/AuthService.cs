
using System;
using System.IO;
using System.Threading.Tasks;
using BiliLearn.CSharp.Plugin.Utils;
using Microsoft.Extensions.Logging;

namespace BiliLearn.CSharp.Plugin.Capabilities.Auth;

/// <summary>
/// 认证服务实现：B站登录（迁绑自 V1 BiliLearnService 登录相关方法）
/// </summary>
public class AuthService : IAuthService
{
    private readonly BiliLearnServices _services;
    private readonly BiliLearnConfig _config;
    private readonly ILogger _logger;
    private readonly Func<string, Task> _poke;
    private readonly Action? _onConfigChanged;

    public AuthService(
        BiliLearnServices services,
        BiliLearnConfig config,
        ILogger logger,
        Func<string, Task> poke,
        Action? onConfigChanged = null)
    {
        _services = services;
        _config = config;
        _logger = logger;
        _poke = poke;
        _onConfigChanged = onConfigChanged;
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

    /// <summary>清理临时文件夹（temp目录下视频、音频、关键帧等缓存文件）</summary>
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

    /// <summary>退出登录</summary>
    public Task<string> LogoutAsync()
    {
        _services.BiliApi.ClearCookie();
        _config.Cookie = "";
        _onConfigChanged?.Invoke();
        _poke("👋 已退出B站登录");
        return Task.FromResult("✅ 已退出登录");
    }
}

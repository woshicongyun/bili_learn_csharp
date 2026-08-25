using System;
using System.IO;
using System.Threading.Tasks;
using BiliLearn.CSharp.Plugin.Utils;
using Microsoft.Extensions.Logging;

namespace BiliLearn.CSharp.Plugin.Capabilities.Auth;

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

    public async Task CheckLoginAsync()
    {
        try
        {
            var result = await _services.BiliApi.VerifyLoginAsync();
            if (result.Valid && result.IsLogin)
                await _poke($"✅ 登录有效，用户: {result.UserName ?? result.Uname ?? "未知"} (UID: {result.Mid})");
            else
                await _poke($"❌ 未登录或登录已失效: {result.Message}");
        }
        catch (Exception ex)
        {
            await _poke($"❌ 检查登录失败: {ex.Message}");
        }
    }

    public async Task QrVerifyAsync()
    {
        var qrInfo = await _services.BiliApi.GenerateQrCodeAsync();
        if (!qrInfo.Success)
        {
            await _poke($"❌ 生成二维码失败: {qrInfo.Message}");
            return;
        }

        var workDir = _services.WorkDir;
        var qrDir = Path.Combine(workDir, "temp");
        Directory.CreateDirectory(qrDir);
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

        await _poke($"📱 **请用B站APP扫码登录**\n\n![QR](data:image/png;base64,{qrBase64})\n\n二维码已自动打开，有效期2分钟！");

        // 使用fire-and-forget，确保Poke在主线程上下文中执行
        var pollTask = Task.Run(async () =>
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
                        _logger.LogInformation("[BiliLearn] 扫码登录成功: {UserName} (UID: {Mid})", poll.UserName, poll.Mid);
                        await _poke($"✅ 登录成功！欢迎 **{poll.UserName ?? "主人"}** (UID: {poll.Mid})！");
                        if (!string.IsNullOrEmpty(poll.Cookie))
                        {
                            _services.BiliApi.SetCookie(poll.Cookie);
                            _config.Cookie = poll.Cookie;
                            _onConfigChanged?.Invoke();
                            _logger.LogInformation("[BiliLearn] Cookie已持久化");
                            await _poke("✅ Cookie已持久化，重启后仍保持登录状态");
                        }
                        return;
                    }
                    if (poll.Status == 0 && lastStatus != 0)
                    {
                        _logger.LogInformation("[BiliLearn] 已扫码，等待用户确认...");
                        await _poke("✅ 已扫码，请在手机上确认登录...");
                        lastStatus = 0;
                    }
                    else if (poll.Status == 2)
                    {
                        _logger.LogWarning("[BiliLearn] 二维码已过期");
                        await _poke("⚠️ 二维码已过期，请重新生成");
                        return;
                    }
                }
                _logger.LogWarning("[BiliLearn] 扫码超时");
                await _poke("⏰ 二维码已过期，请重试");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[BiliLearn] 登录过程异常");
                await _poke($"❌ 登录过程异常: {ex.Message}");
            }
        });
        
        // 监听任务完成，防止静默失败
        pollTask.ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                _logger.LogError(t.Exception, "[BiliLearn] 扫码轮询任务异常终止");
            }
        }, TaskScheduler.Default);
    }

    public async Task CleanTempAsync()
    {
        var tempDir = Path.Combine(_services.WorkDir, "temp");
        if (!Directory.Exists(tempDir))
        {
            await _poke("🧹 临时目录不存在，无需清理");
            return;
        }

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
        await _poke($"🧹 已清理 {deleted} 个临时文件");
    }

    public async Task LogoutAsync()
    {
        _services.BiliApi.ClearCookie();
        _config.Cookie = "";
        _onConfigChanged?.Invoke();
        await _poke("👋 已退出B站登录");
    }
}

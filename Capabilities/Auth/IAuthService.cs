
using System.Threading.Tasks;

namespace BiliLearn.CSharp.Plugin.Capabilities.Auth;

/// <summary>
/// 认证服务接口：B站登录相关
/// </summary>
public interface IAuthService
{
    /// <summary>检查登录状态</summary>
    Task CheckLoginAsync();

    /// <summary>扫码登录</summary>
    Task<string> QrVerifyAsync();

    /// <summary>退出登录</summary>
    Task<string> LogoutAsync();

    /// <summary>清理临时文件夹（temp目录下视频、音频、关键帧等缓存文件）</summary>
    Task<string> CleanTempAsync();
}

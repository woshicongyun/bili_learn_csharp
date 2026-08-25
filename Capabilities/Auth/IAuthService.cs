using System.Threading.Tasks;

namespace BiliLearn.CSharp.Plugin.Capabilities.Auth;

public interface IAuthService
{
    Task CheckLoginAsync();
    Task QrVerifyAsync();
    Task CleanTempAsync();
    Task LogoutAsync();
}

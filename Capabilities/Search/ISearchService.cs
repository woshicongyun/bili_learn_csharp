
using System.Threading.Tasks;

namespace BiliLearn.CSharp.Plugin.Capabilities.Search;

/// <summary>
/// 搜索服务接口：B站视频搜索
/// </summary>
public interface ISearchService
{
    /// <summary>搜索B站视频</summary>
    Task<string> SearchBiliVideoAsync(string keyword, int count = 10);
}

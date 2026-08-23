
using System;
using System.Text;
using System.Threading.Tasks;
using BiliLearn.CSharp.Plugin.Utils;
using Microsoft.Extensions.Logging;

namespace BiliLearn.CSharp.Plugin.Capabilities.Search;

/// <summary>
/// 搜索服务实现：B站视频搜索（迁绑自 V1 BiliLearnService 搜索方法）
/// </summary>
public class SearchService : ISearchService
{
    private readonly BiliLearnServices _services;
    private readonly ILogger _logger;
    private readonly Func<string, Task> _poke;

    public SearchService(
        BiliLearnServices services,
        ILogger logger,
        Func<string, Task> poke)
    {
        _services = services;
        _logger = logger;
        _poke = poke;
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
}

using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace BiliLearn.CSharp.Plugin.Capabilities.Search;

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

    public async Task SearchBiliVideoAsync(string keyword, int count = 10)
    {
        try
        {
            var results = await _services.BiliApi.SearchVideosAsync(keyword, count);
            if (results.Count == 0)
            {
                await _poke($"🔍 未找到与「{keyword}」相关的视频");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"🔍 搜索 \"{keyword}\" 找到 {results.Count} 个视频：");
            for (int i = 0; i < results.Count && i < count; i++)
            {
                var v = results[i];
                sb.AppendLine($"{i + 1}. 【{v.Bvid}】{v.Title} - UP:{v.Author} | 播放:{v.PlayCount} | 时长:{v.Duration}");
            }
            await _poke(sb.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SearchService] 搜索失败");
            await _poke($"❌ 搜索失败: {ex.Message}");
        }
    }
}

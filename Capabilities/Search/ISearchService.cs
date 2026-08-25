using System.Threading.Tasks;

namespace BiliLearn.CSharp.Plugin.Capabilities.Search;

public interface ISearchService
{
    Task SearchBiliVideoAsync(string keyword, int count = 10);
}

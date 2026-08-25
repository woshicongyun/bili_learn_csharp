using System.Threading.Tasks;

namespace BiliLearn.CSharp.Plugin.Capabilities.Learn;

public interface ILearnService
{
    Task LearnAsync(string bvid);
    Task LearnBatchAsync(string bvidsCsv);
    Task CancelLearnAsync(string bvid);
    Task GetQueueStatusAsync();
    Task AnalyzeAsync(string bvid);
}

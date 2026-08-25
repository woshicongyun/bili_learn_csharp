using System.Collections.Generic;
using System.Threading.Tasks;
using BiliLearn.CSharp.Plugin.Models;

namespace BiliLearn.CSharp.Plugin.Domain.Interfaces;

/// <summary>
/// BiliLearn数据库访问接口
/// </summary>
public interface IBiliLearnStore
{
    // 队列操作
    Task<int> EnqueueAsync(QueueItem item);
    Task<QueueItem?> DequeueAsync();
    Task UpdateStatusAsync(int id, string status, string? stage = null, string? error = null);
    Task<List<QueueItem>> GetActiveTasksAsync();
    
    // 去重操作
    Task<bool> IsLearnedAsync(string bvid);
    Task MarkLearnedAsync(LearnedRecord record);
    
    // 历史操作
    Task AddHistoryAsync(HistoryRecord record);
    Task<List<HistoryRecord>> GetHistoryAsync(int limit = 20, int offset = 0);
    
    // 队列清理（返回清理数量）
    Task<int> CleanQueueAsync();
    
    // 评论操作
    Task SaveCommentsAsync(string bvid, List<CommentItem> comments);
    Task<List<CommentItem>> GetCommentsAsync(string bvid);
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BiliLearn.CSharp.Plugin.Models;
using BiliLearn.CSharp.Plugin.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace BiliLearn.CSharp.Plugin.Services;

public class JsonStore : IBiliLearnStore
{
    private readonly object _lock = new();
    private readonly string _filePath;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, QueueItem> _queueMap = new();
    private readonly ConcurrentDictionary<string, LearnedRecord> _learnedMap = new();
    private readonly List<HistoryRecord> _historyList = new();
    private bool _isDirty = false;
    private DateTime _lastSaveTime = DateTime.Now;
    private const int SaveIntervalMs = 3000;
    private int _nextId = 1;

    public JsonStore(string workDir, ILogger logger)
    {
        var dataDir = Path.Combine(workDir, "data");
        Directory.CreateDirectory(dataDir);
        _filePath = Path.Combine(dataDir, "json_store.json");
        _logger = logger;
        LoadFromDisk();
    }

    private void MarkDirty(bool force = false)
    {
        lock (_lock)
        {
            _isDirty = true;
            if (force || (DateTime.Now - _lastSaveTime).TotalMilliseconds > SaveIntervalMs)
                SaveToDiskInternal();
        }
    }

    private void Flush()
    {
        lock (_lock) SaveToDiskInternal();
    }

    private void SaveToDiskInternal()
    {
        if (!_isDirty) return;
        try
        {
            var data = new RootData
            {
                Queue = _queueMap.Values.OrderBy(q => q.Id).ToList(),
                Learned = _learnedMap.Values.ToList(),
                History = _historyList,
                NextId = _nextId
            };
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            var tempPath = _filePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _filePath, overwrite: true);
            _isDirty = false;
            _lastSaveTime = DateTime.Now;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[JsonStore] 落盘失败");
        }
    }

    private void LoadFromDisk()
    {
        if (!File.Exists(_filePath)) return;
        try
        {
            var json = File.ReadAllText(_filePath);
            var data = JsonSerializer.Deserialize<RootData>(json);
            if (data == null) return;
            foreach (var q in data.Queue) _queueMap[q.Bvid] = q;
            foreach (var l in data.Learned) _learnedMap[l.Bvid] = l;
            if (data.History != null) _historyList.AddRange(data.History);
            if (data.NextId > 0) _nextId = data.NextId;
            var dangling = _queueMap.Values.Where(q => q.Status is "Downloading" or "Analyzing" or "Processing").ToList();
            if (dangling.Count > 0)
            {
                foreach (var q in dangling)
                {
                    _queueMap[q.Bvid] = q with { Status = "Queued", Stage = null };
                    _logger.LogInformation("[JsonStore] 重启重置 {Bvid} 为 Queued", q.Bvid);
                }
                SaveToDiskInternal();
            }
            _logger.LogInformation("[JsonStore] 加载完成：队列{Queues} 已学{Learned} 历史{History}",
                _queueMap.Count, _learnedMap.Count, _historyList.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[JsonStore] data.json 损坏");
        }
    }

    public Task<int> EnqueueAsync(QueueItem item)
    {
        var newItem = item with { Id = _nextId++, UpdatedAt = DateTime.Now };
        _queueMap[newItem.Bvid] = newItem;
        MarkDirty(force: true);
        return Task.FromResult(newItem.Id);
    }

    public Task<QueueItem?> DequeueAsync()
    {
        var item = _queueMap.Values.Where(q => q.Status == "Queued").OrderBy(q => q.Id).FirstOrDefault();
        if (item != null)
        {
            var updated = item with { Status = "Processing", Stage = "下载中", UpdatedAt = DateTime.Now };
            _queueMap[item.Bvid] = updated;
            MarkDirty(force: true);
            return Task.FromResult<QueueItem?>(updated);
        }
        return Task.FromResult<QueueItem?>(null);
    }

    public Task UpdateStatusAsync(int id, string status, string? stage = null, string? error = null)
    {
        var kv = _queueMap.FirstOrDefault(kv => kv.Value.Id == id);
        if (kv.Key == null) return Task.CompletedTask;
        var updated = kv.Value with { Status = status, Stage = stage ?? kv.Value.Stage, Error = error ?? kv.Value.Error, UpdatedAt = DateTime.Now };
        _queueMap[kv.Key] = updated;
        MarkDirty(force: status is "Completed" or "Failed" or "Canceled");
        return Task.CompletedTask;
    }

    public Task<List<QueueItem>> GetActiveTasksAsync()
    {
        var active = _queueMap.Values.Where(q => q.Status is "Queued" or "Downloading" or "Downloaded" or "Analyzing" or "Processing").ToList();
        return Task.FromResult(active);
    }

    public Task<bool> IsLearnedAsync(string bvid) => Task.FromResult(_learnedMap.ContainsKey(bvid));

    public Task MarkLearnedAsync(LearnedRecord record)
    {
        _learnedMap[record.Bvid] = record;
        MarkDirty(force: true);
        return Task.CompletedTask;
    }

    public Task AddHistoryAsync(HistoryRecord record)
    {
        _historyList.Add(record);
        MarkDirty(force: true);
        return Task.CompletedTask;
    }

    public Task<List<HistoryRecord>> GetHistoryAsync(int limit = 20, int offset = 0)
    {
        var result = _historyList.OrderByDescending(h => h.LearnedAt).Skip(offset).Take(limit).ToList();
        return Task.FromResult(result);
    }

    public Task<int> CleanQueueAsync()
    {
        lock (_lock)
        {
            var allItems = _queueMap.Values.ToList();
            var toRemove = allItems.Where(q => q.Status is "Completed" or "Failed").ToList();
            _logger.LogInformation("[JsonStore] CleanQueue: 队列总数={Total}, 待清理={Clean}", allItems.Count, toRemove.Count);
            foreach (var item in toRemove)
            {
                _logger.LogInformation("[JsonStore] CleanQueue: 移除 {Bvid} [{Status}]", item.Bvid, item.Status);
                _queueMap.TryRemove(item.Bvid, out _);
            }
            _isDirty = true; // 强制标记为脏，确保SaveToDiskInternal()执行
            Flush();
            return Task.FromResult(toRemove.Count);
        }
    }

    public async Task SaveCommentsAsync(string bvid, List<CommentItem> comments)
    {
        lock (_lock)
        {
            var data = JsonSerializer.Deserialize<RootData>(File.ReadAllText(_filePath));
            if (data != null)
            {
                data.Comments[bvid] = comments;
                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                var tempPath = _filePath + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, _filePath, overwrite: true);
                _logger.LogInformation("[JsonStore] 保存评论成功: {Bvid} 共{Count}条", bvid, comments.Count);
            }
        }
        await Task.CompletedTask;
    }

    public async Task<List<CommentItem>> GetCommentsAsync(string bvid)
    {
        lock (_lock)
        {
            if (!File.Exists(_filePath))
                return new List<CommentItem>();
                
            var data = JsonSerializer.Deserialize<RootData>(File.ReadAllText(_filePath));
            if (data?.Comments.TryGetValue(bvid, out var comments) == true)
                return comments;
            return new List<CommentItem>();
        }
        await Task.CompletedTask;
    }

    public void DisposeFlush()
    {
        Flush();
    }

    private class RootData
    {
        public List<QueueItem> Queue { get; set; } = new();
        public List<LearnedRecord> Learned { get; set; } = new();
        public List<HistoryRecord> History { get; set; } = new();
        public Dictionary<string, List<CommentItem>> Comments { get; set; } = new();
        public int NextId { get; set; } = 1;
    }
}

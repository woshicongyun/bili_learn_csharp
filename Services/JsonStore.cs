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

/// <summary>
/// JSON持久化存储（内存为主 + 节流写入 + 原子替换）
/// 策略：
///   - 频繁状态更新：脏标记 + 节流（3秒合并写盘）
///   - 关键操作（入队/出队/完成/取消/历史追加）：立即强制刷盘
///   - 单一文件 + tmp原子替换，保证最终一致
/// </summary>
public class JsonStore : IBiliLearnStore
{
    private readonly object _lock = new();
    private readonly string _filePath;
    private readonly ILogger _logger;

    // 内存数据（随时修改）
    private readonly ConcurrentDictionary<string, QueueItem> _queueMap = new();
    private readonly ConcurrentDictionary<string, LearnedRecord> _learnedMap = new();
    private readonly List<HistoryRecord> _historyList = new();

    private bool _isDirty = false;
    private DateTime _lastSaveTime = DateTime.Now;
    private const int SaveIntervalMs = 3000; // 3秒内最多写一次磁盘
    private int _nextId = 1;

    public JsonStore(string workDir, ILogger logger)
    {
        var dataDir = Path.Combine(workDir, "data");
        Directory.CreateDirectory(dataDir);
        _filePath = Path.Combine(dataDir, "json_store.json");
        _logger = logger;
        LoadFromDisk();
    }

    // ---- 节流保存 ----
    private void MarkDirty(bool force = false)
    {
        lock (_lock)
        {
            _isDirty = true;
            if (force || (DateTime.Now - _lastSaveTime).TotalMilliseconds > SaveIntervalMs)
            {
                SaveToDiskInternal();
            }
        }
    }

    /// <summary>强制立即落盘（关键操作调用）</summary>
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

            // 启动恢复：Downloading/Analyzing 重置为 Queued（连接已断需重试）
            var dangling = _queueMap.Values.Where(q =>
                q.Status is "Downloading" or "Analyzing" or "Processing").ToList();
            if (dangling.Count > 0)
            {
                foreach (var q in dangling)
                {
                    _queueMap[q.Bvid] = q with { Status = "Queued", Stage = null };
                    _logger.LogInformation("[JsonStore] 重启重置 {Bvid} 为 Queued（原状态 {Status}）", q.Bvid, q.Status);
                }
                SaveToDiskInternal();
            }
            _logger.LogInformation("[JsonStore] 加载完成：队列{Queues} 已学{Learned} 历史{History}",
                _queueMap.Count, _learnedMap.Count, _historyList.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[JsonStore] data.json 损坏，已降级为空状态启动");
        }
    }

    // ---- IBiliLearnStore 实现 ----
    public Task<int> EnqueueAsync(QueueItem item)
    {
        var newItem = item with { Id = _nextId++, UpdatedAt = DateTime.Now };
        _queueMap[newItem.Bvid] = newItem;
        MarkDirty(force: true); // 入队是用户手动触发，立即落盘
        return Task.FromResult(newItem.Id);
    }

    public Task<QueueItem?> DequeueAsync()
    {
        var item = _queueMap.Values
            .Where(q => q.Status == "Queued")
            .OrderBy(q => q.Id)
            .FirstOrDefault();
        if (item != null)
        {
            var updated = item with { Status = "Processing", Stage = "下载中", UpdatedAt = DateTime.Now };
            _queueMap[item.Bvid] = updated;
            MarkDirty(force: true); // 出队关键操作，立即落盘
            return Task.FromResult<QueueItem?>(updated);
        }
        return Task.FromResult<QueueItem?>(null);
    }

    public Task UpdateStatusAsync(int id, string status, string? stage = null, string? error = null)
    {
        var kv = _queueMap.FirstOrDefault(kv => kv.Value.Id == id);
        if (kv.Key == null) return Task.CompletedTask;
        var updated = kv.Value with
        {
            Status = status,
            Stage = stage ?? kv.Value.Stage,
            Error = error ?? kv.Value.Error,
            UpdatedAt = DateTime.Now
        };
        _queueMap[kv.Key] = updated;
        // 终态立即落盘，中间状态节流
        MarkDirty(force: status is "Completed" or "Failed" or "Canceled");
        return Task.CompletedTask;
    }

    public Task<List<QueueItem>> GetActiveTasksAsync()
    {
        var active = _queueMap.Values
            .Where(q => q.Status is "Queued" or "Downloading" or "Downloaded" or "Analyzing" or "Processing")
            .ToList();
        return Task.FromResult(active);
    }

    public Task<bool> IsLearnedAsync(string bvid) =>
        Task.FromResult(_learnedMap.ContainsKey(bvid));

    public Task MarkLearnedAsync(LearnedRecord record)
    {
        _learnedMap[record.Bvid] = record;
        MarkDirty(force: true); // 学习成果，必须落盘
        return Task.CompletedTask;
    }

    public Task AddHistoryAsync(HistoryRecord record)
    {
        _historyList.Add(record);
        MarkDirty(force: true); // 历史记录，不能丢
        return Task.CompletedTask;
    }

    public Task<List<HistoryRecord>> GetHistoryAsync(int limit = 20, int offset = 0)
    {
        var result = _historyList
            .OrderByDescending(h => h.LearnedAt)
            .Skip(offset)
            .Take(limit)
            .ToList();
        return Task.FromResult(result);
    }

    /// <summary>程序退出前调用，确保最后数据落盘</summary>
    public void DisposeFlush()
    {
        Flush();
    }

    private class RootData
    {
        public List<QueueItem> Queue { get; set; } = new();
        public List<LearnedRecord> Learned { get; set; } = new();
        public List<HistoryRecord> History { get; set; } = new();
        public int NextId { get; set; } = 1;
    }
}

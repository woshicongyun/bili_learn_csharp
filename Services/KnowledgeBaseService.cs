using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using BiliLearn.CSharp.Plugin.Domain.Interfaces;

namespace BiliLearn.CSharp.Plugin.Services;

public class KnowledgeEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public string Bvid { get; set; } = "";
    public string Title { get; set; } = "";
    public string Category { get; set; } = "其他";
    public string Summary { get; set; } = "";
    public string? Uploader { get; set; }
    public int Duration { get; set; }
    public string? Description { get; set; }
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, string> Metadata { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// 知识库服务：保存学习记录到JSON索引
/// </summary>
public class KnowledgeBaseService : IKnowledgeRepository
{
    private readonly ILogger _logger;
    private readonly string _storagePath;
    private readonly string _indexPath;
    private readonly string _detailDir;

    public KnowledgeBaseService(ILogger logger, string storageRoot)
    {
        _logger = logger;
        _storagePath = Path.Combine(storageRoot, "knowledge");
        _indexPath = Path.Combine(_storagePath, "index.json");
        _detailDir = Path.Combine(_storagePath, "details");
        Directory.CreateDirectory(_detailDir);
    }

    public async Task SaveAsync(KnowledgeEntry entry, CancellationToken ct = default)
    {
        try
        {
            lock (this)
            {
                var entries = LoadAll();

                var existing = entries.FirstOrDefault(e => e.Bvid == entry.Bvid);
                if (existing != null)
                {
                    entries.Remove(existing);
                    _logger.LogInformation("更新已有知识条目: {Title}", entry.Title);
                }

                entries.Insert(0, entry);
                SaveAll(entries);
            }

            // 保存详情
            var detailPath = Path.Combine(_detailDir, $"{entry.Id}.json");
            await File.WriteAllTextAsync(detailPath,
                JsonSerializer.Serialize(entry, new JsonSerializerOptions { WriteIndented = true }));

            _logger.LogInformation("✅ 知识库保存成功: {Title} (分类: {Category})", entry.Title, entry.Category);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "知识库保存失败");
        }
    }

    public List<KnowledgeEntry> LoadAll()
    {
        try
        {
            if (!File.Exists(_indexPath)) { _logger.LogWarning("[KB] index.json 不存在: {Path}", _indexPath); return new(); }
            var json = File.ReadAllText(_indexPath, System.Text.Encoding.UTF8);
            var list = JsonSerializer.Deserialize<List<KnowledgeEntry>>(json) ?? new();
            _logger.LogInformation("[KB] LoadAll 加载 {Count} 条, 首条标题: {Title}", list.Count, list.Count > 0 ? list[0].Title : "(空)");
            return list;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[KB] LoadAll 失败");
            return new();
        }
    }

    private void SaveAll(List<KnowledgeEntry> entries)
    {
        var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_indexPath, json);
    }

    public List<KnowledgeEntry> Search(string keyword, string? category = null)
    {
        var entries = LoadAll();
        var query = keyword.ToLowerInvariant();
        return entries.Where(e =>
            (string.IsNullOrEmpty(category) || e.Category == category) &&
            (e.Title.ToLowerInvariant().Contains(query) ||
             e.Summary.ToLowerInvariant().Contains(query) ||
             e.Tags.Any(t => t.ToLowerInvariant().Contains(query)))
        ).ToList();
    }

    public List<KnowledgeEntry> GetAll()
    {
        return LoadAll();
    }

    public Dictionary<string, int> GetStats()
    {
        var entries = LoadAll();
        return entries.GroupBy(e => e.Category).ToDictionary(g => g.Key, g => g.Count());
    }
    
    public void Dispose()
    {
        // 无资源需要释放
    }
}

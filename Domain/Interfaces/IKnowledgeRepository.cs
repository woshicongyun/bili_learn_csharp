using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BiliLearn.CSharp.Plugin.Services;

namespace BiliLearn.CSharp.Plugin.Domain.Interfaces;

/// <summary>
/// 知识库存取抽象接口
/// 对应现有 KnowledgeBaseService
/// </summary>
public interface IKnowledgeRepository : IDisposable
{
    /// <summary>
    /// 保存知识条目（自动去重）
    /// </summary>
    Task SaveAsync(KnowledgeEntry entry, CancellationToken ct = default);
    
    /// <summary>
    /// 搜索知识库
    /// </summary>
    List<KnowledgeEntry> Search(string keyword, string? category = null);
    
    /// <summary>
    /// 获取所有条目
    /// </summary>
    List<KnowledgeEntry> GetAll();
    
    /// <summary>
    /// 获取分类统计
    /// </summary>
    Dictionary<string, int> GetStats();
}

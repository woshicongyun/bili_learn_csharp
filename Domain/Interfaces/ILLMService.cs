using System;
using System.Threading;
using System.Threading.Tasks;

namespace BiliLearn.CSharp.Plugin.Domain.Interfaces;

/// <summary>
/// LLM服务抽象接口
/// 对应现有: AlifeLLMAdapter, OpenAICompatibleClient
/// </summary>
public interface ILLMService : IDisposable
{
    /// <summary>
    /// 发起单轮对话，返回文本
    /// </summary>
    Task<string?> ChatAsync(string prompt, int maxTokens = 2000, double temperature = 0.7, CancellationToken ct = default);
}

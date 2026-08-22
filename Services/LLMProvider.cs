
using System.Threading;
using System.Threading.Tasks;

namespace BiliLearn.CSharp.Plugin.Services;

/// <summary>
/// LLM统一接口，屏蔽底层不同实现（Alife内置模型 / DeepSeek外部API）
/// </summary>
public interface LLMProvider
{
    /// <summary>
    /// 发起单轮对话请求，返回回复文本；失败返回 null
    /// </summary>
    Task<string?> ChatAsync(string prompt, int maxTokens = 2000, CancellationToken cancellationToken = default);
}

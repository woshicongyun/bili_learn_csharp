using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Alife.Framework;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Agents;
using BiliLearn.CSharp.Plugin.Domain.Interfaces;

namespace BiliLearn.CSharp.Plugin.Services;

/// <summary>
/// 基于Alife内置语言模型的LLM适配器，复用框架的ILanguageModel能力，
/// 避免依赖外部DeepSeek API。
/// </summary>
public class AlifeLLMAdapter : ILLMService, IDisposable
{
    private readonly ILanguageModel _languageModel;
    private readonly ILogger _logger;
    private bool _disposed = false;

    public AlifeLLMAdapter(ILanguageModel languageModel, ILogger logger)
    {
        _languageModel = languageModel;
        _logger = logger;
    }

    /// <summary>
    /// 发起单轮对话，使用独立的ChatHistoryAgentThread，不影响主对话上下文
    /// </summary>
    public async Task<string?> ChatAsync(string prompt, int maxTokens = 2000, double temperature = 0.7, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            // 独立会话线程，与主对话完全隔离
            var thread = new ChatHistoryAgentThread();
            thread.ChatHistory.AddUserMessage(prompt);

            var resultBuilder = new StringBuilder();

            await _languageModel.ChatStreamingAsync(
                thread,
                text => resultBuilder.Append(text),   // 文本回传
                think => { /* 思考过程忽略 */ },
                usage => _logger.LogInformation("[AlifeLLMAdapter] Token使用: {Usage}", usage),
                exception => _logger.LogWarning("[AlifeLLMAdapter] 流式异常: {Exception}", exception.Message),
                cancellationToken
            );

            string result = resultBuilder.ToString();
            _logger.LogInformation("[AlifeLLMAdapter] 回复完成: {Len}字符", result.Length);
            return result.Length > 0 ? result : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AlifeLLMAdapter] 请求失败");
            return null;
        }
    }
    
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}

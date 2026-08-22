using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using BiliLearn.CSharp.Plugin.Domain.Interfaces;

namespace BiliLearn.CSharp.Plugin.Services;

/// <summary>
/// OpenAI 规范兼容 API 客户端
/// 支持所有 OpenAI 协议的服务：DeepSeek / OpenAI / 通义 / 硅基流动 / Moonshot 等
/// 通过 BaseUrl + Model 配置即可切换供应商，无需改代码
/// </summary>
public class OpenAICompatibleClient : ILLMService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly string _model;
    private readonly ILogger _logger;
    private bool _disposed = false;

    public OpenAICompatibleClient(
        string apiKey,
        ILogger logger,
        string baseUrl = "https://api.deepseek.com/v1",
        string model = "deepseek-chat")
    {
        _apiKey = apiKey;
        _logger = logger;
        _baseUrl = baseUrl.TrimEnd("/");
        _model = model;

        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(5);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var payload = new
        {
            model = _model,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            temperature = 0.3,
            max_tokens = 2000
        };

        var json = JsonSerializer.Serialize(payload);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var resp = await _httpClient.PostAsync(_baseUrl + "/chat/completions", httpContent, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogError("[OpenAICompatible] API error {Code}: {Body}", resp.StatusCode, body);
                throw new Exception($"LLM API 调用失败: {resp.StatusCode} {body}");
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var content = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            return content?.Trim() ?? "";
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OpenAICompatible] 调用LLM失败");
            throw;
        }
    }

    public async Task<string?> ChatAsync(string prompt, int maxTokens = 2000, double temperature = 0.7, CancellationToken ct = default)
    {
        var payload = new
        {
            model = _model,
            messages = new object[]
            {
                new { role = "system", content = "You are a helpful assistant." },
                new { role = "user", content = prompt }
            },
            temperature = temperature,
            max_tokens = maxTokens
        };

        var json = JsonSerializer.Serialize(payload);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var resp = await _httpClient.PostAsync(_baseUrl + "/chat/completions", httpContent, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogError("[OpenAICompatible] API error {Code}: {Body}", resp.StatusCode, body);
                throw new Exception($"LLM API 调用失败: {resp.StatusCode} {body}");
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var content = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            return content?.Trim() ?? "";
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OpenAICompatible] 调用LLM失败");
            throw;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _httpClient?.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}

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
/// DeepSeek API 客户端
/// </summary>
public class DeepSeekAI : ILLMService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly string _model;
    private readonly ILogger _logger;
    private bool _disposed = false;

    public DeepSeekAI(string apiKey, ILogger logger, string baseUrl = "https://api.deepseek.com/v1", string model = "deepseek-chat")
    {
        _apiKey = apiKey;
        _baseUrl = baseUrl.TrimEnd('/');
        _model = model;
        _logger = logger;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.Timeout = TimeSpan.FromSeconds(120);
    }

    /// <summary>
    /// 发起聊天请求
    /// </summary>
    public async Task<string?> ChatAsync(string prompt, int maxTokens = 2000, double temperature = 0.7, CancellationToken ct = default)
    {
        try
        {
            var payload = new
            {
                model = _model,
                messages = new[] { new { role = "user", content = prompt } },
                max_tokens = maxTokens,
                temperature = temperature
            };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync($"{_baseUrl}/chat/completions", content, ct);

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("DeepSeek API 错误: {Status} {Body}", response.StatusCode, responseBody);
                return null;
            }

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var content2 = choices[0].GetProperty("message").GetProperty("content").GetString();
                if (!string.IsNullOrEmpty(content2)) return content2;
            }

            _logger.LogWarning("DeepSeek API 返回异常: {Body}", responseBody.Length > 500 ? responseBody[..500] : responseBody);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DeepSeek 请求失败");
            return null;
        }
    }
    
    public void Dispose()
    {
        if (!_disposed)
        {
            _httpClient?.Dispose();
            _disposed = true;
        }
    }
}

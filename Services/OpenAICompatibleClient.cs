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
        string baseUrl = \

using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Alife.Foundation;
using Alife.Framework;
using Alife.Function.AIModelUtility;
using Alife.Function.FunctionCaller;
using BiliLearn.CSharp.Plugin.Domain.Interfaces;
using BiliLearn.CSharp.Plugin.Capabilities.Learn;
using BiliLearn.CSharp.Plugin.Capabilities.Auth;
using BiliLearn.CSharp.Plugin.Capabilities.Search;
using Microsoft.Extensions.Logging;

namespace BiliLearn.CSharp.Plugin;

public class BiliLearnConfig
{
    [Description("B站 Cookie（格式 k1=v1; k2=v2）")]
    public string Cookie { get; set; } = "";

    [Description("LLM API Key（OpenAI规范，支持DeepSeek/OpenAI/通义/硅基流动/Moonshot等）")]
    public string LlmApiKey { get; set; } = "";

    [Description("LLM API Base URL（OpenAI规范，默认DeepSeek）")]
    public string LlmBaseUrl { get; set; } = "https://api.deepseek.com/v1";

    [Description("LLM 模型ID（如 deepseek-chat / gpt-4o / qwen-plus 等）")]
    public string LlmModel { get; set; } = "deepseek-chat";

    [Description("工作目录（留空使用插件目录）")]
    public string WorkDir { get; set; } = "";

    [Description("视觉分析：抽取关键帧的间隔（秒），默认15")]
    public int FrameExtractInterval { get; set; } = 15;

    [Description("视觉分析：最大抽取帧数，默认20")]
    public int MaxFrames { get; set; } = 20;

    [Description("优先使用Alife内置语言模型（为false时使用OpenAI规范API）")]
    public bool UseAlifeLLM { get; set; } = true;
}

[Module("B站学习分析",
    "使用alife框架的视频模型、音频识别和语言模型，爬取并分析B站视频内容并归档到知识库",
    defaultCategory: "真央的插件")]

public class BiliLearnModule(
    XmlFunctionCaller functionCaller,
    IVisionModel? visionModel,
    IAudioRecognizerProvider? audioRecognizerProvider,
    ILogger<BiliLearnModule> logger,
    Interactor<BiliLearnModule> interactor,
    ILanguageModel? languageModel = null) :
    ChatBehaviour,
    IConfigurable<BiliLearnConfig>
{
    public BiliLearnConfig Configuration { get; set; } = new();

    private ILearnService? _service;
    private ConfirmationService? _confirmationService;
    private ILearnQueue? _queueRunner;
    private IKnowledgeRepository? _knowledgeRepo;
    private IAuthService? _authService;
    private ISearchService? _searchService;

    private readonly object _initLock = new();
    private bool _initialized = false;

    protected override async Task OnAwake()
    {
        functionCaller.RegisterHandler(new XmlHandler(this));
        logger.LogInformation("[BiliLearn] 插件已加载，等待配置注入后初始化");
        
        // 初始化服务
        EnsureInitialized();
        logger.LogInformation("[BiliLearn] 服务初始化完成");
        
        await Task.CompletedTask;
    }

    private void EnsureInitialized()
    {
        if (_initialized) return;
        lock (_initLock)
        {
            if (_initialized) return;
            try
            {
                var services = Bootstrapper.Build(
                    Configuration, visionModel, audioRecognizerProvider,
                    languageModel, logger, interactor);

                _queueRunner = services.LearnQueue;
                _knowledgeRepo = services.KnowledgeRepo;
                _service = services.LearnService;
                _confirmationService = new ConfirmationService(
                    logger,
                    msg => { interactor.Poke(msg); return Task.CompletedTask; },
                    services.AnalyzeService.ProcessAsync);

                _authService = new AuthService(
                    services,
                    Configuration,
                    logger,
                    msg => { interactor.Poke(msg); return Task.CompletedTask; },
                    SaveConfigToDisk);

                _searchService = new SearchService(
                    services,
                    logger,
                    msg => { interactor.Poke(msg); return Task.CompletedTask; });

                _queueRunner.Start();
                _initialized = true;
                logger.LogInformation("[BiliLearn] 初始化完成");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[BiliLearn] 初始化失败");
            }
        }
    }

    [XmlFunction(FunctionMode.OneShot)]
    [Description("分析B站视频：输入BV号，自动获取视频信息、下载、提取关键帧、ASR转写、字幕解析，并生成结构化总结归档到知识库")]
    public async Task<string> Learn([Description("B站视频BV号，如 BV1xx411c7mD")] string bvid)
    {
        EnsureInitialized();
        logger.LogInformation("[BiliLearn] Learn called, bvid={Bvid}, _service is null: {IsNull}", bvid, _service == null);
        if (_service == null)
            return "❌ LearnService未初始化";
        return await _service.LearnAsync(bvid);
    }

    [XmlFunction(FunctionMode.OneShot)]
    [Description("批量分析B站视频：输入多个BV号（用逗号分隔），自动加入队列并行下载、串行分析")]
    public async Task<string> LearnBatch([Description("多个B站视频BV号，用逗号分隔，如 BV1xx411c7mD,BV1yy222c8mF")] string bvids)
    {
        EnsureInitialized();
        return await _service!.LearnBatchAsync(bvids);
    }

    [XmlFunction(FunctionMode.OneShot)]
    [Description("取消正在进行的B站视频分析")]
    public async Task<string> CancelLearn([Description("B站视频BV号")] string bvid)
    {
        EnsureInitialized();
        return await _service!.CancelLearnAsync(bvid);
    }

    [XmlFunction(FunctionMode.OneShot)]
    [Description("获取当前B站学习队列的状态")]
    public async Task<string> QueueStatus()
    {
        EnsureInitialized();
        logger.LogInformation("[BiliLearn] QueueStatus called, _service is null: {IsNull}, _queueRunner is null: {QueueNull}", _service == null, _queueRunner == null);
        if (_service == null)
            return "❌ LearnService未初始化";
        try
        {
            var result = await _service.GetQueueStatusAsync();
            logger.LogInformation("[BiliLearn] QueueStatus result: {Result}", result);
            interactor.Poke(result);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[BiliLearn] QueueStatus exception");
            return $"❌ 获取队列状态异常: {ex.Message}";
        }
    }

    [XmlFunction(FunctionMode.OneShot)]
    [Description("检查B站登录状态和账号信息")]
    public async Task CheckLogin()
    {
        EnsureInitialized();
        await _authService!.CheckLoginAsync();
    }

    [XmlFunction(FunctionMode.OneShot)]
    [Description("搜索B站视频：按关键词搜索，返回视频列表（含BV号、标题、UP主、时长、播放量等）")]
    public async Task<string> SearchBiliVideo([Description("搜索关键词")] string keyword, [Description("返回结果数量，默认10")] int count = 10)
    {
        EnsureInitialized();
        return await _searchService!.SearchBiliVideoAsync(keyword, count);
    }

    [XmlFunction(FunctionMode.OneShot)]
    [Description("通过扫码方式登录B站，生成二维码供用户扫码，扫码成功后自动获取Cookie并完成登录")]
    public async Task<string> QrVerify()
    {
        EnsureInitialized();
        return await _authService!.QrVerifyAsync();
    }

    [XmlFunction(FunctionMode.OneShot)]
    [Description("退出B站登录")]
    public async Task<string> Logout()
    {
        EnsureInitialized();
        return await _authService!.LogoutAsync();
    }

    [XmlFunction(FunctionMode.OneShot)]
    [Description("清理临时文件夹（temp目录下视频、音频、关键帧等缓存文件），保持插件根目录整洁")]
    public async Task<string> CleanTemp()
    {
        EnsureInitialized();
        return await _authService!.CleanTempAsync();
    }

    protected async void OnMessageReceived(string message)
    {
        EnsureInitialized();
        await _confirmationService!.OnMessageReceivedAsync(message);
    }

    private void SaveConfigToDisk()
    {
        try
        {
            var cfgPath = Path.Combine(AlifePath.StorageFolderPath, "Configuration/BiliLearn.CSharp.Plugin.BiliLearnModule.json");
            string? dir = Path.GetDirectoryName(cfgPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            string json = System.Text.Json.JsonSerializer.Serialize(Configuration, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(cfgPath, json);
            logger.LogInformation("[BiliLearn] 配置已持久化到 {0}", cfgPath);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[BiliLearn] 持久化配置到磁盘失败");
        }
    }

    public void Dispose()
    {
        _service?.Dispose();
        _confirmationService?.Dispose();
    }
}

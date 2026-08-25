
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Alife.Foundation;
using Alife.Framework;
using Alife.Function.AIModelUtility;
using Alife.Function.FunctionCaller;
using BiliLearn.CSharp.Plugin.Capabilities.Auth;
using BiliLearn.CSharp.Plugin.Capabilities.Learn;
using BiliLearn.CSharp.Plugin.Capabilities.Search;
using BiliLearn.CSharp.Plugin.Domain.Interfaces;
using BiliLearn.CSharp.Plugin.Models;
using Microsoft.Extensions.Logging;

namespace BiliLearn.CSharp.Plugin;

public class BiliLearnConfig
{
    [Description("B站 Cookie")]
    public string Cookie { get; set; } = "";
    [Description("LLM API Key")]
    public string LlmApiKey { get; set; } = "";
    [Description("LLM API Base URL")]
    public string LlmBaseUrl { get; set; } = "https://api.deepseek.com/v1";
    [Description("LLM 模型ID")]
    public string LlmModel { get; set; } = "deepseek-chat";
    [Description("工作目录")]
    public string WorkDir { get; set; } = "";
    [Description("优先使用Alife内置语言模型")]
    public bool UseAlifeLLM { get; set; } = true;
    [Description("HTTP请求超时（秒）")]
    public int HttpTimeoutSeconds { get; set; } = 300;
    [Description("最大重试次数")]
    public int MaxRetries { get; set; } = 3;
    [Description("分片大小（字节）")]
    public int ChunkSize { get; set; } = 512 * 1024;
    [Description("并发分片数")]
    public int MaxConcurrentSegments { get; set; } = 4;
    [Description("重试基础延迟（秒）")]
    public double RetryBaseDelaySeconds { get; set; } = 1.5;
    [Description("UA标识")]
    public string UserAgent { get; set; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36";
    [Description("视觉分析间隔（秒）")]
    public int FrameExtractInterval { get; set; } = 15;
    [Description("最大抽取帧数")]
    public int MaxFrames { get; set; } = 20;
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

    private LearnService? _learnService;
    private ConfirmationService? _confirmationService;
    private ILearnQueue? _queueRunner;
    private IBiliLearnStore? _store;
    private IAuthService? _authService;
    private ISearchService? _searchService;

    protected override async Task OnAwake()
    {
        // 注册 XmlHandler，挂 DestroyCancellationToken 以支持热重载
        XmlHandler xmlHandler = new(this) {
            Description = "此服务可以为你提供B站视频学习分析功能。",
            Explanation = "提供B站视频分析、批量学习、队列管理、扫码登录、视频搜索等能力。"
        };
        functionCaller.RegisterHandler(xmlHandler,
            DocumentMode.Implicit,
            cancellationToken: DestroyCancellationToken);

        try
        {
            var services = Bootstrapper.Build(
                Configuration, visionModel, audioRecognizerProvider,
                languageModel, logger, interactor);

            _queueRunner = services.LearnQueue;
            _store = services.Store;
            _learnService = services.LearnService;
            _confirmationService = new ConfirmationService(
                logger,
                msg => { interactor.Poke(msg); return Task.CompletedTask; },
                services.AnalyzeService.ProcessAsync,
                services.Store);

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
            logger.LogInformation("[BiliLearn] 初始化完成");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[BiliLearn] 初始化失败");
        }
    }

    protected override Task OnDestroy()
    {
        _queueRunner?.Stop();
        _confirmationService?.Dispose();
        logger.LogInformation("[BiliLearn] 插件已卸载");
        return Task.CompletedTask;
    }

    [XmlFunction(FunctionMode.OneShot)]
    [Description("分析B站视频：输入BV号")]
    public async Task Learn([Description("B站视频BV号")] string bvid)
    {
        if (_learnService == null)
        {
            interactor.Poke("❌ LearnService未初始化");
            return;
        }
        await _learnService.LearnAsync(bvid);
    }

    [XmlFunction(FunctionMode.OneShot)]
    [Description("批量分析B站视频")]
    public async Task LearnBatch([Description("多个BV号，逗号分隔")] string bvids)
    {
        if (_learnService == null)
        {
            interactor.Poke("❌ LearnService未初始化");
            return;
        }
        await _learnService.LearnBatchAsync(bvids);
    }

    [XmlFunction(FunctionMode.OneShot)]
    [Description("取消正在进行的B站视频分析")]
    public async Task CancelLearn([Description("B站视频BV号")] string bvid)
    {
        if (_learnService == null)
        {
            interactor.Poke("❌ LearnService未初始化");
            return;
        }
        await _learnService.CancelLearnAsync(bvid);
    }

    [XmlFunction(FunctionMode.OneShot)]
    [Description("获取当前B站学习队列的状态")]
    public async Task QueueStatus()
    {
        if (_learnService == null)
        {
            interactor.Poke("❌ LearnService未初始化");
            return;
        }
        await _learnService.GetQueueStatusAsync();
    }

    [XmlFunction(FunctionMode.OneShot)]
    [Description("检查B站登录状态")]
    public async Task CheckLogin()
    {
        await _authService!.CheckLoginAsync();
    }

    [XmlFunction(FunctionMode.OneShot)]
    [Description("搜索B站视频")]
    public async Task SearchBiliVideo([Description("搜索关键词")] string keyword, [Description("返回结果数量")] int count = 10)
    {
        await _searchService!.SearchBiliVideoAsync(keyword, count);
    }

    [XmlFunction(FunctionMode.OneShot)]
    [Description("通过扫码方式登录B站")]
    public async Task QrVerify()
    {
        await _authService!.QrVerifyAsync();
    }

    [XmlFunction(FunctionMode.OneShot)]
    [Description("退出B站登录")]
    public async Task Logout()
    {
        await _authService!.LogoutAsync();
    }

    [XmlFunction(FunctionMode.OneShot)]
    [Description("清理临时文件夹")]
    public async Task CleanTemp()
    {
        await _authService!.CleanTempAsync();
    }

    [XmlFunction(FunctionMode.OneShot)]
    [Description("清理队列中所有Failed和Completed任务")]
    public async Task CleanQueue()
    {
        if (_store == null)
        {
            interactor.Poke("❌ Store未初始化");
            return;
        }
        var cleaned = await _store.CleanQueueAsync();
        interactor.Poke($"✅ 已清理{cleaned}条任务记录");
    }

    protected async void OnMessageReceived(string message)
    {
        if (message.StartsWith("history", StringComparison.OrdinalIgnoreCase))
        {
            await HandleHistoryCommand(message);
            return;
        }
        await _confirmationService!.OnMessageReceivedAsync(message);
    }

    private void SaveConfigToDisk()
    {
        try
        {
            var cfgPath = Path.Combine(AlifePath.StorageFolderPath, "Configuration/BiliLearn.CSharp.Plugin.BiliLearnModule.json");
            var dir = Path.GetDirectoryName(cfgPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            var json = System.Text.Json.JsonSerializer.Serialize(Configuration, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(cfgPath, json);
            logger.LogInformation("[BiliLearn] 配置已持久化");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[BiliLearn] 持久化配置失败");
        }
    }

    private async Task HandleHistoryCommand(string message)
    {
        try
        {
            var parts = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            int limit = 10;
            if (parts.Length > 1 && int.TryParse(parts[1], out int parsedLimit))
                limit = parsedLimit;

            var records = await _store!.GetHistoryAsync(limit, 0);

            if (records == null || records.Count == 0)
            {
                interactor.Poke("暂无学习记录哦~");
                return;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"📚 最近{records.Count}条学习记录：");
            sb.AppendLine("---");

            foreach (var record in records)
            {
                sb.AppendLine($"• [{record.Bvid}] {record.Title}");
                sb.AppendLine($"  分类：{record.Category} | 学习时间：{record.LearnedAt}");
                if (!string.IsNullOrEmpty(record.Summary))
                    sb.AppendLine($"  总结：{record.Summary}");
                sb.AppendLine();
            }

            interactor.Poke(sb.ToString());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[BiliLearn] 处理history命令失败");
            interactor.Poke("查询学习历史失败，请稍后重试~");
        }
    }
}

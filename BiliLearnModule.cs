using System;
using System.Collections.Concurrent;
using System.Linq;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Alife.Foundation;
using Alife.Framework;
using Alife.Function.AIModelUtility;
using Alife.Function.FunctionCaller;
using BiliLearn.CSharp.Plugin.Domain.Interfaces;
using BiliLearn.CSharp.Plugin.Models;
using BiliLearn.CSharp.Plugin.Orchestrator;
using BiliLearn.CSharp.Plugin.Processors;
using BiliLearn.CSharp.Plugin.Services;
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
    // 待确认状态字典
    private IKnowledgeRepository? _knowledgeRepo;
    private readonly ConcurrentDictionary<string, PendingConfirmation> PendingConfirmations = new();

    private readonly Interactor<BiliLearnModule> _interactor = interactor;
    private readonly ILanguageModel? _languageModel = languageModel;
    public BiliLearnConfig Configuration { get; set; } = new();
    private VideoProcessingOrchestrator? _orchestrator;
    private IProgressReporter? _progressReporter;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeTasks = new();

    protected override async Task OnAwake()
    {
        functionCaller.RegisterHandler(new XmlHandler(this));
        logger.LogInformation("[BiliLearn] 插件已加载，等待配置注入后初始化");

        // 订阅用户消息事件，处理去重确认等交互
        if (ChatBot != null)
        {
            ChatBot.ChatSent += OnMessageReceived;
            logger.LogInformation("[BiliLearn] 已订阅用户消息事件");
        }
        else
        {
            logger.LogWarning("[BiliLearn] ChatBot为空，无法订阅用户消息");
        }

        await Task.CompletedTask;
    }

    private readonly object _initLock = new();

    private void EnsureInitialized()
    {
        if (_orchestrator != null) return;
        lock (_initLock)
        {
            if (_orchestrator != null) return;

            var cfg = Configuration ?? new BiliLearnConfig();
            var workDir = string.IsNullOrEmpty(cfg.WorkDir)
                ? Path.Combine(AlifePath.StorageFolderPath, "Plugins", "Alife.Plugin.BiliLearn")
                : cfg.WorkDir;
            Directory.CreateDirectory(workDir);

            logger.LogInformation("[BiliLearn] 配置 - Cookie长度: {Len}, LLM Key长度: {KeyLen}, 模型: {Model}, BaseUrl: {BaseUrl}, 工作目录: {Dir}",
                cfg.Cookie?.Length ?? 0, cfg.LlmApiKey?.Length ?? 0, cfg.LlmModel, cfg.LlmBaseUrl, workDir);

            var biliApi = new BilibiliApiService(cfg.Cookie ?? "", logger);
            var downloader = new MediaDownloader(logger);
            var visionProcessor = new VisionProcessor(visionModel, logger);
            logger.LogInformation("[BiliLearn] audioRecognizerProvider注入: {Provider}", audioRecognizerProvider == null ? "NULL ❌" : audioRecognizerProvider.GetType().Name);
            var audioProcessor = new AudioProcessor(audioRecognizerProvider, logger);
            var subtitleProcessor = new SubtitleProcessor(logger);
            ILLMService llm;
            if (cfg.UseAlifeLLM && _languageModel != null)
            {
                logger.LogInformation("[BiliLearn] 使用Alife内置语言模型");
                llm = new AlifeLLMAdapter(_languageModel, logger);
            }
            else
            {
                if (cfg.UseAlifeLLM && _languageModel == null)
                    logger.LogWarning("[BiliLearn] UseAlifeLLM=true 但未注入ILanguageModel，回退到OpenAI规范API");
                else
                    logger.LogInformation("[BiliLearn] 使用OpenAI规范API: {BaseUrl} 模型: {Model}", cfg.LlmBaseUrl, cfg.LlmModel);
                llm = new OpenAICompatibleClient(cfg.LlmApiKey ?? "", logger, baseUrl: cfg.LlmBaseUrl, model: cfg.LlmModel);
            }
            var knowledgeBase = new KnowledgeBaseService(logger, workDir);
            _knowledgeRepo = knowledgeBase;
            
            var llmIntegrator = new LLMIntegrator(llm, knowledgeBase, logger);
            var progressReporter = _progressReporter = new BiliLearnProgressReporter(
                logger,
                msg =>
                {
                    try
                    {
                        _interactor.Poke(msg);
                        return Task.CompletedTask;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "[BiliLearn] Poke 失败");
                        return Task.CompletedTask;
                    }
                });

            _orchestrator = new VideoProcessingOrchestrator(
                biliApi, downloader, visionProcessor, audioProcessor,
                subtitleProcessor, llmIntegrator, logger, workDir, Configuration, progressReporter);

            logger.LogInformation("[BiliLearn] 初始化完成，工作目录: {Dir}", workDir);
        }
    }

    [XmlFunction(FunctionMode.OneShot)]
    [Description("分析B站视频：输入BV号，自动获取视频信息、下载、提取关键帧、ASR转写、字幕解析，并生成结构化总结归档到知识库")]
    public async Task<string> Learn([Description("B站视频BV号，如 BV1xx411c7mD")] string bvid)
    {
        // 检查是否已学习过该视频
        EnsureInitialized();
        if (_orchestrator == null) return "插件未初始化";

        var existingEntry = await _knowledgeRepo!.GetByBvidAsync(bvid);
        if (existingEntry != null)
        {
            return await HandleExistingVideo(bvid, existingEntry);
        }

        if (_activeTasks.ContainsKey(bvid))
            return $"⚠️ 该视频正在分析中，可先取消（CancelLearn）再重试";
        
        var cts = new CancellationTokenSource();
        _activeTasks[bvid] = cts;
        
        _interactor.Poke($"🔍 开始分析 {bvid}...");
        
        _ = Task.Run(async () => {
            try
            {
                var result = await _orchestrator.ProcessAsync(bvid, cts.Token);
                if (cts.IsCancellationRequested)
                {
                    await _progressReporter!.ReportAsync($"🛑 已取消分析: {bvid}", ProgressLevel.LogAndPush);
                    return;
                }
                if (result.Success)
                {
                    var src = result.SourceStatus;
                                        var msg = $"🎓 **学习完成！**\n" +
                        $"📺 **{result.Title}**\n" +
                        $"🔗 链接：https://www.bilibili.com/video/{result.Bvid}\n" +
                        $"🏷️ 分类：{result.Category}\n" +
                        $"🔍 字幕 {(src.TryGetValue("subtitle", out bool s) && s ? "✅" : "❌")} | ASR {(src.TryGetValue("asr", out bool a) && a ? "✅" : "❌")} | 视觉 {(src.TryGetValue("visual", out bool v) && v ? "✅" : "❌")}\n" +
                        $"📌 摘要：{result.Summary}";
                    _interactor.Poke(msg);
                }
                else
                    await _progressReporter!.ReportAsync($"❌ 失败: {result.Message}", ProgressLevel.LogAndPush);
            }
            catch (OperationCanceledException)
            {
                _interactor.Poke($"🛑 已取消分析: {bvid}");
            }
            catch (Exception ex)
            {
                _interactor.Poke($"❌ 异常: {ex.Message}");
            }
            finally
            {
                _activeTasks.TryRemove(bvid, out _);
                cts.Dispose();
            }
        });
        
        return "✅ 已开始分析，进度实时推送中...";
    }
    
    [XmlFunction(FunctionMode.OneShot)]
    [Description("确认重新学习指定BV号的视频，用于去重确认后的继续操作")]
    public async Task<string> ConfirmRelearn([Description("B站视频BV号")] string bvid)
    {
        EnsureInitialized();
        if (_orchestrator == null) return "插件未初始化";

        // 从待确认队列移除
        PendingConfirmations.TryRemove(bvid, out _);

        if (_activeTasks.ContainsKey(bvid))
            return "⚠️ 该视频正在分析中";

        var cts = new CancellationTokenSource();
        _activeTasks[bvid] = cts;

        if (_progressReporter != null)
            await _progressReporter.ReportAsync($"✅ 开始重新学习: {bvid}", ProgressLevel.LogAndPush);

        _ = Task.Run(async () => {
            try
            {
                var result = await _orchestrator.ProcessAsync(bvid, cts.Token);
                if (cts.IsCancellationRequested)
                {
                    await _progressReporter!.ReportAsync($"🛑 已取消分析: {bvid}", ProgressLevel.LogAndPush);
                    return;
                }
                if (result.Success)
                {
                    await _progressReporter!.ReportAsync($"🎓 **重新学习完成！**\n📺 {bvid}", ProgressLevel.LogAndPush);
                }
                else
                {
                    await _progressReporter!.ReportAsync($"❌ 学习失败: {result.Message}", ProgressLevel.LogAndPush);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[BiliLearn] 重新学习异常: {Bvid}", bvid);
                if (_progressReporter != null)
                    await _progressReporter.ReportAsync($"❌ 学习异常: {ex.Message}", ProgressLevel.LogAndPush);
            }
            finally
            {
                _activeTasks.TryRemove(bvid, out _);
            }
        });

        return "已开始重新学习";
    }

    [XmlFunction(FunctionMode.OneShot)]
    [Description("取消正在进行的B站视频分析")]
    public async Task<string> CancelLearn([Description("B站视频BV号")] string bvid)
    {
        if (_activeTasks.TryRemove(bvid, out var cts))
        {
            cts.Cancel();
            _interactor.Poke($"🛑 正在取消: {bvid}");
            return $"✅ 已发送取消信号: {bvid}";
        }
        return $"⚠️ 未找到正在进行的分析任务: {bvid}";
    }

    [XmlFunction(FunctionMode.OneShot)]
    [Description("检查B站登录状态和账号信息")]
    public async Task CheckLogin()
    {
        EnsureInitialized();
        if (_orchestrator == null)
        {
            _interactor.Poke("❌ 插件未初始化");
            return;
        }
        await _orchestrator.CheckLoginAsync();
    }


    [XmlFunction(FunctionMode.OneShot)]
    [Description("更新B站Cookie并验证登录状态。输入Cookie字符串（格式 k1=v1; k2=v2）")] 
    public async Task<string> Login([Description("B站Cookie字符串（格式 k1=v1; k2=v2）")] string cookie)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(cookie))
                return "❌ Cookie不能为空";

            // 更新配置
            Configuration.Cookie = cookie;

            // 重新初始化，让新Cookie生效
            _orchestrator?.Dispose();
            _orchestrator = null;
            EnsureInitialized();

            if (_orchestrator == null)
                return "❌ 插件初始化失败";

            // 验证登录状态
            await _orchestrator.CheckLoginAsync();
            return "✅ 已更新Cookie并执行登录验证，结果见上方推送";
        }
        catch (Exception ex)
        {
            return $"❌ 登录异常: {ex.Message}";
        }
    }

    [XmlFunction(FunctionMode.OneShot)]
    [Description("清理临时文件夹（temp目录下视频、音频、关键帧等缓存文件），保持插件根目录整洁")]
    public string CleanTemp()
    {
        try
        {
            var workDir = string.IsNullOrEmpty(Configuration.WorkDir)
                ? Path.Combine(AlifePath.StorageFolderPath, "Plugins", "Alife.Plugin.BiliLearn")
                : Configuration.WorkDir;
            var tempDir = Path.Combine(workDir, "temp");
            
            if (!Directory.Exists(tempDir))
                return "✅ temp目录不存在，无需清理";

            var files = Directory.GetFiles(tempDir);
            int count = 0;
            long totalSize = 0;

            foreach (var file in files)
            {
                try
                {
                    var info = new FileInfo(file);
                    totalSize += info.Length;
                    File.Delete(file);
                    count++;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "删除临时文件失败: {File}", file);
                }
            }

            // 尝试删除空目录
            if (Directory.GetFiles(tempDir).Length == 0)
                Directory.Delete(tempDir, false);

            _interactor.Poke($"🧹 **清理完成**\n共删除 {count} 个临时文件，释放 {totalSize / 1024.0 / 1024.0:F1} MB");
            return $"✅ 已清理 {count} 个临时文件，释放 {totalSize / 1024.0 / 1024.0:F1} MB";
        }
        catch (Exception ex)
        {
            return $"❌ 清理异常: {ex.Message}";
        }
    }

    [XmlFunction(FunctionMode.OneShot)]
    [Description("搜索B站视频：按关键词搜索，返回视频列表（含BV号、标题、UP主、时长、播放量等）")]
    public async Task<string> SearchBiliVideo([Description("搜索关键词")] string keyword, [Description("返回结果数量，默认10")] int count = 10)
    {
        try
        {
            EnsureInitialized();
            if (_orchestrator == null || _orchestrator.BiliApi == null)
                return "插件未初始化或B站API不可用";

            // 限制搜索数量
            count = Math.Clamp(count, 1, 20);

            var results = await _orchestrator.BiliApi.SearchVideosAsync(keyword, count);
            if (results.Count == 0)
            {
                _interactor.Poke($"🔍 搜索\"{keyword}\" 未找到相关视频");
                return "未找到相关视频";
            }

            var msg = $"🔍 **搜索 \"{keyword}\" → {results.Count} 个结果**\n\n";
            for (int i = 0; i < results.Count; i++)
            {
                var v = results[i];
                msg += $"{i + 1}. **{v.Title}**\n" +
                       $"   UP主: {v.Author} | 时长: {v.Duration} | 播放: {v.PlayCount}\n" +
                       $"   BV号: {v.Bvid}\n\n";
            }

            _interactor.Poke(msg);
            return $"✅ 已搜索到 {results.Count} 个视频，结果已推送";
        }
        catch (Exception ex)
        {
            return $"❌ 搜索异常: {ex.Message}";
        }
    }

    public void Dispose()
    {
        if (ChatBot != null)
        {
            ChatBot.ChatSent -= OnMessageReceived;
            logger.LogInformation("[BiliLearn] 已取消订阅用户消息事件");
        }

        _orchestrator?.Dispose();
    }

    // 处理已学习过的视频
    private async Task<string> HandleExistingVideo(string bvid, KnowledgeEntry entry)
    {
        var pending = new PendingConfirmation
        {
            Bvid = bvid,
            OldEntry = entry,
            UserQuery = $"该视频已于 {entry.CreatedAt:yyyy-MM-dd HH:mm} 学习过。{Environment.NewLine}📝 摘要：{entry.Summary.Substring(0, Math.Min(150, entry.Summary.Length))}...{Environment.NewLine}🔗 链接：https://www.bilibili.com/video/{bvid}{Environment.NewLine}{Environment.NewLine}是否需要重新学习？回复yes重新学习，回复no取消。",
            Timestamp = DateTime.Now
        };
        
        PendingConfirmations[bvid] = pending;
        _interactor.Poke($"📚 {pending.UserQuery}");
        return "已学习过该视频，等待确认...";
    }

    // 处理用户消息，检测确认回复
    // 处理用户确认
    protected async Task HandleUserConfirmationAsync(string message)
    {
        try
        {
            logger.LogInformation("[BiliLearn] 收到消息: {Message}", message);
            var entries = PendingConfirmations.ToArray();
            if (entries.Length == 0) return;

            var lowerMsg = message.ToLower().Trim();
            var confirmPatterns = new[] { "是", "好的", "重新学习", "重新学", "学", "y", "yes" };
            var denyPatterns = new[] { "否", "不用了", "取消", "不学", "n", "no" };

            foreach (var (bvid, pending) in entries)
            {
                if (confirmPatterns.Any(p => lowerMsg.Contains(p)))
                {
                    PendingConfirmations.TryRemove(bvid, out _);
                    if (_progressReporter != null)
                        await _progressReporter.ReportAsync($"✅ 正在重新学习: {bvid}", ProgressLevel.LogAndPush);
                    try
                    {
                        EnsureInitialized();
                        var ct = new CancellationTokenSource();
                        _activeTasks.TryAdd(bvid, ct);
                        await _orchestrator.ProcessAsync(bvid, ct.Token);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "[BiliLearn] 重新学习失败: {Bvid}", bvid);
                    }
                    break;
                }
                else if (denyPatterns.Any(p => lowerMsg.Contains(p)))
                {
                    PendingConfirmations.TryRemove(bvid, out _);
                    if (_progressReporter != null)
                        await _progressReporter.ReportAsync($"已取消重新学习: {bvid}", ProgressLevel.LogAndPush);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[BiliLearn] 处理用户确认失败");
        }
    }

    // 由事件触发
    protected void OnMessageReceived(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        if (message.StartsWith("[")) return;
        _ = HandleUserConfirmationAsync(message);
    }
        
}

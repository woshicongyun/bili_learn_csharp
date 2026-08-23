
using System;
using System.IO;
using Alife.Foundation;
using Alife.Framework;
using Alife.Function.AIModelUtility;
using BiliLearn.CSharp.Plugin.Domain.Interfaces;
using BiliLearn.CSharp.Plugin.Models;
using BiliLearn.CSharp.Plugin.Processors;
using BiliLearn.CSharp.Plugin.Capabilities.Analyze;
using BiliLearn.CSharp.Plugin.Capabilities.Learn;
using BiliLearn.CSharp.Plugin.Services;
using Microsoft.Extensions.Logging;

namespace BiliLearn.CSharp.Plugin;

/// <summary>
/// 服务装配器：从 BiliLearnModule 中剥离初始化逻辑
/// </summary>
public static class Bootstrapper
{
    /// <summary>
    /// 构建所有核心服务，返回服务容器
    /// </summary>
    public static BiliLearnServices Build(
        BiliLearnConfig config,
        IVisionModel? visionModel,
        IAudioRecognizerProvider? audioRecognizerProvider,
        ILanguageModel? languageModel,
        ILogger logger,
        Interactor<BiliLearnModule> interactor)
    {
        var cfg = config ?? new BiliLearnConfig();
        var workDir = string.IsNullOrEmpty(cfg.WorkDir)
            ? Path.Combine(AlifePath.StorageFolderPath, "Plugins", "Alife.Plugin.BiliLearn")
            : cfg.WorkDir;
        Directory.CreateDirectory(workDir);

        // 1. 基础服务
        var biliApi = new BilibiliApiService(cfg.Cookie ?? "", logger);
        var downloader = new MediaDownloader(logger);

        // 2. 分析处理器
        var visionProcessor = new VisionProcessor(visionModel, logger);
        var audioProcessor = new AudioProcessor(audioRecognizerProvider, logger);
        var subtitleProcessor = new SubtitleProcessor(logger);

        // 3. LLM 服务
        ILLMService llm;
        if (cfg.UseAlifeLLM && languageModel != null)
        {
            logger.LogInformation("[BiliLearn] 使用Alife内置语言模型");
            llm = new AlifeLLMAdapter(languageModel, logger);
        }
        else
        {
            logger.LogInformation("[BiliLearn] 使用OpenAI规范API: {BaseUrl} 模型: {Model}", cfg.LlmBaseUrl, cfg.LlmModel);
            llm = new OpenAICompatibleClient(cfg.LlmApiKey ?? "", logger, baseUrl: cfg.LlmBaseUrl, model: cfg.LlmModel);
        }

        // 4. 知识库与整合
        var knowledgeBase = new KnowledgeBaseService(logger, workDir);
        var llmIntegrator = new LLMIntegrator(llm, knowledgeBase, logger);

        // 5. 进度报告器（Poke到聊天窗口）
        var progressReporter = new BiliLearnProgressReporter(
            logger,
            msg =>
            {
                try
                {
                    interactor.Poke(msg);
                    return System.Threading.Tasks.Task.CompletedTask;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[BiliLearn] Poke 失败");
                    return System.Threading.Tasks.Task.CompletedTask;
                }
            });

        // 6. 分析服务（Analyze柱）
        var analyzeService = new AnalyzeService(
            biliApi, downloader, visionProcessor, audioProcessor,
            subtitleProcessor, llmIntegrator, logger, workDir, cfg, progressReporter);

        // 7. 队列组件
        var downloadStage = new DownloadStage(biliApi, downloader, logger, workDir, 2);

        // 8. 确认服务（LearnService依赖）
        var confirmation = new ConfirmationService(
            logger,
            msg => { interactor.Poke(msg); return System.Threading.Tasks.Task.CompletedTask; },
            analyzeService.ProcessAsync);

        // 9. 队列组件
        var learnQueue = new LearnQueue(
            downloadStage, logger, analyzeService.ProcessAsync,
            msg => { interactor.Poke(msg); return System.Threading.Tasks.Task.CompletedTask; });

        learnQueue.Start();

        // 10. Learn柱：学习流程（必须在队列创建后，确保services完整）
        var learnService = new LearnService(
            new BiliLearnServices
            {
                AnalyzeService = analyzeService,
                BiliApi = biliApi,
                KnowledgeRepo = knowledgeBase,
                ProgressReporter = progressReporter,
                WorkDir = workDir,
                LearnService = null!,
                LearnQueue = learnQueue
            }, confirmation, logger,
            msg => { interactor.Poke(msg); return System.Threading.Tasks.Task.CompletedTask; });

        return new BiliLearnServices
        {
            AnalyzeService = analyzeService,
            BiliApi = biliApi,
            KnowledgeRepo = knowledgeBase,
            ProgressReporter = progressReporter,
            WorkDir = workDir,
            LearnService = learnService,
            LearnQueue = learnQueue
        };
    }
}

/// <summary>Bootstrapper 返回的容器</summary>
public class BiliLearnServices
{
    public required IAnalyzeService AnalyzeService { get; init; }
    public required IBilibiliFetcher BiliApi { get; init; }
    public required IKnowledgeRepository KnowledgeRepo { get; init; }
    public required IProgressReporter ProgressReporter { get; init; }
    public required string WorkDir { get; init; }
    public required LearnService LearnService { get; init; }
    public required LearnQueue LearnQueue { get; init; }
}

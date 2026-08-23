
using System;
using System.IO;
using Alife.Foundation;
using Alife.Framework;
using Alife.Function.AIModelUtility;
using BiliLearn.CSharp.Plugin.Domain.Interfaces;
using BiliLearn.CSharp.Plugin.Models;
using BiliLearn.CSharp.Plugin.Orchestrator;
using BiliLearn.CSharp.Plugin.Processors;
using BiliLearn.CSharp.Plugin.Services;
using Microsoft.Extensions.Logging;

namespace BiliLearn.CSharp.Plugin;

/// <summary>
/// 服务装配器：从 BiliLearnModule 中剥离初始化逻辑
/// </summary>
public static class Bootstrapper
{
    /// <summary>
    /// 构建所有核心服务，返回一个包含 Orchestrator 和 QueueRunner 的容器
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

        // 6. 编排器
        var orchestrator = new VideoProcessingOrchestrator(
            biliApi, downloader, visionProcessor, audioProcessor,
            subtitleProcessor, llmIntegrator, logger, workDir, cfg, progressReporter);

        // 7. 队列组件
        var downloadStage = new DownloadStage(biliApi, downloader, logger, workDir, 2);
        var queueRunner = new QueueRunner(
            downloadStage,
            logger,
            orchestrator.ProcessAsync,
            msg => { interactor.Poke(msg); return System.Threading.Tasks.Task.CompletedTask; });

        return new BiliLearnServices
        {
            Orchestrator = orchestrator,
            QueueRunner = queueRunner,
            BiliApi = biliApi,
            KnowledgeRepo = knowledgeBase,
            ProgressReporter = progressReporter,
            WorkDir = workDir
        };
    }
}

/// <summary>Bootstrapper 返回的容器</summary>
public class BiliLearnServices
{
    public required VideoProcessingOrchestrator Orchestrator { get; init; }
    public required QueueRunner QueueRunner { get; init; }
    public required IBilibiliFetcher BiliApi { get; init; }
    public required IKnowledgeRepository KnowledgeRepo { get; init; }
    public required IProgressReporter ProgressReporter { get; init; }
    public required string WorkDir { get; init; }
}

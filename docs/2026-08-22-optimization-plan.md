# 0822 优化工作事项

## 一、代码问题修复（低风险小改动）

### 1.1 接入 IMediaAnalyzer 接口（消除死代码）
- 现状：Domain/Interfaces/IMediaAnalyzer.cs 已定义，但 AudioProcessor/SubtitleProcessor/VisionProcessor 均未实现
- 方案：三个 Processor 实现该接口，编排器面向接口编程
- 文件清单：
  - Infrastructure/Processors/AudioProcessor.cs
  - Infrastructure/Processors/SubtitleProcessor.cs
  - Infrastructure/Processors/VisionProcessor.cs
  - Orchestrator/VideoProcessingOrchestrator.cs

### 1.2 消除同步阻塞调用
- 位置：BiliLearnModule.HandleExistingVideo 中拒绝分支
- 现状：.GetAwaiter().GetResult() 同步等待
- 方案：改为 await 异步等待

### 1.3 删除 SubtitleText 冗余字段
- 位置：Models/VideoProcessingContext.cs
- 现状：SubtitleText 与 AsrTranscription 职责重叠，且无消费方读取
- 方案：直接删除该字段

## 二、待办事项实现

### 2.1 学习完成推送详细内容
- 位置：VideoProcessingOrchestrator.ProcessAsync 成功回调
- 内容：标题 + 分类 + 标签 + 摘要要点（来自 KeyFrameDescriptions）
- 方式：_interactor.Poke 推送

### 2.2 LLM 调用泛化为 OpenAI 规范接口（架构升级）
- 目标：不锁死在 DeepSeek 上，支持所有 OpenAI 规范兼容的 API
- 方案：
  - 抽象 OpenAICompatibleClient，用 BaseUrl + ModelName 配置即可切换供应商
  - DeepSeekAI 改造为通用 OpenAI 兼容客户端
  - 配置项：LLM.BaseUrl、LLM.ApiKey、LLM.ModelId
- 兼容供应商：DeepSeek、OpenAI、通义、硅基流动、Moonshot 等

### 2.3 配置解耦 + 长代码拆分
- 新增 Models/BiliLearnConfig.cs，集中所有可配置项
- 使用 IOptions<BiliLearnConfig> 注入，模块不再直接操作 IConfiguration
- BiliLearnModule 拆分为：
  - VideoProcessorOrchestrator（编排流程）
  - ConfirmationService（去重征询）
  - BiliLearnModule（仅保留入口方法）

## 三、优先级建议
1. 先做 1.1 + 1.2 + 1.3（问题点，无风险）
2. 再做 2.2 + 2.3（一起做，避免反复改动）
3. 最后做 2.1（功能增强，依赖 1.1 的编排器改造）
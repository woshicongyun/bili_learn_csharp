# CHANGELOG

本文件记录 BiliLearn 插件的重要版本变更。

## [v1.00] - 2026-08-25

### ✨ 正式发布（自内测以来最完整版本）

#### 核心能力
- **四源解析**：视觉帧（QwenVL）、音频ASR（SenseVoice）、字幕文本（B站API）、热门评论（B站评论API）四路互备互校
- **完整学习闭环**：`learn` 指令 → 获取视频信息 → 下载 → 四源解析 → LLM整合 → 知识归档
- **批量学习**：`learnbatch` 预下载并行 + 分析串行，优雅控制带宽与显存

#### 架构（V2 纵切式）
- **Capabilities/** 按能力竖切：Learn / Analyze / Search / Auth 自含 API+处理+状态+错误
- **热重载**：OnAwake/OnDestroy 对称 + XmlHandler 注册传 DestroyCancellationToken + reload_plugin 含重编译，改动即时生效无需冷启动
- **JsonStore 持久化**：内存为主 + Write-Behind 原子落盘，零外部依赖

#### 依赖（正式对外声明）
- `Alife.Function.IVisionModel` —— Alife 内置视觉模型（QwenVL）
- `Alife.Function.IAudioRecognizerProvider` —— SenseVoice 语音识别
- `Alife.Function.ILanguageModel` —— OpenAI 规范语言模型（LLM整合）
- `Alife.Function.FunctionCaller` / `Alife.Function.AIModelUtility` —— 框架基础

#### 配置（全部可调）
- Cookie / LlmApiKey / LlmBaseUrl / LlmModel / WorkDir
- FrameExtractInterval / MaxFrames / HttpTimeoutSeconds / UserAgent
- RetryBaseDelaySeconds / MaxRetries / ChunkSize / MaxConcurrentSegments
- UseAlifeLLM（复用 Alife 内置模型，降本增效）

---

## [v4.6.0] - 2026-08-24

### JsonStore 替换 SQLite + 下载参数配置化 + 清理遗留
- **JsonStore**：内存为主 + Write-Behind 原子落盘，零依赖纯C#
- 移除 SQLite（AppContext.BaseDirectory 指向热编译盘、跨进程调试地狱）
- 下载并行管理（SemaphoreSlim 控并发，默认2）
- 队列状态机 + 持久化 + 启动恢复活跃任务

## [v4.5.0] - 2026-08-24

### 热重载彻底重构
- OnAwake/OnDestroy 对称
- XmlHandler 注册传 DestroyCancellationToken
- reload_plugin 含重编译，改动即时生效无需冷启动

## [v4.4.0] - 2026-08-23

### DLL 冲突修复 + 复用 Alife 语音
- NAudio.Core.dll 全局字典 Add 同名 Key 异常修复（副本移 NAudio_bak/）
- AudioProcessor 改反射调 Alife 内置 AudioDecoder，复用框架能力

## [v4.3.0] - 2026-08-23

### V2 纵切式架构落地（四阶段）
- Capabilities/{Learn,Analyze,Auth,Search} + Shared/Models
- Learn 柱（ILearnService/ILearnQueue）迁绑 V1 成熟逻辑
- 严格 FIFO 队列：FirstOrDefault(Queued) 取队首，Add 尾部队尾

## [v4.2.0] - 2026-08-22

### WBI 签名 + 搜索能力
- B站 API 风控突破：WBI 签名（mixinKeyEncTab），过滤 `!'()*` 特殊字符
- conclusion 接口（aid/cid/up_mid 需 WBI）可作 player/v2 替代字幕源
- `search` 搜索带 Origin 头

## [v4.1.0] - 2026-08-21

### 视频学习流水线完整跑通（三源）
- 获取视频信息 → 下载 → 视觉抽帧 + ASR + 字幕 → LLM 整合 → 归档
- 队列调度 + 状态机
- 知识库归档（Markdown 详情 + JSON 索引）

---
*内测版本（v4.x）期间从「能跑的脚本」逐步演进为「结构化的插件」，v1.00 为第一个正式发布版本。*

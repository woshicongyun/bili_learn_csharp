# Alife.Plugin.BiliLearn —— B站视频智能学习助手

> 一款深度集成于 [Alife](https://github.com/BDFFZI/Alife) 桌宠框架的 B 站视频分析插件。
> 输入一个 BV 号，自动完成视频下载 → 四源解析 → LLM 整合 → 知识归档的完整学习闭环。

---

## ✨ 最大特点：全自动四源解析 + 知识化管理

一般视频工具只给你"转文字"，**BiliLearn 给的是"结构化知识"**。

一条 `learn` 指令触发完整流水线：

```
📥 获取视频信息 → 🎬 开始分析 → ⏬ 下载视频/音频
→ 🖼️ 视觉解析（抽帧 + QwenVL 理解画面内容）
→ 🎙️ ASR 转写（提取完整语音文案）
→ 📝 字幕解析（三源互校，时间轴对齐）
→ 💬 评论解析（获取高赞热门评论，捕捉社区观点）
→ 🧠 LLM 整合（DeepSeek 或 OpenAI 规范模型生成结构化总结）
→ 📚 归档（Markdown 详情 + JSON 索引 + 知识库）
```

### 四源解析管线
| 数据源 | 技术 | 解决的问题 |
|--------|------|-----------|
| 🖼️ 视觉帧 | FFmpeg 抽帧 + QwenVL | 看懂画面内容、图表、文字 |
| 🎙️ 音频 ASR | 自动语音识别 | 无字幕视频也能转写 |
| 📝 字幕文本 | B站 API 直接拉取 | 高精度文本基准，与 ASR 互校 |
| 💬 热门评论 | B站评论 API | 捕捉社区高赞观点与讨论焦点 |

四条数据流互备互校，单条失效不影响整体学习结果。

---

## 📦 当前版本：v5.0.0 (S5 评论分析)

### 十大功能一览

| 功能 | 说明 |
|------|------|
| 📚 **Learn** | 输入 BV 号自动完成视频分析 + 知识归档（单视频学习） |
| 📚 **LearnBatch** | 批量入队学习（预下载并行 + 分析串行） |
| 🚫 **CancelLearn** | 取消分析任务（支持取消单个/全部队列） |
| 🔍 **SearchBiliVideo** | 关键词搜索（含 **WBI 签名**，突破 B 站风控） |
| 🔐 **CheckLogin** | 检查 B 站登录状态与账号信息 |
| 📱 **QrVerify** | 扫码登录 B 站（自动生成二维码、轮询确认、持久化 Cookie） |
| 🚪 **Logout** | 退出登录，销毁 Cookie |
| 📊 **QueueStatus** | 查看队列状态（各视频阶段 + 汇总） |
| 💬 **评论分析** | 获取视频高赞评论，捕捉社区观点 |
| 📜 **History** | 查询学习历史（支持分页） |
| 🧹 **CleanTemp** | 一键清理临时文件（视频/音频/关键帧缓存） |

### 配置项（IConfigurable 注入，全部可调）

| 配置 | 默认值 | 说明 |
|------|--------|------|
| `Cookie` | 空 | B站登录 Cookie（k1=v1; k2=v2 格式） |
| `LlmApiKey` | 空 | LLM API Key（OpenAI规范） |
| `LlmBaseUrl` | DeepSeek | OpenAI 规范兼容地址 |
| `LlmModel` | `deepseek-chat` | 模型名 |
| `WorkDir` | 插件目录 | 工作目录 |
| `FrameExtractInterval` | 15 | 视觉抽帧间隔（秒） |
| `MaxFrames` | 20 | 最大抽帧数 |
| `UseAlifeLLM` | true | 优先复用 Alife 内置模型，降本增效 |
| `HttpTimeoutSeconds` | 300 | HTTP 请求超时（秒） |
| `UserAgent` | 默认 UA | B站风控敏感，通常无需修改 |
| `RetryBaseDelaySeconds` | 1.5 | 重试基础延迟（秒），实际=基础值×次数 |
| `MaxRetries` | 3 | 最大重试次数 |
| `ChunkSize` | 512KB | 分片大小（字节） |
| `MaxConcurrentSegments` | 4 | 并发分片数 |

> **模型无关**：基于 OpenAI 规范抽象，DeepSeek / OpenAI / 通义 / 硅基流动 / Moonshot 均可即插即用。

---

## 🧠 零模型依赖：完全复用 Alife 官方模型

这是本插件最大的技术特色——**不下载、不部署、不管理任何AI模型**。

全部 AI 能力直接复用 Alife 框架内置：

| 能力 | 复用 Alife 组件 | 作用 |
|------|----------------|------|
| 👁️ 视觉理解 | Alife 内置视觉模型（QwenVL） | 理解抽帧画面、图表、文字 |
| 🎧 语音转写 | Alife 内置 ASR 模型 | 自动转写音频为文本 |
| ✍️ 文本整合 | Alife 内置语言模型（或 OpenAI 规范 API） | 生成结构化总结 |

### 带来的好处
- **零额外下载**：无需单独下载 GB 级模型文件，即装即用
- **零显存压力**：与 Alife 共享模型实例，不重复加载不浪费内存
- **持续自动升级**：模型版本跟随 Alife 框架升级，永远用最新能力
- **热插拔切换**：`UseAlifeLLM=false` 即可无缝切到任意 OpenAI 规范 API

> 插件只是"调度员"，真正的"大脑"全部来自 Alife——这才是真正优雅的架构。

---

## 🏗️ 技术架构（V2 纵切式）

```
BiliLearnModule.cs          # 入口：XML 函数暴露 + 配置模型（IConfigurable<BiliLearnConfig>）
Bootstrapper.cs             # 服务装配（集中注入依赖，注册各能力柱）
ConfirmationService.cs      # 待确认 / 重学确认逻辑（去重征询）
DownloadStage.cs            # 下载并行管理（SemaphoreSlim 控并发，默认2）

Capabilities/               # ★ V2 纵切式：按能力竖切，自含 API+处理+状态+错误
  Learn/                    #   学习流程：ILearnService/ILearnQueue（队列调度+状态机+持久化）
  Analyze/                  #   三源解析+LLM整合+归档（AnalyzeService）
  Search/                   #   B站搜索（WBI 签名）
  Auth/                     #   登录/登出/清理（QrVerify/CheckLogin/Logout/CleanTemp）

Services/
  JsonStore.cs              #   JSON持久化（内存为主+Write-Behind原子落盘，启动恢复活跃任务）
  IBiliLearnStore.cs        #   持久化接口定义
  BilibiliApiService.cs     #   B站API（WBI 签名搜索、conclusion 字幕源）
  OpenAICompatibleClient.cs #   通用 LLM 客户端
  MediaDownloader.cs        #   媒体下载（断点续传+智能重试+并发分片，配置参数化）

Models/                     # 数据模型
  QueueItem.cs              #   队列任务模型
  LearnedRecord.cs          #   已学记录模型
  HistoryRecord.cs          #   历史查询模型

Shared/Models/              # 共享数据模型（VideoStatus/ProcessingResult 等）
Domain/Interfaces/          # 接口抽象（ILLMService 等）
Utils/
  FFmpegHelper.cs           # 视频处理工具
  QrCodeGenerator.cs        # 二维码生成（扫码登录）
```

**分层设计**：
- 底层（Models/Services/Utils）零 Alife 框架依赖 → 可独立单测
- 上层（Processors/Orchestrator/Module）只依赖接口 → 低耦合易替换
- 配置集中管理 → 改配置不改代码，**改配置即时生效（无需重编译）**

---

## 📚 知识库结构

```
knowledge/
  index.json           # 全局结构化索引（JSON）
  details/             # 按视频归档的详情 JSON
  *.md                 # 人类可读的 Markdown 总结
```

每次学习完成，自动生成：
- **结构化总结**：标题、UP主、分类、标签、KeyFrameDescriptions 要点摘要
- **双重索引**：Markdown 详情 + JSON 结构化，既给人读也方便程序检索
- **去重机制**：视频已学过会自动征询"是否重新学习"，避免重复分析

---

## 🛡️ 技术攻坚亮点

### 🔓 WBI 签名搜索（v4.1.0 → v4.2.0 核心突破）
- **现象**：`searchbilivideo` 返回空结果，Python 直连 B 站报 **412 request was banned**
- **难度**：视频学习流程正常，唯独搜索接口风控
- **解法**：完整实现 WBI 签名链路（`nav` 获取密钥 → 重排生成 `mixin_key` → MD5 生成 `w_rid`）
- **结果**：搜索接口完全突破，返回精确结果

### 🗂️ 扫码登录一体化
- 调用 `nav` 二维码生成 → 轮询扫码状态 → 成功自动提取 Cookie → 注入配置
- 全流程自动化，无需手动复制粘贴 Cookie

### 🤖 双模型协同
- 视觉/音频分析用本地推理（QwenVL / ASR）
- 最终总结用 LLM 整合（可切 Alife 内置模型或任意 OpenAI 规范 API）
- 智能降本：`UseAlifeLLM=true` 时优先复用框架模型

### 💾 JsonStore 持久化（v4.6.0）
- **内存为主 + Write-Behind 落盘**：入队/更新先写内存，后台节流批量写入磁盘，兼顾性能与安全
- **原子替换**：写入临时文件后 `File.Move` 原子替换，避免写一半损坏
- **启动恢复**：插件启动时从 JSON 恢复活跃（非终态）任务为 Queued，可立即重新调度
- **零依赖**：不引入 SQLite/外部进程，纯 C# 实现，数据文件管理员可读可改

### ⚙️ 配置参数化（v4.6.0）
- 下载参数（超时/UA/重试/分片）从硬编码改为 `BiliLearnConfig` 注入
- 走框架 `IConfigurable<T>` 机制，改配置无需重编译、即时生效
- `MediaDownloader` 解析配置带容错（非法值回退默认），不因配置错误崩溃

---

## 🚀 快速开始

1. **安装**：将插件放到 `Alife/Storage/Plugins/Alife.Plugin.BiliLearn`，重载插件
2. **登录**：调用 `qrverify` 扫码登录 B 站（或手动配置 `Cookie`）
3. **学习**：`learn bvid="BV1xx411c7mD"` —— 全自动分析归档
4. **效果**：学习完成后自动推送摘要 + 已归档到知识库

### XML 函数调用示例
```xml
<learn bvid="BV1xx411c7mD" />
<searchbilivideo keyword="原神" count="10" />
<checklogin />
<qrverify />
<logout />
<cleantemp />
<learnbatch bvids="BV1xx411c7mD,BV1yy222c8mF" />
<queuestatus />
```

---

## 📈 版本历史

| 版本 | 时间 | 核心内容 |
|------|------|----------|
| v4.0.0 | 2026-08-20 | 基于 Alife C# 插件框架重构，三层架构落地 |
| v1.00 | 2026-08-25 | **正式发布**：四源解析+评论分析、热重载、JsonStore、配置参数化 |
| v4.1.0 | 2026-08-21 | 视频学习流水线完整跑通，三源解析上线 |
| v4.1.x | 2026-08-22 | 修复后台任务信号量、关键帧文案计算 |
| **v4.2.0** | 2026-08-22 | **WBI 签名搜索 + 扫码登录 + 退出登录 + 清理临时文件，功能全补齐** |
| **v4.3.0** | 2026-08-23 | **队列能力（预下载并行+分析串行+批量入队+状态查询）+ Module 瘦身解耦** |
| **v4.4.0** | 2026-08-23 | **V2 纵切式架构（Lear/Analyze 柱落地）+ 清理旧代码 + 梗概推送** |
| **v4.5.0** | 2026-08-24 | **SQLite持久化（队列任务持久化、启动恢复、去重检查、历史记录）** |
| **v4.6.0** | 2026-08-24 | **JsonStore 替换 SQLite + 下载参数配置化 + 清理遗留** |

---

## 🔧 v1.00 技术决策——JsonStore vs SQLite

| 决策项 | 选择 | 理由 |
|--------|------|------|
| 存储介质 | JSON 文件 | 零依赖，避免引入 SQLite+Python 进程 |
| C#写入方式 | Write-Behind 原子替换 | 内存优先，后台节流落盘，兼顾性能与安全 |
| 并发控制 | ConcurrentDictionary | 线程安全，无锁读 |
| 启动恢复 | 恢复非终态任务 | 已学任务无需重复入队 |
| 配置加载 | 框架 IConfigurable 注入 | 不用自造加载器，改配置即时生效 |

---

## ⚠️ 开发经验沉淀（血泪教训）

这是一个从实战中成长的项目，我们把踩过的坑都写进了 `插件开发经验` Skill：

1. `<write>` 标签 ≠ 代码执行 —— 误用会**整个覆盖**源码文件
2. 每次里程碑完成**立即推送 GitHub**，绝不攒一堆再推
3. git 不可依时用 GitHub REST API，先 GET 确认 sha 再 PUT
4. 服务层现有能力要主动暴露为 XML 函数（主模块 ≠ 全部能力）
5. B 站搜索接口必须带 WBI 签名，光加请求头不够
6. 删文件前先 grep 查类型定义位置，避免连带删除（ProcessingResult 惨案）
7. ContinueWith 的 Faulted 分支必须处理，否则异常被吞、队列卡死
8. 框架已有的机制优先（IConfigurable），别重复造 ConfigLoader
9. 同名类会互相遮蔽，新增前先 grep 确认

---

**Made with ❤️ by 真央 & 主人 · 奋战整个夏天的 B 站学习助手**

# Alife.Plugin.BiliLearn —— B站视频智能学习助手

> 一款深度集成于 [Alife](https://github.com/BDFFZI/Alife) 桌宠框架的 B 站视频分析插件。
> 输入一个 BV 号，自动完成视频下载 → 三源解析 → LLM 整合 → 知识归档的完整学习闭环。

---

## ✨ 最大特点：全自动三源解析 + 知识化管理

一般视频工具只给你"转文字"，**BiliLearn 给的是"结构化知识"**。

一条 `learn` 指令触发完整流水线：

```
📥 获取视频信息 → 🎬 开始分析 → ⏬ 下载视频/音频
→ 🖼️ 视觉解析（抽帧 + QwenVL 理解画面内容）
→ 🎙️ ASR 转写（提取完整语音文案）
→ 📝 字幕解析（三源互校，时间轴对齐）
→ 🧠 LLM 整合（DeepSeek 或 OpenAI 规范模型生成结构化总结）
→ 📚 归档（Markdown 详情 + JSON 索引 + 知识库）
```

### 三源解析管线
| 数据源 | 技术 | 解决的问题 |
|--------|------|-----------|
| 🖼️ 视觉帧 | FFmpeg 抽帧 + QwenVL | 看懂画面内容、图表、文字 |
| 🎙️ 音频 ASR | 自动语音识别 | 无字幕视频也能转写 |
| 📝 字幕文本 | B站 API 直接拉取 | 高精度文本基准，与 ASR 互校 |

三条数据流互备互校，单条失效不影响整体学习结果。

---

## 📦 当前版本：v4.2.0

### 七大功能一览

| 功能 | 说明 |
|------|------|
| 📚 **Learn** | 输入 BV 号自动完成视频分析 + 知识归档 |
| 🚫 **CancelLearn** | 取消正在进行的分析任务 |
| 🔍 **SearchBiliVideo** | 关键词搜索（含 **WBI 签名**，突破 B 站风控） |
| 🔐 **CheckLogin** | 检查 B 站登录状态与账号信息 |
| 📱 **QrVerify** | 扫码登录 B 站（自动生成二维码、轮询确认、持久化 Cookie） |
| 🚪 **Logout** | 退出登录，销毁 Cookie |
| 🧹 **CleanTemp** | 一键清理临时文件（视频/音频/关键帧缓存） |

### 配置项（全部可调，参数化优先）

| 配置 | 默认值 | 说明 |
|------|--------|------|
| `BiliCookie` | 空 | B站登录 Cookie（k1=v1; k2=v2 格式） |
| `LLM.BaseUrl` | DeepSeek | OpenAI 规范兼容地址，可切换任意供应商 |
| `LLM.ApiKey` | 空 | 密钥 |
| `LLM.ModelId` | `deepseek-chat` | 模型名 |
| `FrameExtractInterval` | 15秒 | 视觉抽帧间隔 |
| `MaxFrames` | 20帧 | 最大抽帧数 |
| `UseAlifeBuiltinLLM` | true | 优先复用 Alife 内置模型，降本增效 |

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
- **热插拔切换**：`UseAlifeBuiltinLLM=false` 即可无缝切到任意 OpenAI 规范 API

> 插件只是"调度员"，真正的"大脑"全部来自 Alife——这才是真正优雅的架构。

---

## 🏗️ 技术架构

```
BiliLearnModule.cs          # 入口：XML 函数暴露 + 配置声明
Orchestrator/
  VideoProcessingOrchestrator.cs  # 编排器：调度整条学习流水线
Services/
  BilibiliApiService.cs    # B站API（含 WBI 签名搜索）
  OpenAICompatibleClient.cs# 通用 LLM 客户端
Processors/
  VisionProcessor.cs       # 视觉分析（抽帧 + QwenVL）
  AudioProcessor.cs        # ASR 转写
  SubtitleProcessor.cs     # 字幕解析
Models/                    # 数据模型（干净无框架依赖）
Domain/Interfaces/         # 接口抽象（IMediaAnalyzer 等）
Utils/
  FFmpegHelper.cs          # 视频处理工具
  QrCodeGenerator.cs       # 二维码生成（扫码登录）
```

**分层设计**：
- 底层（Models/Services/Utils）零 Alife 框架依赖 → 可独立单测
- 上层（Processors/Orchestrator/Module）只依赖接口 → 低耦合易替换
- 配置集中管理 → 改配置不改代码

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
- 智能降本：`UseAlifeBuiltinLLM=true` 时优先复用框架模型

---

## 🚀 快速开始

1. **安装**：将插件放到 `Alife/Storage/Plugins/Alife.Plugin.BiliLearn`，重载插件
2. **登录**：调用 `qrverify` 扫码登录 B 站（或手动配置 `bilicookie`）
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
```

---

## 📈 版本历史

| 版本 | 时间 | 核心内容 |
|------|------|----------|
| v4.0.0 | 2026-08-20 | 基于 Alife C# 插件框架重构，三层架构落地 |
| v4.1.0 | 2026-08-21 | 视频学习流水线完整跑通，三源解析上线 |
| v4.1.x | 2026-08-22 | 修复后台任务信号量、关键帧文案计算 |
| **v4.2.0** | 2026-08-22 | **WBI 签名搜索 + 扫码登录 + 退出登录 + 清理临时文件，功能全补齐** |

---

## ⚠️ 开发经验沉淀（血泪教训）

这是一个从实战中成长的项目，我们把踩过的坑都写进了 `插件开发经验` Skill：

1. `<write>` 标签 ≠ 代码执行 —— 误用会**整个覆盖**源码文件
2. 每次里程碑完成**立即推送 GitHub**，绝不攒一堆再推
3. git 不可依时用 GitHub REST API，先 GET 确认 sha 再 PUT
4. 服务层现有能力要主动暴露为 XML 函数（主模块 ≠ 全部能力）
5. B 站搜索接口必须带 WBI 签名，光加请求头不够

---

**Made with ❤️ by 真央 & 主人 · 奋战整个夏天的 B 站学习助手**


# B站学习队列 —— 改造设计方案（v4）

## 一、总体路线
| 阶段 | 目标 | 说明 |
|------|------|------|
| **短期（本期）** | V1 加入队列能力 + 入口瘦身 | 增量改造，保持稳定可用 |
| **远期** | V2 全新纵切式项目 | 独立源码目录 + 独立仓库，彻底重构后迁移 |

---

## 第一部分：短期（V1 增量改造）

## 二、V1 队列需求总览
将"单任务即时学习"改造成"**预下载并行 + 分析串行**"，支持批量入队、结构化进度、取消。

核心思想：**下载吃带宽不吃 GPU，分析吃 GPU 不吃带宽**——
- 下载阶段：多个排队视频可同时预下载（并行，默认并发 2）
- 分析阶段：同一时刻只跑一个（串行，避免抢显存）

## 三、V1 模块变更总览
| 文件 | 动作 |
|------|------|
| `QueueRunner.cs` | 新增，队列核心（入队/出队/调度/状态） |
| `VideoStatus.cs` | 新增，每个视频的状态模型 |
| `DownloadStage.cs` | 新增，下载并行管理（纵切式下载柱雏形） |
| `BiliLearnModule.cs` | 修改，接入队列、瘦身解耦、改 Learn/CancelLearn、新增状态查询 |
| `Bootstrapper.cs` | 新增，服务装配独立（从 Module 抽出） |
| `BiliLearnService.cs` | 新增，业务方法下沉（从 Module 抽出） |
| `ConfirmationService.cs` | 新增，待确认/重学确认独立（从 Module 抽出） |

## 四、VideoStatus 状态模型
```csharp
public enum VideoStage
{
    Queued, Downloading, Downloaded, Analyzing,
    Completed, Failed, Canceled
}
```
每个视频含：Bvid、Stage、Title、Progress(0-100)、Error、QueuedAt。

## 五、QueueRunner 调度
- 持锁 `List<VideoStatus>`（支持中间项移除）
- 单后台循环：取排队中→交 DownloadStage 预下载；取已下载→抢分析信号量→串行分析
- 分析信号量 `SemaphoreSlim(1,1)`
- 入队三重防重：知识库已学→重学确认；正在任意阶段→提示；未命中→入队
- 队列上限 5（仅限排队中，下载/分析中不占名额）

## 六、DownloadStage（下载柱雏形）
- `SemaphoreSlim` 控制并行下载数（默认 2，可配）
- 自含：取地址（BilibiliApiService）+ 落盘（MediaDownloader）+ 下载状态流转 + 风控退避
- 下载完成回调 QueueRunner 标记 Downloaded

## 七、取消机制
- 正在分析 → CTS 取消，继续调度下一个
- 排队中/下载中 → 从 List 移除（下载中同步取消下载任务）

## 八、结构化状态 Poke
每次状态变化推送总览表：
```
📋 B站学习队列（共3个）
▶ 分析中（1/3）BV1... 【标题】 45%
⏳ 已下载（2/3）BV2... 【标题】 等待分析
⬇ 下载中（3/3）BV3... 【标题】 30%
✅ 完成1 | ⏳ 排队0 | ⬇ 下载1 | ▶ 分析1
```

## 九、失败处理
下载/分析失败 → 标 Failed + Error，Poke 报错，跳过继续；不自动重试；主动取消标 Canceled。

## 十、V1 入口瘦身（BiliLearnModule 解耦）
**现状问题**：BiliLearnModule 一个类承担六件事——XmlHandler 宿主、配置持有、服务装配、待确认状态、业务 XmlFunction、进度 Poke，300+ 行过载。

**解耦方案**：
| 原职责 | 去向 |
|--------|------|
| 服务装配（EnsureInitialized 里 new 一堆） | 抽到 `Bootstrapper`，Module 只留 `_orchestrator` 引用 |
| 业务方法（Learn/CancelLearn/Search/CheckLogin/QrVerify） | 抽到 `BiliLearnService`，Module 只做路由转发 |
| 待确认/重学确认（PendingConfirmations/HandleExistingVideo） | 抽到 `ConfirmationService` |
| 配置持有（BiliLearnConfig） | 移到独立 `Configuration/` 文件 |
| XmlHandler 宿主 + Poke | 留在 Module（保持入口职责） |

**瘦身后 Module 只剩**：XmlHandler 路由 + Poke 状态，职责单一，与远期 V2 入口层一致。

## 十一、V1 修改点（BiliLearnModule.cs 瘦身后）
- 移除 `_activeTasks`，新增 `_queueRunner`
- `Learn(string)` 兼容单参 + 新增 `LearnBatch(string)` 批量
- `CancelLearn` 双分支（分析中取消/排队移除）
- 新增 `QueueStatus` 查询函数
- 重学确认走队列，队满提示稍后再试

---

## 第二部分：远期（V2 纵切式项目）

## 十二、为何重构
现 V1 是横向三层，一个功能横跨多层，AI 维护需跨层跳、认知负担大。改为**纵切式**：按业务能力竖切，每条能力自包含一条竖柱。

## 十三、V2 项目形态
- **独立源码目录**（如 `D:\AlifePluginsV2\BiliLearn`），配独立 Git 仓库
- 自带 `.csproj` + 编译脚本，可独立 `dotnet build` 编译
- 联调时把编译产物复制到部署区独立 V2 插件目录加载
- 期间 V1 保持生产可用，互不影响

## 十四、V2 纵切架构
```
入口层（统一 XML 路由 + 队列编排，变薄）
├── 下载柱  自含：取地址+B站API+落盘+状态+风控退避
├── 字幕柱  自含：conclusion/player取字幕+解析+状态
├── 视觉柱  自含：抽帧+分析+状态
├── 语音柱  自含：ASR+状态
└── LLM柱   自含：整合+生成+状态
```
每条竖柱自包含完整的 API 调用、处理、状态、错误定义，改一个能力只动一条柱。

## 十五、V2 落地节奏
1. 首个纵柱：**下载柱**（承接短期 DownloadStage + Bootstrapper 装配）+ 队列框架
2. 验证纵柱模式稳定后，逐步竖切字幕柱/视觉柱/语音柱/LLM柱
3. 起步即纳入 **SQLite 队列持久化**（一次到位）
4. 开发就绪后，决定整体替换或双版本共存

## 十六、V1 入口瘦身 → V2 入口层衔接
V1 瘦身后的 Module（路由 + Poke）与 BizService（业务下沉实体）正好是 V2 入口层与能力柱的雏形，迁移时直接把对应类整体搬进 V2，减少二次拆分。

---

## 十七、边界情况（总体）
| 场景 | 行为 |
|------|------|
| 队列空时入队 | 立即开始下载+分析 |
| 下载中取消 | 取消下载，从队列移除 |
| 分析中取消 | 触发取消，继续下一个 |
| 批量入队超上限 | 满的部分拒绝，其余成功 |
| 出队时发现已学 | 异步确认，等答复 |
| 重学确认且队满 | 提示稍后再试 |

---

## 十八、V2 补充建议（来自 Agens 小帮手的评审）

### 1. 取消机制要真正贯穿到底
- **V1 问题**：取消分析时只是从队列移除，但底层 LLM 调用仍在运行，GPU 资源没释放
- **V2 方案**：每根纵柱内 `CancellationToken` 从入口贯穿到最底层 API 调用，取消时不仅移除队列项，还要真正中断底层任务

### 2. 断点续传的自动恢复
- **V1 现状**：MediaDownloader 本身支持 `.part` + Range 断点下载，但重启后没有机制扫描遗留 `.part` 并恢复任务
- **V2 方案**：SQLite 持久化后，启动时扫描临时目录的 `.part` 文件，自动恢复未完成任务到队列，实现真正无缝接续

### 3. 下载地址时效性处理
- **风险**：BilibiliApiService 获取的 playUrl 可能带签名有时效，恢复任务时链接可能已失效
- **V2 方案**：恢复任务时若下载链接过期，自动重新获取播放地址后再继续

### 4. PokeStatus 推送频率优化
- **V1 现状**：每次状态变化（含下载进度回调）都推送全量列表，高频进度会刷屏
- **V2 方案**：支持静默模式或合并推送，例如下载进度按百分比走（每 5% 一推），状态切换才立即推送

### 5. 失败智能重试策略
- **V1 现状**：失败后不自动重试，需手动重新入队
- **V2 方案**：按错误类型区分——风控错误延迟重试、网络错误快速重试、永久错误直接放弃并标记

---

*以上建议由 Agens 小帮手 在 2026-08-23 评审后补充*

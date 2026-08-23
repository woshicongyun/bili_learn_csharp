
# Learn 柱

> V2 纵切核心柱之一：视频学习（单视频 + 队列）

## 职责
- 单视频学习全流程（去重 → 入队 → 下载 → 分析 → 归档）
- 批量学习、取消、状态查询
- 队列调度（预下载并行 + 分析串行）

## 文件构成
| 文件 | 说明 |
|------|------|
| `ILearnService.cs` | 服务接口：单视频/批量/取消/状态/登录/搜索/二维码 |
| `ILearnQueue.cs` | 队列接口：入队/批量/取消/状态/启动/停止 |
| `LearnService.cs` | 服务实现（迁绑自 V1 BiliLearnService） |
| `LearnQueue.cs` | 队列实现（迁绑自 V1 QueueRunner + DownloadStage） |

## 迁移边界
- 复用 V1 的 `BiliLearnServices` 容器（Bootstrapper 构建）
- 复用 V1 的 `ConfirmationService`（去重确认）
- 复用 V1 的 `BiliLearnProgressReporter`（进度 Poke）
- 依赖 V1 五大接口：`IBilibiliFetcher` / `IKnowledgeRepository` / `ILLMService` / `IMediaAnalyzer` / `IProgressReporter`

## 验收标准
1. 编译通过（reload_plugin 成功）
2. `LearnAsync(BV)` 行为与 V1 完全一致（去重/全流程/归档）
3. `LearnBatchAsync(多个BV)` 并行下载、串行分析、进度 Poke
4. `CancelLearnAsync` 支持取消排队/下载中/分析中
5. `GetQueueStatusAsync` 返回结构化状态
6. 所有 V1 对外 XmlFunction 行为不变（回归）

## 后续扩展
- `VideoStatus` 补 `Tags` 字段（为标签抽取打底）
- 评论区数据源（新增 Comments 柱，独立接入）

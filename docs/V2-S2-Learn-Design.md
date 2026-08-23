
# V2 阶段二：Lear 柱接口设计规格

> 给操刀模型的规格书。V1 现有接口（Domain/Interfaces）已相当成熟，阶段二是**按业务边界重新组织**，不是推倒重来。

## 背景与原则

- V1 五个成熟接口：`IBilibiliFetcher`（API调用）、`IKnowledgeRepository`（知识存储）、`ILLMService`（LLM抽象）、`IMediaAnalyzer`（媒体分析）、`IProgressReporter`（进度汇报）
- V2 纵切式：把上述能力按柱子重新归类，**接口本身可复用、不强推重写**
- 阶段二目标是拆出 **Lear 柱**（单视频学习 + 队列），在 `Capabilities/Learn/` 建 `ILearnService` + `ILearnQueue`，绑定 V1 编排逻辑

## 新建接口

### 1. `Capabilities/Learn/ILearnService.cs`

```csharp
namespace BiliLearn.CSharp.Plugin.Capabilities.Learn;

/// <summary>单视频学习流程</summary>
public interface ILearnService
{
    /// <summary>学习单个视频（含去重检查，返回结果消息）</summary>
    Task<string> LearnAsync(string bvid, CancellationToken ct = default);

    /// <summary>检查视频是否已学习过</summary>
    Task<bool> ExistsAsync(string bvid, CancellationToken ct = default);
}
```

> 实现：复用 V1 `BiliLearnService.LearnAsync` + `KnowledgeBaseService.ExistsAsync`，迁入柱内并绑定 `VideoProcessingOrchestrator`

### 2. `Capabilities/Learn/ILearnQueue.cs`

```csharp
namespace BiliLearn.CSharp.Plugin.Capabilities.Learn;

/// <summary>学习队列（预下载并行 + 分析串行）</summary>
public interface ILearnQueue
{
    /// <summary>加入队列（批量，返回各视频结果）</summary>
    Task<List<string>> EnqueueAsync(IEnumerable<string> bvids, CancellationToken ct = default);

    /// <summary>查看队列状态</summary>
    Task<string> GetStatusAsync(CancellationToken ct = default);

    /// <summary>取消指定视频（单个/全部）</summary>
    Task<string> CancelAsync(string? bvid = null, CancellationToken ct = default);
}
```

> 实现：复用 V1 `QueueRunner`（已含预下载2并发 + 分析串行），在 `LearnQueue.cs` 包装 `QueueRunner` 并扩展状态汇报

## 共享模型（Shared/Models）

`VideoStatus` 已在 V1 `Models/VideoStatus.cs` 存在，V2 沿用并**补充 `Tags` 字段**（为标签抽取打底）：

```csharp
// Models/VideoStatus.cs 现有基础上增加：
public List<string> Tags { get; set; } = new();
```

> 当前 `KnowledgeEntry` 已含 `Tags` 字段——可直接沿用，**无需动 IKnowledgeRepository**

## 边界说明

| 柱子 | 承担的V1组件 | 阶段二动作 |
|------|-------------|-----------|
| Learn | BiliLearnService, QueueRunner, DownloadStage | 建接口 + 迁实现 |
| Analyze | VisionProcessor, AudioProcessor, SubtitleProcessor | 阶段三拆 |
| Search | BilibiliApiService.WBI搜索 | 阶段三拆 |
| Auth | GenerateQrCode/VerifyLogin | 阶段三拆 |

## 验收标准

1. 编译通过（reload_plugin 成功）
2. `LearnAsync(BV)` 行为与 V1 完全一致（去重/全流程/归档）
3. `EnqueueAsync(多个BV)` 并行下载、串行分析、进度Poke
4. 所有 V1 对外 XmlFunction 行为不变（回归）

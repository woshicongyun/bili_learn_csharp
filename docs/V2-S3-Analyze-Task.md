
# V2 阶段三任务书 · Analyze柱拆分 + V1遗留清理

> 目标：把视频分析链路从 `VideoProcessingOrchestrator` 拆成独立的 Analyze 柱，让 Lear 柱只管调度、Analyze 柱只管分析，然后清理 V1 不再使用的代码。

---

## 背景与现状

- **当前入口**：`BiliLearnModule` → `ILearnService/LearnQueue` → `QueueRunner.Enqueue` → 预下载后调 `orchestrator.ProcessAsync(bvid, ct)`
- **现有分析链路**（全在 `VideoProcessingOrchestrator.cs`，约 400 行）：
  1. `GetVideoInfoAsync` 获取元信息 → `ctx` 装载
  2. 并发下载视频/音频到 `temp/`
  3. 三路并行解析：字幕(`GetSubtitleAsync`) / ASR(`GetAsrAsync`) / 视觉(`GetVisualAsync`)
  4. `LLMIntegrator.GenerateSummaryAndCategoryAsync` 综合总结
  5. `LLMIntegrator.SaveToKnowledgeBaseAsync` 归档
- **问题**：分析逻辑与编排调度耦合在同一个类里，不符合纵切架构，且未来要扩展评论区/标签时无从下手。

---

## 目标形态（阶段三完成后）

```
Capabilities/
├── Learn/        # 已拆：学习流程 + 队列调度（Lear柱，不依赖Analyze实现细节）
├── Auth/         # 已拆：登录/清理（Auth柱）
├── Search/       # 已拆：搜索（Search柱）
└── Analyze/      # 【本次新建】：视频分析（Analyze柱）
    ├── IAnalyzeService.cs    # 接口：输入bvid，输出分析结果
    ├── AnalyzeService.cs     # 实现：迁绑 VideoProcessingOrchestrator 的分析链路
    └── README.md             # 柱职责自述
```

---

## 实施步骤

### Step 1：新建 Analyze 柱骨架

**文件1：`Capabilities/Analyze/IAnalyzeService.cs`**

```csharp
using System.Threading;
using System.Threading.Tasks;
using BiliLearn.CSharp.Plugin.Models;

namespace BiliLearn.CSharp.Plugin.Capabilities.Analyze;

/// <summary>
/// 分析服务接口：视频分析（元信息+下载+三源解析+LLM整合+归档）
/// </summary>
public interface IAnalyzeService : System.IDisposable
{
    /// <summary>
    /// 分析单个视频，返回结构化结果
    /// </summary>
    Task<ProcessingResult> ProcessAsync(string bvid, CancellationToken cancellationToken = default);

    /// <summary>
    /// 底层B站API（供外部调用搜索等方法），暂保留
    /// </summary>
    BiliLearn.CSharp.Plugin.Services.BilibiliApiService BiliApi { get; }
}
```

**文件2：`Capabilities/Analyze/AnalyzeService.cs`**

- 从 `VideoProcessingOrchestrator.cs` 复制完整实现（构造参数签名保持一致）
- 注意：`_kbService` 字段暂保留（内部仍用 `KnowledgeBaseService`），后续阶段再做知识库统一
- 改类名 `VideoProcessingOrchestrator` → `AnalyzeService`，实现 `IAnalyzeService`
- **不要动任何业务逻辑**！本步骤只做「搬家+改名」，行为零变化

**文件3：`Capabilities/Analyze/README.md`**

```markdown
# Analyze柱

职责：视频分析全流程。
- 输入：bvid
- 输出：ProcessingResult（标题/摘要/分类/信息源状态）
- 依赖：BilibiliApiService、MediaDownloader、三个IMediaAnalyzer、LLMIntegrator、KnowledgeBaseService
- 说明：从 V1 的 VideoProcessingOrchestrator 迁绑，接口稳定后交由弱模型维护
```

---

### Step 2：更新 Bootstrapper

- 删掉 `Orchestrator = orchestrator` 这句（容器不再暴露 Orchestrator）
- 新增 `AnalyzeService = analyzeService`（类型：`AnalyzeService`）
- 新增 `IAnalyzeService AnalyzeService { get; init; }` 属性

当前容器字段：
```csharp
public required VideoProcessingOrchestrator Orchestrator { get; init; }  // 删
public required QueueRunner QueueRunner { get; init; }                    // 保留（V1桥，待删）
public required IBilibiliFetcher BiliApi { get; init; }                   // 保留
public required IKnowledgeRepository KnowledgeRepo { get; init; }         // 保留
public required IProgressReporter ProgressReporter { get; init; }         // 保留
public required string WorkDir { get; init; }                             // 保留
public required LearnService LearnService { get; init; }                  // 保留
public required LearnQueue LearnQueue { get; init; }                      // 保留
// 新增：
public required IAnalyzeService AnalyzeService { get; init; }
```

---

### Step 3：更新 LearnQueue 的分析函数

当前 `LearnQueue` 构造里传的 `analyzeFunc: orchestrator.ProcessAsync`，改成 `analyzeService.ProcessAsync`：

- Bootstrapper 创建 `learnQueue` 时，把参数从 `orchestrator.ProcessAsync` 改为 `analyzeService.ProcessAsync`
- 注意 `analyzeService.ProcessAsync` 和 `orchestrator.ProcessAsync` 方法签名完全一致，直接替换即可

---

### Step 4：清理 V1 遗留（删除/精简）

以下文件分析是否仍被引用，确认无引用后**移到 `_disabled/` 目录**（不要直接删！先移到 `_disabled/` 并编译验证，确认没问题过1~2天后再删）：

| 文件 | 处理 |
|------|------|
| `QueueRunner.cs` | 移 `_disabled/`（已被 `LearnQueue` 取代） |
| `BiliLearnService.cs` | 移 `_disabled/`（已被 `LearnService` + Auth/Search 柱取代） |
| `Services/DeepSeekAI.cs` | 查引用：若 `LLMProvider` 使用则保留，否则移 `_disabled/` |
| `Services/IProgressReporter.cs` | 查引用：与 `Domain/IProgressReporter.cs` 是否重复，重复则移 `_disabled/` |

**清理 V1 后 Bootstrapper 精简点**：
- `queueRunner` 相关变量删除（改用 `learnQueue`）
- `orchestrator` 相关变量删除（改用 `analyzeService`）

---

### Step 5：编译验收（必须）

1. 重载插件：`<reload_plugin pluginId="Alife.Plugin.BiliLearn" />`
2. 验证登录：调用 `CheckLogin` 函数，返回 `✅ 登录有效`
3. 验证学习：调用 `Learn bvid="BV1VJ8M64E9b"`，观察进度推送（视频信息→下载→三源解析→LLM整合→归档）
4. 验证搜索：调用 `SearchBiliVideo keyword="测试"`，返回视频列表
5. 若编译失败，优先读报错信息，查看是否缺 `using` 或字段更名遗漏

---

## 注意事项

1. **Analyze 柱不要创新**！本次只做「搬家+改名」，任何新功能（评论区/标签）留到后续阶段单独做
2. **不要用 write 覆盖整个文件**！优先用 Python 的 `content.replace` 精准修改
3. **不改动任何业务行为**：输出格式、进度消息、去重逻辑保持现状
4. **改完一个文件就编译验证**：每次改动后 `reload_plugin` 看是否通过，不要憋大招
5. 遇到编译错误先看报错行号，常见原因：缺 `using` 引用、字段名更替遗漏、接口与实现不一致
6. 全局搜索 VideoProcessingOrchestrator 引用，确保无外部调用
---

## 完成标准（验收清单）

- [ ] `Capabilities/Analyze/IAnalyzeService.cs` + `AnalyzeService.cs` + `README.md` 存在
- [ ] Bootstrapper 容器不再暴露 `Orchestrator`，新增 `AnalyzeService` 属性
- [ ] `LearnQueue` 的分析函数指向 `analyzeService.ProcessAsync`
- [ ] `QueueRunner.cs`、`BiliLearnService.cs` 移入 `_disabled/`（或确认删除）
- [ ] 重载编译通过，CheckLogin / Learn / SearchBiliVideo 三个功能实测正常
- [ ] 测试中途取消是否正常:LearnQueue 构造参数替换：仅改了传参，但 LearnQueue 内部对 analyzeFunc 的调用可能仍依赖旧上下文（如 CancellationToken 传递）。任务书提到签名一致，但最好在验收时重点测试取消操作。

## 风险点（执行时注意）
1。编译依赖：新增 IAnalyzeService 后，其他柱子（如 Learn）可能间接依赖 AnalyzeService 的类型，而 AnalyzeService 又依赖 KnowledgeBaseService，确保所有依赖在 Bootstrapper 中正确注册。
2.命名空间冲突：BiliLearn.CSharp.Plugin.Capabilities.Analyze 与原有的 Services 命名空间下类重名？需检查。
3.回滚策略：虽然移入 _disabled/，但若问题严重，还需能快速恢复 Orchestrator。建议在 _disabled/ 中保留原文件，并确保 Git 备份。
4.缓存命中率：重构期间会引入大量新代码（新命名空间、新类名），缓存命中率会下降，但这是正常的。重构完成后，这些新代码会形成稳定的缓存前缀，后续维护成本会降低。
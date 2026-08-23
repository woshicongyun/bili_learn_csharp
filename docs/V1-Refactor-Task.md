
# V1 改造任务书（交付给执行模型）

## 一、你的任务
对现有 B站学习插件 `Alife.Plugin.BiliLearn` 做两件事：
1. 加入队列能力（预下载并行 + 分析串行 + 批量入队 + 结构化状态）
2. 给 `BiliLearnModule.cs` 瘦身解耦

**重要约束**：
- 这是生产插件，改动必须**保持现有功能不破坏**（搜索、登录、扫码、学习、总结都要能用）
- 不要改动部署区以外的任何文件
- 不要动 `BilibiliApiService` 的既有对外接口签名（除非任务要求）
- 所有改动必须能编译通过

## 二、参考文档（先读完再动手）
- 设计总览：`docs/Queue-Design.md`（v4，完整方案）
- 插件开发规范：`C:\Users\SEELE\Documents\Alife\Storage\Skills\插件开发经验\SKILL.md`
- 插件清单写法：`C:\Users\SEELE\Documents\Alife\Storage\Skills\插件开发经验\`（若含 manifest 示例）
- 若需查更详细操作，可阅读 Alife 框架内置文档（通过开发工具等入口获取）

## 二点五、操作环境与工具（务必先掌握）

### 你的运行环境
- 插件部署区：`C:\Users\SEELE\Documents\Alife\Storage\Plugins\Alife.Plugin.BiliLearn`（所有改动都在此）
- 编译机制：框架按插件目录扫描 cs 文件编译，修改后需重载

### 重载插件环境（改完代码后如何验证）
1. **改完一个步骤的代码后**，用开发工具调用重载：
   - 重载插件环境：`reload_plugin_environment`（装依赖+加载插件）
   - 仅重载单个插件（更快）：`reload_plugin` 参数填插件 id（即目录名 `Alife.Plugin.BiliLearn`）
2. 重载后**必须验证编译是否成功**：编译失败会报错，成功则不报错且插件出现在已加载列表
3. 重载后**必须验证现有功能**：调用 `CheckLogin`，若返回登录有效说明编译通过、功能没破坏

### 验证示例（真央已跑通，可参照）
```
新文件 Models/VideoStatus.cs 建好 → reload_plugin_environment → CheckLogin 返回"✅ 登录有效，用户: 请叫我云总" → 编译通过、功能正常
```

### 常见操作提醒
- 列出已加载插件：`list_plugins_in_system`（确认插件在即加载成功）
- 列出系统模块：`list_modules_in_system`
- 开发文档入口：`read_plugin_module_demo`、`read_plugin_manifest_demo`、`read_alife_framework_guide`

## 三、要新增的文件（4个）
1. `VideoStatus.cs` — 状态模型（枚举 VideoStage + 类 VideoStatus），字段见设计文档第四节
2. `QueueRunner.cs` — 队列核心，持锁 List 队列、分析信号量、单后台循环
3. `DownloadStage.cs` — 下载并行管理，SemaphoreSlim 控并发（默认2）
4. `Bootstrapper.cs` — 服务装配（从 Module 的 EnsureInitialized 抽出）
5. `BiliLearnService.cs` — 业务方法下沉（从 Module 抽出）
6. `ConfirmationService.cs` — 待确认/重学确认独立

## 四、要修改的文件（1个）
- `BiliLearnModule.cs` — 瘦身 + 接入队列

## 五、实施步骤（按顺序，每步结束要能编译）

### 步骤1：先建状态模型 VideoStatus.cs
- 创建 `VideoStage` 枚举 + `VideoStatus` 类，字段与设计文档一致
- 新建后编译，确认无错误
- **示例（已由真央完成，可参照）**：`Models/VideoStatus.cs` 已创建，内容：
  ```
  using System;
  namespace BiliLearn.CSharp.Plugin.Models;
  public enum VideoStage { Queued, Downloading, Downloaded, Analyzing, Completed, Failed, Canceled }
  public class VideoStatus
  {
      public string Bvid { get; set; } = "";
      public VideoStage Stage { get; set; } = VideoStage.Queued;
      public string? Title { get; set; }
      public int Progress { get; set; }
      public string? Error { get; set; }
      public DateTime QueuedAt { get; set; }
  }
  ```
  - 参照要点：命名空间为 `BiliLearn.CSharp.Plugin.Models`；文件放入 `Models/` 目录；字段与设计文档完全一致；无外部依赖，是队列的基石。

### 步骤2：建 DownloadStage.cs
- 实现并行下载管理，控制并发数，下载完成回调
- 自含：取地址 + 落盘 + 状态流转 + 风控退避
- 编译确认

### 步骤3：建 QueueRunner.cs
- 实现队列调度（入队/出队/取消/状态）
- 依赖 DownloadStage 做下载、SemaphoreSlim 控分析串行
- 编译确认

### 步骤4：抽 Bootstrapper.cs
- 把 Module 的 EnsureInitialized 里的服务 new 逻辑整体搬过来
- Module 改为调用 Bootstrapper 获取 _orchestrator
- 编译确认，**此时现有功能必须仍正常**

### 步骤5：抽 BiliLearnService.cs
- 把 Learn/CancelLearn/SearchBiliVideo/CheckLogin/QrVerify 的方法体搬到 Service
- Module 里保留 [XmlFunction] 方法签名，方法体改为"调 Service + Poke 转发"
- 编译确认，功能仍正常

### 步骤6：抽 ConfirmationService.cs
- 把 PendingConfirmations + HandleExistingVideo 逻辑搬过去
- Module 改为持有 ConfirmationService 引用
- 编译确认

### 步骤7：Module 接入队列（改造完成）
- 移除 _activeTasks，新增 _queueRunner
- Learn 兼容单参 + 新增 LearnBatch
- CancelLearn 双分支
- 新增 QueueStatus
- 重学确认走队列
- 编译确认，全功能验证

## 六、每个步骤的验收标准
- ✅ 编译通过（无 error，warning 可接受）
- ✅ 原有功能未被破坏
- ✅ 新文件职责单一，没有把逻辑堆在 Module 里

## 七、常见坑（务必避开）
1. **别用 write 覆盖已存在文件**——修改现有文件用"先读全文→精确替换/新增"的方式
2. **别删现有功能**——搜索/登录/扫码等对外接口必须保留
3. **别动部署区以外的文件**——只改 BiliLearn 插件目录
4. **别新增外部 NuGet 包**——尽量用现有依赖
5. **泛型尖括号会被 XML 解析**——代码里涉及泛型（如 List 尖括号）在写文件时注意不要被误解析，必要时用纯文本描述

## 八、完成后
- 在控制台（或告知主人）确认所有编译通过
- 报告每步改动摘要与验证结果

请严格按此任务书执行，遇到设计文档未覆盖的情况，停下询问主人，不要自作主张。

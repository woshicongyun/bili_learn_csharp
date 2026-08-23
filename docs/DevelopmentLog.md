
## 2026-08-23 20:38 V1 队列改造完成：预下载并行 + 分析串行 + Module 瘦身

**背景**：按 `docs/Queue-Design.md`（v4）方案，为 BiliLearn 加入队列能力，并给 `BiliLearnModule.cs` 瘦身解耦。

**实施内容**（对应任务书 `docs/V1-Refactor-Task.md` 七步）：
- 新增 `Models/VideoStatus.cs`：视频生命周期状态模型（Queued/Downloading/Downloaded/Analyzing/Completed/Failed/Canceled）
- 新增 `DownloadStage.cs`：下载并行管理，SemaphoreSlim 控并发（默认2）
- 新增 `QueueRunner.cs`：队列调度（入队/出队/取消/状态），分析串行（信号量）
- 新增 `Bootstrapper.cs`：服务装配，从 Module 的 EnsureInitialized 抽出
- 新增 `BiliLearnService.cs`：业务方法下沉（Learn/Cancel/Search/CheckLogin/QrVerify 等）
- 新增 `ConfirmationService.cs`：待确认/重学确认逻辑独立
- 修改 `BiliLearnModule.cs`：瘦身，只留路由 + Poke 转发；接入队列；新增 LearnBatch / QueueStatus

**推送与验证**：
- 本地 git 曾遇 HTTP 401，改用 GitHub REST API 推送成功（提交 `d634594`，7 个文件）
- 推送前已重载插件环境，`CheckLogin` 正常 → 编译通过、功能未破坏

**沉淀**：
1. 本地 git remote token 失效时，GitHub REST API 的 `/git/blobs` → `/git/trees` → `/git/commits` → `/git/refs` 链路可完整替代提交
2. 步骤化重构（每步编译验证）对弱模型执行者很重要，写成任务书可大幅降低跑偏风险
3. 设计先行：先对齐队列调度与纵切式架构，再动手改造，产出更稳

---
## 2026-08-23 16:33 BiliLearn 视频直读功能：显存风险教训

**背景**：曾尝试为BiliLearn引入“视频直读”能力，让Qwen2.5-VL直接输入视频而非逐帧分析。

**方案评估**：
- 方案A：改Alife源码（`Alife.Function.AIModelUtility`）扩充 `IVisionModel` 接口 → 破坏框架纯净，不可取
- 方案B：在BiliLearn内部定义 `IVideoVisionModel` 接口 + 反射探测 → 合理，但受限于Alife接口约束
- 方案C：独立 `PythonPipeProcess` 拉起第二份Qwen模型 → 实测把12GB显存撑爆！

**关键教训**：
1. **单独在BiliLearn插件里 new 独立的QwenPython 进程 = 模型加载两份 = 显存翻倍**，RTX 4070 12GB 直接爆掉
2. 验证新能力前，**先确认显存余量**（nvidia-smi），尤其是涉及模型重载的操作
3. 已验证：Qwen底层Python代码已支持视频通道（已导入 `process_vision_info`、processor 接收 `videos=` 参数），但 **`IVisionModel` 接口只暴露 `QueryAsync(imagePath, ...)`**，无法直传视频路径
4. **结论**：当前架构下“视频直读”不可行，**采用逐帧fallback方案兜底**——每帧按图片分析，仅复用Alife已加载的模型实例，显存安全

**沉淀**：增加新能力前，先做小范围可行性验证，尤其涉及模型重载时优先复用已加载实例，且不要一开始就改原项目结构。

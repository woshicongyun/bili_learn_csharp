
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

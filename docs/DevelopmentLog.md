
# BiliLearn 插件开发日志

**作者**: Agens小帮手（搭载Alife框架的桌宠助手）
**日期**: 2026-08-23

## 2026-08-23 V1队列化重构完成 & 问题排查

### 已完成工作
1. **V1队列化重构**：将BiliLearn模块重构成六步架构
   - DownloadStage.cs：控制下载并发（默认2）
   - LearnQueue.cs：队列调度核心（上限5、批量入队、取消）
   - Bootstrapper.cs：服务装配器
   - LearnService.cs：业务方法下沉
   - ConfirmationService.cs：分离确认逻辑
   - BiliLearnModule.cs：轻量路由，保持XML接口不变

2. **修复编译错误**
   - Poke方法的Task返回类型问题
   - 命名空间引用缺失
   - 类重复定义

3. **修复队列状态不显示标题问题**
   - 原因：LearnAsync直接入队，未先获取视频标题
   - 修复：先调用GetVideoInfoAsync获取标题再入队
   - 路径：`info?.Data?.Title`（注意VideoInfoResult.Data包装层）

4. **修复队列循环不启动问题**
   - 原因：Bootstrapper缺少`learnQueue.Start()`调用
   - 修复：在Bootstrapper中添加`learnQueue.Start()`

### 当前问题：后台日志无输出

**现象**：
- 队列状态显示"排队中"但后台无下载/分析日志
- pet.log为空（0行）
- Alife.DeskPet.Client.exe进程不存在

**定位**：
- LearnQueue.cs代码逻辑正确，有ILogger注入
- LoopAsync方法有`_logger.LogInformation("[LearnQueue] 队列循环已启动")`
- 但进程已退出，导致日志无输出

**根因**：
- Alife主程序已崩溃或退出
- 插件重载命令执行成功但实际未生效（热编译缓存问题）

**解决方案**：
- 重启Alife主程序（skill经验："重启Alife是热编译终极解药"）
- 重启后重新测试队列功能

### 经验沉淀
1. **队列/循环类必须显式调用Start()**才能激活后台任务
2. **Alife框架中函数返回值≠AI可见输出**，必须显式Poke
3. **热编译问题**：reload_plugin可能不够，需重启Alife
4. **路径访问注意**：VideoInfoResult.Data.Title而非直接.Title
5. **日志调试**：检查进程是否存在，pet.log是否为空

### 待办
- [ ] 重启Alife后验证队列功能
- [ ] 推送代码到GitHub（Token 401问题待解决）
- [ ] 知识库路径统一问题排查


---

## 2026-08-23 · V2 纵切式架构改造 + 队列/梗概推送完善

### 版本：v4.4.0

### 架构演进：V2 纵切式（按能力竖切）
- 从 V1 横向三层（Module→Service→Processor）升级为纵切能力竖柱：
  Capabilities/{Learn,Search,Auth,Analyze} + Shared/Models
- 每根柱子自含 API + 处理逻辑 + 状态定义 + 错误处理，互不越界

### Lear 柱（最难，真央亲自操刀）
- 新增 ILearnService/ILearnQueue 接口 + LearnService/LearnQueue 实现
- 完整迁绑 V1 BiliLearnService + QueueRunner（预下载并行+分析串行）
- 自绘 PokeStatus 状态推送（Queued→Downloading→Downloaded→Analyzing→Completed）

### Analyze 柱（弱模型按任务书实施）
- 小A 按 V2-S3-Analyze-Task.md 完成 AnalyzeService 拆分
- 旧 Orchestrator/DeepSeekAI/IProgressReporter_old 移入 _disabled，审查后批准退休

### 清理旧代码（老家伙们退休）
- 删除 _disabled/ 三个旧文件，重建 Models/ProcessingResult.cs
- 清理5处 using Or 残留。教训：删文件先 grep 查类型定义

### 修复 ContinueWith 吞异常
- 加 IsFaulted 分支，GetBaseException 取真实异常+移除队列+记日志

### 学习完成推送梗概
- 分析完成推送补上梗概内容，学习真正落地到用户眼前

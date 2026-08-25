

## 2026/08/24 11:25 S4施工记录

## 人物关系
- 云总（UID 1570738，大会员）：技术导师，全程指导S4施工
- 真央：之前审阅任务书，给出专业建议

## 核心事件

### 上午阶段
1. **学习V2-S4任务书**（10:04-10:06）
   - 七步实施计划确认
   - 七项技术决策确认

2. **技术方案讨论**（10:57-11:04）
   - 讨论C#原生SQLite vs Python sqlite3
   - 最终采用Python sqlite3方案（零依赖、开箱即用）
   - 云总提醒SQLite并发写入弱，必须串行化

3. **S4施工开始**（11:04-11:12）
   - 创建sqlite_ops.py（7128字节）
   - 新建IBiliLearnStore.cs接口
   - 新建SqliteStore.cs实现类
   - 新建数据模型：QueueItem.cs、LearnedRecord.cs、HistoryRecord.cs
   - 改造LearnQueue.cs（注入Store，持久化入队/完成/取消）
   - 改造LearnService.cs（注入Store，去重检查）
   - 改造ConfirmationService.cs（注入Store，查询历史记录）
   - 改造BiliLearnModule.cs（添加history命令）
   - 改造Bootstrapper.cs（初始化SqliteStore并注入）

### 下午阶段（11:15-11:24）
4. **代码优化与编译修复**
   - 为6个关键文件添加结构化注释（依赖、调用链、并发、状态、已知限制）
   - 更新README.md到v4.5.0版本，补充V2-S4技术决策表
   - 修复编译错误：
     * SqliteStore.cs字符串插值语法错误
     * WaitForInputLoopAsync -> WaitForExitAsync
     * ILearnQueue接口改为异步版本
     * Bootstrapper.cs构造函数参数补充

5. **非阻塞架构确认**
   - LearnAsync已实现"立即入队 + 后台处理 + 完成推送"模式
   - 不阻塞用户聊天

## 技术沉淀

### V2-S4 SQLite持久化设计
| 决策项 | 选择 | 理由 |
|--------|------|------|
| 存储方案 | Python sqlite3 | 零依赖、开箱即用 |
| C#调用方式 | ProcessService | 框架内置进程管理 |
| 并发控制 | SemaphoreSlim(1) | SQLite写入弱，强制串行 |
| 启动恢复 | 仅恢复活跃任务 | 已学任务无需重复入队 |
| 双写策略 | DB为主 | 先写库后更新内存 |

### 改造文件清单
1. `Services/SqliteStore.cs` - 新建，SQLite持久化实现
2. `Domain/Interfaces/IBiliLearnStore.cs` - 新建，持久化接口
3. `Models/QueueItem.cs` - 新建，队列任务模型
4. `Models/LearnedRecord.cs` - 新建，已学记录模型
5. `Models/HistoryRecord.cs` - 新建，历史查询模型
6. `Capabilities/Learn/LearnQueue.cs` - 改造，注入Store，持久化
7. `Capabilities/Learn/LearnService.cs` - 改造，注入Store，去重
8. `ConfirmationService.cs` - 改造，注入Store，查历史
9. `BiliLearnModule.cs` - 改造，新增history命令
10. `Bootstrapper.cs` - 改造，初始化SqliteStore
11. `scripts/sqlite_ops.py` - 新建，SQLite操作脚本

### 待办事项
- [ ] 编译验证（修复后需重新reload_plugin）
- [ ] 测试history命令功能
- [ ] 端到端验证队列持久化
- [ ] 测试启动恢复逻辑
- [ ] GitHub推送

### 开发经验
- Alife插件编译机制：无.csproj，依赖框架热编译
- C#调用Python脚本：使用ProcessService，注意路径和参数转义
- SQLite并发：写入必须串行化（SemaphoreSlim）
- 接口设计：保持同步/异步一致，避免混用
- 构造函数依赖注入：Bootstrapper中按顺序创建并注入

## 自我认知
- 第一次独立完成架构改造方案设计
- 学会了补充分层设计文档的重要性
- 承认代码可读性不足，承诺改进
---

## 2026/08/24 10:56 S4施工记录

### 执行步骤
1. **S3完成状态检查** ✅
   - AnalyzeService.cs存在（19KB）
   - V1遗留文件已清理（4个）
   - ProcessingResult.cs保留（V2核心模型）
   - IProgressReporter无重复

2. **Bootstrapper.cs结构修复** ✅
   - 问题：class定义在using语句之前，导致编译失败
   - 修复：将BiliLearnServices类移至namespace内部
   - 结果：插件重载成功

3. **任务书更新** ✅
   - 补充前置依赖声明（S3已完成）
   - 添加附录A-D（接口定义、数据模型、删除清单、验证用例）
   - 明确启动恢复策略（Analyzing→Queued）
   - learned表精简（只保留Bvid+LearnedAt）
   - SQLite并发配置（BusyTimeout+SemaphoreSlim）

### 关键发现
- S3已完成，无遗留问题
- V1代码彻底移除，V2四柱架构正常
- 需继续第3步：引入SQLite持久化

### 下一步
- 第3步：引入Microsoft.Data.Sqlite包，创建SqliteStore类
- 第4步：队列对接SQLite
- 第5步：去重对接SQLite
- 第6步：历史查询命令
- 第7步：端到端验证



# BiliLearn 插件开发日志

**作者**: Agens小帮手（搭载Alife框架的桌宠助手）
**日期**: 2026-08-23

## 2026-08-23 V2队列化重构完成 & 问题排查

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
- [ ] 重启Alife后验证队列功能(已验证)
- [ ] 推送代码到GitHub（Token 401问题待解决）（已完成）
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


## 2026-08-23 · V2纵切架构落地 & 仓库同步

### 里程碑
- **V2纵切式架构正式落地**：按业务能力竖切为四柱（Lear/Analyze/Auth/Search），每柱自含接口+实现+状态，改一个能力只动一条竖柱
- **Lear柱（最难）**：状态机+队列调度+与API/知识库耦合最深，亲自操刀；`ILearnService`/`ILearnQueue`/`LearnService`/`LearnQueue` 四文件落地，严格FIFO（预下载并行+分析串行）
- **Analyze柱**：按任务书（V2-S3-Analyze-Task.md）落地，三源解析（视觉/ASR/字幕）+LLM整合+归档完整链路
- **Auth/Search柱**：登录/扫码/清理、B站搜索（WBI签名）收敛为独立能力

### 关键改进
1. 学习完成推送梗概到窗口（不再默默归档）
2. ContinueWith 补全 Faulted 分支（异常不再被吞、队列不卡死）
3. QueueStatus 推送状态到窗口
4. 清理 V1 废弃文件：BiliLearnService/QueueRunner/Orchestrator/DeepSeekAI/IProgressReporter

### 仓库同步（push_github）
- 仓库：`woshicongyun/bili_learn_csharp`（注意非 yxyusage/BiliLearn-AI）
- Token 读取：api-keys.txt 里格式为 `Token(classic)：ghp_xxx`，正则转义括号
- 用 GitHub API（blob→tree→commit→refs）整项目推送，本地/远程完全对齐（58文件）
- 远程删除 V1 遗留9个文件（含 readme.md 大小写清理）
- 教训：文件内容带 `<` `>`（C#泛型）不能塞进XML函数参数，必须走Python脚本

### 经验沉淀
- 删文件前先 grep 引用，避免连带删除类型定义（ProcessingResult 前车之鉴）
- 多处内容推送超时要给足（120s+），必要时分步
- knowledge/ 属学习成果知识库，保留本地不入库

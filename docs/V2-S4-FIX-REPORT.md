# V2-S4 SQLite持久化改造 - 修复报告

## 修复内容

### 1. SqliteStore.cs
- 修复字符串插值语法错误
- WaitForInputLoopAsync -> WaitForExitAsync

### 2. ILearnQueue.cs
- 接口改为异步版本（Task<int>、Task<(int,int)>）

### 3. Bootstrapper.cs
- LearnService构造函数补充store参数
- ConfirmationService构造函数补充store参数
- SqliteStore传入ILogger<SqliteStore>

## 待验证

- [ ] 编译通过
- [ ] history命令功能
- [ ] 队列持久化端到端测试
- [ ] 启动恢复逻辑

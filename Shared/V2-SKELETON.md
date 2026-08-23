# V2 架构骨架（阶段一）

> 说明：本骨架用于 V2 纵切式重构的落地。现有 V1 文件（BiliLearnModule.cs 等）**不移动、不删除**，V2 的柱子在 Capabilities/ 下新建，成熟一条替换一条。

## 目录约定

```
Capabilities/          # 纵切核心：一个业务能力一条柱
  Learn/               # 核心：视频学习（单视频 + 队列）
  Search/              # 核心：WBI 签名搜索
  Auth/                # 核心：扫码登录/Cookie
  Analyze/             # 核心：分析管线（视觉/ASR/字幕）
Shared/                # 跨能力共享（稳定）
  Models/             # 共享模型（VideoStatus/Tags 等）
  (后续：BilibiliApiClient/LLMClient 等抽到此处)
```

## 柱子规范
1. 每条柱 = 接口 + 实现 + 模型 + 自述文档
2. 改一个能力只动柱内文件
3. 新增能力 = 新增一条柱，不动现有柱
4. 入口层（BiliLearnModule）永远瘦，只做路由

## 阶段一已建
- Capabilities/{Learn,Search,Auth,Analyze} 目录
- Shared/Models 目录
- 本说明文档

## 后续阶段
- 阶段二：拆 Learn 柱（状态机 + 队列）→ 迁入 Capabilities/Learn
- 阶段三：拆 Search/Auth/Analyze → 迁入对应柱子
- 阶段四：废弃 V1 + SQLite 落库


# B站评论区API调研文档

## 调研目标
为V2-S5评论观点分析功能确定可用的评论拉取接口方案。

## 测试结果

### ✅ API连通性验证
**测试时间**: 2026/8/25 16:19  
**测试视频**: BV1w7b96aEFC (桌宠开发分享-1)  
**AID**: 117104464367233

### 接口调用成功
```
GET https://api.bilibili.com/x/v2/reply
参数: type=1, oid=117104464367233, sort=1, ps=10, pn=1
状态码: 200
响应: {"code":0,"message":"OK"}
```

## B站评论API规范

### 主接口
```
GET https://api.bilibili.com/x/v2/reply
```

### 请求参数

| 参数名 | 类型 | 必填 | 说明 | 示例 |
|--------|------|------|------|------|
| type | int | 是 | 评论类型：1=视频, 2=动态 | 1 |
| oid | int | 是 | 目标对象ID（视频的aid） | 117104464367233 |
| sort | int | 否 | 排序：0=时间, 1=点赞 | 1 |
| ps | int | 否 | 每页条数，默认20 | 10 |
| pn | int | 否 | 页码，默认1 | 1 |

### 响应结构
```json
{
  "code": 0,
  "message": "OK",
  "ttl": 1,
  "data": {
    "page": {
      "num": 1,
      "size": 10,
      "count": 16
    },
    "replies": [
      {
        "rpid": 313869558768,
        "member": {
          "mid": "35949109",
          "uname": "BDFFZI"
        },
        "content": {
          "message": "评论内容"
        },
        "like": 7,
        "ctime": 1786874037
      }
    ]
  }
}
```

## 相关接口

### bvid转aid
```
GET https://api.bilibili.com/x/web-interface/view?bvid=BV1w7b96aEFC
```
响应：
```json
{
  "data": {
    "aid": 117104464367233
  }
}
```

## 现有代码兼容性

### 已实现
- ✅ BaseUrl常量定义
- ✅ HttpClient配置（含Cookie容器）
- ✅ GetUserAgent()方法
- ✅ GetVideoInfoAsync返回aid
- ✅ 登录验证有效（UID: 1570738）

### 待新增
- ⏳ CommentItem数据模型
- ⏳ GetTopCommentsAsync方法
- ⏳ 评论持久化方法

## 实现方案

### 方案A：独立方法（推荐）
```csharp
public async Task<List<CommentItem>> GetTopCommentsAsync(
    string bvid, 
    int limit = 10, 
    CancellationToken ct = default)
```

**流程**：
1. 调用GetVideoInfoAsync获取aid（复用已有逻辑）
2. 构造reply接口请求参数
3. 解析replies数组，提取member.uname、content.message、like
4. 返回CommentItem列表

### 方案B：封装在VideoInfoResult中
在现有GetVideoInfoAsync中增加comment字段，但职责过重，不推荐。

## 风险与限制

| 风险点 | 概率 | 应对策略 |
|--------|------|----------|
| API限流 | 低 | 添加重试机制（最多3次） |
| 评论数为0 | 中 | 返回空列表，不报错 |
| 未登录访问受限 | 低 | 已有登录机制，失败时降级 |
| 响应结构变更 | 低 | 防御性解析，缺失字段返回null |

## 测试用例

### 测试视频
- BV1w7b96aEFC（桌宠开发分享）- 已有评论 ✅
- BV1o4gP6iEeo（DeepSeek教程）- 待测试

### 验证点
1. ✅ 正确解析评论列表
2. ✅ 获取点赞数排序
3. ⏳ 处理无评论情况
4. ⏳ 异常时的错误信息

## 下一步行动

### 开发顺序
1. 新建 Models/CommentItem.cs
2. 修改 Services/BilibiliApiService.cs - 新增GetTopCommentsAsync
3. 修改 Models/VideoProcessingContext.cs - 新增Comments字段
4. 修改 Services/AnalyzeService.cs - 集成评论拉取
5. 修改 Services/LLMIntegrator.cs - 加入评论分析prompt
6. 修改 Services/JsonStore.cs - 评论持久化

---
调研完成时间: 2026/8/25 16:20
状态: ✅ 验证通过，可开始实现

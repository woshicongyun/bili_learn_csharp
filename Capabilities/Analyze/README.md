
# Analyze 柱

**职责**：视频分析全流程。

## 输入输出

- **输入**：bvid（B站视频编号）
- **输出**：ProcessingResult（标题/摘要/分类/信息源状态）

## 依赖

- BilibiliApiService：B站API调用
- MediaDownloader：视频/音频下载
- IMediaAnalyzer × 3：字幕/ASR/视觉解析器
- LLMIntegrator：多源整合+知识库归档
- KnowledgeBaseService：知识存储
- IProgressReporter：进度推送

## 历史

从 V1 的 `VideoProcessingOrchestrator` 迁绑而来，接口稳定后交由弱模型维护。

## 流程

1. 获取视频元信息
2. 并发下载视频/音频
3. 三路并行解析：字幕 / ASR / 视觉
4. LLM整合分析
5. 归档至知识库

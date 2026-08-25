# BiliLearn插件代码索引

> 自动生成，请勿手动修改
> 生成时间: 2026-08-25 16:32:26

---

## 文件概览

共 48 个C#文件

---

### 📄 BiliLearnModule.cs

**类定义:**
- `BiliLearnConfig` (第20行)
- `BiliLearnModule` (第55行)

**方法:**
- `BiliLearnModule.OnAwake()` (第74行)
- `BiliLearnModule.OnDestroy()` (第121行)
- `BiliLearnModule.Learn()` (第131行)
- `BiliLearnModule.LearnBatch()` (第143行)
- `BiliLearnModule.CancelLearn()` (第155行)
- `BiliLearnModule.QueueStatus()` (第167行)
- `BiliLearnModule.CheckLogin()` (第179行)
- `BiliLearnModule.SearchBiliVideo()` (第186行)
- `BiliLearnModule.QrVerify()` (第193行)
- `BiliLearnModule.Logout()` (第200行)
- `BiliLearnModule.CleanTemp()` (第207行)
- `BiliLearnModule.CleanQueue()` (第214行)
- `BiliLearnModule.OnMessageReceived()` (第225行)
- `BiliLearnModule.HandleHistoryCommand()` (第229行)
- `BiliLearnModule.SaveConfigToDisk()` (第235行)
- `BiliLearnModule.HandleHistoryCommand()` (第253行)

**属性:**
- `BiliLearnConfig.Cookie` (第23行)
- `BiliLearnConfig.LlmApiKey` (第25行)
- `BiliLearnConfig.LlmBaseUrl` (第27行)
- `BiliLearnConfig.LlmModel` (第29行)
- `BiliLearnConfig.WorkDir` (第31行)
- `BiliLearnConfig.UseAlifeLLM` (第33行)
- `BiliLearnConfig.HttpTimeoutSeconds` (第35行)
- `BiliLearnConfig.MaxRetries` (第37行)
- `BiliLearnConfig.ChunkSize` (第39行)
- `BiliLearnConfig.MaxConcurrentSegments` (第41行)
- ... 还有 5 个属性

---

### 📄 Bootstrapper.cs

**类定义:**
- `BiliLearnServices` (第34行)
- `Bootstrapper` (第49行)

**方法:**
- `Bootstrapper.Build()` (第54行)

**属性:**
- `BiliLearnServices.AnalyzeService` (第36行)
- `BiliLearnServices.BiliApi` (第37行)
- `BiliLearnServices.KnowledgeRepo` (第38行)
- `BiliLearnServices.ProgressReporter` (第39行)
- `BiliLearnServices.WorkDir` (第40行)
- `BiliLearnServices.LearnService` (第41行)
- `BiliLearnServices.LearnQueue` (第42行)
- `BiliLearnServices.Store` (第43行)

---

### 📄 Capabilities\Analyze\AnalyzeService.cs

**类定义:**
- `AnalyzeService` (第18行)

**方法:**
- `AnalyzeService.AnalyzeService()` (第40行)
- `AnalyzeService.ProcessAsync()` (第71行)
- `AnalyzeService.Fail()` (第84行)
- `AnalyzeService.Fail()` (第193行)
- `AnalyzeService.Fail()` (第220行)
- `AnalyzeService.GetSubtitleAsync()` (第224行)
- `AnalyzeService.GetAsrAsync()` (第322行)
- `AnalyzeService.GetVisualAsync()` (第348行)
- `AnalyzeService.CheckLoginAsync()` (第377行)
- `AnalyzeService.SearchKnowledgeAsync()` (第401行)
- `AnalyzeService.Fail()` (第421行)
- `AnalyzeService.FormatDuration()` (第428行)
- `AnalyzeService.Dispose()` (第436行)

**属性:**
- `AnalyzeService.BiliApi` (第33行)
- `AnalyzeService.ProcessingResult` (第425行)

---

### 📄 Capabilities\Analyze\IAnalyzeService.cs

**接口定义:**
- `IAnalyzeService` (第14行)

---

### 📄 Capabilities\Auth\AuthService.cs

**类定义:**
- `AuthService` (第9行)

**方法:**
- `AuthService.AuthService()` (第17行)
- `AuthService.CheckLoginAsync()` (第31行)
- `AuthService._poke()` (第37行)
- `AuthService._poke()` (第39行)
- `AuthService._poke()` (第43行)
- `AuthService.QrVerifyAsync()` (第47行)
- `AuthService._poke()` (第52行)
- `AuthService._poke()` (第72行)
- `AuthService._poke()` (第88行)
- `AuthService._poke()` (第95行)
- `AuthService._poke()` (第102行)
- `AuthService._poke()` (第108行)
- `AuthService._poke()` (第113行)
- `AuthService._poke()` (第118行)
- `AuthService.CleanTempAsync()` (第132行)
- `AuthService._poke()` (第137行)
- `AuthService._poke()` (第152行)
- `AuthService.LogoutAsync()` (第155行)
- `AuthService._poke()` (第160行)

---

### 📄 Capabilities\Auth\IAuthService.cs

**接口定义:**
- `IAuthService` (第5行)

---

### 📄 Capabilities\Learn\ILearnQueue.cs

**接口定义:**
- `ILearnQueue` (第13行)

---

### 📄 Capabilities\Learn\ILearnService.cs

**接口定义:**
- `ILearnService` (第5行)

---

### 📄 Capabilities\Learn\LearnQueue.cs

**类定义:**
- `LearnQueue` (第41行)

**方法:**
- `LearnQueue.LearnQueue()` (第68行)
- `LearnQueue.Start()` (第83行)
- `LearnQueue.RestoreActiveTasksAsync()` (第89行)
- `LearnQueue.RestoreActiveTasksAsync()` (第96行)
- `LearnQueue.Stop()` (第135行)
- `LearnQueue.LoopAsync()` (第142行)
- `LearnQueue.GetNextForAnalysis()` (第275行)
- `LearnQueue.NoPendingWork()` (第283行)
- `LearnQueue.Enqueue()` (第293行)
- `LearnQueue.Cancel()` (第355行)
- `LearnQueue.CancelAll()` (第381行)
- `LearnQueue.PokeStatus()` (第398行)
- `LearnQueue.FormatStatus()` (第410行)
- `LearnQueue.Dispose()` (第436行)

---

### 📄 Capabilities\Learn\LearnService.cs

**类定义:**
- `LearnService` (第13行)

**方法:**
- `LearnService.LearnService()` (第21行)
- `LearnService.LearnAsync()` (第35行)
- `LearnService._poke()` (第73行)
- `LearnService._poke()` (第81行)
- `LearnService._poke()` (第84行)
- `LearnService._poke()` (第87行)
- `LearnService._poke()` (第90行)
- `LearnService._poke()` (第93行)
- `LearnService.LearnBatchAsync()` (第98行)
- `LearnService._poke()` (第107行)
- `LearnService._poke()` (第140行)
- `LearnService._poke()` (第146行)
- `LearnService._poke()` (第148行)
- `LearnService.CancelLearnAsync()` (第151行)
- `LearnService._poke()` (第155行)
- `LearnService._poke()` (第160行)
- `LearnService._poke()` (第164行)
- `LearnService.GetQueueStatusAsync()` (第168行)
- `LearnService._poke()` (第172行)
- `LearnService._poke()` (第177行)
- ... 还有 3 个方法

---

### 📄 Capabilities\Search\ISearchService.cs

**接口定义:**
- `ISearchService` (第5行)

---

### 📄 Capabilities\Search\SearchService.cs

**类定义:**
- `SearchService` (第8行)

**方法:**
- `SearchService.SearchService()` (第14行)
- `SearchService.SearchBiliVideoAsync()` (第24行)
- `SearchService._poke()` (第31行)
- `SearchService._poke()` (第42行)
- `SearchService._poke()` (第47行)

---

### 📄 ConfirmationService.cs

**类定义:**
- `ConfirmationService` (第13行)

**方法:**
- `ConfirmationService.ConfirmationService()` (第21行)
- `ConfirmationService.HandleExistingVideoAsync()` (第33行)
- `ConfirmationService.poke()` (第47行)
- `ConfirmationService.OnMessageReceivedAsync()` (第50行)
- `ConfirmationService._poke()` (第69行)
- `ConfirmationService._processFunc()` (第72行)
- `ConfirmationService._poke()` (第76行)
- `ConfirmationService._poke()` (第83行)
- `ConfirmationService.Dispose()` (第89行)

---

### 📄 Domain\Interfaces\IBiliLearnStore.cs

**接口定义:**
- `IBiliLearnStore` (第10行)

---

### 📄 Domain\Interfaces\IBilibiliFetcher.cs

**接口定义:**
- `IBilibiliFetcher` (第9行)

---

### 📄 Domain\Interfaces\IKnowledgeRepository.cs

**接口定义:**
- `IKnowledgeRepository` (第13行)

---

### 📄 Domain\Interfaces\ILLMService.cs

**接口定义:**
- `ILLMService` (第11行)

---

### 📄 Domain\Interfaces\IMediaAnalyzer.cs

**接口定义:**
- `IMediaAnalyzer` (第14行)

---

### 📄 Domain\Interfaces\IProgressReporter.cs

**接口定义:**
- `IProgressReporter` (第6行)

---

### 📄 DownloadStage.cs

**类定义:**
- `DownloadStage` (第16行)
- `VideoDownloadResult` (第169行)

**方法:**
- `DownloadStage.DownloadStage()` (第27行)
- `DownloadStage.DownloadAsync()` (第49行)
- `DownloadStage.onProgress()` (第137行)
- `DownloadStage.Dispose()` (第162行)

**属性:**
- `DownloadStage.MaxConcurrentDownloads` (第25行)
- `VideoDownloadResult.Bvid` (第171行)
- `VideoDownloadResult.Success` (第172行)
- `VideoDownloadResult.Canceled` (第173行)
- `VideoDownloadResult.Message` (第174行)
- `VideoDownloadResult.Title` (第175行)
- `VideoDownloadResult.DurationSeconds` (第176行)
- `VideoDownloadResult.VideoFilePath` (第177行)
- `VideoDownloadResult.AudioFilePath` (第178行)
- `VideoDownloadResult.VideoError` (第179行)
- ... 还有 1 个属性

---

### 📄 Models\CommentItem.cs

**类定义:**
- `CommentItem` (第9行)
- `CommentResult` (第55行)

**属性:**
- `CommentItem.Rpid` (第14行)
- `CommentItem.MemberMid` (第19行)
- `CommentItem.Author` (第24行)
- `CommentItem.Message` (第29行)
- `CommentItem.LikeCount` (第34行)
- `CommentItem.ReplyCount` (第39行)
- `CommentItem.Ctime` (第44行)
- `CommentResult.Success` (第57行)
- `CommentResult.Message` (第58行)
- `CommentResult.Comments` (第59行)

---

### 📄 Models\FrameDescription.cs

**类定义:**
- `FrameDescription` (第9行)

**属性:**
- `FrameDescription.FramePath` (第11行)
- `FrameDescription.Description` (第12行)
- `FrameDescription.StartTime` (第13行)
- `FrameDescription.EndTime` (第14行)

---

### 📄 Models\HistoryRecord.cs

---

### 📄 Models\LearnedRecord.cs

---

### 📄 Models\Models.cs

**类定义:**
- `LoginStatus` (第9行)
- `RecommendItem` (第26行)
- `VideoSearchResult` (第40行)
- `QrCodeInfo` (第55行)
- `QrCodePollResult` (第66行)

**属性:**
- `LoginStatus.Valid` (第11行)
- `LoginStatus.IsLogin` (第12行)
- `LoginStatus.Mid` (第13行)
- `LoginStatus.Uid` (第14行)
- `LoginStatus.Uname` (第15行)
- `LoginStatus.UserName` (第16行)
- `LoginStatus.Level` (第17行)
- `LoginStatus.Message` (第18行)
- `LoginStatus.IsVip` (第19行)
- `LoginStatus.VipLabel` (第20行)
- ... 还有 22 个属性

---

### 📄 Models\PendingConfirmation.cs

**类定义:**
- `PendingConfirmation` (第6行)

**属性:**
- `PendingConfirmation.Bvid` (第8行)
- `PendingConfirmation.OldEntry` (第9行)
- `PendingConfirmation.UserQuery` (第10行)
- `PendingConfirmation.Timestamp` (第11行)

---

### 📄 Models\ProcessingResult.cs

**类定义:**
- `ProcessingResult` (第6行)
- `VideoDownloadResult` (第18行)

**属性:**
- `ProcessingResult.Success` (第8行)
- `ProcessingResult.Bvid` (第9行)
- `ProcessingResult.Title` (第10行)
- `ProcessingResult.Summary` (第11行)
- `ProcessingResult.Category` (第12行)
- `ProcessingResult.Message` (第13行)
- `ProcessingResult.SourceStatus` (第14行)
- `VideoDownloadResult.Success` (第20行)
- `VideoDownloadResult.Canceled` (第21行)
- `VideoDownloadResult.FilePath` (第22行)
- ... 还有 1 个属性

---

### 📄 Models\ProgressLevel.cs

---

### 📄 Models\QueueItem.cs

---

### 📄 Models\StructuredSubtitle.cs

**类定义:**
- `SubtitleItem` (第11行)
- `StructuredSubtitle` (第21行)

**属性:**
- `SubtitleItem.From` (第13行)
- `SubtitleItem.To` (第14行)
- `SubtitleItem.Text` (第15行)
- `StructuredSubtitle.Items` (第23行)

---

### 📄 Models\VideoInfo.cs

**类定义:**
- `VideoInfo` (第5行)
- `VideoInfoResult` (第25行)

**属性:**
- `VideoInfo.Bvid` (第7行)
- `VideoInfo.Cid` (第8行)
- `VideoInfo.Title` (第9行)
- `VideoInfo.DurationSeconds` (第10行)
- `VideoInfo.Description` (第11行)
- `VideoInfo.Pic` (第12行)
- `VideoInfo.Owner` (第13行)
- `VideoInfo.IsExclusiveForVip` (第14行)
- `VideoInfo.NeedCharge` (第15行)
- `VideoInfo.NeedVip` (第16行)
- ... 还有 11 个属性

---

### 📄 Models\VideoProcessingContext.cs

**类定义:**
- `VideoProcessingContext` (第9行)

**属性:**
- `VideoProcessingContext.Bvid` (第11行)
- `VideoProcessingContext.Cid` (第12行)
- `VideoProcessingContext.VideoTitle` (第13行)
- `VideoProcessingContext.DurationSeconds` (第14行)
- `VideoProcessingContext.IsExclusiveForVip` (第15行)
- `VideoProcessingContext.VideoDescription` (第16行)
- `VideoProcessingContext.UploaderName` (第17行)
- `VideoProcessingContext.VideoCoverUrl` (第18行)
- `VideoProcessingContext.VideoUrl` (第21行)
- `VideoProcessingContext.AudioUrl` (第22行)
- ... 还有 9 个属性

---

### 📄 Models\VideoStatus.cs

**类定义:**
- `VideoStatus` (第18行)

**属性:**
- `VideoStatus.Id` (第21行)
- `VideoStatus.Bvid` (第23行)
- `VideoStatus.Stage` (第24行)
- `VideoStatus.Title` (第25行)
- `VideoStatus.Progress` (第26行)
- `VideoStatus.Error` (第27行)
- `VideoStatus.QueuedAt` (第28行)
- `VideoStatus.UpdatedAt` (第29行)

---

### 📄 Processors\AudioProcessor.cs

**类定义:**
- `AudioProcessor` (第17行)

**方法:**
- `AudioProcessor.AudioProcessor()` (第22行)
- `AudioProcessor.TranscribeAsync()` (第30行)
- `AudioProcessor.OnRecognized()` (第51行)
- `AudioProcessor.AnalyzeVisualAsync()` (第80行)
- `AudioProcessor.ParseSubtitleAsync()` (第83行)
- `AudioProcessor.Dispose()` (第137行)

---

### 📄 Processors\SubtitleProcessor.cs

**类定义:**
- `SubtitleProcessor` (第16行)

**方法:**
- `SubtitleProcessor.SubtitleProcessor()` (第20行)
- `SubtitleProcessor.ParseSubtitleAsync()` (第22行)
- `SubtitleProcessor.TranscribeAsync()` (第77行)
- `SubtitleProcessor.AnalyzeVisualAsync()` (第80行)
- `SubtitleProcessor.Dispose()` (第83行)

---

### 📄 Processors\VisionProcessor.cs

**类定义:**
- `VisionProcessor` (第18行)

**方法:**
- `VisionProcessor.VisionProcessor()` (第23行)
- `VisionProcessor.AnalyzeVisualAsync()` (第29行)
- `VisionProcessor.AnalyzeFrameAsync()` (第64行)
- `VisionProcessor.new()` (第71行)
- `VisionProcessor.new()` (第90行)
- `VisionProcessor.TranscribeAsync()` (第94行)
- `VisionProcessor.ParseSubtitleAsync()` (第97行)
- `VisionProcessor.Dispose()` (第100行)

---

### 📄 Services\AlifeLLMAdapter.cs

**类定义:**
- `AlifeLLMAdapter` (第16行)

**方法:**
- `AlifeLLMAdapter.AlifeLLMAdapter()` (第22行)
- `AlifeLLMAdapter.ChatAsync()` (第31行)
- `AlifeLLMAdapter.Dispose()` (第62行)

---

### 📄 Services\BiliLearnProgressReporter.cs

**类定义:**
- `BiliLearnProgressReporter` (第9行)

**方法:**
- `BiliLearnProgressReporter.BiliLearnProgressReporter()` (第14行)
- `BiliLearnProgressReporter.ReportAsync()` (第20行)
- `BiliLearnProgressReporter._onProgress()` (第28行)

---

### 📄 Services\BilibiliApiService.cs

**类定义:**
- `BilibiliApiService` (第17行)

**方法:**
- `BilibiliApiService.BilibiliApiService()` (第25行)
- `BilibiliApiService.SetCookie()` (第60行)
- `BilibiliApiService.ClearCookie()` (第76行)
- `BilibiliApiService.GenerateQrCodeAsync()` (第83行)
- `BilibiliApiService.PollQrCodeStatusAsync()` (第121行)
- `BilibiliApiService.VerifyLoginAsync()` (第235行)
- `BilibiliApiService.GetVideoInfoAsync()` (第300行)
- `BilibiliApiService.GetSubtitleAsync()` (第411行)
- `BilibiliApiService.GetRecommendAsync()` (第561行)
- `BilibiliApiService.GetMixinKeyAsync()` (第596行)
- `BilibiliApiService.SignParams()` (第615行)
- `BilibiliApiService.SearchVideosAsync()` (第629行)
- `BilibiliApiService.Dispose()` (第692行)

**属性:**
- `BilibiliApiService.QrCodeInfo` (第95行)
- `BilibiliApiService.QrCodeInfo` (第101行)
- `BilibiliApiService.QrCodeInfo` (第108行)
- `BilibiliApiService.QrCodeInfo` (第112行)
- `BilibiliApiService.QrCodeInfo` (第117行)
- `BilibiliApiService.QrCodePollResult` (第133行)
- `BilibiliApiService.QrCodePollResult` (第139行)
- `BilibiliApiService.QrCodePollResult` (第148行)
- `BilibiliApiService.QrCodePollResult` (第151行)
- `BilibiliApiService.QrCodePollResult` (第154行)
- ... 还有 10 个属性

---

### 📄 Services\JsonStore.cs

**类定义:**
- `JsonStore` (第14行)
- `RootData` (第189行)

**方法:**
- `JsonStore.JsonStore()` (第27行)
- `JsonStore.MarkDirty()` (第36行)
- `JsonStore.Flush()` (第46行)
- `JsonStore.SaveToDiskInternal()` (第51行)
- `JsonStore.LoadFromDisk()` (第76行)
- `JsonStore.EnqueueAsync()` (第107行)
- `JsonStore.DequeueAsync()` (第115行)
- `JsonStore.UpdateStatusAsync()` (第128行)
- `JsonStore.GetActiveTasksAsync()` (第138行)
- `JsonStore.IsLearnedAsync()` (第144行)
- `JsonStore.MarkLearnedAsync()` (第146行)
- `JsonStore.AddHistoryAsync()` (第153行)
- `JsonStore.GetHistoryAsync()` (第160行)
- `JsonStore.CleanQueueAsync()` (第166行)
- `JsonStore.DisposeFlush()` (第184行)

**属性:**
- `RootData.Queue` (第191行)
- `RootData.Learned` (第192行)
- `RootData.History` (第193行)
- `RootData.NextId` (第194行)

---

### 📄 Services\KnowledgeBaseService.cs

**类定义:**
- `KnowledgeEntry` (第13行)
- `KnowledgeBaseService` (第31行)

**方法:**
- `KnowledgeBaseService.KnowledgeBaseService()` (第38行)
- `KnowledgeBaseService.SaveAsync()` (第47行)
- `KnowledgeBaseService.LoadAll()` (第79行)
- `KnowledgeBaseService.new()` (第92行)
- `KnowledgeBaseService.SaveAll()` (第96行)
- `KnowledgeBaseService.Search()` (第102行)
- `KnowledgeBaseService.GetAll()` (第114行)
- `KnowledgeBaseService.LoadAll()` (第116行)
- `KnowledgeBaseService.ExistsAsync()` (第123行)
- `KnowledgeBaseService.GetByBvidAsync()` (第132行)
- `KnowledgeBaseService.GetStats()` (第138行)
- `KnowledgeBaseService.Dispose()` (第144行)

**属性:**
- `KnowledgeEntry.Id` (第15行)
- `KnowledgeEntry.Bvid` (第16行)
- `KnowledgeEntry.Title` (第17行)
- `KnowledgeEntry.Category` (第18行)
- `KnowledgeEntry.Summary` (第19行)
- `KnowledgeEntry.Uploader` (第20行)
- `KnowledgeEntry.Duration` (第21行)
- `KnowledgeEntry.Description` (第22行)
- `KnowledgeEntry.Tags` (第23行)
- `KnowledgeEntry.Metadata` (第24行)
- ... 还有 1 个属性

---

### 📄 Services\LLMIntegrator.cs

**类定义:**
- `LLMIntegrator` (第15行)

**方法:**
- `LLMIntegrator.LLMIntegrator()` (第22行)
- `LLMIntegrator.GenerateSummaryAndCategoryAsync()` (第32行)
- `LLMIntegrator.SaveToKnowledgeBaseAsync()` (第110行)
- `LLMIntegrator.ParseLLMResult()` (第137行)
- `LLMIntegrator.Dispose()` (第179行)

---

### 📄 Services\LLMProvider.cs

**接口定义:**
- `LLMProvider` (第10行)

---

### 📄 Services\MediaDownloader.cs

---

### 📄 Services\OpenAICompatibleClient.cs

**类定义:**
- `OpenAICompatibleClient` (第18行)

**方法:**
- `OpenAICompatibleClient.OpenAICompatibleClient()` (第27行)
- `OpenAICompatibleClient.CompleteAsync()` (第43行)
- `OpenAICompatibleClient.Exception()` (第68行)
- `OpenAICompatibleClient.ChatAsync()` (第87行)
- `OpenAICompatibleClient.Exception()` (第112行)
- `OpenAICompatibleClient.Dispose()` (第131行)

---

### 📄 Utils\FFmpegHelper.cs

**类定义:**
- `FFmpegHelper` (第15行)

**方法:**
- `FFmpegHelper.FindPath()` (第17行)
- `FFmpegHelper.ExtractFramesAsync()` (第47行)

---

### 📄 Utils\M3U8Parser.cs

**类定义:**
- `M3U8Parser` (第17行)

**方法:**
- `M3U8Parser.M3U8Parser()` (第22行)
- `M3U8Parser.ParsePlaylist()` (第31行)
- `M3U8Parser.DownloadAndMergeAsync()` (第80行)
- `M3U8Parser.InvalidOperationException()` (第88行)
- `M3U8Parser.InvalidOperationException()` (第94行)
- `M3U8Parser.DownloadWithRetryAsync()` (第112行)
- `M3U8Parser.DownloadWithRetryAsync()` (第134行)
- `M3U8Parser.HttpRequestException()` (第166行)
- `M3U8Parser.ResolveRelativeUrl()` (第169行)

---

### 📄 Utils\QrCodeGenerator.cs

**类定义:**
- `QrCodeGenerator` (第13行)

**方法:**
- `QrCodeGenerator.GeneratePng()` (第18行)
- `QrCodeGenerator.open()` (第29行)

---

## 快速查找


### 按方法名查找

- `AddHistoryAsync`: `JsonStore` in [Services\JsonStore.cs:153]
- `AlifeLLMAdapter`: `AlifeLLMAdapter` in [Services\AlifeLLMAdapter.cs:22]
- `AnalyzeAsync`: `LearnService` in [Capabilities\Learn\LearnService.cs:223]
- `AnalyzeFrameAsync`: `VisionProcessor` in [Processors\VisionProcessor.cs:64]
- `AnalyzeService`: `AnalyzeService` in [Capabilities\Analyze\AnalyzeService.cs:40]
- `AnalyzeVisualAsync`: `AudioProcessor` in [Processors\AudioProcessor.cs:80], `SubtitleProcessor` in [Processors\SubtitleProcessor.cs:80], `VisionProcessor` in [Processors\VisionProcessor.cs:29]
- `AudioProcessor`: `AudioProcessor` in [Processors\AudioProcessor.cs:22]
- `AuthService`: `AuthService` in [Capabilities\Auth\AuthService.cs:17]
- `BiliLearnProgressReporter`: `BiliLearnProgressReporter` in [Services\BiliLearnProgressReporter.cs:14]
- `BilibiliApiService`: `BilibiliApiService` in [Services\BilibiliApiService.cs:25]
- `Build`: `Bootstrapper` in [Bootstrapper.cs:54]
- `Cancel`: `LearnQueue` in [Capabilities\Learn\LearnQueue.cs:355]
- `CancelAll`: `LearnQueue` in [Capabilities\Learn\LearnQueue.cs:381]
- `CancelLearn`: `BiliLearnModule` in [BiliLearnModule.cs:155]
- `CancelLearnAsync`: `LearnService` in [Capabilities\Learn\LearnService.cs:151]
- `ChatAsync`: `AlifeLLMAdapter` in [Services\AlifeLLMAdapter.cs:31], `OpenAICompatibleClient` in [Services\OpenAICompatibleClient.cs:87]
- `CheckLogin`: `BiliLearnModule` in [BiliLearnModule.cs:179]
- `CheckLoginAsync`: `AnalyzeService` in [Capabilities\Analyze\AnalyzeService.cs:377], `AuthService` in [Capabilities\Auth\AuthService.cs:31]
- `CleanQueue`: `BiliLearnModule` in [BiliLearnModule.cs:214]
- `CleanQueueAsync`: `JsonStore` in [Services\JsonStore.cs:166]
- `CleanTemp`: `BiliLearnModule` in [BiliLearnModule.cs:207]
- `CleanTempAsync`: `AuthService` in [Capabilities\Auth\AuthService.cs:132]
- `ClearCookie`: `BilibiliApiService` in [Services\BilibiliApiService.cs:76]
- `CompleteAsync`: `OpenAICompatibleClient` in [Services\OpenAICompatibleClient.cs:43]
- `ConfirmationService`: `ConfirmationService` in [ConfirmationService.cs:21]
- `DequeueAsync`: `JsonStore` in [Services\JsonStore.cs:115]
- `Dispose`: `AnalyzeService` in [Capabilities\Analyze\AnalyzeService.cs:436], `LearnQueue` in [Capabilities\Learn\LearnQueue.cs:436], `ConfirmationService` in [ConfirmationService.cs:89] ... (共12处)
- `DisposeFlush`: `JsonStore` in [Services\JsonStore.cs:184]
- `DownloadAndMergeAsync`: `M3U8Parser` in [Utils\M3U8Parser.cs:80]
- `DownloadAsync`: `DownloadStage` in [DownloadStage.cs:49]
- `DownloadStage`: `DownloadStage` in [DownloadStage.cs:27]
- `DownloadWithRetryAsync`: `M3U8Parser` in [Utils\M3U8Parser.cs:112], `M3U8Parser` in [Utils\M3U8Parser.cs:134]
- `Enqueue`: `LearnQueue` in [Capabilities\Learn\LearnQueue.cs:293]
- `EnqueueAsync`: `JsonStore` in [Services\JsonStore.cs:107]
- `Exception`: `OpenAICompatibleClient` in [Services\OpenAICompatibleClient.cs:68], `OpenAICompatibleClient` in [Services\OpenAICompatibleClient.cs:112]
- `ExistsAsync`: `KnowledgeBaseService` in [Services\KnowledgeBaseService.cs:123]
- `ExtractFramesAsync`: `FFmpegHelper` in [Utils\FFmpegHelper.cs:47]
- `Fail`: `AnalyzeService` in [Capabilities\Analyze\AnalyzeService.cs:84], `AnalyzeService` in [Capabilities\Analyze\AnalyzeService.cs:193], `AnalyzeService` in [Capabilities\Analyze\AnalyzeService.cs:220] ... (共4处)
- `FindPath`: `FFmpegHelper` in [Utils\FFmpegHelper.cs:17]
- `Flush`: `JsonStore` in [Services\JsonStore.cs:46]
- `FormatDuration`: `AnalyzeService` in [Capabilities\Analyze\AnalyzeService.cs:428]
- `FormatStatus`: `LearnQueue` in [Capabilities\Learn\LearnQueue.cs:410]
- `GeneratePng`: `QrCodeGenerator` in [Utils\QrCodeGenerator.cs:18]
- `GenerateQrCodeAsync`: `BilibiliApiService` in [Services\BilibiliApiService.cs:83]
- `GenerateSummaryAndCategoryAsync`: `LLMIntegrator` in [Services\LLMIntegrator.cs:32]
- `GetActiveTasksAsync`: `JsonStore` in [Services\JsonStore.cs:138]
- `GetAll`: `KnowledgeBaseService` in [Services\KnowledgeBaseService.cs:114]
- `GetAsrAsync`: `AnalyzeService` in [Capabilities\Analyze\AnalyzeService.cs:322]
- `GetByBvidAsync`: `KnowledgeBaseService` in [Services\KnowledgeBaseService.cs:132]
- `GetHistoryAsync`: `JsonStore` in [Services\JsonStore.cs:160]
- `GetMixinKeyAsync`: `BilibiliApiService` in [Services\BilibiliApiService.cs:596]
- `GetNextForAnalysis`: `LearnQueue` in [Capabilities\Learn\LearnQueue.cs:275]
- `GetQueueStatusAsync`: `LearnService` in [Capabilities\Learn\LearnService.cs:168]
- `GetRecommendAsync`: `BilibiliApiService` in [Services\BilibiliApiService.cs:561]
- `GetStats`: `KnowledgeBaseService` in [Services\KnowledgeBaseService.cs:138]
- `GetSubtitleAsync`: `AnalyzeService` in [Capabilities\Analyze\AnalyzeService.cs:224], `BilibiliApiService` in [Services\BilibiliApiService.cs:411]
- `GetVideoInfoAsync`: `BilibiliApiService` in [Services\BilibiliApiService.cs:300]
- `GetVisualAsync`: `AnalyzeService` in [Capabilities\Analyze\AnalyzeService.cs:348]
- `HandleExistingVideoAsync`: `ConfirmationService` in [ConfirmationService.cs:33]
- `HandleHistoryCommand`: `BiliLearnModule` in [BiliLearnModule.cs:229], `BiliLearnModule` in [BiliLearnModule.cs:253]
- `HttpRequestException`: `M3U8Parser` in [Utils\M3U8Parser.cs:166]
- `InvalidOperationException`: `M3U8Parser` in [Utils\M3U8Parser.cs:88], `M3U8Parser` in [Utils\M3U8Parser.cs:94]
- `IsLearnedAsync`: `JsonStore` in [Services\JsonStore.cs:144]
- `JsonStore`: `JsonStore` in [Services\JsonStore.cs:27]
- `KnowledgeBaseService`: `KnowledgeBaseService` in [Services\KnowledgeBaseService.cs:38]
- `LLMIntegrator`: `LLMIntegrator` in [Services\LLMIntegrator.cs:22]
- `Learn`: `BiliLearnModule` in [BiliLearnModule.cs:131]
- `LearnAsync`: `LearnService` in [Capabilities\Learn\LearnService.cs:35]
- `LearnBatch`: `BiliLearnModule` in [BiliLearnModule.cs:143]
- `LearnBatchAsync`: `LearnService` in [Capabilities\Learn\LearnService.cs:98]
- `LearnQueue`: `LearnQueue` in [Capabilities\Learn\LearnQueue.cs:68]
- `LearnService`: `LearnService` in [Capabilities\Learn\LearnService.cs:21]
- `LoadAll`: `KnowledgeBaseService` in [Services\KnowledgeBaseService.cs:79], `KnowledgeBaseService` in [Services\KnowledgeBaseService.cs:116]
- `LoadFromDisk`: `JsonStore` in [Services\JsonStore.cs:76]
- `Logout`: `BiliLearnModule` in [BiliLearnModule.cs:200]
- `LogoutAsync`: `AuthService` in [Capabilities\Auth\AuthService.cs:155]
- `LoopAsync`: `LearnQueue` in [Capabilities\Learn\LearnQueue.cs:142]
- `M3U8Parser`: `M3U8Parser` in [Utils\M3U8Parser.cs:22]
- `MarkDirty`: `JsonStore` in [Services\JsonStore.cs:36]
- `MarkLearnedAsync`: `JsonStore` in [Services\JsonStore.cs:146]
- `NoPendingWork`: `LearnQueue` in [Capabilities\Learn\LearnQueue.cs:283]
- `OnAwake`: `BiliLearnModule` in [BiliLearnModule.cs:74]
- `OnDestroy`: `BiliLearnModule` in [BiliLearnModule.cs:121]
- `OnMessageReceived`: `BiliLearnModule` in [BiliLearnModule.cs:225]
- `OnMessageReceivedAsync`: `ConfirmationService` in [ConfirmationService.cs:50]
- `OnRecognized`: `AudioProcessor` in [Processors\AudioProcessor.cs:51]
- `OpenAICompatibleClient`: `OpenAICompatibleClient` in [Services\OpenAICompatibleClient.cs:27]
- `ParseLLMResult`: `LLMIntegrator` in [Services\LLMIntegrator.cs:137]
- `ParsePlaylist`: `M3U8Parser` in [Utils\M3U8Parser.cs:31]
- `ParseSubtitleAsync`: `AudioProcessor` in [Processors\AudioProcessor.cs:83], `SubtitleProcessor` in [Processors\SubtitleProcessor.cs:22], `VisionProcessor` in [Processors\VisionProcessor.cs:97]
- `PokeStatus`: `LearnQueue` in [Capabilities\Learn\LearnQueue.cs:398]
- `PollQrCodeStatusAsync`: `BilibiliApiService` in [Services\BilibiliApiService.cs:121]
- `ProcessAsync`: `AnalyzeService` in [Capabilities\Analyze\AnalyzeService.cs:71]
- `QrVerify`: `BiliLearnModule` in [BiliLearnModule.cs:193]
- `QrVerifyAsync`: `AuthService` in [Capabilities\Auth\AuthService.cs:47]
- `QueueStatus`: `BiliLearnModule` in [BiliLearnModule.cs:167]
- `ReportAsync`: `BiliLearnProgressReporter` in [Services\BiliLearnProgressReporter.cs:20]
- `ResolveRelativeUrl`: `M3U8Parser` in [Utils\M3U8Parser.cs:169]
- `RestoreActiveTasksAsync`: `LearnQueue` in [Capabilities\Learn\LearnQueue.cs:89], `LearnQueue` in [Capabilities\Learn\LearnQueue.cs:96]
- `SaveAll`: `KnowledgeBaseService` in [Services\KnowledgeBaseService.cs:96]
- `SaveAsync`: `KnowledgeBaseService` in [Services\KnowledgeBaseService.cs:47]
- `SaveConfigToDisk`: `BiliLearnModule` in [BiliLearnModule.cs:235]
- `SaveToDiskInternal`: `JsonStore` in [Services\JsonStore.cs:51]
- `SaveToKnowledgeBaseAsync`: `LLMIntegrator` in [Services\LLMIntegrator.cs:110]
- `Search`: `KnowledgeBaseService` in [Services\KnowledgeBaseService.cs:102]
- `SearchBiliVideo`: `BiliLearnModule` in [BiliLearnModule.cs:186]
- `SearchBiliVideoAsync`: `SearchService` in [Capabilities\Search\SearchService.cs:24]
- `SearchKnowledgeAsync`: `AnalyzeService` in [Capabilities\Analyze\AnalyzeService.cs:401]
- `SearchService`: `SearchService` in [Capabilities\Search\SearchService.cs:14]
- `SearchVideosAsync`: `BilibiliApiService` in [Services\BilibiliApiService.cs:629]
- `SetCookie`: `BilibiliApiService` in [Services\BilibiliApiService.cs:60]
- `SignParams`: `BilibiliApiService` in [Services\BilibiliApiService.cs:615]
- `Start`: `LearnQueue` in [Capabilities\Learn\LearnQueue.cs:83]
- `Stop`: `LearnQueue` in [Capabilities\Learn\LearnQueue.cs:135]
- `SubtitleProcessor`: `SubtitleProcessor` in [Processors\SubtitleProcessor.cs:20]
- `TranscribeAsync`: `AudioProcessor` in [Processors\AudioProcessor.cs:30], `SubtitleProcessor` in [Processors\SubtitleProcessor.cs:77], `VisionProcessor` in [Processors\VisionProcessor.cs:94]
- `UpdateStatusAsync`: `JsonStore` in [Services\JsonStore.cs:128]
- `VerifyLoginAsync`: `BilibiliApiService` in [Services\BilibiliApiService.cs:235]
- `VisionProcessor`: `VisionProcessor` in [Processors\VisionProcessor.cs:23]
- `_onProgress`: `BiliLearnProgressReporter` in [Services\BiliLearnProgressReporter.cs:28]
- `_poke`: `AuthService` in [Capabilities\Auth\AuthService.cs:37], `AuthService` in [Capabilities\Auth\AuthService.cs:39], `AuthService` in [Capabilities\Auth\AuthService.cs:43] ... (共37处)
- `_processFunc`: `ConfirmationService` in [ConfirmationService.cs:72]
- `new`: `VisionProcessor` in [Processors\VisionProcessor.cs:71], `VisionProcessor` in [Processors\VisionProcessor.cs:90], `KnowledgeBaseService` in [Services\KnowledgeBaseService.cs:92]
- `onProgress`: `DownloadStage` in [DownloadStage.cs:137]
- `open`: `QrCodeGenerator` in [Utils\QrCodeGenerator.cs:29]
- `poke`: `ConfirmationService` in [ConfirmationService.cs:47]

### 按类名查找

- `AlifeLLMAdapter`: [Services\AlifeLLMAdapter.cs:16]
- `AnalyzeService`: [Capabilities\Analyze\AnalyzeService.cs:18]
- `AudioProcessor`: [Processors\AudioProcessor.cs:17]
- `AuthService`: [Capabilities\Auth\AuthService.cs:9]
- `BiliLearnConfig`: [BiliLearnModule.cs:20]
- `BiliLearnModule`: [BiliLearnModule.cs:55]
- `BiliLearnProgressReporter`: [Services\BiliLearnProgressReporter.cs:9]
- `BiliLearnServices`: [Bootstrapper.cs:34]
- `BilibiliApiService`: [Services\BilibiliApiService.cs:17]
- `Bootstrapper`: [Bootstrapper.cs:49]
- `CommentItem`: [Models\CommentItem.cs:9]
- `CommentResult`: [Models\CommentItem.cs:55]
- `ConfirmationService`: [ConfirmationService.cs:13]
- `DownloadStage`: [DownloadStage.cs:16]
- `FFmpegHelper`: [Utils\FFmpegHelper.cs:15]
- `FrameDescription`: [Models\FrameDescription.cs:9]
- `JsonStore`: [Services\JsonStore.cs:14]
- `KnowledgeBaseService`: [Services\KnowledgeBaseService.cs:31]
- `KnowledgeEntry`: [Services\KnowledgeBaseService.cs:13]
- `LLMIntegrator`: [Services\LLMIntegrator.cs:15]
- `LearnQueue`: [Capabilities\Learn\LearnQueue.cs:41]
- `LearnService`: [Capabilities\Learn\LearnService.cs:13]
- `LoginStatus`: [Models\Models.cs:9]
- `M3U8Parser`: [Utils\M3U8Parser.cs:17]
- `OpenAICompatibleClient`: [Services\OpenAICompatibleClient.cs:18]
- `PendingConfirmation`: [Models\PendingConfirmation.cs:6]
- `ProcessingResult`: [Models\ProcessingResult.cs:6]
- `QrCodeGenerator`: [Utils\QrCodeGenerator.cs:13]
- `QrCodeInfo`: [Models\Models.cs:55]
- `QrCodePollResult`: [Models\Models.cs:66]
- `RecommendItem`: [Models\Models.cs:26]
- `RootData`: [Services\JsonStore.cs:189]
- `SearchService`: [Capabilities\Search\SearchService.cs:8]
- `StructuredSubtitle`: [Models\StructuredSubtitle.cs:21]
- `SubtitleItem`: [Models\StructuredSubtitle.cs:11]
- `SubtitleProcessor`: [Processors\SubtitleProcessor.cs:16]
- `VideoDownloadResult`: [DownloadStage.cs:169], [Models\ProcessingResult.cs:18]
- `VideoInfo`: [Models\VideoInfo.cs:5]
- `VideoInfoResult`: [Models\VideoInfo.cs:25]
- `VideoProcessingContext`: [Models\VideoProcessingContext.cs:9]
- `VideoSearchResult`: [Models\Models.cs:40]
- `VideoStatus`: [Models\VideoStatus.cs:18]
- `VisionProcessor`: [Processors\VisionProcessor.cs:18]
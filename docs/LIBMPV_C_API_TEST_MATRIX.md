# libmpv C API 測試矩陣

## 定位

此矩陣用來區分「C API 包裝覆蓋」與「所有實戰情境都已完全驗證」。核心包裝器涵蓋 libmpv stable v0.41.0 的 `client.h`、`render.h`、`render_gl.h` 與 `stream_cb.h` 公開 API；每個項目仍需用平台相符的原生 libmpv、影片檔、URL 與錯誤案例進行播放驗證。

## API 覆蓋狀態

- 官方 stable 版本：mpv v0.41.0。
- 最新 provider git build 對齊：shinchiro `20260421` / mpv `5921fe5`、zhongfly `2026-05-08-e0eb42c303` / mpv `e0eb42c303`。
- 公開匯出函式：官方標頭 54 個，`MpvNative` P/Invoke 54 個，缺漏 0 個。
- 列舉與旗標：`MpvErrorCode`、`MpvFormat`、`MpvLogLevel`、`MpvEndFileReason`、`MpvRenderParamType`、`MpvRenderFrameInfoFlags`、`MpvRenderUpdateFlags` 已對齊 v0.41.0。
- 事件列舉：v0.41.0 事件已覆蓋；舊版相容事件值保留在 `MpvEventId`，不得視為 v0.41.0 新增事件。
- 原生資料結構：事件、節點、stream callback、OpenGL FBO、OpenGL 初始化、DRM、render frame info 與 `mpv_byte_array` 皆有受控對應。
- 自動化入口：`tests/MediaEmbedKit.Mpv.Tests` 先覆蓋不需原生執行階段的格式 selector、runtime catalog、播放器選項與外部工具引數格式化；`tests/MediaEmbedKit.Mpv.IntegrationTests` 覆蓋原生初始化、屬性、錯誤路徑、本機播放事件與 stream callback；`tests/MediaEmbedKit.Mpv.PlaybackSmoke` 用於執行範例播放冒煙測試。

## client.h

| API 區域 | 受控入口 | 驗證狀態 |
| --- | --- | --- |
| 版本與錯誤 | `MpvPlayer.ClientApiVersion()`、`MpvError`、`MpvException` | 原生整合測試已驗證基本初始化與不存在屬性錯誤碼；仍需擴充完整錯誤碼文字驗證 |
| 用戶端生命週期 | `MpvPlayer`、`Initialize()`、`Dispose()`、`CreateClientHandle()`、`CreateWeakClientHandle()`、`DestroyClientHandle()`、`TerminateDestroyClientHandle()` | 原生整合測試已驗證單一 client 初始化與釋放；仍需驗證多 client 與 shutdown 事件 |
| 設定檔、腳本與時間 | `MpvPlayerOptions.ConfigDirectory`、`MpvPlayerOptions.ConfigFiles`、`MpvPlayerOptions.InputConfigFile`、`MpvPlayerOptions.LoadScripts`、`MpvPlayerOptions.ScriptFiles`、`LoadConfigFile()`、`LoadInputConfigFile()`、`LoadScript()`、`GetTimeNanoseconds()`、`GetTimeMicroseconds()` | 已補上 runtime folder、指定設定檔與 Lua/JavaScript 腳本載入入口，需驗證設定檔錯誤、腳本後端與時間單調性 |
| 選項 | `SetOptionString()`、`SetOptionFlag()`、`SetOptionInt64()`、`SetOptionDouble()`、`SetOptionNode()` | 建置通過，需驗證初始化前後可設定範圍 |
| 命令 | `Command()`、`CommandString()`、`CommandWithResult()`、`CommandNode()`、`CommandNamed()`、`GetCommandList()` | 已補上具名命令、命令清單、播放清單、外部軌、文字、OSD、截圖、濾鏡、輸入、指令碼、外部處理序、watch-later、快取與 A-B loop 便利 API；`CommandNamed()` 使用 `_name` 保存命令名稱以對齊最新 provider git build，需驗證命令回傳節點與錯誤命令 |
| 非同步命令 | `CommandAsync()`、`CommandNodeAsync()`、`CommandNamedAsync()`、`AbortAsyncCommand()`、`CommandReply` | 建置通過，需驗證成功、錯誤與中止事件 |
| 屬性 | `GetProperty*()`、`SetProperty*()`、`DeleteProperty()`、`Pause`、`Volume`、`Mute`、`Speed`、`LoopFile`、`LoopPlaylist`、`GetProfiles()`、`GetDecoders()`、`GetProtocols()`、`GetDemuxers()`、`GetChapters()`、`GetEditions()`、`GetMetadata()`、`GetAudioDevices()`、`GetVideoParameters()`、`GetAudioParameters()`、`GetInputBindings()`、`GetDemuxerCacheState()` | 原生整合測試已驗證 `mpv-version`、`Volume`、`Pause` 與 typed 屬性錯誤路徑；仍需驗證所有 `MpvFormat` 轉換與能力清單 |
| 非同步屬性 | `SetProperty*Async()`、`GetPropertyAsync()`、`GetPropertyNodeAsync()` | 已補回 `Task<MpvNode>` 回覆路徑，需驗證成功與錯誤回覆 |
| 觀察屬性 | `ObserveProperty()`、`ObserveTrackList()`、`UnobserveProperty()`、`PropertyChanged`、`TracksChanged` | 原生整合測試已驗證 `time-pos` 觀察屬性；仍需驗證取消觀察後不再收到事件與 track-list |
| 事件 | `EventReceived`、`EventNodeReceived`、`StartFile`、`EndFile`、`FileLoaded`、`Idle`、`VideoReconfigured`、`AudioReconfigured`、`SeekStarted`、`PlaybackRestarted`、`QueueOverflow`、`Shutdown`、`ClientMessage`、`Hook`、`LogMessageReceived`、`PropertyChanged`、`CommandReply` | 原生整合測試已驗證 `FileLoaded`、`EndFile` 與 `PropertyChanged` 基本路徑；仍需驗證每個事件資料結構 |
| 喚醒與事件迴圈 | `SetWakeupCallback()`、`Wakeup()`、`GetWakeupPipe()`、`WaitAsyncRequests()` | 建置通過，需驗證 UI 事件迴圈整合 |
| 掛鉤 | `AddHook()`、`Hook`、`ContinueHook()` | 建置通過，需驗證 hook id 與流程繼續 |

## render.h 與 render_gl.h

已比對 provider git build `5921fe5` 與 `e0eb42c303`；相對 stable v0.41.0 未發現公開 render API header 形狀變更。

| API 區域 | 受控入口 | 驗證狀態 |
| --- | --- | --- |
| OpenGL render API | `MpvOpenGlRenderContext`、`MpvOpenGlRenderContextOptions`、`MpvOpenGlDrmDisplayOptions`、`MpvOpenGlDrmDrawSurfaceSize` | 核心 C API 入口已補齊；需驗證 OpenGL context、resize、frame timing、DRM 與 `ReportSwap()` |
| software render API | `MpvSoftwareRenderContext` | 核心 C API 入口已補齊；需驗證 stride、像素格式、resize、skip rendering 與錯誤尺寸 |
| render 事件與進階參數 | `UpdateAvailable`、`Update()`、`GetNextFrameInformation()`、`SetParameter()`、`GetInformation()`、`SetIccProfile()`、`ClearIccProfile()`、`SetAmbientLight()`、`SkipRender()` | C API 入口已補齊，需驗證 render thread、UI thread、ICC、環境光與 frame timing 互動 |

## stream_cb.h

| API 區域 | 受控入口 | 驗證狀態 |
| --- | --- | --- |
| 自訂唯讀通訊協定 | `MpvPlayer.RegisterStreamProtocol(string, Func<string, Stream?>)` | 原生整合測試已使用 `mektest://...` 驗證可播放受控 WAV stream |
| 事件式開啟 | `MpvPlayer.RegisterStreamProtocol(string, EventHandler<MpvStreamOpenEventArgs>)` | 已建置通過，需驗證事件處理常式回傳串流 |
| 串流讀取 | `Stream.Read()` 對應 `read_fn` | 原生整合測試已驗證一般讀取與 EOF；仍需驗證錯誤傳回 |
| 串流搜尋與大小 | `Stream.Seek()`、`Stream.Length` 對應 `seek_fn`、`size_fn` | 原生整合測試已透過可搜尋 `MemoryStream` 驗證基本路徑；仍需驗證不可搜尋串流 |
| 串流關閉 | `Stream.Dispose()` 對應 `close_fn` | 原生整合測試已透過播放完成覆蓋基本關閉路徑；仍需驗證錯誤路徑會釋放串流 |
| 串流取消 | `cancel_fn` 目前為非阻塞空操作 | 已建置通過，需針對可取消來源設計進階中止語意 |

## 完成定義

不得只用建置成功宣稱 libmpv 已完全支援。完成宣告至少需要：

- 以 Windows x64 `libmpv-2.dll` 實際初始化。
- 以本機檔案與 `https://www.youtube.com/watch?v=dQw4w9WgXcQ` 分別驗證播放。
- 驗證命令、屬性、觀察屬性、事件節點、記錄、掛鉤、自訂串流與 render API。
- 驗證失敗路徑，包括不存在的屬性、錯誤格式、載入失敗、URL 工具不存在與 render context 建立失敗。

不需要原生執行階段的核心測試：

```powershell
dotnet run --project .\tests\MediaEmbedKit.Mpv.Tests\MediaEmbedKit.Mpv.Tests.csproj
```

需要 Windows x64 `libmpv-2.dll` 的原生整合測試：

```powershell
dotnet run --project .\tests\MediaEmbedKit.Mpv.IntegrationTests\MediaEmbedKit.Mpv.IntegrationTests.csproj
```

需要 Windows x64 原生執行階段與 GUI 的範例播放冒煙測試：

```powershell
dotnet run --project .\tests\MediaEmbedKit.Mpv.PlaybackSmoke\MediaEmbedKit.Mpv.PlaybackSmoke.csproj -- --seconds 20
```

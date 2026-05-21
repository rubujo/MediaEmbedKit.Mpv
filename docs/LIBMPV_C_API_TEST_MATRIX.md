# libmpv C API 測試矩陣

本矩陣區分「C API 包裝覆蓋」與「實戰情境驗證」。核心包裝器已覆蓋 libmpv stable v0.41.0 的 `client.h`、`render.h`、`render_gl.h` 與 `stream_cb.h` 公開 API；整合測試會以 Windows 原生程式庫、媒體檔與錯誤路徑驗證主要實戰語意。

## 覆蓋狀態

| 項目 | 狀態 |
| --- | --- |
| 官方基準 | mpv v0.41.0 |
| 提供者對齊 | shinchiro `20260521` / mpv `d521821`；zhongfly `2026-05-21-db73857997` / mpv `db73857997` |
| 公開匯出函式 | 官方標頭 54 個；`MpvNative` P/Invoke 54 個 |
| 列舉與旗標 | `MpvErrorCode`、`MpvFormat`、`MpvLogLevel`、`MpvEndFileReason`、render 相關列舉已對齊 v0.41.0 |
| 原生資料結構 | 事件、節點、stream callback、OpenGL、DRM、render frame info 與 `mpv_byte_array` 皆有受控對應 |

## client.h

| 區域 | 受控入口 | 驗證狀態 |
| --- | --- | --- |
| 版本與錯誤 | `ClientApiVersion()`、`MpvError`、`MpvException` | 已驗證初始化、完整列舉錯誤訊息、屬性錯誤、格式錯誤、選項錯誤與命令錯誤。 |
| 用戶端生命週期 | `MpvPlayer`、`Initialize()`、`Dispose()`、client handle API | 已驗證單一 player、多 client、weak client、raw client destroy 與 shutdown 事件。 |
| 設定與 scripts | `ConfigDirectory`、`ConfigFiles`、`InputConfigFile`、`ScriptFiles`、`LoadScript()` | 已驗證設定檔錯誤、script 載入錯誤與 Lua script message 往返。 |
| 選項 | `SetOptionString()`、`SetOptionFlag()`、`SetOptionInt64()`、`SetOptionDouble()`、`SetOptionNode()`、`MpvPlayerOptions` 鏈式輔助方法、`MpvEncodingOptions` | 已驗證初始化前常用選項、無效選項錯誤、播放選項組態、鏈式輔助方法與 encoding mode 選項套用。 |
| 命令 | `Command()`、`CommandNode()`、`CommandNamed()`、`GetCommandList()` | 已驗證同步命令、命令錯誤、節點回傳與常用高階 API。 |
| 非同步命令 | `CommandAsync()`、`AbortAsyncCommand()`、`CommandReply` | 已驗證成功、錯誤回覆、命令回覆事件與取消未知要求後的後續命令穩定性。 |
| 屬性 | `GetProperty*()`、`SetProperty*()`、常用強型別屬性 | 已驗證字串、旗標、數值、節點、格式錯誤與常用播放屬性。 |
| 觀察屬性 | `ObserveProperty()`、`UnobserveProperty()`、`PropertyChanged` | 已驗證 `time-pos`、`pause`、`track-list` 與取消觀察。 |
| 事件 | `EventReceived`、typed event、`EventNodeReceived` | 已驗證 StartFile、FileLoaded、EndFile、CommandReply、ClientMessage、Hook、LogMessage、PropertyChange、EventNodeReceived 與 Shutdown。`MpvPlayer.TracksChanged` 由 PropertyChange 觀察 `track-list` 屬性合成。`MpvEventId` 對齊 mpv master 配置，數值 9 / 10 / 12 / 13 / 15 為 mpv 已從 enum 移除的歷史 ID（前 `TRACKS_CHANGED` / `TRACK_SWITCHED` / `PAUSE` / `UNPAUSE` / `SCRIPT_INPUT_DISPATCH`），刻意保留缺口、改以對應屬性觀察取代。 |
| hook 與 wakeup | `AddHook()`、`ContinueHook()`、`Wakeup()` | 已驗證 hook 觸發與繼續流程；事件迴圈 wakeup 由播放器生命週期覆蓋。 |

## render.h 與 render_gl.h

已比對 shinchiro git build `d521821` 與 zhongfly git build `db73857997`，未發現相對 stable v0.41.0 的公開 header 形狀差異。

| 區域 | 受控入口 | 驗證狀態 |
| --- | --- | --- |
| OpenGL render API | `MpvOpenGlRenderContext` | 已驗證 context 建立錯誤、無 OpenGL 函式位址錯誤、更新旗標、frame info 與 `ReportSwap()` 呼叫路徑。 |
| software render API | `MpvSoftwareRenderContext` | 已驗證 context 建立或 runtime 不支援錯誤、stride、像素格式、錯誤尺寸與 `ReportSwap()` 呼叫路徑。 |
| render 參數 | `SetParameter()`、`GetInformation()`、ICC、環境光、skip rendering | 已驗證 skip rendering、ICC 清除、環境光設定、frame info 與錯誤參數。 |

## stream_cb.h

| 區域 | 受控入口 | 驗證狀態 |
| --- | --- | --- |
| 自訂唯讀通訊協定 | `RegisterStreamProtocol(...)` | 已以受控 WAV stream 驗證基本播放。 |
| 事件式開啟 | `RegisterStreamProtocol(string, EventHandler<MpvStreamOpenEventArgs>)` | 已驗證事件處理常式可提供 stream 或拒絕開啟。 |
| 讀取、搜尋、大小、關閉 | `Stream` 對應 callback | 已驗證基本讀取、不可搜尋串流、讀取錯誤與關閉流程。 |
| 取消 | `cancel_fn` | 已透過 `IMpvStreamCancellationHandler` 驗證取消通知可解除阻塞讀取。 |

## 高階 API

| 區域 | 受控入口 | 驗證狀態 |
| --- | --- | --- |
| 生命週期 | `MpvPlayer.InitializeAsync`、`MpvPlayer.ShutdownAsync`、`MpvPlayer.DisposeAsync` | 整合測試已覆蓋 graceful shutdown 與 cancellation。 |
| Capability 快照 | `MpvPlayer.GetCapabilities`、`MpvCapabilities`、`SupportsProtocol` | 整合測試以實際 runtime 驗證 client API 版本與協定。 |
| 鏈式 builder | `MpvAppBuilder`、`BuildAsync` | 整合測試覆蓋 `UseRuntime` + `ConfigureOptions` 端到端流程。 |
| 媒體項目 | `MpvMediaItem`、`MpvPlayer.Load`、`MpvPlayer.LoadAsync` | 整合測試以本機 WAV 驗證 per-file 選項 + FileLoaded 等候。 |
| 屬性 observable | `MpvPlayer.WatchProperty<T>`、`IObservable<T>` | 整合測試覆蓋多訂閱者共享與 Player Dispose 時 OnCompleted。 |
| ILogger 整合 | `MpvPlayerOptions.LoggerFactory` | 由 `MpvPlayer` 建構期掛接；整合測試以記憶體 logger 驗證等級對應。 |

## 執行階段輔助工具

| 區域 | 受控入口 | 驗證狀態 |
| --- | --- | --- |
| 暫存／套用／回滾 | `MpvLibraryUpdateScheduler` | 整合測試以 fake fixture 走 stage → apply → rollback 路徑。 |
| 啟動健檢 | `MpvRuntimeHealthCheck.AnalyzeAsync` | 整合測試以實際 runtime 驗證 `probeLibMpv: true`。 |
| 授權稽核 | `MpvLicenseAuditor.AnalyzeAsync`、`MpvBuildLicense` | 單元測試覆蓋 mpv-configuration / ffmpeg -version 分類規則。 |
| 提供者備援 | `MpvWindowsBuildDownloadOptions.ProviderFallbackOrder` | 單元測試覆蓋預設值；下載端備援走發行檢查閘門。 |
| 串流外部工具輸出 | `ExternalToolProcessRunner.StreamAsync`、`YtDlpProcessRunner.StreamFormatsAsync`、`DenoProcessRunner.StreamVersionAsync` | 程式碼路徑共用 base StreamAsync；發行檢查閘門期間實戰驗證。 |

## 完成定義

目前整合驗證涵蓋：

- Windows `libmpv-2.dll` 實際初始化。
- 本機檔案與 `https://www.youtube.com/watch?v=dQw4w9WgXcQ` 播放。
- 命令、屬性、觀察屬性、事件節點、記錄、hook、自訂串流與 render API。
- 不存在屬性、錯誤格式、載入失敗、URL 工具不存在與 render context 建立失敗。

## 測試入口

```powershell
dotnet run --project .\tests\MediaEmbedKit.Mpv.Tests\MediaEmbedKit.Mpv.Tests.csproj
dotnet run --project .\tests\MediaEmbedKit.Mpv.IntegrationTests\MediaEmbedKit.Mpv.IntegrationTests.csproj
dotnet run --project .\tests\MediaEmbedKit.Mpv.PlaybackSmoke\MediaEmbedKit.Mpv.PlaybackSmoke.csproj -- --seconds 20
```

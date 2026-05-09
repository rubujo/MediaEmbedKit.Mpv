# libmpv C API 測試矩陣

本矩陣區分「C API 包裝覆蓋」與「實戰情境驗證」。核心包裝器已覆蓋 libmpv stable v0.41.0 的 `client.h`、`render.h`、`render_gl.h` 與 `stream_cb.h` 公開 API；整合測試會以 Windows x64 原生程式庫、媒體檔與錯誤路徑驗證主要實戰語意。

## 覆蓋狀態

| 項目 | 狀態 |
| --- | --- |
| 官方基準 | mpv v0.41.0 |
| provider 對齊 | shinchiro `20260421` / mpv `5921fe5`；zhongfly `2026-05-08-e0eb42c303` / mpv `e0eb42c303` |
| 公開匯出函式 | 官方標頭 54 個；`MpvNative` P/Invoke 54 個 |
| 列舉與旗標 | `MpvErrorCode`、`MpvFormat`、`MpvLogLevel`、`MpvEndFileReason`、render 相關列舉已對齊 v0.41.0 |
| 原生資料結構 | 事件、節點、stream callback、OpenGL、DRM、render frame info 與 `mpv_byte_array` 皆有受控對應 |

## client.h

| 區域 | 受控入口 | 驗證狀態 |
| --- | --- | --- |
| 版本與錯誤 | `ClientApiVersion()`、`MpvError`、`MpvException` | 已驗證初始化、完整列舉錯誤訊息、屬性錯誤、格式錯誤、選項錯誤與命令錯誤。 |
| 用戶端生命週期 | `MpvPlayer`、`Initialize()`、`Dispose()`、client handle API | 已驗證單一 player、多 client、weak client、raw client destroy 與 shutdown 事件。 |
| 設定與 scripts | `ConfigDirectory`、`ConfigFiles`、`InputConfigFile`、`ScriptFiles`、`LoadScript()` | 已驗證設定檔錯誤、script 載入錯誤與 Lua script message 往返。 |
| 選項 | `SetOptionString()`、`SetOptionFlag()`、`SetOptionInt64()`、`SetOptionDouble()`、`SetOptionNode()` | 已驗證初始化前常用選項、無效選項錯誤與播放選項組態套用。 |
| 命令 | `Command()`、`CommandNode()`、`CommandNamed()`、`GetCommandList()` | 已驗證同步命令、命令錯誤、節點回傳與常用高階 API。 |
| 非同步命令 | `CommandAsync()`、`AbortAsyncCommand()`、`CommandReply` | 已驗證成功、錯誤回覆、命令回覆事件與取消未知要求後的後續命令穩定性。 |
| 屬性 | `GetProperty*()`、`SetProperty*()`、常用強型別屬性 | 已驗證字串、旗標、數值、節點、格式錯誤與常用播放屬性。 |
| 觀察屬性 | `ObserveProperty()`、`UnobserveProperty()`、`PropertyChanged` | 已驗證 `time-pos`、`pause`、`track-list` 與取消觀察。 |
| 事件 | `EventReceived`、typed event、`EventNodeReceived` | 已驗證 StartFile、FileLoaded、EndFile、CommandReply、ClientMessage、Hook、LogMessage、PropertyChange、TracksChanged、EventNodeReceived 與 Shutdown。 |
| hook 與 wakeup | `AddHook()`、`ContinueHook()`、`Wakeup()` | 已驗證 hook 觸發與繼續流程；事件迴圈 wakeup 由播放器生命週期覆蓋。 |

## render.h 與 render_gl.h

已比對 provider git build `5921fe5` 與 `e0eb42c303`，未發現相對 stable v0.41.0 的公開 header 形狀差異。

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

## 完成定義

目前整合驗證涵蓋：

- Windows x64 `libmpv-2.dll` 實際初始化。
- 本機檔案與 `https://www.youtube.com/watch?v=dQw4w9WgXcQ` 播放。
- 命令、屬性、觀察屬性、事件節點、記錄、hook、自訂串流與 render API。
- 不存在屬性、錯誤格式、載入失敗、URL 工具不存在與 render context 建立失敗。

## 測試入口

```powershell
dotnet run --project .\tests\MediaEmbedKit.Mpv.Tests\MediaEmbedKit.Mpv.Tests.csproj
dotnet run --project .\tests\MediaEmbedKit.Mpv.IntegrationTests\MediaEmbedKit.Mpv.IntegrationTests.csproj
dotnet run --project .\tests\MediaEmbedKit.Mpv.PlaybackSmoke\MediaEmbedKit.Mpv.PlaybackSmoke.csproj -- --seconds 20
```

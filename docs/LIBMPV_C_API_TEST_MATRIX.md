# libmpv C API 測試矩陣

本矩陣區分「C API 包裝覆蓋」與「實戰情境驗證」。核心包裝器已覆蓋 libmpv stable v0.41.0 的 `client.h`、`render.h`、`render_gl.h` 與 `stream_cb.h` 公開 API；完整產品宣告仍需搭配原生程式庫、媒體檔、URL 與錯誤路徑驗證。

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
| 版本與錯誤 | `ClientApiVersion()`、`MpvError`、`MpvException` | 已驗證初始化與不存在屬性錯誤；仍需擴充完整錯誤碼驗證 |
| 用戶端生命週期 | `MpvPlayer`、`Initialize()`、`Dispose()`、client handle API | 已驗證單一 client；仍需驗證多 client 與 shutdown |
| 設定與 scripts | `ConfigDirectory`、`ConfigFiles`、`InputConfigFile`、`ScriptFiles`、`LoadScript()` | 入口已完成；需驗證設定錯誤與 script 後端 |
| 選項 | `SetOptionString()`、`SetOptionFlag()`、`SetOptionInt64()`、`SetOptionDouble()`、`SetOptionNode()` | 入口已完成；需驗證初始化前後可設定範圍 |
| 命令 | `Command()`、`CommandNode()`、`CommandNamed()`、`GetCommandList()` | 入口與常用便利 API 已完成；需擴充錯誤命令與回傳節點驗證 |
| 非同步命令 | `CommandAsync()`、`AbortAsyncCommand()`、`CommandReply` | 入口已完成；需驗證成功、錯誤與中止 |
| 屬性 | `GetProperty*()`、`SetProperty*()`、常用強型別屬性 | 已驗證部分屬性；需擴充格式轉換與能力清單驗證 |
| 觀察屬性 | `ObserveProperty()`、`UnobserveProperty()`、`PropertyChanged` | 已驗證 `time-pos`；需驗證取消觀察 |
| 事件 | `EventReceived`、typed event、`EventNodeReceived` | 已驗證基本播放事件；需驗證每個事件資料結構 |
| hook 與 wakeup | `AddHook()`、`ContinueHook()`、`Wakeup()` | 入口已完成；需補執行階段驗證 |

## render.h 與 render_gl.h

已比對 provider git build `5921fe5` 與 `e0eb42c303`，未發現相對 stable v0.41.0 的公開 header 形狀差異。

| 區域 | 受控入口 | 驗證狀態 |
| --- | --- | --- |
| OpenGL render API | `MpvOpenGlRenderContext` | 入口已完成；需驗證 context、resize、frame timing 與 `ReportSwap()` |
| software render API | `MpvSoftwareRenderContext` | 入口已完成；需驗證 stride、像素格式與錯誤尺寸 |
| render 參數 | `SetParameter()`、`GetInformation()`、ICC、環境光、skip rendering | 入口已完成；需驗證 render thread 與 UI thread 互動 |

## stream_cb.h

| 區域 | 受控入口 | 驗證狀態 |
| --- | --- | --- |
| 自訂唯讀通訊協定 | `RegisterStreamProtocol(...)` | 已以受控 WAV stream 驗證基本播放 |
| 事件式開啟 | `RegisterStreamProtocol(string, EventHandler<MpvStreamOpenEventArgs>)` | 入口已完成；需補事件處理常式驗證 |
| 讀取、搜尋、大小、關閉 | `Stream` 對應 callback | 已驗證基本路徑；需補錯誤與不可搜尋串流 |
| 取消 | `cancel_fn` | 目前為非阻塞空操作；進階取消語意尚未定義 |

## 完成定義

完整驗證至少需涵蓋：

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

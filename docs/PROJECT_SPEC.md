# MediaEmbedKit.Mpv 專案規範

本文件是專案規範入口。細節由專責文件維護；變更支援目標、平台狀態、runtime 政策、工程規則或 AI agent 指示時，必須同步更新對應文件。

| 主題 | 文件 |
| --- | --- |
| 目標框架與平台狀態 | `docs/SUPPORT_MATRIX.md` |
| UI 後端與 AirSpace 限制 | `docs/UI_BACKENDS.md` |
| libmpv、yt-dlp、Deno、FFmpeg runtime 政策 | `docs/RUNTIME_ASSETS.md` |
| libmpv C API 覆蓋與測試矩陣 | `docs/LIBMPV_C_API_TEST_MATRIX.md` |
| 高階 API 與 ergonomics 指南 | `docs/HIGH_LEVEL_API.md` |
| 控制項共通綁定 API | `docs/CONTROLS_API.md` |
| 發佈前本機檢查 | `docs/RELEASE_CHECKLIST.md` |
| Windows 設計階段檢查 | `docs/DESIGN_TIME_CHECKLIST.md` |
| 工程、文件、提交與驗證規則 | `docs/ENGINEERING_STANDARDS.md` |
| AI agent 與 Agent Skills 結構 | `docs/AI_AGENT_INTEGRATION.md` |
| 參考來源 | `docs/REFERENCE_SOURCES.md` |

## 目標

MediaEmbedKit.Mpv 提供 .NET libmpv 包裝器與 Windows 桌面 UI 控制項。核心套件需覆蓋 libmpv stable v0.41.0 公開 C API，並提供常用高階播放 API。UI 套件不得重複宣告核心 P/Invoke。

目前產品支援範圍為 Windows x64。未符合支援準則的平台、架構或 UI 後端不得列入支援矩陣。

## 設計原則

- 核心 API 盡量保持平台中立。
- 控制項建構函式與初始化流程不得自動下載第三方二進位檔。
- `libmpv-2.dll` 載入後不可在同一處理序 hot reload；更新必須暫存並提示重新啟動。
- Windows x64 runtime 資料夾可同層放置 `libmpv-2.dll`、`yt-dlp.exe`、`deno.exe`、`ffmpeg.exe`、`ffprobe.exe`、mpv 設定檔與 scripts。
- runtime helper 必須提供 SHA-256 驗證、來源鎖定與可由使用者選擇的驗證策略；生產環境不得只依賴未鎖定的 latest 下載。
- 高階 API 可提供薄型 fluent helper，但不得引入會隱藏下載、初始化或釋放責任的 pipeline/flow 引擎。
- WinForms、WPF、WinUI 3 與 MAUI Windows 主線控制項使用 HWND 後端。
- Avalonia 使用 Windows x64 OpenGL render API 後端。
- C# 區域變數、`using` 陳述式與 `foreach` 迴圈變數使用明確型別；只有必要時才使用 `var`。
- 提交訊息必須遵循慣例式提交 1.0.0，且同時包含主旨與正文。

## 支援準則

新增平台或處理器架構前，必須符合下列條件：

- 有可重複取得且可授權評估的 libmpv 原生執行階段來源。
- runtime helper 可下載、指向或驗證該原生程式庫。
- yt-dlp、Deno 與 FFmpeg 也有相同平台與架構可用來源。
- UI 後端完成 surface、resize、生命週期、AirSpace 或組合限制處理。
- 範例能以本機檔案與 URL 播放通過冒煙測試。
- 文件、sample、solution 與 catalog 只列出已符合條件的目標。

## 目前狀態

- 核心 `MediaEmbedKit.Mpv` 支援 `netstandard2.0;net472;net48;net8.0;net10.0`。
- libmpv C API 包裝已對齊 stable v0.41.0，公開 P/Invoke 匯出函式 54/54。
- 已比對 shinchiro `20260421` 與 zhongfly `2026-05-13-37f4edffaf` provider git build header，未發現需新增 P/Invoke 的差異。
- 已提供命令、屬性、節點、事件、render API、stream callback、mpv encoding mode 附帶輸出、薄型 fluent options API 與常用高階播放 API。
- WinForms/WPF 使用 Windows HWND 後端。
- WinUI 3/MAUI Windows 使用 Windows HWND 後端。
- Avalonia 使用 Windows x64 OpenGL render API 後端。
- Windows x64 runtime helper 支援 libmpv、yt-dlp、Deno 與 yt-dlp FFmpeg-Builds 下載、更新與同層配置。
- 範例播放冒煙測試涵蓋 WinForms、WPF、Avalonia、WinUI 3 與 MAUI Windows。
- 已提供本機發佈前驗證、NuGet 套件內容檢查、乾淨 consumer 建置驗證、GUI consumer 實際播放驗證、第一階段壓力測試與長時間 GUI 播放壓力測試腳本。
- Windows UI 控制項設計階段檢查清單已由 `docs/DESIGN_TIME_CHECKLIST.md` 維護。
- 高階 API 已提供 `MpvAppBuilder`、`MpvMediaItem`、`MpvPlayer.LoadAsync`、`MpvPlayer.WatchProperty<T>`、`MpvCapabilities`、`IAsyncDisposable`、`Microsoft.Extensions.Logging.Abstractions` 整合、`MpvServiceCollectionExtensions.AddMpvPlayerFactory`。
- 高階 encoding API 已提供 `MpvEncodingOptions` (fluent + `*-add` 加值 + trim / `WithFrameAccurateSeek` / 字幕 burn-in / filter 逃生口 / metadata 個別 tag)、`MpvVideoCodecPreset` / `MpvAudioCodecPreset`（含 `Copy` stream-copy）、`MpvEncoder.EncodeAsync` / `EncodeTwoPassAsync` / `RemuxAsync` / `ExtractAudioAsync` / `ExtractVideoAsync` / `ExtractFrameAsync` / `ExtractFramesAsync` / `ConcatenateAsync` (EDL) / `SplitAsync`、`MpvAppBuilder.UseEncodingTo`。`EncodeAsync` cancellation 觸發後給 libmpv 3 秒寬限期等候自發 `EndFile`，逾時則回傳 `EndReason=Stop` 結果。明確不支援的場景（multi-track output / HLS/DASH / 字幕匯出 / 原檔 in-place 編輯）由 `docs/HIGH_LEVEL_API.md` 「libmpv 結構性不支援」段維護。
- Runtime helper 健康檢查語意拆分：`MpvRuntimeHealthReport.IsHealthy` 僅檢查核心 libmpv 是否可用（能播媒體最小條件），`IsComplete` 額外要求 yt-dlp / deno / ffmpeg / ffprobe 全部齊備，`IsHealthyFor(MpvRuntimeTools)` 接受 `[Flags]` 列舉 `YtDlp` / `Deno` / `FFmpeg` / `FFprobe` / `All` 自訂必備工具子集。
- Runtime helper 已提供 `MpvLibraryUpdateScheduler` 暫存／套用／回滾、`MpvRuntimeHealthCheck` 啟動健檢、`MpvLicenseAuditor` 授權稽核、provider fallback、`YtDlpProcessRunner` / `DenoProcessRunner` / `ExternalToolProcessRunner` 的 `IAsyncEnumerable` 串流輸出。
- 高階 API 與 ergonomics 詳細指南由 `docs/HIGH_LEVEL_API.md` 維護。
- 5 個 UI 框架控制項共通的 `Source` / `Position` / `Duration` / `Volume` / `IsPaused` / `IsMuted` / `PlaybackState` 綁定屬性與 `PlayCommand` / `PauseCommand` / `StopCommand` / `TogglePauseCommand` / `ToggleMuteCommand` 等 `ICommand` 詳細說明由 `docs/CONTROLS_API.md` 維護。

libmpv C API wrapper 可宣稱已完成 stable v0.41.0 公開 C API 包裝覆蓋。實戰驗證範圍依 `docs/LIBMPV_C_API_TEST_MATRIX.md` 維護。

## 驗證

```powershell
dotnet format .\MediaEmbedKit.Mpv.slnx --no-restore
dotnet run --project .\tests\MediaEmbedKit.Mpv.Tests\MediaEmbedKit.Mpv.Tests.csproj
dotnet run --project .\tests\MediaEmbedKit.Mpv.IntegrationTests\MediaEmbedKit.Mpv.IntegrationTests.csproj
dotnet build .\MediaEmbedKit.Mpv.slnx
```

整合測試需要 Windows x64 `libmpv-2.dll`。URL 播放需要 `yt-dlp.exe` 可被 mpv 找到，或透過 `MpvPlayerOptions.YtdlpPath` 指定。FFmpeg-Builds 下載驗證需可連線至 GitHub Releases。

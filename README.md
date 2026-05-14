# MediaEmbedKit.Mpv

MediaEmbedKit.Mpv 是 .NET libmpv 包裝器與 Windows 桌面 UI 控制項專案。專案提供核心 libmpv C API 包裝、常用高階播放 API、Windows x64 / ARM64 執行階段資產 helper，以及 WinForms、WPF、Avalonia、WinUI 3 與 .NET MAUI Windows 範例。

本專案不是 mpv、yt-dlp、Deno、FFmpeg 或其相關建置提供者的官方專案。`MediaEmbedKit` 是本專案名稱；`Mpv` 僅表示本專案與 mpv/libmpv 的整合目標。

## AI 產製聲明

本專案目前的原始碼、文件與範例主要由 AI 服務依使用者需求產生、整理與修改。採用本專案前，請自行完成下列審查：

- 程式碼正確性、例外處理、執行緒安全與資源釋放。
- 安全性、隱私、網路下載行為與供應鏈風險。
- 第三方元件授權、散發義務與目標環境相容性。
- 實際播放品質、UI 行為、效能與長時間穩定性。

AI 產製內容可能包含缺漏、錯誤假設或未涵蓋的邊界情境。本專案不應在未經完整驗證前直接用於生產環境。

## 支援範圍

目前產品支援範圍收斂為 Windows x64 與 Windows ARM64。

| 套件 | 狀態 |
| --- | --- |
| `MediaEmbedKit.Mpv` | 核心 API，支援 `netstandard2.0;net472;net48;net8.0;net10.0`。 |
| `MediaEmbedKit.Mpv.WinForms` | Windows HWND 控制項。 |
| `MediaEmbedKit.Mpv.Wpf` | `HwndHost` 控制項，內建 AirSpace 覆蓋層。 |
| `MediaEmbedKit.Mpv.Avalonia` | Windows x64 / ARM64 OpenGL render API 控制項。 |
| `MediaEmbedKit.Mpv.WinUI` | WinUI 3 Windows HWND 控制項。 |
| `MediaEmbedKit.Mpv.Maui` | .NET MAUI Windows handler。 |

未列入支援矩陣的平台、處理器架構與 UI 後端不提供支援承諾。詳細狀態請參閱 `docs/SUPPORT_MATRIX.md` 與 `docs/UI_BACKENDS.md`。

## 功能概要

- libmpv stable v0.41.0 公開 C API 包裝（54/54 函式對齊 mpv master）。
- 通用 command、property、node 與 event 入口。
- 高階播放 API：`MpvAppBuilder` fluent 建構、`MpvMediaItem` per-file 選項、`LoadAsync`、`WatchProperty<T>`、`MpvCapabilities`、`IAsyncDisposable`、`Microsoft.Extensions.Logging.Abstractions` 整合、`MpvServiceCollectionExtensions.AddMpvPlayerFactory`；以及播放狀態、音量、速度、播放清單、章節、軌道、字幕、OSD、截圖、濾鏡、輸入事件、script message。
- 高階 encoding API：`MpvEncoder.EncodeAsync` / `EncodeTwoPassAsync` 一站式轉碼（含 `IProgress<MpvEncodingProgress>` 進度與 `CancellationToken` 支援，取消含 3 秒 grace period 並以結果回報）、`RemuxAsync` stream-copy 重新封裝、`ExtractAudioAsync` / `ExtractVideoAsync` 單軌抽取、`ExtractFrameAsync` / `ExtractFramesAsync` 影格抽圖、`ConcatenateAsync`（EDL）多檔串接、`SplitAsync` 多段切割、`MpvAppBuilder.UseEncodingTo` 整合、`MpvVideoCodecPreset` / `MpvAudioCodecPreset`（含 `Copy` stream-copy）。
- 五個 UI 框架控制項共通綁定屬性（Source / Position / Duration / Volume / IsPaused / IsMuted / PlaybackState）與 MVVM Commands（Play / Pause / Stop / TogglePause / ToggleMute），詳見 `docs/CONTROLS_API.md`。
- OpenGL render API、software render API 與 stream callback 的核心包裝。
- Windows x64 / ARM64 runtime helper：`MpvLibraryUpdateScheduler` stage / apply / rollback、`MpvRuntimeHealthCheck`（含 `IsHealthy` / `IsComplete` / `IsHealthyFor(MpvRuntimeTools)` 健康語意拆分）、`MpvLicenseAuditor`、provider fallback；可由使用者明確下載或更新 `libmpv-2.dll`、`yt-dlp.exe`、`deno.exe`、`ffmpeg.exe` 與 `ffprobe.exe`（架構依目前處理序自動偵測）。
- 預設下載驗證政策為 `RequireGitHubDigest`（GitHub Releases API 提供的 `sha256:` digest 必須驗證一致）；可選 `RequireProviderChecksum` / `RequirePinnedSha256` 或 `BestEffort` 相容模式。
- yt-dlp 格式預設值與自訂 selector；yt-dlp / Deno / FFmpeg / ffprobe 外部處理序執行器（`StreamAsync` 即時消費 stdout/stderr）。
- WinForms、WPF、Avalonia、WinUI 3 與 MAUI Windows 範例（含 MVVM 綁定示範區）。

## 基本使用

應用程式可自行部署 `libmpv-2.dll`，或明確呼叫 helper 建立 Windows x64 / ARM64 runtime 資料夾（helper 依目前處理序架構自動選擇對應資產）。

```csharp
MpvWindowsRuntimeDownloadResult runtime =
    await MpvWindowsRuntimeInstaller.InstallOrUpdateAsync("runtime");

MpvPlayerOptions options =
    MpvWindowsRuntimeInstaller.CreatePlayerOptions(runtime.RuntimeDirectory);

using MpvPlayer player = new MpvPlayer(options);
player.Initialize();
player.LoadFile("https://www.youtube.com/watch?v=dQw4w9WgXcQ");
```

若要讓 runtime 資料夾同時作為 mpv 設定資料夾，可啟用設定載入：

```csharp
MpvPlayerOptions options =
    MpvWindowsRuntimeInstaller.CreatePlayerOptions(
        runtime.RuntimeDirectory,
        loadRuntimeConfiguration: true);
```

若要使用 mpv encoding mode 進行簡單輸出，推薦使用 `MpvEncoder.EncodeAsync` 一站式 API（自行管理短生命週期 player、套用選項、await `EndFile`、回報進度）：

```csharp
MpvEncodingOptions encoding = new MpvEncodingOptions(outputPath)
    .AsMp4()
    .WithVideoCodec(MpvVideoCodecPreset.H264)
    .WithVideoCodecOption("crf", "23")
    .WithAudioCodec(MpvAudioCodecPreset.Aac);

MpvPlayerOptions playerOptions =
    MpvWindowsRuntimeInstaller.CreatePlayerOptions(runtime.RuntimeDirectory);

Progress<MpvEncodingProgress> progress = new Progress<MpvEncodingProgress>(p =>
    Console.WriteLine($"{p.Percent:F1}%  pos={p.Position}  bytes={p.OutputBytes}"));

MpvEncodingResult result = await MpvEncoder.EncodeAsync(
    inputPath,
    encoding,
    playerOptions,
    progress);

if (!result.Success)
{
    Console.Error.WriteLine($"編碼失敗：reason={result.EndReason} err={result.ErrorCode}");
}
```

兩階段、stream-copy、抽取音訊／視訊／影格、多檔串接（EDL）、多段切割、字幕 burn-in 等場景請參考 `docs/HIGH_LEVEL_API.md` Encoding 段。

encoding mode 屬於 mpv 的附帶能力，本專案在其之上提供 C# 友善的高階入口；mpv 結構性不支援的場景（多軌輸出 / HLS-DASH 切片 / 字幕匯出 / 原檔 in-place 編輯）請改用 FFmpeg。

高階 API 採薄型 helper 設計：常用設定可用 fluent 方式組合，但播放器初始化、runtime 下載與資源釋放仍由應用程式明確控制。

## 範例

範例位於 `samples`。GUI 範例會先顯示視窗，再於背景呼叫共用 helper 準備 runtime 資產；Console 範例則示範核心 `MpvPlayer` 最小生命週期。範例涵蓋初始化、事件輸出、播放控制與釋放流程。

```powershell
dotnet run --project .\samples\WinFormsSample\MediaEmbedKit.Mpv.Samples.WinForms.csproj
dotnet run --project .\samples\WpfSample\MediaEmbedKit.Mpv.Samples.Wpf.csproj
dotnet run --project .\samples\AvaloniaSample\MediaEmbedKit.Mpv.Samples.Avalonia.csproj
dotnet run --project .\samples\WinUISample\MediaEmbedKit.Mpv.Samples.WinUI.csproj
dotnet run --project .\samples\MauiSample\MediaEmbedKit.Mpv.Samples.Maui.csproj
dotnet run --project .\samples\ConsoleMinimalSample\MediaEmbedKit.Mpv.Samples.ConsoleMinimal.csproj
```

範例說明請參閱 `samples/README.md`。

## 測試

```powershell
dotnet run --project .\tests\MediaEmbedKit.Mpv.Tests\MediaEmbedKit.Mpv.Tests.csproj
dotnet run --project .\tests\MediaEmbedKit.Mpv.IntegrationTests\MediaEmbedKit.Mpv.IntegrationTests.csproj
dotnet run --project .\tests\MediaEmbedKit.Mpv.PlaybackSmoke\MediaEmbedKit.Mpv.PlaybackSmoke.csproj -- --seconds 20
.\tools\Invoke-GuiConsumerPlaybackValidation.ps1 -Seconds 20
```

整合測試與播放冒煙測試需要 Windows x64 原生執行階段。URL 播放需要 `yt-dlp.exe` 可被 mpv 找到，或透過 `MpvPlayerOptions.YtdlpPath` 指定。預設 runtime helper 也會準備 yt-dlp 建議使用的 `ffmpeg.exe` 與 `ffprobe.exe`；如不需要，可設定 `MpvWindowsRuntimeDownloadOptions.IncludeFFmpeg = false`。

發佈前可執行本機驗證腳本：

```powershell
.\tools\Invoke-PreReleaseValidation.ps1
```

此腳本是 Windows x64 發佈前本機驗證主流程，會執行格式檢查、測試、Release 建置、NuGet 套件內容驗證與乾淨 consumer 專案驗證。若要執行完整 Windows release gate，可使用：

```powershell
.\tools\Invoke-PreReleaseValidation.ps1 -IncludeWindowsReleaseGate -GuiPlaybackSeconds 20
```

GUI consumer 實際播放驗證會以本機 NuGet 套件建立臨時 consumer sample，並播放到指定秒數後關閉。長時間 GUI 播放壓力測試可使用：

```powershell
.\tools\Invoke-GuiPlaybackStress.ps1 -Seconds 120 -Iterations 2
```

## 文件

- `docs/PROJECT_SPEC.md`：專案規範入口。
- `docs/SUPPORT_MATRIX.md`：目標框架與支援狀態。
- `docs/UI_BACKENDS.md`：UI 後端與 AirSpace 限制。
- `docs/RUNTIME_ASSETS.md`：runtime 下載與更新政策。
- `docs/HIGH_LEVEL_API.md`：高階 API 與 encoding 操作指南。
- `docs/CONTROLS_API.md`：五個 UI 框架控制項共通綁定屬性與 Commands。
- `docs/LIBMPV_C_API_TEST_MATRIX.md`：C API 覆蓋與驗證矩陣。
- `docs/RELEASE_CHECKLIST.md`：發佈前本機檢查。
- `docs/DESIGN_TIME_CHECKLIST.md`：Windows UI 控制項設計階段檢查。
- `docs/ENGINEERING_STANDARDS.md`：工程、文件、提交與格式規範。
- `docs/AI_AGENT_INTEGRATION.md`：AI agent 入口與 skill 結構。

## 授權與第三方元件

本儲存庫中的受控原始碼與文件採用 CC0-1.0。第三方原生執行階段二進位檔不簽入本儲存庫，也不因本專案授權而改變其授權條款。

散發 mpv/libmpv、yt-dlp、Deno、FFmpeg 或其相依項目前，請自行確認授權與合規義務。FFmpeg-Builds 目前使用 GPL build；本專案只提供下載 helper，不將該二進位檔納入 NuGet 套件。詳細資訊請參閱 `THIRD_PARTY_NOTICES.md`。

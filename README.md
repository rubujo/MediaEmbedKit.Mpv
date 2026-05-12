# MediaEmbedKit.Mpv

MediaEmbedKit.Mpv 是 .NET libmpv 包裝器與 Windows 桌面 UI 控制項專案。專案提供核心 libmpv C API 包裝、常用高階播放 API、Windows x64 執行階段資產 helper，以及 WinForms、WPF、Avalonia、WinUI 3 與 .NET MAUI Windows 範例。

本專案不是 mpv、yt-dlp、Deno 或其相關建置提供者的官方專案。`MediaEmbedKit` 是本專案名稱；`Mpv` 僅表示本專案與 mpv/libmpv 的整合目標。

## AI 產製聲明

本專案目前的原始碼、文件與範例主要由 AI 服務依使用者需求產生、整理與修改。採用本專案前，請自行完成下列審查：

- 程式碼正確性、例外處理、執行緒安全與資源釋放。
- 安全性、隱私、網路下載行為與供應鏈風險。
- 第三方元件授權、散發義務與目標環境相容性。
- 實際播放品質、UI 行為、效能與長時間穩定性。

AI 產製內容可能包含缺漏、錯誤假設或未涵蓋的邊界情境。本專案不應在未經完整驗證前直接用於生產環境。

## 支援範圍

目前產品支援範圍收斂為 Windows x64。

| 套件 | 狀態 |
| --- | --- |
| `MediaEmbedKit.Mpv` | 核心 API，支援 `netstandard2.0;net472;net48;net8.0;net10.0`。 |
| `MediaEmbedKit.Mpv.WinForms` | Windows HWND 控制項。 |
| `MediaEmbedKit.Mpv.Wpf` | `HwndHost` 控制項，內建 AirSpace 覆蓋層。 |
| `MediaEmbedKit.Mpv.Avalonia` | Windows x64 OpenGL render API 控制項。 |
| `MediaEmbedKit.Mpv.WinUI` | WinUI 3 Windows HWND 控制項。 |
| `MediaEmbedKit.Mpv.Maui` | .NET MAUI Windows handler。 |

未列入支援矩陣的平台、處理器架構與 UI 後端不提供支援承諾。詳細狀態請參閱 `docs/SUPPORT_MATRIX.md` 與 `docs/UI_BACKENDS.md`。

## 功能概要

- libmpv stable v0.41.0 公開 C API 包裝。
- 通用 command、property、node 與 event 入口。
- 常用高階播放 API：播放狀態、音量、速度、播放清單、章節、軌道、字幕、OSD、截圖、濾鏡、輸入事件、script message、薄型 fluent options API 與 mpv encoding mode 附帶輸出。
- OpenGL render API、software render API 與 stream callback 的核心包裝。
- Windows x64 runtime helper，可由使用者明確下載或更新 `libmpv-2.dll`、`yt-dlp.exe` 與 `deno.exe`。
- yt-dlp 格式預設值與自訂 selector。
- yt-dlp 與 Deno 外部處理序執行器，可接收 stdout/stderr 事件。
- WinForms、WPF、Avalonia、WinUI 3 與 MAUI Windows 範例。

## 基本使用

應用程式可自行部署 `libmpv-2.dll`，或明確呼叫 helper 建立 Windows x64 runtime 資料夾。

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

若要使用 mpv encoding mode 進行簡單輸出，可在初始化前套用 `MpvEncodingOptions`：

```csharp
MpvEncodingOptions encoding = MpvEncodingOptions.ToFile(outputPath)
    .AsMp4()
    .WithVideoCodec("libx264", "crf=23")
    .WithAudioCodec("aac");

MpvPlayerOptions options = new MpvPlayerOptions()
    .UseYtdlpFormat(MpvYtdlpFormatPreset.UpTo1080p)
    .UseEncoding(encoding);

using MpvPlayer player = new MpvPlayer(options);
player.Initialize();
player.LoadFile(inputPath);
```

encoding mode 屬於 mpv 的附帶能力。本專案只包裝 mpv 相關選項，不提供正式轉檔佇列、硬體編碼策略、批次重試或完整轉檔診斷。

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

整合測試與播放冒煙測試需要 Windows x64 原生執行階段。URL 播放需要 `yt-dlp.exe` 可被 mpv 找到，或透過 `MpvPlayerOptions.YtdlpPath` 指定。

發佈前可執行本機驗證腳本：

```powershell
.\tools\Invoke-PreReleaseValidation.ps1
```

此腳本會執行格式檢查、測試、建置、NuGet 套件內容驗證與乾淨 consumer 專案驗證。若要一併執行第一階段壓力測試，可加入 `-IncludeStressTests`。CI 工作流程尚未建立，需等待平台與 runner 策略確認。

GUI consumer 實際播放驗證會以本機 NuGet 套件建立臨時 consumer sample，並播放到指定秒數後關閉。長時間 GUI 播放壓力測試可使用：

```powershell
.\tools\Invoke-GuiPlaybackStress.ps1 -Seconds 120 -Iterations 2
```

## 文件

- `docs/PROJECT_SPEC.md`：專案規範入口。
- `docs/SUPPORT_MATRIX.md`：目標框架與支援狀態。
- `docs/UI_BACKENDS.md`：UI 後端與 AirSpace 限制。
- `docs/RUNTIME_ASSETS.md`：runtime 下載與更新政策。
- `docs/LIBMPV_C_API_TEST_MATRIX.md`：C API 覆蓋與驗證矩陣。
- `docs/RELEASE_CHECKLIST.md`：發佈前本機檢查。
- `docs/ENGINEERING_STANDARDS.md`：工程、文件、提交與格式規範。
- `docs/AI_AGENT_INTEGRATION.md`：AI agent 入口與 skill 結構。

## 授權與第三方元件

本儲存庫中的受控原始碼與文件採用 CC0-1.0。第三方原生執行階段二進位檔不簽入本儲存庫，也不因本專案授權而改變其授權條款。

散發 mpv/libmpv、yt-dlp、Deno 或其相依項目前，請自行確認授權與合規義務。詳細資訊請參閱 `THIRD_PARTY_NOTICES.md`。

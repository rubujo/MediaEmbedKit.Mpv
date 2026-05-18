# MediaEmbedKit.Mpv

[![作業系統](https://img.shields.io/badge/作業系統-Windows%20x64%20%2F%20ARM64-003A6D?style=for-the-badge)](https://learn.microsoft.com/zh-tw/windows/)
[![.NET Runtime](https://img.shields.io/badge/Runtime-.NET%2010%20%2F%20Framework%204.7.2%2B-512BD4?logo=dotnet&logoColor=white&style=for-the-badge)](https://dotnet.microsoft.com/zh-tw/download/dotnet/10.0)
[![程式語言](https://img.shields.io/badge/程式語言-C%23-1B5E20?style=for-the-badge)](https://learn.microsoft.com/zh-tw/dotnet/csharp/)
[![libmpv 基準](https://img.shields.io/badge/libmpv-stable%20v0.41.0-A84300?style=for-the-badge)](https://github.com/mpv-player/mpv)
[![UI 框架](https://img.shields.io/badge/UI%20框架-WinForms%20%2F%20WPF%20%2F%20Avalonia%20%2F%20WinUI%203%20%2F%20MAUI-107C10?style=for-the-badge)](docs/SUPPORT_MATRIX.md)
[![授權](https://img.shields.io/badge/授權-CC0%201.0%20Universal-424242?style=for-the-badge)](https://creativecommons.org/publicdomain/zero/1.0/deed.zh-hant)

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
| `MediaEmbedKit.Mpv` | 核心 API，支援 `netstandard2.0;net472;net48;net10.0`。 |
| `MediaEmbedKit.Mpv.WinForms` | Windows HWND 控制項。 |
| `MediaEmbedKit.Mpv.Wpf` | `HwndHost` 控制項，內建 AirSpace 覆蓋層。 |
| `MediaEmbedKit.Mpv.Avalonia` | Windows x64 / ARM64 OpenGL render API 控制項。 |
| `MediaEmbedKit.Mpv.WinUI` | WinUI 3 Windows HWND 控制項。 |
| `MediaEmbedKit.Mpv.Maui` | .NET MAUI Windows handler。 |

未列入支援矩陣的平台、處理器架構與 UI 後端不提供支援承諾。詳細狀態請參閱 `docs/SUPPORT_MATRIX.md` 與 `docs/UI_BACKENDS.md`。

## 功能概要

- libmpv stable v0.41.0 公開 C API 完整包裝（含命令、屬性、節點、事件、render API、stream callback）。
- 高階播放 API：fluent `MpvAppBuilder`、per-file `MpvMediaItem`、`LoadAsync`、`WatchProperty<T>`、`IAsyncDisposable`、`Microsoft.Extensions.Logging` / `Microsoft.Extensions.DependencyInjection` 整合。詳見 `docs/HIGH_LEVEL_API.md`。
- 高階 encoding API：單／兩階段轉碼、stream-copy 重新封裝、單軌抽取、影格抽圖、多檔 EDL 串接、多段切割；含 `IProgress<MpvEncodingProgress>` 進度與 `CancellationToken` 取消。詳見 `docs/HIGH_LEVEL_API.md`「Encoding」段。
- 5 個 UI 框架控制項（WinForms / WPF / Avalonia / WinUI 3 / MAUI Windows）共通綁定屬性與 MVVM Commands。詳見 `docs/CONTROLS_API.md`。
- Windows x64 / ARM64 runtime helper：libmpv / yt-dlp / Deno / FFmpeg / ffprobe 下載、更新、健康檢查、授權稽核與 provider fallback；架構依目前處理序自動偵測。詳見 `docs/RUNTIME_ASSETS.md`。

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

若要讓 runtime 資料夾同時作為 mpv 設定資料夾，於 `CreatePlayerOptions` 傳入 `loadRuntimeConfiguration: true`。

Fluent builder、`MpvMediaItem` per-file 選項、`MpvEncoder` 轉碼、`WatchProperty<T>` 等高階入口請參考 `docs/HIGH_LEVEL_API.md`。

高階 API 採薄型 helper 設計：常用設定可用 fluent 方式組合，但播放器初始化、runtime 下載與資源釋放仍由應用程式明確控制。

### Runtime 授權預設值

`InstallOrUpdateAsync` 的預設組合往「對不確定散發授權的多數使用者較安全」方向收緊：

- **libmpv = LGPL build**（`Provider = Zhongfly` + `LicensePreference = PreferLgpl`，搭配 `Shinchiro` 作為 fallback）。`PreferLgpl` 是偏好不是保證 —— 切到 `Provider = Shinchiro` 會 silently fallback 到 GPL（該 provider 不發 LGPL 變體）。
- **FFmpeg 預設不下載**（`IncludeFFmpeg = false`）。yt-dlp/FFmpeg-Builds 僅發 GPL，啟用即視同接受 GPLv2+ 散發義務。

詳細真值表、警示與商用嚴格合規路徑見 [`docs/RUNTIME_ASSETS.md`](docs/RUNTIME_ASSETS.md)。散發前可用 `MpvLicenseAuditor.AnalyzeAsync(runtimeDirectory)` 在執行階段驗證實際拿到的授權。

## 取得套件

本專案不發行至 nuget.org。發行版的 6 個 `.nupkg` 與對應 `.snupkg`（symbol package）由 GitHub Releases 提供，consumer 自行下載後以本機 NuGet feed 安裝。完整步驟（含 `nuget.config` 設定、`packageSourceMapping`、SourceLink 行為、IDE 整合）請參考 `docs/CONSUMING_PACKAGES.md`。

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

整合測試、播放冒煙測試需要 Windows 原生執行階段；環境條件詳見 `docs/RUNTIME_ASSETS.md`。

發佈前驗證以 `docs/RELEASE_CHECKLIST.md` 與 `tools/Invoke-PreReleaseValidation.ps1` 為主流程。GitHub Actions 的 `ci.yml` / `release.yml` workflow 跑同一個 release gate 腳本（GitHub-hosted runner 無 display 略過 GUI playback）。

長時間穩定性以 `tests/MediaEmbedKit.Mpv.SoakTests` 提供的 24 小時連續播放 soak harness 驗證；不在 release gate 內，視需要手動執行。

## 文件

- `docs/PROJECT_SPEC.md`：專案規範入口。
- `docs/SUPPORT_MATRIX.md`：目標框架與支援狀態。
- `docs/UI_BACKENDS.md`：UI 後端與 AirSpace 限制。
- `docs/RUNTIME_ASSETS.md`：runtime 下載與更新政策。
- `docs/CONSUMING_PACKAGES.md`：從 GitHub Release 下載 `.nupkg` 後以本機 NuGet feed 安裝。
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

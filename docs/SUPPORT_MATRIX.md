# 支援矩陣

本專案目前僅列入已完成基本建置、runtime 來源與範例驗證的 Windows x64 / ARM64 目標。未列入本文件者不提供支援承諾。

## 平台支援判準

一個平台會被列入下方支援矩陣，前提是同時滿足三項條件：

1. **C# UI framework 可在該平台執行**：至少有一個本專案目前支援的 UI 套件（WinForms / WPF / Avalonia / WinUI 3 / .NET MAUI）在該平台可實際執行。
2. **libmpv 有可程式化取得的 runtime**：該平台有公開、可散佈的 libmpv 二進位資產，且至少滿足以下其一：
   - 有 GitHub Release 提供穩定命名的二進位資產（本專案 runtime helper 直接下載與驗證）；或
   - 有作業系統官方套件管理器套件，使用者預期會自行安裝（本專案不提供 runtime helper，僅以 `DllImport` 名稱解析機制接上）。
3. **mpv 上游聲明支援該平台**：mpv 自家 [`mpv.io/installation`](https://mpv.io/installation/) 與 `mpv-player/mpv` 文件對該平台有正式記載。

## 目標框架

| 套件 | 目標框架 | 狀態 |
| --- | --- | --- |
| `MediaEmbedKit.Mpv` | `netstandard2.0;net472;net48;net10.0` | 支援 |
| `MediaEmbedKit.Mpv.WinForms` | `net472;net48;net10.0-windows` | 支援 |
| `MediaEmbedKit.Mpv.Wpf` | `net472;net48;net10.0-windows` | 支援 |
| `MediaEmbedKit.Mpv.Avalonia` | `net10.0-windows` | 支援 |
| `MediaEmbedKit.Mpv.WinUI` | `net10.0-windows10.0.19041.0` | 支援 |
| `MediaEmbedKit.Mpv.Maui.Windows` | `net10.0-windows10.0.19041.0` | 支援（Windows-only；套件名自帶 `.Windows` 明示 scope） |

`netstandard2.0` 用於共用核心 API；核心套件僅多 target `net10.0`，UI 套件僅多 target `net10.0-windows`（搭配 .NET Framework 4.7.2 / 4.8）。`net8.0` / `net8.0-windows` 已不在 multi-target 矩陣（核心於 commit 668d367 移除、UI 於後續清理一併移除以縮小發行矩陣）；要在 .NET 8 環境使用 UI 套件，可改 target `net10.0-windows` 或回退到 `net472` / `net48`。基於 Microsoft 對 .NET Framework 使用 .NET Standard 2.0 的建議，本專案不支援 .NET Framework 4.0、4.5 或 4.6.1。

## UI 與作業系統

| 平台 | UI 框架 | 狀態 | 原生程式庫 |
| --- | --- | --- | --- |
| Windows x64 | WinForms / WPF / Avalonia / WinUI 3 / .NET MAUI Windows | 支援 | `libmpv-2.dll`（`mpv-dev-x86_64-*.7z`） |
| Windows ARM64 | WinForms / WPF / Avalonia / WinUI 3 / .NET MAUI Windows | 支援（程式碼路徑就緒，待物理機驗證） | `libmpv-2.dll`（`mpv-dev-aarch64-*.7z`） |

### Windows ARM64 注意事項

- `.NET 10` 在 ARM64 為原生 first-class 支援。
- `net472` / `net48` 沒有原生 ARM64，Windows on ARM 上會走 x64 emulation；本專案不額外承諾 emulation 路徑下的效能或相容性。
- 硬體編碼器（NVENC / Quick Sync / AMF）在主流 ARM64 Windows 裝置（Snapdragon X 等）上預期全部 unavailable；`MpvEncoder` 硬體 encoder preset probe 會回報 `unavailable`，使用者請走軟體 preset。Qualcomm Adreno 編解碼目前不在本專案 preset 內。

### Runtime 資產來源（兩個架構皆有兩個 provider 備援）

| 元件 | x64 資產 | ARM64 資產 |
| --- | --- | --- |
| libmpv | `mpv-dev-x86_64-*.7z` | `mpv-dev-aarch64-*.7z` |
| FFmpeg | `ffmpeg-master-latest-win64-gpl.zip` | `ffmpeg-master-latest-winarm64-gpl.zip` |
| yt-dlp | `yt-dlp.exe`（自 2026-03-17 起亦可走 ARM64 命名） | `yt-dlp_arm64.exe`（自 2026-03-17 起）|
| Deno | `deno-x86_64-pc-windows-msvc.zip` | `deno-aarch64-pc-windows-msvc.zip`（自 Deno 2.7、2026-02 起）|

libmpv 兩個架構皆由 [shinchiro/mpv-winbuild-cmake](https://github.com/shinchiro/mpv-winbuild-cmake/releases) 與 [zhongfly/mpv-winbuild](https://github.com/zhongfly/mpv-winbuild/releases) 提供，命名規範完全相同；既有 `ProviderFallbackOrder` 機制在兩個架構上行為一致。**預設 `Provider = Zhongfly` + `ProviderFallbackOrder = [Shinchiro]`** —— zhongfly 是兩家中唯一同時提供 LGPL 與 GPL libmpv build 的來源；shinchiro 只發 GPL。詳見 [`RUNTIME_ASSETS.md`](RUNTIME_ASSETS.md#libmpv-授權版本選擇gpl-vs-lgpl)。

## 驗證狀態

Windows 發佈前驗證以本機 release gate 為準：

```powershell
.\tools\Invoke-PreReleaseValidation.ps1
.\tools\Invoke-PreReleaseValidation.ps1 -IncludeWindowsReleaseGate -GuiPlaybackSeconds 20
```

核心 API 測試、原生整合測試、NuGet 套件內容、乾淨 consumer 建置、Console minimal 播放與 GUI consumer 播放都應在發佈前通過。WinForms、WPF、Avalonia、WinUI 3 與 MAUI Windows 範例需以 YouTube 測試網址播放到指定秒數後正常關閉。

### 實機驗證範圍

| 維度 | 已驗證 | 未驗證 |
| --- | --- | --- |
| OS 版本 | Windows 11 Pro for Workstations 10.0.26200 | Windows 10、其他 Windows 11 build、Windows Server |
| 架構 | x64（開發機） | ARM64（程式碼路徑完整、資產對應有單元測試覆蓋，但未實機跑過） |
| 長時穩定性 | 24 小時連續播放 soak 1916 iterations（wav / mp4 / cancel 三路）通過、0 leak 信號（[`tests/MediaEmbedKit.Mpv.SoakTests`](../tests/MediaEmbedKit.Mpv.SoakTests)） | — |

未在表中的 OS 版本與架構**不在實機驗證範圍**。設計目標仍是支援 csproj 內 `TargetPlatformMinVersion` 宣告的所有版本（WinUI / MAUI 為 `10.0.17763.0`、WPF / WinForms 為 `.NET Framework 4.7.2+` 或 `.NET 10`），但實際相容性需由使用者在目標環境自行驗證。

### libmpv header drift 檢查

定期執行 [`tools/libmpv/Check-LibMpvHeaderDrift.ps1`](../tools/libmpv/Check-LibMpvHeaderDrift.ps1)（建議每月或每季）追蹤 shinchiro / zhongfly 的 mpv git build 是否引入新的 public header 變更。偵測到變更時須評估是否更新 [`src/MediaEmbedKit.Mpv/Native/MpvNative.cs`](../src/MediaEmbedKit.Mpv/Native/MpvNative.cs) 與 [`docs/runtime/libmpv-git-builds.json`](runtime/libmpv-git-builds.json)。

Windows runtime helper 目前支援 `libmpv-2.dll`、`yt-dlp.exe`、`deno.exe`、`ffmpeg.exe` 與 `ffprobe.exe` 的同層配置，並依目前處理序架構自動選擇對應的 x64 / ARM64 資產。FFmpeg 來源限定為 yt-dlp `FFmpeg-Builds` Windows GPL build；NuGet 套件不包含任何第三方 runtime 二進位檔。

# 支援矩陣

本專案目前僅列入已完成基本建置、runtime 來源與範例驗證的 Windows x64 / ARM64 目標。未列入本文件者不提供支援承諾。

## 平台支援判準

一個平台會被列入下方支援矩陣，前提是同時滿足三項條件：

1. **C# UI framework 可在該平台執行**：至少有一個本專案目前支援的 UI 套件（WinForms / WPF / Avalonia / WinUI 3 / .NET MAUI）在該平台可實際執行。
2. **libmpv 有可程式化取得的 runtime**：該平台有公開、可散佈的 libmpv 二進位資產，且至少滿足以下其一：
   - 有 GitHub Release 提供穩定命名的二進位資產（本專案 runtime helper 直接下載與驗證）；或
   - 有作業系統官方套件管理器套件，使用者預期會自行安裝（本專案不提供 runtime helper，僅以 `DllImport` 名稱解析機制接上）。
3. **mpv 上游聲明支援該平台**：mpv 自家 [`mpv.io/installation`](https://mpv.io/installation/) 與 `mpv-player/mpv` 文件對該平台有正式記載。

不同時滿足上述三項的平台不列入 roadmap。已知不滿足的例子：

| 平台 | 不滿足的條件 | 說明 |
| --- | --- | --- |
| iOS / iPadOS | (2) | 雖有 `media-kit/libmpv-darwin-build` 等社群 build，但 App Store 散佈受 GPL 限制 |
| Tizen | (2)(3) | 無 first-party libmpv runtime |
| Browser / WebAssembly | (2)(3) | libmpv 無法在瀏覽器執行 |

「runtime helper 不適用」的特殊案例（雖可程式化取得但採不同支援哲學）：

- **Linux**：libmpv 透過 distro 套件管理器（`apt` / `dnf` / `pacman`）安裝；本專案不提供 runtime helper，而是要求使用者已安裝 `libmpv1` / `libmpv-dev`（或對等套件），程式以 `DllImport("libmpv-2")` + `NativeLibrary` 名稱解析接上。
- **Android**：[`mpv-android/mpv-android`](https://github.com/mpv-android/mpv-android/releases) 把 libmpv `.so` 打包在 APK 中而非獨立散佈；要支援需要重新設計 packaging 模型。

## 目標框架

| 套件 | 目標框架 | 狀態 |
| --- | --- | --- |
| `MediaEmbedKit.Mpv` | `netstandard2.0;net472;net48;net8.0;net10.0` | 支援 |
| `MediaEmbedKit.Mpv.WinForms` | `net472;net48;net8.0-windows;net10.0-windows` | 支援 |
| `MediaEmbedKit.Mpv.Wpf` | `net472;net48;net8.0-windows;net10.0-windows` | 支援 |
| `MediaEmbedKit.Mpv.Avalonia` | `net8.0-windows;net10.0-windows` | 支援 |
| `MediaEmbedKit.Mpv.WinUI` | `net10.0-windows10.0.19041.0` | 支援 |
| `MediaEmbedKit.Mpv.Maui` | `net10.0-windows10.0.19041.0` | 支援 |

`netstandard2.0` 用於共用核心 API。基於 Microsoft 對 .NET Framework 使用 .NET Standard 2.0 的建議，本專案不支援 .NET Framework 4.0、4.5 或 4.6.1。

## UI 與作業系統

| 平台 | UI 框架 | 狀態 | 原生程式庫 |
| --- | --- | --- | --- |
| Windows x64 | WinForms / WPF / Avalonia / WinUI 3 / .NET MAUI Windows | 支援 | `libmpv-2.dll`（`mpv-dev-x86_64-*.7z`） |
| Windows ARM64 | WinForms / WPF / Avalonia / WinUI 3 / .NET MAUI Windows | 支援（程式碼路徑就緒，待物理機驗證） | `libmpv-2.dll`（`mpv-dev-aarch64-*.7z`） |

### Windows ARM64 注意事項

- `.NET 8 / .NET 10` 在 ARM64 為原生 first-class 支援。
- `net472` / `net48` 沒有原生 ARM64，Windows on ARM 上會走 x64 emulation；本專案不額外承諾 emulation 路徑下的效能或相容性。
- 硬體編碼器（NVENC / Quick Sync / AMF）在主流 ARM64 Windows 裝置（Snapdragon X 等）上預期全部 unavailable；`MpvEncoder` 硬體 encoder preset probe 會回報 `unavailable`，使用者請走軟體 preset。Qualcomm Adreno 編解碼目前不在本專案 preset 內。

### Runtime 資產來源（兩個架構皆有兩個 provider 備援）

| 元件 | x64 資產 | ARM64 資產 |
| --- | --- | --- |
| libmpv | `mpv-dev-x86_64-*.7z` | `mpv-dev-aarch64-*.7z` |
| FFmpeg | `ffmpeg-master-latest-win64-gpl.zip` | `ffmpeg-master-latest-winarm64-gpl.zip` |
| yt-dlp | `yt-dlp.exe`（自 2026-03-17 起亦可走 ARM64 命名） | `yt-dlp_arm64.exe`（自 2026-03-17 起）|
| Deno | `deno-x86_64-pc-windows-msvc.zip` | `deno-aarch64-pc-windows-msvc.zip`（自 Deno 2.7、2026-02 起）|

libmpv 兩個架構皆由 [shinchiro/mpv-winbuild-cmake](https://github.com/shinchiro/mpv-winbuild-cmake/releases) 與 [zhongfly/mpv-winbuild](https://github.com/zhongfly/mpv-winbuild/releases) 提供，命名規範完全相同；既有 `ProviderFallbackOrder` 機制在兩個架構上行為一致。

## 驗證狀態

Windows x64 發佈前驗證以本機 release gate 為準：

```powershell
.\tools\Invoke-PreReleaseValidation.ps1
.\tools\Invoke-PreReleaseValidation.ps1 -IncludeWindowsReleaseGate -GuiPlaybackSeconds 20
```

核心 API 測試、原生整合測試、NuGet 套件內容、乾淨 consumer 建置、Console minimal 播放與 GUI consumer 播放都應在發佈前通過。WinForms、WPF、Avalonia、WinUI 3 與 MAUI Windows 範例需以 YouTube 測試網址播放到指定秒數後正常關閉。

**ARM64 物理機驗證尚未納入本機 release gate**：目前 ARM64 路徑為程式碼層級就緒（架構偵測、資產對應、catalog 與單元測試完整），但未在 ARM64 物理機跑過 release gate。雙 provider（shinchiro / zhongfly）對 ARM64 都提供 `mpv-dev-aarch64-*.7z`，命名規範與 x64 一致；風險點為硬體 encoder probe 預期回報 unavailable 與少數 `MpvEncoder` 硬體 preset 整合測試會 skip。

Windows runtime helper 目前支援 `libmpv-2.dll`、`yt-dlp.exe`、`deno.exe`、`ffmpeg.exe` 與 `ffprobe.exe` 的同層配置，並依目前處理序架構自動選擇對應的 x64 / ARM64 資產。FFmpeg 來源限定為 yt-dlp `FFmpeg-Builds` Windows GPL build；NuGet 套件不包含任何第三方 runtime 二進位檔。

# 支援矩陣

本專案目前僅列入已完成基本建置、runtime 來源與範例驗證的 Windows x64 目標。未列入本文件者不提供支援承諾。

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
| Windows x64 | WinForms | 支援 | `libmpv-2.dll` |
| Windows x64 | WPF | 支援 | `libmpv-2.dll` |
| Windows x64 | Avalonia | 支援 | `libmpv-2.dll` |
| Windows x64 | WinUI 3 | 支援 | `libmpv-2.dll` |
| Windows x64 | .NET MAUI Windows | 支援 | `libmpv-2.dll` |

## 驗證狀態

Windows x64 發佈前驗證以本機 release gate 為準：

```powershell
.\tools\Invoke-PreReleaseValidation.ps1
.\tools\Invoke-PreReleaseValidation.ps1 -IncludeWindowsReleaseGate -GuiPlaybackSeconds 20
```

核心 API 測試、原生整合測試、NuGet 套件內容、乾淨 consumer 建置、Console minimal 播放與 GUI consumer 播放都應在發佈前通過。WinForms、WPF、Avalonia、WinUI 3 與 MAUI Windows 範例需以 YouTube 測試網址播放到指定秒數後正常關閉。

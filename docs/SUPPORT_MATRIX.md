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

最近一次完整驗證包含：

```powershell
dotnet run --project .\tests\MediaEmbedKit.Mpv.Tests\MediaEmbedKit.Mpv.Tests.csproj
dotnet run --project .\tests\MediaEmbedKit.Mpv.IntegrationTests\MediaEmbedKit.Mpv.IntegrationTests.csproj
dotnet build .\MediaEmbedKit.Mpv.slnx
dotnet run --project .\tests\MediaEmbedKit.Mpv.PlaybackSmoke\MediaEmbedKit.Mpv.PlaybackSmoke.csproj -- --seconds 20
```

核心 API 測試與原生整合測試通過。WinForms、WPF、Avalonia、WinUI 3 與 MAUI Windows 範例已以 YouTube 測試網址播放超過 20 秒並正常關閉。

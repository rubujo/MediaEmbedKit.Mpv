# 支援矩陣

## 目標框架

| 套件 | 目標框架 | 狀態 |
| --- | --- | --- |
| `MediaEmbedKit.Mpv` | `netstandard2.0;net472;net48;net8.0;net10.0` | 已建置通過 |
| `MediaEmbedKit.Mpv.WinForms` | `net472;net48;net8.0-windows;net10.0-windows` | 已建置通過 |
| `MediaEmbedKit.Mpv.Wpf` | `net472;net48;net8.0-windows;net10.0-windows` | 已建置通過 |
| `MediaEmbedKit.Mpv.Avalonia` | `net8.0-windows;net10.0-windows` | OpenGL render API 預覽，未列入 HWND-only 完成範圍 |
| `MediaEmbedKit.Mpv.WinUI` | `net10.0-windows10.0.19041.0` | HWND 預覽 |
| `MediaEmbedKit.Mpv.Maui` | `net10.0-windows10.0.19041.0` | Windows HWND 預覽 |

`netstandard2.0` 用於在 .NET Framework 與現代 .NET 之間共用核心 API。Microsoft 文件說明，若要同時支援 .NET Framework 與其他 .NET 實作，程式庫應以 .NET Standard 2.0 作為共用目標；.NET Framework 使用 .NET Standard 2.0 時建議採用 4.7.2 或更新版本。因此本專案不支援 .NET Framework 4.0、4.5 或 4.6.1。

`.NET 10` 是目前的主要 LTS 目標；`.NET 8` 保留給仍在 LTS 週期內的使用者。`.NET 9` 是 STS，不作為本專案必要目標。

## 作業系統與 UI 狀態

| 平台 | UI 框架 | 狀態 | 原生 libmpv 形式 |
| --- | --- | --- | --- |
| Windows x64 | WinForms | 支援 | `libmpv-2.dll` |
| Windows x64 | WPF | 支援 | `libmpv-2.dll` |
| Windows x64 | Avalonia | OpenGL render API 預覽，未列入 HWND-only 完成範圍 | `libmpv-2.dll` |
| Windows x64 | WinUI 3 | HWND 預覽 | `libmpv-2.dll` |
| Windows x64 | .NET MAUI Windows | HWND 預覽 | `libmpv-2.dll` |

目前支援範圍先收斂為 Windows x64。未符合支援範圍收斂準則的目標不列入目前支援矩陣。

## 收斂規則

目前支援矩陣只列入 Windows x64；未符合支援範圍收斂準則的目標不在本文件保留來源清單、狀態列或預告式支援宣告。

## 驗證狀態

最近一次完整建置：

```powershell
dotnet build .\MediaEmbedKit.Mpv.slnx --no-restore
```

結果：成功，0 warning，0 error。

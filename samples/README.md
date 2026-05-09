# 範例總覽

此資料夾包含 MediaEmbedKit.Mpv 的 Windows x64 桌面範例。所有範例都假設應用程式可以找到對應的 libmpv 原生程式庫；URL 播放情境也需要 `yt-dlp.exe` 可由 mpv 找到。

## 範例專案

- `WinFormsSample`：示範 `MpvPlayerControl` 的 HWND 嵌入方式。
- `WpfSample`：示範內建 `OverlayContent` AirSpace 覆蓋層的 `MpvWpfPlayer`。
- `AvaloniaSample`：示範 `MpvAvaloniaPlayer` 的 OpenGL render API 預覽方式。
- `WinUISample`：示範 WinUI 3 Windows HWND 控制項。
- `MauiSample`：示範 MAUI Windows 高效能優先預覽 handler。

## 執行前準備

WinForms、WPF、Avalonia、WinUI 3 與 MAUI 範例會在啟動時呼叫共用 `SampleRuntime.InstallOrUpdateAsync()`，再由該 helper 委派到 `MpvRuntimeInstaller.InstallOrUpdateAsync(...)`。目前範例支援範圍收斂為 Windows x64，會自動建立輸出資料夾下的 `runtime` 目錄，並將 `libmpv-2.dll`、`yt-dlp.exe` 與 `deno.exe` 放在同一層。下載的二進位檔不應簽入儲存庫。

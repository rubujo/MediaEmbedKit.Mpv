# 範例總覽

本資料夾包含 MediaEmbedKit.Mpv 的 Windows 範例（x64 / ARM64，runtime helper 依目前處理序架構自動選擇對應資產）。範例用於示範 runtime 準備、播放器初始化、事件輸出、播放控制、AirSpace 行為與資源釋放。

## 專案

| 範例 | 內容 |
| --- | --- |
| `WinFormsSample` | WinForms HWND 控制項。 |
| `WpfSample` | WPF `HwndHost` 控制項與內建 AirSpace 覆蓋層。 |
| `AvaloniaSample` | Avalonia OpenGL render API 控制項。 |
| `WinUISample` | WinUI 3 Windows HWND 控制項。 |
| `MauiSample` | .NET MAUI Windows handler。 |
| `ConsoleMinimalSample` | 核心 `MpvPlayer` 最小生命週期。 |

## 共同行為

- GUI 範例會先顯示視窗，再於背景呼叫 `SampleRuntime.InstallOrUpdateAsync()` 準備 `libmpv-2.dll`、`yt-dlp.exe`、`deno.exe`、`ffmpeg.exe` 與 `ffprobe.exe`。
- Console 範例明確呼叫 runtime helper 後建立 `MpvPlayer`，用於展示核心 API 最小生命週期。
- 範例為了展示 yt-dlp 後處理、FFmpeg / FFprobe 診斷與完整 runtime，會明確啟用 yt-dlp/FFmpeg-Builds GPL binaries；散發應用程式前請依 [`docs/RUNTIME_ASSETS.md`](../docs/RUNTIME_ASSETS.md) 確認授權義務。
- 預設播放 YouTube 測試網址，也可輸入本機檔案或媒體網址。
- 事件清單顯示 libmpv 事件、記錄訊息、屬性變更、ytdl hook 結果與範例生命週期。
- 關閉視窗或頁面離開時釋放事件橋接器與播放器資源。
- 功能列示範 yt-dlp 格式、OSD、跳轉、音量、靜音、速度、字幕、軌道、截圖、設定檔與 Lua script。
- yt-dlp 與 Deno 診斷會透過程式庫的外部處理序執行器接收 stdout/stderr；FFmpeg 與 FFprobe 由 runtime helper 作為 yt-dlp 附帶工具準備。

## 播放冒煙測試

```powershell
dotnet run --project .\tests\MediaEmbedKit.Mpv.PlaybackSmoke\MediaEmbedKit.Mpv.PlaybackSmoke.csproj -- --seconds 20
```

可用 `--sample WinForms`、`--sample WPF`、`--sample Avalonia`、`--sample WinUI` 或 `--sample MAUI` 執行單一範例。

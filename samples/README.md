# 範例總覽

本資料夾包含 MediaEmbedKit.Mpv 的 Windows x64 範例。範例用於示範 runtime 準備、播放器初始化、事件輸出、播放控制、AirSpace 行為與資源釋放。

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

- GUI 範例會先顯示視窗，再於背景呼叫 `SampleRuntime.InstallOrUpdateAsync()` 準備 `libmpv-2.dll`、`yt-dlp.exe` 與 `deno.exe`。
- Console 範例明確呼叫 `SampleRuntime.InstallOrUpdateAsync()` 後建立 `MpvPlayer`。
- 預設播放 YouTube 測試網址，也可輸入本機檔案或媒體網址。
- 事件清單顯示 libmpv 事件、記錄訊息、屬性變更、ytdl hook 結果與範例生命週期。
- 關閉視窗或頁面離開時釋放事件橋接器與播放器資源。
- 功能列示範 yt-dlp 格式、OSD、跳轉、音量、靜音、速度、字幕、軌道、截圖、設定檔與 Lua script。
- yt-dlp 與 Deno 診斷會透過程式庫的外部處理序執行器接收 stdout/stderr。

## 播放冒煙測試

```powershell
dotnet run --project .\tests\MediaEmbedKit.Mpv.PlaybackSmoke\MediaEmbedKit.Mpv.PlaybackSmoke.csproj -- --seconds 20
```

可用 `--sample WinForms`、`--sample WPF`、`--sample Avalonia`、`--sample WinUI` 或 `--sample MAUI` 執行單一範例。

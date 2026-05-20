# WPF 範例

本範例示範 `MpvWpfPlayer` 在 WPF 中的 `HwndHost` 嵌入與 AirSpace 覆蓋層處理。

## 示範內容

- 啟動時準備 Windows 執行階段資料夾。
- 使用控制項內建 `OverlayContent` 顯示影片上方 UI。
- 透過 `SamplePlayerEventBridge` 輸出 libmpv 事件、記錄訊息與屬性變更。
- 示範播放控制、字幕、截圖、OSD、yt-dlp 格式、yt-dlp/Deno 診斷與自我更新命令。

## 執行

```powershell
dotnet run --project .\samples\WpfSample\MediaEmbedKit.Mpv.Samples.Wpf.csproj
```

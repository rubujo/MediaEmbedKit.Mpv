# WinUI 3 範例

本範例示範 `MpvWinUiPlayer` 的 Windows HWND 後端。影片上方 UI 由控制項內建覆蓋層處理。

## 示範內容

- 啟動時準備 Windows x64 runtime 資料夾。
- 使用 HWND 後端播放。
- 同時展示控制項內建 `OverlayContent` 與一般 WinUI 覆蓋層。
- 透過 `SamplePlayerEventBridge` 輸出 libmpv 事件、記錄訊息與屬性變更。
- 示範播放控制、字幕、截圖、OSD、yt-dlp 格式、yt-dlp/Deno 診斷與自我更新命令。
- 範例 app 使用 Windows App SDK self-contained 部署與 `win-x64` RID。

## 執行

```powershell
dotnet run --project .\samples\WinUISample\MediaEmbedKit.Mpv.Samples.WinUI.csproj
```

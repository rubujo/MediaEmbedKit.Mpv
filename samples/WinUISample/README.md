# WinUI 3 範例

本範例示範 `MpvWinUiPlayer` 的 Windows HWND 後端。影片上方 UI 由控制項內建覆蓋層處理。

## 示範內容

- 啟動時準備 Windows runtime 資料夾。
- 使用 HWND 後端播放。
- 使用控制項內建 `OverlayContent` 顯示影片上方 UI。
- 透過 `SamplePlayerEventBridge` 輸出 libmpv 事件、記錄訊息與屬性變更。
- 示範播放控制、字幕、截圖、OSD、yt-dlp 格式、yt-dlp/Deno 診斷與自我更新命令。
- 範例 app 使用 Windows App SDK self-contained 部署；csproj 列入 `win-x64` 與 `win-arm64` RID，預設以目前處理序架構選擇 publish target，可透過 `dotnet publish -r win-arm64` 或環境變數覆寫。

## 執行

```powershell
dotnet run --project .\samples\WinUISample\MediaEmbedKit.Mpv.Samples.WinUI.csproj
```

# .NET MAUI 範例

本範例示範 MAUI Windows handler `MpvView`。Windows handler 以 WinUI 3 `MpvWinUiPlayer` 作為平台控制項。

## 示範內容

- 透過 `UseMediaEmbedKitMpv()` 註冊 MAUI handler。
- 啟動時準備 Windows x64 runtime 資料夾。
- 使用 Windows HWND 後端播放。
- 透過 `SamplePlayerEventBridge` 輸出 libmpv 事件、記錄訊息與屬性變更。
- 示範播放控制、字幕、截圖、OSD、yt-dlp 格式、yt-dlp/Deno 診斷與自我更新命令。
- 範例 app 使用 Windows App SDK self-contained 部署與 `win-x64` RID。

## 執行

```powershell
dotnet run --project .\samples\MauiSample\MediaEmbedKit.Mpv.Samples.Maui.csproj
```

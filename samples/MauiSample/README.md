# .NET MAUI 範例

本範例示範 MAUI Windows handler `MpvView`。Windows handler 以 WinUI 3 `MpvWinUiPlayer` 作為平台控制項。

## 示範內容

- 透過 `UseMediaEmbedKitMpv()` 註冊 MAUI handler。
- 視窗先顯示，再於背景準備 Windows 執行階段資料夾。
- 使用 Windows HWND 後端播放。
- 優先使用 `OverlayView` 示範 MAUI 覆蓋層；handler 會負責轉換與清理對應的平台檢視。
- `OverlayContent` 僅保留給需要直接提供 WinUI 元素的 Windows 原生情境，且會優先於 `OverlayView`。
- 透過 `SamplePlayerEventBridge` 輸出 libmpv 事件、記錄訊息與屬性變更。
- 示範播放控制、字幕、截圖、OSD、yt-dlp 格式、yt-dlp/Deno 診斷與自我更新命令。
- 範例 app 使用 Windows App SDK self-contained 部署；csproj 列入 `win-x64` 與 `win-arm64` RID，預設以目前處理序架構選擇 publish target，可透過 `dotnet publish -r win-arm64` 或環境變數覆寫。

## 執行

```powershell
dotnet run --project .\samples\MauiSample\MediaEmbedKit.Mpv.Samples.Maui.csproj
```

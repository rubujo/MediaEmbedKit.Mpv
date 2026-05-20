# WinForms 範例

本範例示範 `MpvPlayerControl` 在 Windows Forms 中的 HWND 嵌入流程。

## 示範內容

- 啟動時準備 Windows 執行階段資料夾。
- 建立控制項 Handle 後初始化 libmpv player。
- 透過 `SamplePlayerEventBridge` 輸出 libmpv 事件、記錄訊息與屬性變更。
- 示範播放、暫停、停止、音量、速度、字幕、截圖、OSD 與 yt-dlp 格式控制。
- 示範 yt-dlp 與 Deno 診斷、自我更新命令與輸出事件。

## 執行

```powershell
dotnet run --project .\samples\WinFormsSample\MediaEmbedKit.Mpv.Samples.WinForms.csproj
```

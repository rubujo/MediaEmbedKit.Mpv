# Avalonia 範例

本範例示範 Windows Avalonia 控制項 `MpvAvaloniaPlayer`（x64 / ARM64，執行階段輔助工具依目前處理序架構自動選擇對應資產）。此控制項使用 libmpv OpenGL render API，不是 HWND `wid` 後端。

## 示範內容

- 視窗先顯示，再於背景準備 Windows 執行階段資料夾。
- 執行階段初始化失敗時會顯示錯誤對話方塊；冒煙測試模式則寫入失敗結果後關閉。
- 透過 Avalonia `OpenGlControlBase` 管理 OpenGL 內容與 render 生命週期。
- 顯示 Avalonia OpenGL render API 與一般 Avalonia 覆蓋層的同層組合行為。
- 透過 `SamplePlayerEventBridge` 輸出 libmpv 事件、記錄訊息與屬性變更。
- 示範播放控制、字幕、截圖、OSD、yt-dlp 格式、yt-dlp/Deno 診斷與自我更新命令。

## 執行

```powershell
dotnet run --project .\samples\AvaloniaSample\MediaEmbedKit.Mpv.Samples.Avalonia.csproj
```

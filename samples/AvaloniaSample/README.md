# Avalonia 範例

此範例示範 Windows x64 Avalonia 預覽控制項 `MpvAvaloniaPlayer`。此控制項使用 libmpv OpenGL render API，並由 Avalonia `OpenGlControlBase` 管理 OpenGL 內容與轉譯生命週期；它不是 HWND `wid` 嵌入後端。

## 行為

- 範例啟動時會呼叫 `SampleRuntime.InstallOrUpdateAsync()`，下載或更新同層 `runtime` 資料夾中的 `libmpv-2.dll`、`yt-dlp.exe` 與 `deno.exe`。
- 視窗開啟後會自動載入預設 YouTube 範例網址，也可輸入檔案路徑或媒體網址後按下 `Load` 載入播放項目。
- 播放區域示範一般 Avalonia 覆蓋層與 OpenGL render API 預覽控制項的組合方式。
- 下方事件清單會顯示 libmpv 事件、記錄訊息、屬性變更、mpv ytdl hook 的 JSON 子程序結果與範例生命週期。
- 視窗關閉時會釋放 `SamplePlayerEventBridge`，示範取消事件訂閱與屬性觀察。
- 此範例目前只標示為 Windows x64 預覽；不列入 HWND-only 完成範圍。
- 功能列示範 yt-dlp 格式預設值切換、OSD、相對跳轉、音量、靜音、播放速度、外部字幕、播放軌、截圖、載入設定檔與載入 Lua 指令碼。
- `yt-dlp`、`Deno`、`Update yt` 與 `Update Deno` 按鈕會呼叫共用 helper 執行診斷或自我更新命令；診斷命令會透過程式庫的處理序執行器接收標準輸出與標準錯誤事件，並寫入事件清單。

## 執行

```powershell
dotnet run --project .\samples\AvaloniaSample\MediaEmbedKit.Mpv.Samples.Avalonia.csproj
```

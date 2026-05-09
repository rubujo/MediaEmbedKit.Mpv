# .NET MAUI 範例

此範例示範 MAUI Windows 預覽 handler `MpvView`。目前 handler 會在 Windows 上使用 WinUI 3 HWND 控制項。

## 重點

- `UseMediaEmbedKitMpv()` 會註冊 MAUI handler。
- Windows handler 透過 WinUI 3 `MpvWinUiPlayer` 播放，僅使用 HWND 後端。
- 範例啟動時會呼叫 `SampleRuntime.InstallOrUpdateAsync()`，下載或更新同層 `runtime` 資料夾中的 `libmpv-2.dll`、`yt-dlp.exe` 與 `deno.exe`。
- 輸入檔案路徑或媒體網址後按下 `Load` 載入播放項目。
- 播放區域示範一般 MAUI 覆蓋層與 Windows HWND 播放區域的疊放關係。
- 下方事件清單會顯示 libmpv 事件、記錄訊息、屬性變更、mpv ytdl hook 的 JSON 子程序結果與範例生命週期。
- 頁面離開畫面時會釋放 `SamplePlayerEventBridge`，示範取消事件訂閱與屬性觀察。
- 目前範例只支援 Windows x64。
- 功能列示範 yt-dlp 格式預設值切換、OSD、相對跳轉、音量、靜音、播放速度、外部字幕、播放軌、截圖、載入設定檔與載入 Lua 指令碼。
- `yt-dlp`、`Deno`、`Update yt` 與 `Update Deno` 按鈕會呼叫共用 helper 執行診斷或自我更新命令；診斷命令會透過程式庫的處理序執行器接收標準輸出與標準錯誤事件，並寫入事件清單。

## 執行

```powershell
dotnet run --project .\samples\MauiSample\MediaEmbedKit.Mpv.Samples.Maui.csproj
```

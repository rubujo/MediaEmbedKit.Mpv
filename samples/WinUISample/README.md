# WinUI 3 範例

此範例示範 WinUI 3 Windows HWND 控制項 `MpvWinUiPlayer`。HWND 路線的影片上方 UI 由控制項內建覆蓋層管理，使用者不需要自行處理 AirSpace。

## 重點

- 範例啟動時會呼叫 `SampleRuntime.InstallOrUpdateAsync()`，下載或更新同層 `runtime` 資料夾中的 `libmpv-2.dll`、`yt-dlp.exe` 與 `deno.exe`。
- 輸入檔案路徑或媒體網址後按下 `Load` 開始播放。
- 控制項僅保留 HWND 後端；不提供 software render 或 GPU composition 後端。
- 播放區域同時展示控制項內建 `OverlayContent` 與一般 WinUI 覆蓋層，方便觀察 HWND AirSpace 疊放差異。
- 下方事件清單會顯示 libmpv 事件、記錄訊息、屬性變更、mpv ytdl hook 的 JSON 子程序結果與範例生命週期。
- 視窗關閉時會釋放 `SamplePlayerEventBridge`，示範取消事件訂閱與屬性觀察。
- 功能列示範 yt-dlp 格式預設值切換、OSD、相對跳轉、音量、靜音、播放速度、外部字幕、播放軌、截圖、載入設定檔與載入 Lua 指令碼。
- `yt-dlp`、`Deno`、`Update yt` 與 `Update Deno` 按鈕會呼叫共用 helper 執行診斷或自我更新命令；診斷命令會透過程式庫的處理序執行器接收標準輸出與標準錯誤事件，並寫入事件清單。
- 範例 app 使用 Windows App SDK self-contained 部署設定與 `win-x64` RID，降低測試機缺少 Windows App Runtime 時的啟動失敗機率。

## 執行

```powershell
dotnet run --project .\samples\WinUISample\MediaEmbedKit.Mpv.Samples.WinUI.csproj
```

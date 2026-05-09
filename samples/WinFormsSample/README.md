# WinForms 範例

此範例示範如何在 Windows Forms 應用程式中使用 `MpvPlayerControl`。控制項會建立 WinForms HWND，並把控制代碼傳給 libmpv 的 `wid` 選項。

## 重點

- 範例啟動時會呼叫 `SampleRuntime.InstallOrUpdateAsync()`，下載或更新同層 `runtime` 資料夾中的 `libmpv-2.dll`、`yt-dlp.exe` 與 `deno.exe`。
- 視窗顯示後會自動載入預設 YouTube 範例網址，也可輸入檔案路徑或媒體網址後按下 `Load` 載入播放項目。
- 控制項會在 Handle 建立後初始化 mpv 播放器，並透過 `PlayerCreated` 建立 `SamplePlayerEventBridge`。
- 下方事件清單會顯示 libmpv 事件、記錄訊息、屬性變更與範例生命週期。
- 播放區域提供 WinForms 子控制項覆蓋層，用來觀察同一 HWND 家族控制項的疊放行為。
- 功能列示範 yt-dlp 格式預設值切換、OSD、相對跳轉、音量、靜音、播放速度、外部字幕、播放軌、截圖、載入設定檔與載入 Lua 指令碼。
- `yt-dlp`、`Deno`、`Update yt` 與 `Update Deno` 按鈕會呼叫共用 helper 執行診斷或自我更新命令，並把標準輸出與標準錯誤寫入事件清單。

## 執行

```powershell
dotnet run --project .\samples\WinFormsSample\MediaEmbedKit.Mpv.Samples.WinForms.csproj
```

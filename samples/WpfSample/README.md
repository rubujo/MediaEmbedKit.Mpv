# WPF 範例

此範例示範如何在 WPF 應用程式中使用 `MpvWpfPlayer`。控制項透過 `HwndHost` 建立原生子視窗，並使用內建 `OverlayContent` 管理可覆蓋在視訊上的 WPF UI。

## 重點

- 範例啟動時會呼叫 `SampleRuntime.InstallOrUpdateAsync()`，下載或更新同層 `runtime` 資料夾中的 `libmpv-2.dll`、`yt-dlp.exe` 與 `deno.exe`。
- 視窗顯示後會自動載入預設 YouTube 範例網址，也可輸入檔案路徑或媒體網址後按下 `Load` 載入播放項目。
- `OverlayContent` 由控制項自行轉成 AirSpace 安全覆蓋層。
- 播放區域同時放置一般 WPF 覆蓋層；播放時可直接觀察 HwndHost 視訊區域對一般 WPF 疊放內容的影響。
- 下方事件清單會顯示 libmpv 事件、記錄訊息、屬性變更與範例生命週期。
- 視窗關閉時會釋放 `SamplePlayerEventBridge`，示範取消事件訂閱與屬性觀察。
- 功能列示範 yt-dlp 格式預設值切換、OSD、相對跳轉、音量、靜音、播放速度、外部字幕、播放軌、截圖、載入設定檔與載入 Lua 指令碼。
- `yt-dlp`、`Deno`、`Update yt` 與 `Update Deno` 按鈕會呼叫共用 helper 執行診斷或自我更新命令，並把標準輸出與標準錯誤寫入事件清單。

## 執行

```powershell
dotnet run --project .\samples\WpfSample\MediaEmbedKit.Mpv.Samples.Wpf.csproj
```

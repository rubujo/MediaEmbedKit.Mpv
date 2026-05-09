# WinUI 3 範例

此範例示範 WinUI 3 Windows HWND 控制項 `MpvWinUiPlayer`。HWND 路線的影片上方 UI 由控制項內建覆蓋層管理，使用者不需要自行處理 AirSpace。

## 重點

- 範例啟動時會呼叫 `SampleRuntime.InstallOrUpdateAsync()`，下載或更新同層 `runtime` 資料夾中的 `libmpv-2.dll`、`yt-dlp.exe` 與 `deno.exe`。
- 輸入檔案路徑或媒體網址後按下載入按鈕開始播放。
- 控制項僅保留 HWND 後端；不提供 software render 或 GPU composition 後端。

## 執行

```powershell
dotnet build .\samples\WinUISample\MediaEmbedKit.Mpv.Samples.WinUI.csproj
```

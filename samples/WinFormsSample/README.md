# WinForms 範例

此範例示範如何在 Windows Forms 應用程式中使用 `MpvPlayerControl`。控制項會建立 WinForms HWND，並把控制代碼傳給 libmpv 的 `wid` 選項。

## 重點

- 範例啟動時會呼叫 `SampleRuntime.InstallOrUpdateAsync()`，下載或更新同層 `runtime` 資料夾中的 `libmpv-2.dll`、`yt-dlp.exe` 與 `deno.exe`。
- 視窗顯示後會自動載入預設 YouTube 範例網址，也可輸入檔案路徑或媒體網址後按下 `Open` 載入播放項目。
- 控制項會在 Handle 建立後初始化 mpv 播放器。

## 執行

```powershell
dotnet run --project .\samples\WinFormsSample\MediaEmbedKit.Mpv.Samples.WinForms.csproj
```

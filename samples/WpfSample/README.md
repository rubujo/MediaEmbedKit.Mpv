# WPF 範例

此範例示範如何在 WPF 應用程式中使用 `MpvWpfPlayer`。控制項透過 `HwndHost` 建立原生子視窗，並使用內建 `OverlayContent` 管理可覆蓋在視訊上的 WPF UI。

## 重點

- 範例啟動時會呼叫 `SampleRuntime.InstallOrUpdateAsync()`，下載或更新同層 `runtime` 資料夾中的 `libmpv-2.dll`、`yt-dlp.exe` 與 `deno.exe`。
- 視窗顯示後會自動載入預設 YouTube 範例網址，也可輸入檔案路徑或媒體網址後按下 `Load` 載入播放項目。
- `OverlayContent` 由控制項自行轉成 AirSpace 安全覆蓋層。

## 執行

```powershell
dotnet run --project .\samples\WpfSample\MediaEmbedKit.Mpv.Samples.Wpf.csproj
```

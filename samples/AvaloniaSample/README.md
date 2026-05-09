# Avalonia 範例

此範例示範 Windows x64 Avalonia 預覽控制項 `MpvAvaloniaPlayer`。此控制項使用 libmpv OpenGL render API，並由 Avalonia `OpenGlControlBase` 管理 OpenGL 內容與轉譯生命週期；它不是 HWND `wid` 嵌入後端。

## 行為

- 範例啟動時會呼叫 `SampleRuntime.InstallOrUpdateAsync()`，下載或更新同層 `runtime` 資料夾中的 `libmpv-2.dll`、`yt-dlp.exe` 與 `deno.exe`。
- 視窗開啟後會自動載入預設 YouTube 範例網址，也可輸入檔案路徑或媒體網址後按下 `Load` 載入播放項目。
- 此範例目前只標示為 Windows x64 預覽；不列入 HWND-only 完成範圍。

## 執行

```powershell
dotnet run --project .\samples\AvaloniaSample\MediaEmbedKit.Mpv.Samples.Avalonia.csproj
```

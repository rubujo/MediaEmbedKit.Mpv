# .NET MAUI 範例

此範例示範 MAUI Windows 預覽 handler `MpvView`。目前 handler 會在 Windows 上使用 WinUI 3 HWND 控制項。

## 重點

- `UseMediaEmbedKitMpv()` 會註冊 MAUI handler。
- Windows handler 透過 WinUI 3 `MpvWinUiPlayer` 播放，僅使用 HWND 後端。
- 範例啟動時會呼叫 `SampleRuntime.InstallOrUpdateAsync()`，下載或更新同層 `runtime` 資料夾中的 `libmpv-2.dll`、`yt-dlp.exe` 與 `deno.exe`。
- 輸入檔案路徑或媒體網址後按下 `Load` 載入播放項目。
- 目前範例只支援 Windows x64。

## 執行

```powershell
dotnet build .\samples\MauiSample\MediaEmbedKit.Mpv.Samples.Maui.csproj
```

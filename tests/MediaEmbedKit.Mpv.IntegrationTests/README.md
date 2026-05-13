# MediaEmbedKit.Mpv.IntegrationTests

此專案執行需要 Windows x64 `libmpv-2.dll` 的整合測試，涵蓋初始化、屬性、錯誤路徑、本機 WAV 播放事件、自訂 stream callback 與 FFmpeg-Builds 下載驗證。

## 執行

```powershell
dotnet run --project .\tests\MediaEmbedKit.Mpv.IntegrationTests\MediaEmbedKit.Mpv.IntegrationTests.csproj
```

若要使用既有 runtime 資料夾，可設定：

```powershell
$env:MEDIAEMBEDKIT_MPV_RUNTIME_DIR = "D:\path\to\runtime"
dotnet run --project .\tests\MediaEmbedKit.Mpv.IntegrationTests\MediaEmbedKit.Mpv.IntegrationTests.csproj
```

未設定環境變數時，測試會在輸出資料夾下準備 `libmpv-2.dll`。此測試不需要 `yt-dlp.exe` 或 `deno.exe`，但 FFmpeg-Builds 測試會從 GitHub Releases 下載 `ffmpeg.exe` 與 `ffprobe.exe` 並驗證 checksum。

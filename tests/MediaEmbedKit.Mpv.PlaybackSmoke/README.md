# MediaEmbedKit.Mpv.PlaybackSmoke

此專案用來啟動各範例應用程式，並透過共用冒煙測試環境變數要求範例播放到指定秒數後自行關閉。

## 執行全部範例

```powershell
dotnet run --project .\tests\MediaEmbedKit.Mpv.PlaybackSmoke\MediaEmbedKit.Mpv.PlaybackSmoke.csproj -- --seconds 20
```

## 執行單一範例

```powershell
dotnet run --project .\tests\MediaEmbedKit.Mpv.PlaybackSmoke\MediaEmbedKit.Mpv.PlaybackSmoke.csproj -- --sample WinForms --seconds 20
```

可用範例名稱為 `WinForms`、`WPF`、`Avalonia`、`WinUI`、`MAUI` 或 `all`。此測試會啟動 GUI 應用程式，並可能在第一次執行時下載 `libmpv-2.dll`、`yt-dlp.exe` 與 `deno.exe`。

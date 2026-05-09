# MediaEmbedKit.Mpv.PlaybackSmoke

此專案啟動範例應用程式，並要求範例播放到指定秒數後自行關閉。

## 執行全部範例

```powershell
dotnet run --project .\tests\MediaEmbedKit.Mpv.PlaybackSmoke\MediaEmbedKit.Mpv.PlaybackSmoke.csproj -- --seconds 20
```

## 執行單一範例

```powershell
dotnet run --project .\tests\MediaEmbedKit.Mpv.PlaybackSmoke\MediaEmbedKit.Mpv.PlaybackSmoke.csproj -- --sample WinForms --seconds 20
```

可用範例名稱為 `WinForms`、`WPF`、`Avalonia`、`WinUI`、`MAUI` 或 `all`。第一次執行可能需要下載 Windows x64 runtime 資產。

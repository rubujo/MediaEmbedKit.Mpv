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

## 重複執行與共用 runtime

```powershell
dotnet run --project .\tests\MediaEmbedKit.Mpv.PlaybackSmoke\MediaEmbedKit.Mpv.PlaybackSmoke.csproj -- --seconds 60 --iterations 3 --runtime-directory .\.tmp\gui-playback-runtime
```

指定 `--runtime-directory` 時，測試器會在啟動 GUI sample 前先準備共用 runtime 資料夾；若下載遇到暫時性 HTTP 或代理錯誤，會在 console 輸出失敗原因並重試。`--sample-root` 可指定包含 `samples` 資料夾的根目錄，供乾淨 consumer sample 驗證腳本重用同一個播放檢查器。

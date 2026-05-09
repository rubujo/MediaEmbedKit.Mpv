# 測試

此資料夾保存可重複執行的驗證入口。

## 核心 API 測試

```powershell
dotnet run --project .\tests\MediaEmbedKit.Mpv.Tests\MediaEmbedKit.Mpv.Tests.csproj
```

此測試不需要原生執行階段檔案，適合在每次程式碼變更後執行。

## 原生整合測試

```powershell
dotnet run --project .\tests\MediaEmbedKit.Mpv.IntegrationTests\MediaEmbedKit.Mpv.IntegrationTests.csproj
```

此測試需要 Windows x64 `libmpv-2.dll`，會驗證初始化、屬性、錯誤路徑、本機 WAV 播放事件與自訂 stream callback。可用 `MEDIAEMBEDKIT_MPV_RUNTIME_DIR` 指向既有 runtime 資料夾。

## 播放冒煙測試

```powershell
dotnet run --project .\tests\MediaEmbedKit.Mpv.PlaybackSmoke\MediaEmbedKit.Mpv.PlaybackSmoke.csproj -- --seconds 20
```

此測試會依序啟動範例應用程式，並等待影片實際播放到指定秒數。第一次執行可能需要下載 Windows x64 執行階段檔案。

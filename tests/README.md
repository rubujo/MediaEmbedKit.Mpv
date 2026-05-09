# 測試

此資料夾保存可重複執行的驗證入口。

## 核心 API 測試

```powershell
dotnet run --project .\tests\MediaEmbedKit.Mpv.Tests\MediaEmbedKit.Mpv.Tests.csproj
```

此測試不需要原生執行階段檔案，適合在每次程式碼變更後執行。

## 播放冒煙測試

```powershell
dotnet run --project .\tests\MediaEmbedKit.Mpv.PlaybackSmoke\MediaEmbedKit.Mpv.PlaybackSmoke.csproj -- --seconds 20
```

此測試會依序啟動範例應用程式，並等待影片實際播放到指定秒數。第一次執行可能需要下載 Windows x64 執行階段檔案。

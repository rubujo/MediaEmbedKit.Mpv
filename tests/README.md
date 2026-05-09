# 測試

本資料夾包含核心 API 測試、原生整合測試與範例播放冒煙測試。

## 核心 API 測試

```powershell
dotnet run --project .\tests\MediaEmbedKit.Mpv.Tests\MediaEmbedKit.Mpv.Tests.csproj
```

此測試不初始化 libmpv，也不需要原生執行階段。

## 原生整合測試

```powershell
dotnet run --project .\tests\MediaEmbedKit.Mpv.IntegrationTests\MediaEmbedKit.Mpv.IntegrationTests.csproj
```

此測試需要 Windows x64 `libmpv-2.dll`，涵蓋初始化、屬性、錯誤路徑、本機播放事件與 stream callback。

## 播放冒煙測試

```powershell
dotnet run --project .\tests\MediaEmbedKit.Mpv.PlaybackSmoke\MediaEmbedKit.Mpv.PlaybackSmoke.csproj -- --seconds 20
```

此測試會啟動範例應用程式，並等待影片播放到指定秒數後關閉。

# MediaEmbedKit.Mpv.Tests

此專案是不依賴第三方測試套件的核心 API 測試執行器，主要驗證不需要原生 `libmpv-2.dll` 的受控邏輯。

## 執行

```powershell
dotnet run --project .\tests\MediaEmbedKit.Mpv.Tests\MediaEmbedKit.Mpv.Tests.csproj
```

此測試不會下載執行階段檔案，也不會初始化 libmpv。

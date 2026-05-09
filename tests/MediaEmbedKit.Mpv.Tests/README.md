# MediaEmbedKit.Mpv.Tests

此專案驗證不需原生 `libmpv-2.dll` 的受控核心邏輯。

## 執行

```powershell
dotnet run --project .\tests\MediaEmbedKit.Mpv.Tests\MediaEmbedKit.Mpv.Tests.csproj
```

測試不會下載 runtime，也不會初始化 libmpv。

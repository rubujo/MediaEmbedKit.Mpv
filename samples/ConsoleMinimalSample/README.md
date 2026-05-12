# Console Minimal 範例

本範例示範 Windows x64 runtime helper 與核心 `MpvPlayer` 的最小生命週期，不依賴任何 UI framework。此範例不代表跨平台 Console 播放支援。

## 示範內容

- 明確呼叫 `SampleRuntime.PrepareCoreRuntimeAsync()` 準備 Windows x64 runtime；該 helper 會重用既有 runtime，必要時才委派公開 runtime installer。
- 透過 `MpvRuntimeInstaller.CreatePlayerOptions()` 建立播放器選項。
- 初始化 `MpvPlayer`、載入媒體來源、輸出 libmpv 事件與記錄訊息。
- 使用 `using` 區塊確保播放器釋放。

## 執行

```powershell
dotnet run --project .\samples\ConsoleMinimalSample\MediaEmbedKit.Mpv.Samples.ConsoleMinimal.csproj
```

可傳入第一個命令列引數指定檔案路徑或媒體網址。

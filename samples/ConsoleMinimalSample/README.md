# Console Minimal 範例

本範例示範核心 `MpvPlayer` 的最小生命週期，不依賴任何 UI framework。

## 示範內容

- 明確呼叫 `SampleRuntime.InstallOrUpdateAsync()` 準備 Windows x64 runtime。
- 建立 `MpvPlayerOptions` 並套用範例 runtime 路徑。
- 初始化 `MpvPlayer`、載入媒體來源、輸出 libmpv 事件與記錄訊息。
- 使用 `using` 區塊確保播放器釋放。

## 執行

```powershell
dotnet run --project .\samples\ConsoleMinimalSample\MediaEmbedKit.Mpv.Samples.ConsoleMinimal.csproj
```

可傳入第一個命令列引數指定檔案路徑或媒體網址。

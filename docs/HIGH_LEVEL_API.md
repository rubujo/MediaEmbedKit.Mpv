# 高階 API 與 ergonomics 指南

本文件描述 MediaEmbedKit.Mpv 為 .NET 開發者提供的高階 API，並對照舊式直接呼叫 `new MpvPlayer(options)` 的寫法。所有新 API 都是純加形式，舊用法仍可使用。

## 建議入口

```csharp
await using MpvPlayer player = await new MpvAppBuilder()
    .UseRuntime(runtimeDirectory)
    .UseYtdlp(MpvYtdlpFormatPreset.UpTo1080p)
    .UseHardwareDecoding()
    .UseLogger(loggerFactory)
    .BuildAsync(cancellationToken);

await player.LoadAsync(new MpvMediaItem(url).WithStartTime(TimeSpan.FromSeconds(30)));
```

## MpvAppBuilder

`MpvAppBuilder` 是一站式 fluent 入口，將 runtime 準備、`MpvPlayerOptions` 設定、`new MpvPlayer(...)` 與 `InitializeAsync` 合併到一次 `BuildAsync` 呼叫。

| 鏈式方法 | 用途 |
| --- | --- |
| `UseLibrary(path)` | 指定 libmpv 原生程式庫路徑。 |
| `UseRuntime(directory, loadRuntimeConfiguration)` | 直接套用已備妥的執行階段資料夾。 |
| `UseWindowsRuntimeAutoInstall(directory, configure)` | 由 builder 在 build 時呼叫 `MpvRuntimeInstaller.InstallOrUpdateAsync`。 |
| `UseYtdlp(preset)` / `UseYtdlpFormat(selector)` / `UseYtdlpMaximumHeight(height)` | 設定 yt-dlp 格式策略。 |
| `UseHardwareDecoding(mode)` | 將 `hwdec` 加入初始選項。 |
| `UseLogger(loggerFactory, logLevel)` | 把 libmpv 記錄訊息轉送到 `ILogger`。 |
| `ConfigureOptions(action)` | 對 `MpvPlayerOptions` 做任意進一步調整。 |
| `BuildAsync(cancellationToken)` | 建構並 `InitializeAsync`；回傳就緒的 `MpvPlayer`。 |

## MpvMediaItem

對每一個媒體項目提供獨立的 HTTP 標頭、起訖時間、yt-dlp 格式與任意 mpv 選項，內部走 `mpv_command_node` 陣列避開字串 escape 風險。

```csharp
MpvMediaItem item = new MpvMediaItem(url)
    .WithStartTime(TimeSpan.FromSeconds(30))
    .WithEndTime(TimeSpan.FromMinutes(2))
    .WithHeader("User-Agent", "Mozilla/5.0")
    .WithHeader("Referer", "https://example.com")
    .WithOption("hwdec", "auto-safe")
    .WithYtdlpFormat(MpvYtdlpFormatPreset.UpTo720p);

player.Load(item);

// 或者等待載入完成：
await player.LoadAsync(item, MpvLoadFileMode.Replace, TimeSpan.FromSeconds(30));
```

## WatchProperty<T>

把 libmpv 屬性變更包裝為 `IObservable<T>`，多訂閱者共享單一 `ObserveProperty` 註冊。Player 釋放時所有訂閱者會收到 `OnCompleted`。

```csharp
IDisposable subscription = player
    .WatchProperty<double>("time-pos")
    .Subscribe(position => UpdateUi(position));

// Player 釋放時：subscription 會收到 OnCompleted，無須手動 dispose。
```

目前支援的型別：`double` / `long` / `bool` / `string` / `MpvNode`。

## Microsoft.Extensions.Logging 整合

```csharp
using Microsoft.Extensions.Logging;

ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddSimpleConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

await using MpvPlayer player = await new MpvAppBuilder()
    .UseRuntime(runtimeDirectory)
    .UseLogger(loggerFactory, logLevel: "info")
    .BuildAsync();
```

libmpv 記錄等級對應：fatal → Critical、error → Error、warn → Warning、info → Information、v → Debug、debug/trace → Trace。

> Library 本身只依賴 `Microsoft.Extensions.Logging.Abstractions`；要使用 `LoggerFactory.Create` 須在呼叫端額外引用 `Microsoft.Extensions.Logging` 套件。

## Microsoft.Extensions.DependencyInjection 整合

```csharp
using MediaEmbedKit.Mpv.Hosting;

services.AddMpvPlayer(builder => builder
    .UseRuntime(runtimeDirectory)
    .UseHardwareDecoding());

// 或者以工廠形式註冊，配合 IHostedService：
services.AddMpvPlayerFactory(builder => builder.UseRuntime(runtimeDirectory));
```

`AddMpvPlayer` 註冊為 singleton；建構時透過 `MpvAppBuilder.BuildAsync().GetAwaiter().GetResult()` 阻塞建立。若應用程式啟動時間敏感，建議改用 `AddMpvPlayerFactory` 並在 `IHostedService.StartAsync` 中 await。

## MpvCapabilities

對 libmpv 取一次性快照，內含 client API 版本、mpv 版本字串、mpv-configuration、protocols、decoders、demuxers，並提供 `SupportsProtocol` / `ContainsDemuxer` / `ContainsDecoder` 預斷方法。

```csharp
MpvCapabilities capabilities = player.GetCapabilities();
if (!capabilities.SupportsProtocol("https"))
{
    throw new InvalidOperationException("libmpv 未編譯 HTTPS 支援。");
}
```

## Runtime 更新與健康檢查

```csharp
MpvLibraryUpdateScheduler scheduler = new(runtimeDirectory);

// 啟動前套用上次暫存的更新：
scheduler.ApplyStagedOnStartup();

// 啟動健檢失敗時自動回滾：
MpvRuntimeHealthReport report = await MpvRuntimeHealthCheck.AnalyzeAsync(runtimeDirectory, probeLibMpv: true);
if (!report.IsHealthy)
{
    scheduler.Rollback();
}

// 應用程式執行中下載並暫存最新 libmpv build；下次啟動才實際套用：
MpvLibraryStageResult stageResult = await scheduler.StageAsync();
if (stageResult.RequiresProcessRestart)
{
    NotifyUserToRestart();
}
```

## 授權稽核

```csharp
MpvLicenseAuditReport audit = await MpvLicenseAuditor.AnalyzeAsync(runtimeDirectory, probeLibMpv: true);
if (audit.OverallLicense == MpvBuildLicense.NonFree)
{
    throw new InvalidOperationException("此 runtime 含 nonfree 元件，散發限制較嚴格。");
}
```

可能的整體判定：`Unknown` / `Lgpl` / `Gpl` / `NonFree`，以兩個來源中較嚴格者為準。

## 串流外部工具輸出

`YtDlpProcessRunner` / `DenoProcessRunner` / `ExternalToolProcessRunner` 都提供 `StreamAsync` 與場景專用對應方法（`StreamFormatsAsync` / `StreamVersionAsync`），可用 `await foreach` 即時消費 stdout/stderr：

```csharp
YtDlpProcessRunner runner = new(ytDlpPath);
await foreach (ExternalToolOutputEventArgs line in runner.StreamFormatsAsync(url, cancellationToken))
{
    Console.WriteLine(line.Line);
}
```

## 對照表

| 舊式 | 新式 |
| --- | --- |
| `new MpvPlayer(options).Initialize()` | `await new MpvAppBuilder().BuildAsync()` |
| `using player = ...` | `await using player = ...` |
| `player.LoadFile(url)` + 自己觀察 `FileLoaded` | `await player.LoadAsync(new MpvMediaItem(url))` |
| `player.ObserveProperty(...)` + `PropertyChanged` 事件 | `player.WatchProperty<double>(...).Subscribe(...)` |
| 直接覆蓋 `libmpv-2.dll` | `MpvLibraryUpdateScheduler.StageAsync` + `ApplyStagedOnStartup` |
| 自己解析 `demuxer-cache-state` 節點 | `player.GetDemuxerCacheState()` 取 `MpvDemuxerCacheState` |

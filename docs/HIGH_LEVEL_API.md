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

### ObserveProperty 與 WatchProperty 遷移

`ObserveProperty(string, MpvFormat)` 是 libmpv `mpv_observe_property` 的薄包裝，仍可使用，但建議新程式碼改用 `WatchProperty<T>`。對應關係如下：

| 舊式 | 新式 |
| --- | --- |
| `ulong id = player.ObserveProperty("time-pos", MpvFormat.Double);` 搭配 `player.PropertyChanged += ...;` 內手動 switch | `IDisposable sub = player.WatchProperty<double>("time-pos").Subscribe(...);` |
| `player.UnobserveProperty(id);` | `sub.Dispose();` |
| 共用 `PropertyChanged` 事件多屬性 routing | 每個屬性各自 `WatchProperty<T>`，內部以 `(Name, Format)` 作鍵共享單一 `ObserveProperty` 註冊 |

`WatchProperty<T>` 不會取代 `PropertyChanged` 事件；若你需要監聽未事先 `WatchProperty<T>` 的任意屬性（例如 mpv script 觸發的自訂屬性），仍應使用 `PropertyChanged`。

### TryGetProperty — 非例外風格

`GetPropertyXxx` 預設在屬性不存在（`PropertyNotFound`）或暫時無法使用（`PropertyUnavailable`）時擲回 `MpvException`。對於可選屬性，使用 `TryGetProperty*` 多載可直接以布林結果回報：

```csharp
if (player.TryGetPropertyDouble("video-bitrate", out double bitrate))
{
    UpdateBitrate(bitrate);
}
```

支援：`TryGetPropertyString`、`TryGetPropertyFlag`、`TryGetPropertyInt64`、`TryGetPropertyDouble`、`TryGetPropertyNode`。其他錯誤（例如未初始化、格式不匹配）仍會以 `MpvException` 擲回，因此這組 API 只吞下「屬性不存在／暫時無法使用」兩種錯誤碼。

## 事件分派與執行緒模型

libmpv 的事件迴圈跑在獨立的背景執行緒（`MpvPlayer` 內部命名為 `"libmpv event loop"`）。因此下列入口皆 **在背景執行緒** 觸發，而非 UI 執行緒：

- 所有 `event EventHandler<T>`（`EventReceived` / `PropertyChanged` / `StateChanged` / `LogMessageReceived` / `EndFile` / `StartFile` / `FileLoaded` / `Hook` / `ClientMessage` / `CommandReply` / `TracksChanged` 等）
- `WatchProperty<T>` 訂閱者的 `OnNext`

如果是 console 或服務，直接處理即可；若要更新 UI，請自行 marshal 至 UI thread：

| 框架 | 切回 UI thread 的方式 |
| --- | --- |
| WinForms | `Control.BeginInvoke(() => ...)` |
| WPF | `Dispatcher.BeginInvoke(() => ...)` |
| Avalonia | `Dispatcher.UIThread.Post(() => ...)` |
| WinUI 3 | `DispatcherQueue.TryEnqueue(() => ...)` |
| MAUI | `Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() => ...)` 或 `IDispatcher.Dispatch(...)` |

> 本專案 `MediaEmbedKit.Mpv.WinForms` / `.Wpf` / `.Avalonia` / `.WinUI` / `.Maui` 控制項已在內部完成 UI thread marshalling，使用其 `DependencyProperty` / `BindableProperty` 不必再手動切。

命令 / 屬性 API（`Command*` / `SetProperty*` / `GetProperty*` / `LoadFile` / `LoadAsync` …）皆執行緒安全，可由任意執行緒並行呼叫；player 釋放後再呼叫會擲回 `ObjectDisposedException`。

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

services.AddMpvPlayerFactory(builder => builder
    .UseRuntime(runtimeDirectory)
    .UseHardwareDecoding());
```

`AddMpvPlayerFactory` 會註冊一個 `Func<Task<MpvPlayer>>`，由呼叫端在啟動流程（例如 `IHostedService.StartAsync`、應用程式啟動程式碼）以 `await` 建立並自行決定生命週期。`MpvPlayer` 的初始化本質上是非同步的，本函式庫只提供非同步入口，不提供同步等待版本以避免常見的 `GetAwaiter().GetResult()` 死鎖。

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

## Encoding（轉碼）高階 API

mpv 的 encoding mode 允許把 player 當作一次性轉碼器：設定 `o=...` 等 `--o*` 選項後 `loadfile` 一次就會輸出檔案。本函式庫在此之上提供：

- `MpvEncodingOptions`：fluent 設定容器、codec、metadata、`*-add` 變體與通用選項。
- `MpvVideoCodecPreset` / `MpvAudioCodecPreset`：2026-05 Windows x64 build 實際內建編碼器的列舉預設。
- `MpvEncoder.EncodeAsync`：管 player 生命週期、套用選項、`loadfile`、等 `EndFile`、回報 `IProgress<MpvEncodingProgress>`、回傳 `MpvEncodingResult`。
- `MpvEncoder.EncodeTwoPassAsync`：為 libx264 / libx265 / libvpx-vp9 注入 `flags=+pass1` / `flags=+pass2` + `passlogfile`；對 `libsvtav1` 改用其原生 `pass=1` / `pass=2` 慣例。
- `MpvAppBuilder.UseEncodingTo(...)`：在 builder 流程上直接套 encoding options。

### 基線（2026-05）

| 項目 | 版本 |
| --- | --- |
| mpv stable | `v0.41.0`（2025-12-21） |
| FFmpeg（shinchiro / zhongfly build 內建） | git master commits（2026-04+，內含 SVT-AV1 two-pass patch `5ba2525`，2026-02-25）；對應 stable 為 FFmpeg 8.1 "Hoare"（2026-03-16） |
| SVT-AV1 | `4.0`（2026-01-13） |

可用編碼器（依 shinchiro 20260421 / zhongfly 2026-05-12 build）：

- 視訊：`libx264` / `libx265` / `libvpx-vp9` / `libsvtav1` / `libaom-av1` / `*_nvenc` / `*_qsv` / `*_amf`
- 音訊：`aac`（內建） / `libopus` / `libmp3lame`
- 不含：`librav1e`、`libfdk_aac`（GPL build 未編入）

### 單階段範例

```csharp
MpvEncodingOptions options = new MpvEncodingOptions(@"D:\out\clip.m4a")
    .AsAudioOnly()
    .WithAudioCodec(MpvAudioCodecPreset.Aac)
    .WithAudioCodecOption("b", "192k");

Progress<MpvEncodingProgress> progress = new Progress<MpvEncodingProgress>(p =>
    Console.WriteLine($"{p.Percent:F1}%  pos={p.Position}  bytes={p.OutputBytes}"));

MpvEncodingResult result = await MpvEncoder.EncodeAsync(
    inputPath: @"D:\src\clip.wav",
    encodingOptions: options,
    playerOptions: MpvRuntimeInstaller.CreatePlayerOptions(runtimeDirectory),
    progress: progress,
    cancellationToken: cts.Token);

if (!result.Success)
{
    Console.Error.WriteLine($"編碼失敗：reason={result.EndReason} err={result.ErrorCode}");
}
```

`MpvEncodingProgress`：`Position` / `Duration` / `Percent` / `Elapsed` / `EstimatedRemaining` / `OutputBytes`。進度僅取自 `time-pos` / `percent-pos` / `duration` 三個 mpv 屬性；不採用 `video-bitrate` / `audio-bitrate`，因為它們是解碼端瞬時值而非 encoder 輸出。

`MpvEncodingResult`：`Success` 僅在 `EndReason == EndOfFile` 且 `ErrorCode == Success` 且 `OutputBytes > 0` 時為 `true`。注意 mpv 對某些 corrupt input 會以 `EndOfFile` 完成，因此檢查 `ErrorCode` 與 `OutputBytes` 是必要的。

#### 取消行為

呼叫端傳入觸發的 `CancellationToken` 後，helper 會：

1. 對 player 呼叫 `Stop()`，請 libmpv 主動結束（通常會發出 `EndFile` with `Reason=Stop`）。
2. 等待 libmpv 自發 `EndFile` 事件，**最多 3 秒寬限期**（`CancellationGracePeriod`）；多數情況遠少於此時間。
3. 收到 `EndFile` → 走正常 `BuildResult` 路徑，`MpvEncodingResult.EndReason` 反映 libmpv 回報的真實原因（通常 `Stop`）。
4. 寬限期逾時（libmpv 因任何原因未發 `EndFile`）→ 走 `BuildCancelledResult`，回傳 `Success=false` / `EndReason=Stop` / `ErrorCode=Generic`，避免 `await EncodeAsync` 永遠卡住。

換言之：**取消會以結果（result）回報，不會擲出 `OperationCanceledException`**。呼叫端判讀 `result.Success == false && result.EndReason != EndOfFile` 即為取消／失敗路徑。

### 兩階段範例（libx264）

> **兩階段選項完整性**：呼叫 `EncodeTwoPassAsync` 時，所有透過 `WithVideoCodecOption` / `WithAudioCodecOption` / `WithMuxerOption` / `WithMetadataTag` / `WithoutMetadataTag` 累加的選項都會被原樣傳遞到第一階段與第二階段。這是 libx264 / libx265 / libvpx 兩階段 rate-control 正確運作的前提（兩階段必須拿到相同的編碼參數，第二階段才能用第一階段產生的統計資料）。


```csharp
MpvEncodingOptions options = new MpvEncodingOptions(@"D:\out\clip.mp4")
    .WithVideoCodec(MpvVideoCodecPreset.H264)
    .WithVideoCodecOption("b", "4000k")
    .WithAudioCodec(MpvAudioCodecPreset.Aac);

MpvTwoPassEncodingResult result = await MpvEncoder.EncodeTwoPassAsync(
    inputPath,
    options,
    playerOptions);
```

helper 會：
1. 於 `Path.GetTempPath()/mediaembedkit-mpv-2pass-*` 建立暫存 `passlogfile`。
2. 第一階段：輸出到暫存資料夾內的 `pass1.null`，搭配 `of=null`、`aid=no`，並於 `ovcopts` 注入 `flags=+pass1,passlogfile=<temp>`。
3. 第二階段：輸出到真正路徑、`flags=+pass2`。
4. 完成或例外時清理整個 temp 資料夾。

對 `MpvVideoCodecPreset.Av1`（解析為 `libsvtav1`）改用 `pass=1` / `pass=2` codec option，不注入 `passlogfile`。**此寫法需 FFmpeg master ≥ 2026-02-25**（FFmpeg commit [`5ba2525`](https://github.com/FFmpeg/FFmpeg/commit/5ba2525c7affc29cbd99e6266946b382d3fffe8b) "avcodec/libsvtav1: enable 2-pass encoding"），對應 stable 為 FFmpeg 8.1 "Hoare"（2026-03-16）若有 backport。shinchiro 20260421+ / zhongfly 2026-05+ build 都已內含；自備 FFmpeg 在較舊版本上 `pass=1/2` 對 libsvtav1 為 no-op（會靜默退化成單階段），請改用 FFmpeg 較新版本或用 `WithVideoCodecOption("svtav1-params", "passes=2:pass=N:stats=<file>")` 走 SVT-AV1 自身的兩階段控制。

### 整合到 `MpvAppBuilder`

```csharp
await using MpvPlayer encoder = await new MpvAppBuilder()
    .UseRuntime(runtimeDirectory)
    .UseEncodingTo(new MpvEncodingOptions(@"D:\out\clip.mp4")
        .WithVideoCodec(MpvVideoCodecPreset.H264)
        .WithAudioCodec(MpvAudioCodecPreset.Aac))
    .BuildAsync();

encoder.LoadFile(inputPath);
// ...應用程式自行監聽 EndFile / Stop。
```

builder 路徑適合需要混合其他 `Use*` 設定（hwdec、yt-dlp 格式、logger）時使用；單純一次性轉碼仍建議直接走 `MpvEncoder.EncodeAsync`。

### 常見陷阱

- **不要** 設 `vo=null` / `ao=null` 給 encoding player：encoding profile 會自動把 `vo` / `ao` 設為 `lavc`，預先寫死 null 會阻斷編碼管線。
- mpv 在 `EndFile` 之後才把最終 bytes flush 到檔案（觀察 log：`encoded ... bytes` 在 `Run command: quit` 之後才出現）；`MpvEncoder.EncodeAsync` 已先 dispose player 再讀檔案大小，自行包裝請仿照此順序。
- 跳過音訊 / 視訊串流用 `vid=no` / `aid=no`（mpv API 名稱），不是 shell 的 `--no-audio` / `--no-video`（後者非 libmpv option API 接受的選項名稱）。

### 編輯類高階入口

| 入口 | 行為 | 對應 mpv 機制 |
| --- | --- | --- |
| `MpvEncoder.EncodeAsync` | 單檔轉碼 | `o=`, `oac=`, `ovc=` 等 |
| `MpvEncoder.EncodeTwoPassAsync` | 兩階段轉碼 | `flags=+pass1/2` 或 SVT-AV1 原生 `pass=1/2` |
| `MpvEncoder.RemuxAsync` | Stream-copy 重新封裝 | `ovc=copy`, `oac=copy` |
| `MpvEncoder.ExtractAudioAsync` / `ExtractVideoAsync` | 抽單一軌 | `vid=no` / `aid=no` + 對應 codec |
| `MpvEncoder.ExtractFrameAsync` / `ExtractFramesAsync` | 抽取單一或多張影格圖片 | `start=` + `hr-seek=yes` + `frames=1` |
| `MpvEncoder.ConcatenateAsync` | 多檔合併（會重新編碼） | mpv EDL `# mpv EDL v0` 暫存檔 |
| `MpvEncoder.SplitAsync` | 依時間段切割成多檔 | 多次 `start=` / `end=` 編碼 |
| `MpvEncodingOptions.WithStartTime` / `WithEndTime` / `WithLength` | 裁切 | `start` / `end` / `length` |
| `WithFrameAccurateSeek` | 不限於 keyframe 的逐幀精準切點 | `hr-seek=yes` （此方法由舊名 `WithKeyframeAccurateSeek` 命名顛倒修正而來；pre-release 直接 rename，無 obsolete alias） |
| `WithBurnInSubtitleTrack(int)` / `WithExternalSubtitle(path)` | 字幕燒入 | `sid=` / `sub-files=` + `sub-visibility=yes` |
| `WithVideoFilter` / `WithAudioFilter` / `WithLavfiComplex` | filter 逃生口 | `vf` / `af` / `lavfi-complex` |
| `WithMetadataTag` / `WithoutMetadataTag` | metadata 個別增減 | 累積到 `oset-metadata` / `oremove-metadata` |

### libmpv 結構性不支援的場景

下列場景**不是函式庫實作不完整，而是 libmpv 本身設計上不支援**。對應需求請改用 FFmpeg CLI（runtime 資料夾的 `ffmpeg.exe` 已可直接呼叫）或既有 .NET ffmpeg 函式庫（FFMpegCore / Xabe.FFmpeg）。

| 場景 | 為何 libmpv 不支援 | 替代方案 |
| --- | --- | --- |
| **多軌輸出**（多語音軌、多視訊軌、多字幕軌共存） | mpv encoding 管線只設計單一 `[vo]` + `[ao]` 輸出；`ovc` / `oac` 為純量 | FFmpeg `-map 0:v -map 0:a:0 -map 0:a:1 ...` |
| **HLS / DASH segment muxing**（產生切片與 manifest） | mpv 一次只寫單一輸出 URL，無片段化迴圈或 manifest 處理 | FFmpeg `-f hls` / `-f dash` |
| **字幕匯出成獨立檔**（影片內嵌字幕 → `.srt` 等） | mpv 沒有把字幕封包寫成獨立檔的公開路徑 | FFmpeg `-c:s srt` 或 ffprobe + extract |
| **原檔 in-place metadata / chapter 編輯** | mpv encoding 必經 decode→encode 重新輸出，無原檔覆寫機制 | FFmpeg `-i meta.ffmetadata -c copy` |
| **任意多輸出 filter graph**（一個 graph 同時產生多條輸出 pad） | mpv 假設單一 `[vo]` / `[ao]` 輸出 pad；多輸出不被識別 | FFmpeg `-filter_complex` |
| **轉碼期間動態切換來源** | mpv encoding 一次處理單一輸入（含 EDL 視為單一虛擬輸入） | 應用層協調多次 `EncodeAsync` |

關於 EDL 概念：本函式庫 `ConcatenateAsync` 透過 mpv [EDL](https://github.com/mpv-player/mpv/blob/master/DOCS/edl-mpv.rst) v0 demuxer 串接多個輸入，**結果必經重新編碼**（mpv 結構性無 stream-copy 多輸入支援）。若需要對同 codec / 同參數的多檔做零再編碼合併，請改用 FFmpeg `concat` demuxer + `-c copy`。

關於 stream-copy（`MpvVideoCodecPreset.Copy` / `MpvAudioCodecPreset.Copy` / `MpvEncoder.RemuxAsync`）的已知 caveat：mpv encoding mode 的 `ovc=copy` / `oac=copy` 在部分 codec/container 組合下會以 `AudioOutputInitFailed` 或類似錯誤結束（mpv 的 `ao/lavc` 不支援所有 codec 直通）。若 Remux 對特定來源失敗，請改用顯式 codec preset（如 `MpvAudioCodecPreset.Aac`）做完整轉碼，或直接以 FFmpeg `-c copy` 處理。

## 對照表

| 舊式 | 新式 |
| --- | --- |
| `new MpvPlayer(options).Initialize()` | `await new MpvAppBuilder().BuildAsync()` |
| `using player = ...` | `await using player = ...` |
| `player.LoadFile(url)` + 自己觀察 `FileLoaded` | `await player.LoadAsync(new MpvMediaItem(url))` |
| `player.ObserveProperty(...)` + `PropertyChanged` 事件 | `player.WatchProperty<double>(...).Subscribe(...)` |
| 直接覆蓋 `libmpv-2.dll` | `MpvLibraryUpdateScheduler.StageAsync` + `ApplyStagedOnStartup` |
| 自己解析 `demuxer-cache-state` 節點 | `player.GetDemuxerCacheState()` 取 `MpvDemuxerCacheState` |

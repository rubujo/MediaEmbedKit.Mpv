using System;
using System.Threading;
using System.Threading.Tasks;
using MediaEmbedKit.Mpv;
using MediaEmbedKit.Mpv.Platforms;
using MediaEmbedKit.Mpv.Externals;
using MediaEmbedKit.Mpv.Runtime;
using MediaEmbedKit.Mpv.Diagnostics;
using MediaEmbedKit.Mpv.Samples;

namespace MediaEmbedKit.Mpv.Samples.ConsoleMinimal;

/// <summary>
/// 提供核心播放器最小生命週期範例的進入點，並示範 MpvAppBuilder、MpvMediaItem、WatchProperty 等高階 API。
/// </summary>
internal static class Program
{
    /// <summary>
    /// 執行 Console minimal sample。
    /// </summary>
    /// <param name="args">第一個非旗標引數可指定要播放的檔案路徑或媒體網址；可選旗標：<c>--runtime-check</c>、<c>--license-audit</c>、<c>--apply-staged</c>、<c>--encode &lt;output&gt;</c>。</param>
    /// <returns>處理程序結束代碼。</returns>
    private static async Task<int> Main(string[] args)
    {
        string? source = null;
        string? encodeOutput = null;
        bool runRuntimeCheck = false;
        bool runLicenseAudit = false;
        bool applyStaged = false;
        for (int index = 0; index < args.Length; index++)
        {
            string arg = args[index];
            if (string.Equals(arg, "--runtime-check", StringComparison.OrdinalIgnoreCase))
            {
                runRuntimeCheck = true;
            }
            else if (string.Equals(arg, "--license-audit", StringComparison.OrdinalIgnoreCase))
            {
                runLicenseAudit = true;
            }
            else if (string.Equals(arg, "--apply-staged", StringComparison.OrdinalIgnoreCase))
            {
                applyStaged = true;
            }
            else if (string.Equals(arg, "--encode", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                encodeOutput = args[++index];
            }
            else if (source == null)
            {
                source = arg;
            }
        }

        source = source ?? SampleRuntime.PlaybackUrl;

        try
        {
            Console.WriteLine("準備 Windows runtime...");
            string runtimeDirectory = await SampleRuntime.PrepareCoreRuntimeAsync().ConfigureAwait(false);

            if (encodeOutput != null)
            {
                return await RunEncodeAsync(source, encodeOutput, runtimeDirectory).ConfigureAwait(false);
            }

            if (applyStaged)
            {
                ApplyStagedRuntimeUpdates(runtimeDirectory);
            }

            if (runRuntimeCheck)
            {
                await PrintRuntimeHealthAsync(runtimeDirectory).ConfigureAwait(false);
            }

            if (runLicenseAudit)
            {
                await PrintLicenseAuditAsync(runtimeDirectory).ConfigureAwait(false);
            }

            await using MpvPlayer player = await new MpvAppBuilder()
                .UseRuntime(runtimeDirectory)
                .UseYtdlpFormat(SampleRuntime.SmoothPlaybackYtdlpFormat)
                .UseHardwareDecoding()
                .BuildAsync()
                .ConfigureAwait(false);

            player.EventReceived += PlayerEventReceived;
            player.LogMessageReceived += PlayerLogMessageReceived;
            player.FileLoaded += PlayerFileLoaded;
            player.Shutdown += PlayerShutdown;

            MpvCapabilities capabilities = player.GetCapabilities();
            Console.WriteLine(
                "libmpv client API "
                + capabilities.ClientApiVersion.ToString()
                + "，protocols="
                + capabilities.Protocols.Count.ToString()
                + "，decoders="
                + capabilities.Decoders.Count.ToString());

            // WatchProperty<T> 多屬性 fan-out 示範：對 time-pos / duration / pause /
            // volume 四個屬性同時訂閱；各自獨立 IObservable<T>，內部以 (Name, Format)
            // 鍵共享單一 mpv_observe_property 註冊。player Dispose 時所有 subscription
            // 自動收到 OnCompleted，下面 IDisposable[] 是「明確結束」的範例寫法。
            IDisposable[] subscriptions = new[]
            {
                player.WatchProperty<double>("time-pos").Subscribe(new ConsoleTimePositionObserver()),
                player.WatchProperty<double>("duration").Subscribe(new ActionObserver<double>(value =>
                    Console.WriteLine("[duration] " + value.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + "s"))),
                player.WatchProperty<bool>("pause").Subscribe(new ActionObserver<bool>(paused =>
                    Console.WriteLine("[pause] " + (paused ? "paused" : "playing")))),
                player.WatchProperty<double>("volume").Subscribe(new ActionObserver<double>(volume =>
                    Console.WriteLine("[volume] " + volume.ToString("0", System.Globalization.CultureInfo.InvariantCulture))))
            };

            Console.WriteLine("載入媒體：" + source);
            player.Load(new MpvMediaItem(source));

            await SampleRuntime.WaitForPlaybackAsync("ConsoleMinimalSample", () => player).ConfigureAwait(false);
            Console.WriteLine("播放已開始，按 Enter 停止。");
            if (Console.IsInputRedirected)
            {
                await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
            else
            {
                Console.ReadLine();
            }

            // 明確結束 fan-out 訂閱（不必要——player Dispose 會送 OnCompleted 給所有
            // 未 Dispose 的訂閱者；這裡示範主動清理的 idiom）。
            foreach (IDisposable subscription in subscriptions)
            {
                subscription.Dispose();
            }

            player.Stop();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.GetType().Name + ": " + ex.Message);
            return 1;
        }
    }

    /// <summary>
    /// 以 <see cref="MpvEncoder.EncodeAsync"/> 示範一站式轉碼：根據輸出副檔名自動選擇音訊或音影一同轉碼。
    /// </summary>
    /// <param name="inputPath">輸入媒體路徑或網址。</param>
    /// <param name="outputPath">輸出檔案路徑。</param>
    /// <param name="runtimeDirectory">執行階段資料夾。</param>
    /// <returns>處理程序結束代碼（0 成功；非 0 失敗）。</returns>
    private static async Task<int> RunEncodeAsync(string inputPath, string outputPath, string runtimeDirectory)
    {
        Console.WriteLine("[encode] input=" + inputPath);
        Console.WriteLine("[encode] output=" + outputPath);

        string extension = System.IO.Path.GetExtension(outputPath).ToLowerInvariant();
        bool audioOnly =
            extension == ".m4a" || extension == ".mp3" || extension == ".ogg"
            || extension == ".opus" || extension == ".aac" || extension == ".wav";

        MpvEncodingOptions options;
        if (audioOnly)
        {
            options = new MpvEncodingOptions(outputPath)
                .AsAudioOnly()
                .WithAudioCodec(MpvAudioCodecPreset.Aac);
            Console.WriteLine("[encode] preset: audio-only (AAC)");
        }
        else
        {
            options = new MpvEncodingOptions(outputPath)
                .WithVideoCodec(MpvVideoCodecPreset.H264)
                .WithVideoCodecOption("preset", "veryfast")
                .WithVideoCodecOption("crf", "23")
                .WithAudioCodec(MpvAudioCodecPreset.Aac)
                .WithAudioCodecOption("b", "192k");
            Console.WriteLine("[encode] preset: video=libx264 veryfast crf=23, audio=AAC 192k");
        }

        MpvPlayerOptions playerOptions = MpvRuntimeInstaller.CreatePlayerOptions(runtimeDirectory);
        playerOptions.EnableYtdlp = !audioOnly || inputPath.StartsWith("http", StringComparison.OrdinalIgnoreCase);
        playerOptions.LogLevel = "warn";

        Progress<MpvEncodingProgress> progress = new Progress<MpvEncodingProgress>(p =>
        {
            string remaining = p.EstimatedRemaining.HasValue
                ? p.EstimatedRemaining.Value.ToString(@"mm\:ss")
                : "--:--";
            Console.WriteLine("[encode] "
                + (p.Percent.HasValue ? p.Percent.Value.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) : "--")
                + "%  pos=" + p.Position.ToString(@"mm\:ss")
                + "  bytes=" + p.OutputBytes
                + "  eta=" + remaining);
        });

        using CancellationTokenSource cts = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            Console.WriteLine("[encode] 收到 Ctrl-C，取消編碼...");
            cts.Cancel();
        };

        MpvEncodingResult result = await MpvEncoder.EncodeAsync(
            inputPath,
            options,
            playerOptions,
            progress,
            cts.Token).ConfigureAwait(false);

        Console.WriteLine("[encode] result: Success=" + result.Success
            + " EndReason=" + result.EndReason
            + " ErrorCode=" + result.ErrorCode
            + " OutputBytes=" + result.OutputBytes
            + " Elapsed=" + result.Elapsed.ToString(@"mm\:ss\.fff"));

        return result.Success ? 0 : 1;
    }

    /// <summary>
    /// 在 libmpv 載入前嘗試套用先前暫存的更新；若沒有暫存就略過。
    /// </summary>
    /// <param name="runtimeDirectory">執行階段資料夾。</param>
    private static void ApplyStagedRuntimeUpdates(string runtimeDirectory)
    {
        MpvLibraryUpdateScheduler scheduler = new MpvLibraryUpdateScheduler(runtimeDirectory);
        MpvLibraryApplyResult result = scheduler.ApplyStagedOnStartup();
        Console.WriteLine("[scheduler] " + result.Message);
    }

    /// <summary>
    /// 印出 <see cref="MpvRuntimeHealthCheck"/> 報告摘要。
    /// </summary>
    /// <param name="runtimeDirectory">執行階段資料夾。</param>
    /// <returns>代表分析流程的工作。</returns>
    private static async Task PrintRuntimeHealthAsync(string runtimeDirectory)
    {
        MpvRuntimeHealthReport report = await MpvRuntimeHealthCheck.AnalyzeAsync(runtimeDirectory).ConfigureAwait(false);
        Console.WriteLine(
            "[health] libmpv=" + report.IsLibMpvPresent
            + " yt-dlp=" + report.IsYtdlpPresent
            + " ffmpeg=" + report.IsFFmpegPresent
            + " ffprobe=" + report.IsFFprobePresent
            + " deno=" + report.IsDenoPresent);
        for (int index = 0; index < report.Errors.Count; index++)
        {
            Console.WriteLine("[health] " + report.Errors[index]);
        }
    }

    /// <summary>
    /// 印出 <see cref="MpvLicenseAuditor"/> 報告摘要。
    /// </summary>
    /// <param name="runtimeDirectory">執行階段資料夾。</param>
    /// <returns>代表分析流程的工作。</returns>
    private static async Task PrintLicenseAuditAsync(string runtimeDirectory)
    {
        MpvLicenseAuditReport report = await MpvLicenseAuditor.AnalyzeAsync(runtimeDirectory).ConfigureAwait(false);
        Console.WriteLine(
            "[license] libmpv=" + report.LibMpvLicense
            + " ffmpeg=" + report.FFmpegLicense
            + " overall=" + report.OverallLicense);
        for (int index = 0; index < report.Warnings.Count; index++)
        {
            Console.WriteLine("[license] " + report.Warnings[index]);
        }
    }

    /// <summary>
    /// 輸出 libmpv 一般事件。
    /// </summary>
    /// <param name="sender">引發事件的播放器。</param>
    /// <param name="e">libmpv 事件資料。</param>
    private static void PlayerEventReceived(object? sender, MpvEventArgs e)
    {
        Console.WriteLine("[event] " + e.EventId + " error=" + e.ErrorCode + " reply=" + e.ReplyUserData);
    }

    /// <summary>
    /// 輸出 libmpv 記錄訊息。
    /// </summary>
    /// <param name="sender">引發事件的播放器。</param>
    /// <param name="e">libmpv 記錄訊息資料。</param>
    private static void PlayerLogMessageReceived(object? sender, MpvLogMessageEventArgs e)
    {
        Console.WriteLine("[log] " + e.Level + " " + e.Prefix + " | " + e.Text.TrimEnd());
    }

    /// <summary>
    /// 輸出檔案載入完成事件。
    /// </summary>
    /// <param name="sender">引發事件的播放器。</param>
    /// <param name="e">libmpv 事件資料。</param>
    private static void PlayerFileLoaded(object? sender, MpvEventArgs e)
    {
        Console.WriteLine("[lifecycle] file-loaded");
    }

    /// <summary>
    /// 輸出播放器關閉事件。
    /// </summary>
    /// <param name="sender">引發事件的播放器。</param>
    /// <param name="e">libmpv 事件資料。</param>
    private static void PlayerShutdown(object? sender, MpvEventArgs e)
    {
        Console.WriteLine("[lifecycle] shutdown");
    }

    /// <summary>
    /// 把 <see cref="Action{T}"/> 委派包裝成 <see cref="IObserver{T}"/>，供
    /// <c>WatchProperty&lt;T&gt;().Subscribe(...)</c> 使用。<see cref="IObservable{T}.Subscribe"/>
    /// 本身只收 <see cref="IObserver{T}"/>；本範例不依賴 <c>System.Reactive</c>，所以提供
    /// 此小型 adapter。
    /// </summary>
    /// <typeparam name="T">觀察者接收的值型別。</typeparam>
    private sealed class ActionObserver<T> : IObserver<T>
    {
        /// <summary>
        /// 對應 <see cref="IObserver{T}.OnNext"/> 的委派。
        /// </summary>
        private readonly Action<T> _onNext;

        /// <summary>
        /// 初始化 <see cref="ActionObserver{T}"/> 類別的新執行個體。
        /// </summary>
        /// <param name="onNext">收到新值時要呼叫的委派。</param>
        public ActionObserver(Action<T> onNext)
        {
            _onNext = onNext ?? throw new ArgumentNullException(nameof(onNext));
        }

        /// <summary>
        /// 把新值轉發到委派。
        /// </summary>
        /// <param name="value">收到的新值。</param>
        public void OnNext(T value) => _onNext(value);

        /// <summary>
        /// 訂閱因 player 釋放而結束時忽略；本範例不需特別處理。
        /// </summary>
        public void OnCompleted() { }

        /// <summary>
        /// 訂閱遇到例外狀況時忽略；本範例不需特別處理。
        /// </summary>
        /// <param name="error">未使用。</param>
        public void OnError(Exception error) { }
    }

    /// <summary>
    /// 以 <see cref="IObserver{T}"/> 形式接收 time-pos 變更並節流輸出到 stdout。
    /// </summary>
    private sealed class ConsoleTimePositionObserver : IObserver<double>
    {
        /// <summary>
        /// 最近一次列印時間。
        /// </summary>
        private DateTimeOffset _lastPrintedAt = DateTimeOffset.MinValue;

        /// <summary>
        /// 接收 time-pos 的新值並節流輸出到 stdout。
        /// </summary>
        /// <param name="value">最新的 time-pos 屬性值。</param>
        public void OnNext(double value)
        {
            if (DateTimeOffset.UtcNow - _lastPrintedAt < TimeSpan.FromSeconds(1))
            {
                return;
            }

            _lastPrintedAt = DateTimeOffset.UtcNow;
            Console.WriteLine("[time-pos] " + value.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// 在訂閱因 player 釋放而結束時通知 stdout。
        /// </summary>
        public void OnCompleted()
        {
            Console.WriteLine("[time-pos] (subscription completed)");
        }

        /// <summary>
        /// 在訂閱收到例外狀況時通知 stdout。
        /// </summary>
        /// <param name="error">訂閱觀察到的例外狀況。</param>
        public void OnError(Exception error)
        {
            Console.WriteLine("[time-pos] error: " + error.Message);
        }
    }
}

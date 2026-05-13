using System;
using System.Threading;
using System.Threading.Tasks;
using MediaEmbedKit.Mpv;
using MediaEmbedKit.Mpv.Downloads;
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
    /// <param name="args">第一個非旗標引數可指定要播放的檔案路徑或媒體網址；可選旗標：<c>--runtime-check</c>、<c>--license-audit</c>、<c>--apply-staged</c>。</param>
    /// <returns>處理程序結束代碼。</returns>
    private static async Task<int> Main(string[] args)
    {
        string? source = null;
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
            else if (source == null)
            {
                source = arg;
            }
        }

        source = source ?? SampleRuntime.PlaybackUrl;

        try
        {
            Console.WriteLine("準備 Windows x64 runtime...");
            string runtimeDirectory = await SampleRuntime.PrepareCoreRuntimeAsync().ConfigureAwait(false);

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

            using IDisposable timePositionSubscription = player
                .WatchProperty<double>("time-pos")
                .Subscribe(new ConsoleTimePositionObserver());

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

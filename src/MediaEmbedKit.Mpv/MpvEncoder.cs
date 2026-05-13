using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MediaEmbedKit.Mpv;

/// <summary>
/// 提供 mpv encoding mode 的高階一站式 API。
/// 自行管理短生命週期的 <see cref="MpvPlayer"/>、套用 <see cref="MpvEncodingOptions"/>、
/// 載入輸入、等待 <c>EndFile</c> 並回報進度。
/// </summary>
public static class MpvEncoder
{
    /// <summary>
    /// 進度回報的取樣間隔。
    /// </summary>
    private static readonly TimeSpan ProgressSamplingInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// 以非同步方式編碼單一輸入並寫出到 <see cref="MpvEncodingOptions.OutputPath"/>。
    /// </summary>
    /// <param name="inputPathOrUrl">輸入媒體的檔案路徑或網址。</param>
    /// <param name="encodingOptions">已配置好的 encoding mode 選項。</param>
    /// <param name="playerOptions">建立 <see cref="MpvPlayer"/> 時使用的選項；<see langword="null"/> 表示採用預設值。</param>
    /// <param name="progress">進度回報；<see langword="null"/> 表示不回報。</param>
    /// <param name="cancellationToken">取消編碼的權杖；觸發取消時會對 player 呼叫 <see cref="MpvPlayer.Stop()"/>。</param>
    /// <returns>編碼結果。</returns>
    /// <exception cref="ArgumentException"><paramref name="inputPathOrUrl"/> 為空白時擲出。</exception>
    /// <exception cref="ArgumentNullException"><paramref name="encodingOptions"/> 為 <see langword="null"/> 時擲出。</exception>
    public static Task<MpvEncodingResult> EncodeAsync(
        string inputPathOrUrl,
        MpvEncodingOptions encodingOptions,
        MpvPlayerOptions? playerOptions = null,
        IProgress<MpvEncodingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(inputPathOrUrl))
        {
            throw new ArgumentException("輸入媒體路徑或網址不可為空白。", nameof(inputPathOrUrl));
        }

        if (encodingOptions == null)
        {
            throw new ArgumentNullException(nameof(encodingOptions));
        }

        return EncodeAsyncCore(inputPathOrUrl, encodingOptions, playerOptions, progress, cancellationToken);
    }

    /// <summary>
    /// 以非同步方式執行兩階段（two-pass）編碼。
    /// 對 <c>libx264</c> / <c>libx265</c> / <c>libvpx-vp9</c> 等使用 <c>flags=+pass1</c> / <c>flags=+pass2</c>
    /// 加 <c>passlogfile</c>；對 <c>libsvtav1</c> 改用其原生 <c>pass=1</c> / <c>pass=2</c> 慣例。
    /// </summary>
    /// <param name="inputPathOrUrl">輸入媒體的檔案路徑或網址。</param>
    /// <param name="encodingOptions">已配置好的 encoding mode 選項；其 <see cref="MpvEncodingOptions.VideoCodec"/> 必須非空白。</param>
    /// <param name="playerOptions">建立 <see cref="MpvPlayer"/> 時使用的選項。</param>
    /// <param name="progress">進度回報；兩階段共用同一個通道。</param>
    /// <param name="cancellationToken">取消編碼的權杖。</param>
    /// <returns>兩階段整體結果。</returns>
    /// <exception cref="ArgumentException">輸入為空白或視訊編碼器未設定時擲出。</exception>
    /// <exception cref="ArgumentNullException"><paramref name="encodingOptions"/> 為 <see langword="null"/> 時擲出。</exception>
    public static Task<MpvTwoPassEncodingResult> EncodeTwoPassAsync(
        string inputPathOrUrl,
        MpvEncodingOptions encodingOptions,
        MpvPlayerOptions? playerOptions = null,
        IProgress<MpvEncodingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(inputPathOrUrl))
        {
            throw new ArgumentException("輸入媒體路徑或網址不可為空白。", nameof(inputPathOrUrl));
        }

        if (encodingOptions == null)
        {
            throw new ArgumentNullException(nameof(encodingOptions));
        }

        if (string.IsNullOrWhiteSpace(encodingOptions.VideoCodec))
        {
            throw new ArgumentException("執行兩階段編碼前必須先設定視訊編碼器（VideoCodec）。", nameof(encodingOptions));
        }

        return EncodeTwoPassAsyncCore(inputPathOrUrl, encodingOptions, playerOptions, progress, cancellationToken);
    }

    /// <summary>
    /// 執行 <see cref="EncodeTwoPassAsync"/> 的核心流程。
    /// </summary>
    /// <param name="inputPathOrUrl">輸入媒體的檔案路徑或網址。</param>
    /// <param name="encodingOptions">已配置好的 encoding mode 選項。</param>
    /// <param name="playerOptions">建立 <see cref="MpvPlayer"/> 時使用的選項。</param>
    /// <param name="progress">進度回報通道。</param>
    /// <param name="cancellationToken">取消編碼的權杖。</param>
    /// <returns>兩階段整體結果。</returns>
    private static async Task<MpvTwoPassEncodingResult> EncodeTwoPassAsyncCore(
        string inputPathOrUrl,
        MpvEncodingOptions encodingOptions,
        MpvPlayerOptions? playerOptions,
        IProgress<MpvEncodingProgress>? progress,
        CancellationToken cancellationToken)
    {
        string passlogDirectory = Path.Combine(
            Path.GetTempPath(),
            "mediaembedkit-mpv-2pass-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(passlogDirectory);
        string passlogPrefix = Path.Combine(passlogDirectory, "ffpass");
        bool isSvtAv1 = string.Equals(encodingOptions.VideoCodec, "libsvtav1", StringComparison.OrdinalIgnoreCase);

        try
        {
            MpvEncodingOptions firstPass = ClonePassOptions(encodingOptions, GetNullSinkPath(), passNumber: 1, passlogPrefix, isSvtAv1);
            MpvEncodingResult firstResult = await EncodeAsyncCore(inputPathOrUrl, firstPass, playerOptions, progress, cancellationToken).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested || firstResult.EndReason != MpvEndFileReason.EndOfFile)
            {
                return new MpvTwoPassEncodingResult(firstResult, null);
            }

            MpvEncodingOptions secondPass = ClonePassOptions(encodingOptions, encodingOptions.OutputPath, passNumber: 2, passlogPrefix, isSvtAv1);
            MpvEncodingResult secondResult = await EncodeAsyncCore(inputPathOrUrl, secondPass, playerOptions, progress, cancellationToken).ConfigureAwait(false);
            return new MpvTwoPassEncodingResult(firstResult, secondResult);
        }
        finally
        {
            TryDeleteDirectory(passlogDirectory);
        }
    }

    /// <summary>
    /// 為單一階段建立 <see cref="MpvEncodingOptions"/> 副本並注入 pass 相關旗標。
    /// </summary>
    /// <param name="source">原始選項。</param>
    /// <param name="outputPath">本階段的輸出路徑。</param>
    /// <param name="passNumber">階段編號（1 或 2）。</param>
    /// <param name="passlogPrefix">FFmpeg passlogfile 前綴。</param>
    /// <param name="isSvtAv1">是否為 SVT-AV1 編碼器。</param>
    /// <returns>已注入 pass 設定的選項副本。</returns>
    private static MpvEncodingOptions ClonePassOptions(
        MpvEncodingOptions source,
        string outputPath,
        int passNumber,
        string passlogPrefix,
        bool isSvtAv1)
    {
        MpvEncodingOptions clone = new MpvEncodingOptions(outputPath);
        clone.ContainerFormat = source.ContainerFormat;
        clone.ContainerFormatOptions = source.ContainerFormatOptions;
        clone.VideoCodec = source.VideoCodec;
        clone.VideoCodecOptions = source.VideoCodecOptions;
        clone.AudioCodec = source.AudioCodec;
        clone.AudioCodecOptions = source.AudioCodecOptions;
        clone.CopyRawTimestamps = source.CopyRawTimestamps;
        clone.CopyMetadata = source.CopyMetadata;
        clone.Metadata = source.Metadata;
        clone.RemovedMetadata = source.RemovedMetadata;

        foreach (KeyValuePair<string, string> option in source.AdditionalOptions)
        {
            clone.AdditionalOptions[option.Key] = option.Value;
        }

        if (isSvtAv1)
        {
            clone.WithVideoCodecOption("pass", passNumber.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        else
        {
            clone.WithVideoCodecOption("flags", "+pass" + passNumber.ToString(System.Globalization.CultureInfo.InvariantCulture));
            clone.WithVideoCodecOption("passlogfile", passlogPrefix);
        }

        if (passNumber == 1)
        {
            clone.AudioCodec = null;
            clone.AudioCodecOptions = null;
            clone.WithOption("aid", "no");
        }

        return clone;
    }

    /// <summary>
    /// 取得 mpv 在第一階段 (pass 1) 使用的 null sink 輸出路徑。
    /// </summary>
    /// <returns>跨平台 null sink 路徑。</returns>
    private static string GetNullSinkPath()
    {
        return System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows)
            ? "NUL"
            : "/dev/null";
    }

    /// <summary>
    /// 嘗試刪除暫存 passlogfile 資料夾；無法刪除時忽略例外狀況。
    /// </summary>
    /// <param name="directory">要刪除的資料夾路徑。</param>
    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// 執行 <see cref="EncodeAsync"/> 的核心流程。
    /// </summary>
    /// <param name="inputPathOrUrl">輸入媒體的檔案路徑或網址。</param>
    /// <param name="encodingOptions">已配置好的 encoding mode 選項。</param>
    /// <param name="playerOptions">建立 <see cref="MpvPlayer"/> 時使用的選項。</param>
    /// <param name="progress">進度回報。</param>
    /// <param name="cancellationToken">取消編碼的權杖。</param>
    /// <returns>編碼結果。</returns>
    private static async Task<MpvEncodingResult> EncodeAsyncCore(
        string inputPathOrUrl,
        MpvEncodingOptions encodingOptions,
        MpvPlayerOptions? playerOptions,
        IProgress<MpvEncodingProgress>? progress,
        CancellationToken cancellationToken)
    {
        MpvPlayerOptions effectiveOptions = new MpvPlayerOptions();
        if (playerOptions != null)
        {
            playerOptions.CopyTo(effectiveOptions);
        }

        encodingOptions.ApplyTo(effectiveOptions);

        TaskCompletionSource<MpvEndFileEventArgs> completion =
            new TaskCompletionSource<MpvEndFileEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);

        MpvEndFileEventArgs endEvent;
        TimeSpan elapsed;
        MpvPlayer player = new MpvPlayer(effectiveOptions);
        try
        {
            EventHandler<MpvEndFileEventArgs> endFileHandler = delegate (object? sender, MpvEndFileEventArgs args)
            {
                completion.TrySetResult(args);
            };

            player.EndFile += endFileHandler;
            try
            {
                player.Initialize();

                Stopwatch stopwatch = Stopwatch.StartNew();
                using (CancellationTokenSource progressCts = new CancellationTokenSource())
                {
                    Task progressTask = progress == null
                        ? Task.CompletedTask
                        : RunProgressLoopAsync(player, encodingOptions.OutputPath, stopwatch, progress, progressCts.Token);

                    using (cancellationToken.Register(static state => SafeStop((MpvPlayer)state!), player))
                    {
                        player.LoadFile(inputPathOrUrl);
                        endEvent = await completion.Task.ConfigureAwait(false);
                        stopwatch.Stop();
                        progressCts.Cancel();
                        try
                        {
                            await progressTask.ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                        }

                        elapsed = stopwatch.Elapsed;
                    }
                }
            }
            finally
            {
                player.EndFile -= endFileHandler;
            }
        }
        finally
        {
            player.Dispose();
        }

        return BuildResult(endEvent, encodingOptions.OutputPath, elapsed);
    }

    /// <summary>
    /// 安全呼叫 <see cref="MpvPlayer.Stop()"/>；player 已釋放時忽略例外狀況。
    /// </summary>
    /// <param name="player">要停止的播放器。</param>
    private static void SafeStop(MpvPlayer player)
    {
        try
        {
            player.Stop();
        }
        catch (ObjectDisposedException)
        {
        }
        catch (MpvException)
        {
        }
    }

    /// <summary>
    /// 依固定取樣間隔讀取 player 屬性並回報 <see cref="MpvEncodingProgress"/>。
    /// </summary>
    /// <param name="player">編碼中的 player。</param>
    /// <param name="outputPath">輸出檔案路徑。</param>
    /// <param name="stopwatch">記錄實際經過時間的計時器。</param>
    /// <param name="progress">進度回報通道。</param>
    /// <param name="cancellationToken">取消權杖；於 <c>EndFile</c> 後由呼叫端觸發。</param>
    /// <returns>代表進度迴圈的工作。</returns>
    private static async Task RunProgressLoopAsync(
        MpvPlayer player,
        string outputPath,
        Stopwatch stopwatch,
        IProgress<MpvEncodingProgress> progress,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(ProgressSamplingInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            MpvEncodingProgress snapshot = CapturePlayerSnapshot(player, outputPath, stopwatch.Elapsed);
            progress.Report(snapshot);
        }
    }

    /// <summary>
    /// 從目前 player 與輸出檔讀取進度快照。
    /// </summary>
    /// <param name="player">編碼中的 player。</param>
    /// <param name="outputPath">輸出檔案路徑。</param>
    /// <param name="elapsed">自編碼開始以來實際經過的時間。</param>
    /// <returns>進度快照。</returns>
    private static MpvEncodingProgress CapturePlayerSnapshot(MpvPlayer player, string outputPath, TimeSpan elapsed)
    {
        TimeSpan position = TryReadTimeSpanSeconds(player, "time-pos");
        TimeSpan duration = TryReadTimeSpanSeconds(player, "duration");
        double? percent = TryReadDouble(player, "percent-pos");
        TimeSpan? estimatedRemaining = EstimateRemaining(position, duration, elapsed);
        long outputBytes = TryReadOutputBytes(outputPath);
        return new MpvEncodingProgress(position, duration, percent, elapsed, estimatedRemaining, outputBytes);
    }

    /// <summary>
    /// 嘗試讀取以秒為單位的雙精度屬性，並轉成 <see cref="TimeSpan"/>。
    /// </summary>
    /// <param name="player">編碼中的 player。</param>
    /// <param name="propertyName">mpv 屬性名稱。</param>
    /// <returns>對應的 <see cref="TimeSpan"/>；屬性不可用時為 <see cref="TimeSpan.Zero"/>。</returns>
    private static TimeSpan TryReadTimeSpanSeconds(MpvPlayer player, string propertyName)
    {
        try
        {
            double seconds = player.GetPropertyDouble(propertyName);
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
            {
                return TimeSpan.Zero;
            }

            return TimeSpan.FromSeconds(seconds);
        }
        catch (MpvException)
        {
            return TimeSpan.Zero;
        }
    }

    /// <summary>
    /// 嘗試讀取雙精度屬性。
    /// </summary>
    /// <param name="player">編碼中的 player。</param>
    /// <param name="propertyName">mpv 屬性名稱。</param>
    /// <returns>雙精度屬性值；不可用時為 <see langword="null"/>。</returns>
    private static double? TryReadDouble(MpvPlayer player, string propertyName)
    {
        try
        {
            double value = player.GetPropertyDouble(propertyName);
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return null;
            }

            return value;
        }
        catch (MpvException)
        {
            return null;
        }
    }

    /// <summary>
    /// 依目前進度推估剩餘時間。
    /// </summary>
    /// <param name="position">目前已處理到的來源時間。</param>
    /// <param name="duration">來源總時長。</param>
    /// <param name="elapsed">實際經過的時間。</param>
    /// <returns>推估剩餘時間；資料不足時為 <see langword="null"/>。</returns>
    private static TimeSpan? EstimateRemaining(TimeSpan position, TimeSpan duration, TimeSpan elapsed)
    {
        if (duration <= TimeSpan.Zero || position <= TimeSpan.Zero || elapsed <= TimeSpan.Zero || position >= duration)
        {
            return null;
        }

        double speed = position.TotalSeconds / elapsed.TotalSeconds;
        if (speed <= 0)
        {
            return null;
        }

        double remainingSeconds = (duration.TotalSeconds - position.TotalSeconds) / speed;
        if (double.IsNaN(remainingSeconds) || double.IsInfinity(remainingSeconds) || remainingSeconds < 0)
        {
            return null;
        }

        return TimeSpan.FromSeconds(remainingSeconds);
    }

    /// <summary>
    /// 嘗試讀取輸出檔案位元組大小。
    /// </summary>
    /// <param name="outputPath">輸出檔案路徑。</param>
    /// <returns>位元組大小；檔案不存在或無法讀取時為 0。</returns>
    private static long TryReadOutputBytes(string outputPath)
    {
        try
        {
            FileInfo info = new FileInfo(outputPath);
            return info.Exists ? info.Length : 0;
        }
        catch (IOException)
        {
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            return 0;
        }
    }

    /// <summary>
    /// 由 <c>EndFile</c> 事件與輸出檔組成最終結果。
    /// </summary>
    /// <param name="endEvent"><c>EndFile</c> 事件資料。</param>
    /// <param name="outputPath">輸出檔案路徑。</param>
    /// <param name="elapsed">實際經過的時間。</param>
    /// <returns>編碼結果。</returns>
    private static MpvEncodingResult BuildResult(MpvEndFileEventArgs endEvent, string outputPath, TimeSpan elapsed)
    {
        MpvErrorCode errorCode = (MpvErrorCode)endEvent.MpvErrorCode;
        long outputBytes = TryReadOutputBytes(outputPath);
        bool success = endEvent.Reason == MpvEndFileReason.EndOfFile
            && errorCode == MpvErrorCode.Success
            && outputBytes > 0;
        return new MpvEncodingResult(success, endEvent.Reason, errorCode, outputPath, outputBytes, elapsed);
    }
}

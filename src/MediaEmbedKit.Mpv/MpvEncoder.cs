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
    /// 取消後等待 libmpv 主動發送 <c>EndFile</c> 的最長時間；
    /// 逾時則由 helper 自行終止等待，避免因 libmpv 未發事件而永久卡住。
    /// </summary>
    private static readonly TimeSpan CancellationGracePeriod = TimeSpan.FromSeconds(3);

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
    /// 以 stream-copy 模式重新封裝媒體（不重新編碼視訊與音訊）。
    /// </summary>
    /// <param name="inputPathOrUrl">輸入媒體的檔案路徑或網址。</param>
    /// <param name="outputPath">輸出檔案路徑；副檔名決定容器格式。</param>
    /// <param name="playerOptions">建立 <see cref="MpvPlayer"/> 時使用的選項。</param>
    /// <param name="progress">進度回報。</param>
    /// <param name="cancellationToken">取消編碼的權杖。</param>
    /// <returns>編碼結果。</returns>
    public static Task<MpvEncodingResult> RemuxAsync(
        string inputPathOrUrl,
        string outputPath,
        MpvPlayerOptions? playerOptions = null,
        IProgress<MpvEncodingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("輸出路徑不可為空白。", nameof(outputPath));
        }

        MpvEncodingOptions options = new MpvEncodingOptions(outputPath)
            .WithVideoCodec(MpvVideoCodecPreset.Copy)
            .WithAudioCodec(MpvAudioCodecPreset.Copy);
        return EncodeAsync(inputPathOrUrl, options, playerOptions, progress, cancellationToken);
    }

    /// <summary>
    /// 抽取輸入媒體的音訊軌並以指定編碼器輸出。
    /// </summary>
    /// <param name="inputPathOrUrl">輸入媒體的檔案路徑或網址。</param>
    /// <param name="outputPath">輸出檔案路徑。</param>
    /// <param name="audioCodec">輸出音訊編碼器；可用 <see cref="MpvAudioCodecPreset.Copy"/> 直接 stream-copy。</param>
    /// <param name="playerOptions">建立 <see cref="MpvPlayer"/> 時使用的選項。</param>
    /// <param name="progress">進度回報。</param>
    /// <param name="cancellationToken">取消編碼的權杖。</param>
    /// <returns>編碼結果。</returns>
    public static Task<MpvEncodingResult> ExtractAudioAsync(
        string inputPathOrUrl,
        string outputPath,
        MpvAudioCodecPreset audioCodec,
        MpvPlayerOptions? playerOptions = null,
        IProgress<MpvEncodingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("輸出路徑不可為空白。", nameof(outputPath));
        }

        MpvEncodingOptions options = new MpvEncodingOptions(outputPath)
            .AsAudioOnly()
            .WithAudioCodec(audioCodec);
        return EncodeAsync(inputPathOrUrl, options, playerOptions, progress, cancellationToken);
    }

    /// <summary>
    /// 抽取輸入媒體的視訊軌並以指定編碼器輸出（無音訊）。
    /// </summary>
    /// <param name="inputPathOrUrl">輸入媒體的檔案路徑或網址。</param>
    /// <param name="outputPath">輸出檔案路徑。</param>
    /// <param name="videoCodec">輸出視訊編碼器；可用 <see cref="MpvVideoCodecPreset.Copy"/> 直接 stream-copy。</param>
    /// <param name="playerOptions">建立 <see cref="MpvPlayer"/> 時使用的選項。</param>
    /// <param name="progress">進度回報。</param>
    /// <param name="cancellationToken">取消編碼的權杖。</param>
    /// <returns>編碼結果。</returns>
    public static Task<MpvEncodingResult> ExtractVideoAsync(
        string inputPathOrUrl,
        string outputPath,
        MpvVideoCodecPreset videoCodec,
        MpvPlayerOptions? playerOptions = null,
        IProgress<MpvEncodingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("輸出路徑不可為空白。", nameof(outputPath));
        }

        MpvEncodingOptions options = new MpvEncodingOptions(outputPath)
            .AsVideoOnly()
            .WithVideoCodec(videoCodec);
        return EncodeAsync(inputPathOrUrl, options, playerOptions, progress, cancellationToken);
    }

    /// <summary>
    /// 在指定時間點抽取單一影格並輸出為圖片。
    /// </summary>
    /// <param name="inputPathOrUrl">輸入媒體的檔案路徑或網址。</param>
    /// <param name="at">要抽取的時間點。</param>
    /// <param name="outputPath">輸出影格檔案路徑；副檔名決定影像格式（png / jpg / webp 等由 mpv 推斷）。</param>
    /// <param name="playerOptions">建立 <see cref="MpvPlayer"/> 時使用的選項。</param>
    /// <param name="cancellationToken">取消編碼的權杖。</param>
    /// <returns>編碼結果；<see cref="MpvEncodingResult.OutputBytes"/> 對應產生的影像位元組數。</returns>
    public static Task<MpvEncodingResult> ExtractFrameAsync(
        string inputPathOrUrl,
        TimeSpan at,
        string outputPath,
        MpvPlayerOptions? playerOptions = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("輸出路徑不可為空白。", nameof(outputPath));
        }

        if (at < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(at), at, "時間點不可為負值。");
        }

        MpvEncodingOptions options = new MpvEncodingOptions(outputPath)
            .WithStartTime(at)
            .WithFrameAccurateSeek()
            .AsVideoOnly()
            .WithOption("frames", "1");
        return EncodeAsync(inputPathOrUrl, options, playerOptions, progress: null, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 在多個時間點分別抽取影格並輸出為圖片。
    /// </summary>
    /// <param name="inputPathOrUrl">輸入媒體的檔案路徑或網址。</param>
    /// <param name="frames">時間點與對應輸出路徑序列。</param>
    /// <param name="playerOptions">建立 <see cref="MpvPlayer"/> 時使用的選項。</param>
    /// <param name="cancellationToken">取消編碼的權杖。</param>
    /// <returns>依輸入順序對應的結果清單。</returns>
    public static async Task<IReadOnlyList<MpvEncodingResult>> ExtractFramesAsync(
        string inputPathOrUrl,
        IEnumerable<KeyValuePair<TimeSpan, string>> frames,
        MpvPlayerOptions? playerOptions = null,
        CancellationToken cancellationToken = default)
    {
        if (frames == null)
        {
            throw new ArgumentNullException(nameof(frames));
        }

        List<MpvEncodingResult> results = new List<MpvEncodingResult>();
        foreach (KeyValuePair<TimeSpan, string> frame in frames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MpvEncodingResult result = await ExtractFrameAsync(
                inputPathOrUrl,
                frame.Key,
                frame.Value,
                playerOptions,
                cancellationToken).ConfigureAwait(false);
            results.Add(result);
        }

        return results;
    }

    /// <summary>
    /// 透過 mpv EDL demuxer 把多個輸入串接成一個輸出檔（會重新編碼）。
    /// </summary>
    /// <param name="inputPaths">輸入媒體路徑序列（依序串接）。</param>
    /// <param name="encodingOptions">已配置好的 encoding mode 選項。</param>
    /// <param name="playerOptions">建立 <see cref="MpvPlayer"/> 時使用的選項。</param>
    /// <param name="progress">進度回報。</param>
    /// <param name="cancellationToken">取消編碼的權杖。</param>
    /// <returns>編碼結果。</returns>
    public static async Task<MpvEncodingResult> ConcatenateAsync(
        IEnumerable<string> inputPaths,
        MpvEncodingOptions encodingOptions,
        MpvPlayerOptions? playerOptions = null,
        IProgress<MpvEncodingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (inputPaths == null)
        {
            throw new ArgumentNullException(nameof(inputPaths));
        }

        if (encodingOptions == null)
        {
            throw new ArgumentNullException(nameof(encodingOptions));
        }

        List<string> inputs = new List<string>(inputPaths);
        if (inputs.Count == 0)
        {
            throw new ArgumentException("Concatenate 需要至少一個輸入。", nameof(inputPaths));
        }

        string edlPath = Path.Combine(
            Path.GetTempPath(),
            "mediaembedkit-mpv-concat-" + Guid.NewGuid().ToString("N") + ".edl");
        WriteEdlPlaylist(edlPath, inputs);
        try
        {
            return await EncodeAsync(edlPath, encodingOptions, playerOptions, progress, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                if (File.Exists(edlPath))
                {
                    File.Delete(edlPath);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>
    /// 把單一輸入依時間段切割成多個輸出檔。
    /// </summary>
    /// <param name="inputPathOrUrl">輸入媒體的檔案路徑或網址。</param>
    /// <param name="segments">時間段序列（每段包含起點、終點與輸出路徑）。</param>
    /// <param name="configureBase">套用至每個段的共同 encoding 設定（codec / metadata 等），不含 <c>start</c> / <c>end</c>。</param>
    /// <param name="playerOptions">建立 <see cref="MpvPlayer"/> 時使用的選項。</param>
    /// <param name="progress">進度回報；報告值為 (目前段索引, 總段數, 目前段內進度)。</param>
    /// <param name="cancellationToken">取消編碼的權杖。</param>
    /// <returns>依輸入順序對應的結果清單。</returns>
    public static async Task<IReadOnlyList<MpvEncodingResult>> SplitAsync(
        string inputPathOrUrl,
        IEnumerable<MpvEncodingSegment> segments,
        Func<MpvEncodingOptions, MpvEncodingOptions> configureBase,
        MpvPlayerOptions? playerOptions = null,
        IProgress<MpvSplitProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (segments == null)
        {
            throw new ArgumentNullException(nameof(segments));
        }

        if (configureBase == null)
        {
            throw new ArgumentNullException(nameof(configureBase));
        }

        List<MpvEncodingSegment> segmentList = new List<MpvEncodingSegment>(segments);
        if (segmentList.Count == 0)
        {
            throw new ArgumentException("Split 需要至少一個時間段。", nameof(segments));
        }

        List<MpvEncodingResult> results = new List<MpvEncodingResult>();
        for (int index = 0; index < segmentList.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MpvEncodingSegment segment = segmentList[index];
            if (segment.End <= segment.Start)
            {
                throw new ArgumentException(
                    "段索引 " + index + " 的終點必須大於起點。",
                    nameof(segments));
            }

            MpvEncodingOptions options = configureBase(new MpvEncodingOptions(segment.OutputPath))
                .WithStartTime(segment.Start)
                .WithEndTime(segment.End);

            int currentIndex = index;
            int total = segmentList.Count;
            IProgress<MpvEncodingProgress>? segmentProgress = progress == null
                ? null
                : new ProgressForwarder(progress, currentIndex, total);

            MpvEncodingResult result = await EncodeAsync(
                inputPathOrUrl,
                options,
                playerOptions,
                segmentProgress,
                cancellationToken).ConfigureAwait(false);
            results.Add(result);
        }

        return results;
    }

    /// <summary>
    /// 把進度從單段事件轉送為 (index, total, snapshot) 三元組事件。
    /// </summary>
    private sealed class ProgressForwarder : IProgress<MpvEncodingProgress>
    {
        /// <summary>
        /// 上游 progress channel。
        /// </summary>
        private readonly IProgress<MpvSplitProgress> _target;
        /// <summary>
        /// 目前段索引。
        /// </summary>
        private readonly int _index;
        /// <summary>
        /// 總段數。
        /// </summary>
        private readonly int _total;

        /// <summary>
        /// 初始化轉送器。
        /// </summary>
        /// <param name="target">上游 progress channel。</param>
        /// <param name="index">目前段索引。</param>
        /// <param name="total">總段數。</param>
        public ProgressForwarder(IProgress<MpvSplitProgress> target, int index, int total)
        {
            _target = target;
            _index = index;
            _total = total;
        }

        /// <summary>
        /// 將單一編碼進度快照轉換為切割流程進度並轉送到上游通道。
        /// </summary>
        /// <param name="value">目前段落的編碼進度快照。</param>
        public void Report(MpvEncodingProgress value)
        {
            _target.Report(new MpvSplitProgress(_index, _total, value));
        }
    }

    /// <summary>
    /// 寫入 mpv EDL v0 playlist 檔。
    /// </summary>
    /// <param name="path">EDL 檔輸出路徑。</param>
    /// <param name="inputs">輸入媒體路徑序列。</param>
    private static void WriteEdlPlaylist(string path, IReadOnlyList<string> inputs)
    {
        using (StreamWriter writer = new StreamWriter(path, false, new System.Text.UTF8Encoding(false)))
        {
            writer.NewLine = "\n";
            writer.WriteLine("# mpv EDL v0");
            for (int index = 0; index < inputs.Count; index++)
            {
                string input = inputs[index];
                if (string.IsNullOrWhiteSpace(input))
                {
                    throw new ArgumentException("Concatenate 輸入序列含有空白項目。", nameof(inputs));
                }

                writer.WriteLine(EncodeEdlFilename(input));
            }
        }
    }

    /// <summary>
    /// 將檔名以 mpv EDL <c>%bytecount%filename</c> 格式編碼以避開逗號 / 引號 / 空白等 escape 問題。
    /// </summary>
    /// <param name="filename">原始檔名或路徑。</param>
    /// <returns>EDL 安全的單行表示。</returns>
    private static string EncodeEdlFilename(string filename)
    {
        int byteCount = System.Text.Encoding.UTF8.GetByteCount(filename);
        return "%" + byteCount.ToString(System.Globalization.CultureInfo.InvariantCulture) + "%" + filename;
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

        source.CopyAccumulatedListsTo(clone);

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

        MpvEndFileEventArgs? endEvent = null;
        bool cancellationGraceExpired = false;
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
                        endEvent = await WaitForEndFileAsync(completion, cancellationToken).ConfigureAwait(false);
                        if (endEvent == null)
                        {
                            cancellationGraceExpired = true;
                        }

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

        if (endEvent == null)
        {
            return BuildCancelledResult(encodingOptions.OutputPath, elapsed, cancellationGraceExpired);
        }

        return BuildResult(endEvent, encodingOptions.OutputPath, elapsed);
    }

    /// <summary>
    /// 等待 libmpv 發送 <c>EndFile</c>；當 <paramref name="cancellationToken"/> 觸發後，
    /// 最多再等 <see cref="CancellationGracePeriod"/> 讓 libmpv 自行收尾。
    /// 逾時則回傳 <see langword="null"/>，呼叫端據此產生取消結果。
    /// </summary>
    /// <param name="completion">由 <c>EndFile</c> handler 完成的 TCS。</param>
    /// <param name="cancellationToken">使用者傳入的取消權杖。</param>
    /// <returns>收到的 <see cref="MpvEndFileEventArgs"/>；逾時取消時為 <see langword="null"/>。</returns>
    private static async Task<MpvEndFileEventArgs?> WaitForEndFileAsync(
        TaskCompletionSource<MpvEndFileEventArgs> completion,
        CancellationToken cancellationToken)
    {
        if (!cancellationToken.CanBeCanceled)
        {
            return await completion.Task.ConfigureAwait(false);
        }

        Task<MpvEndFileEventArgs> waitTask = completion.Task;
        TaskCompletionSource<bool> cancellationTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using (cancellationToken.Register(static state => ((TaskCompletionSource<bool>)state!).TrySetResult(true), cancellationTcs))
        {
            Task firstStage = await Task.WhenAny(waitTask, cancellationTcs.Task).ConfigureAwait(false);
            if (firstStage == waitTask)
            {
                return await waitTask.ConfigureAwait(false);
            }
        }

        // CT 已觸發；給 libmpv CancellationGracePeriod 收尾。
        Task delay = Task.Delay(CancellationGracePeriod);
        Task finished = await Task.WhenAny(waitTask, delay).ConfigureAwait(false);
        if (finished == waitTask)
        {
            return await waitTask.ConfigureAwait(false);
        }

        return null;
    }

    /// <summary>
    /// 為取消（或寬限期逾時）情境建立 <see cref="MpvEncodingResult"/>。
    /// </summary>
    /// <param name="outputPath">輸出檔案路徑。</param>
    /// <param name="elapsed">取消當下的耗時。</param>
    /// <param name="graceExpired">是否因 grace period 逾時而非 mpv 主動結束。</param>
    /// <returns>對應取消的結果。</returns>
    private static MpvEncodingResult BuildCancelledResult(string outputPath, TimeSpan elapsed, bool graceExpired)
    {
        MpvErrorCode errorCode = graceExpired ? MpvErrorCode.Generic : MpvErrorCode.Success;
        long outputBytes = TryReadOutputBytes(outputPath);
        return new MpvEncodingResult(false, MpvEndFileReason.Stop, errorCode, outputPath, outputBytes, elapsed);
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

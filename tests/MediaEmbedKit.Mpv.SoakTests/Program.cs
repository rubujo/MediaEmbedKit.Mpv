using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaEmbedKit.Mpv.Downloads;

namespace MediaEmbedKit.Mpv.SoakTests;

/// <summary>
/// 24 小時連續播放 soak harness：循環 Load → 播放 → Stop → 取樣 → 記錄。
/// 工作負載按 iteration 輪流：WAV（audio only）／MP4（video + audio）／取消（cancel-in-flight）。
/// 每回合都 subscribe WatchProperty time-pos + pause + dispose，並讀取多個 property
/// 涵蓋 decoder / event / property cache 路徑。
/// 跑完後線性回歸推算整段累計成長並比對絕對量門檻（pass/fail gate）；同時做
/// Mann-Kendall 趨勢檢定輸出方向描述（純資訊用，MK 對 magnitude 無感所以不參與 gate）。
/// </summary>
internal static class Program
{
    private const int SampleRate = 44100;

    private static async Task<int> Main(string[] args)
    {
        SoakOptions options;
        try
        {
            options = SoakOptions.Parse(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("引數解析失敗：" + ex.Message);
            SoakOptions.PrintUsage(Console.Error);
            return 2;
        }

        if (options.ShowHelp)
        {
            SoakOptions.PrintUsage(Console.Out);
            return 0;
        }

        Directory.CreateDirectory(options.OutputDirectory);
        string samplesCsv = Path.Combine(options.OutputDirectory, "samples.csv");
        string reportTxt = Path.Combine(options.OutputDirectory, "report.txt");
        string runtimeDirectory = await ResolveRuntimeDirectoryAsync(options.RuntimeDirectoryOverride).ConfigureAwait(false);

        Console.WriteLine("== Soak run ==");
        Console.WriteLine("runtime directory:   " + runtimeDirectory);
        Console.WriteLine("output directory:    " + options.OutputDirectory);
        Console.WriteLine("target duration:     " + options.Duration);
        Console.WriteLine("playback seconds:    " + options.PlaybackSeconds.ToString("0.0", CultureInfo.InvariantCulture));
        Console.WriteLine("idle seconds:        " + options.IdleSeconds.ToString("0.0", CultureInfo.InvariantCulture));
        Console.WriteLine("samples csv:         " + samplesCsv);

        string wavPath = Path.Combine(options.OutputDirectory, "soak-tone.wav");
        File.WriteAllBytes(wavPath, CreateSineWave(TimeSpan.FromSeconds(90)));

        string? mp4Path = TryGenerateTestMp4(runtimeDirectory, options.OutputDirectory);
        if (mp4Path == null)
        {
            Console.WriteLine("警告：找不到 ffmpeg.exe，video workload 將略過。");
        }
        else
        {
            Console.WriteLine("video 樣本：" + mp4Path);
        }

        List<SoakSample> samples = new List<SoakSample>(capacity: 2048);
        CancellationTokenSource cts = new CancellationTokenSource();
        Console.CancelKeyPress += delegate (object? sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            cts.Cancel();
            Console.Error.WriteLine("收到 Ctrl-C，將收尾並輸出目前分析。");
        };

        using (StreamWriter csv = new StreamWriter(samplesCsv, append: false, Encoding.UTF8))
        {
            WriteCsvHeader(csv);
            DateTime start = DateTime.UtcNow;
            DateTime deadline = start + options.Duration;
            int iteration = 0;
            while (!cts.IsCancellationRequested && DateTime.UtcNow < deadline)
            {
                SoakWorkload workload = PickWorkload(iteration, mp4Path != null);
                SoakSample sample = await RunIterationAsync(
                    runtimeDirectory,
                    wavPath,
                    mp4Path,
                    workload,
                    iteration,
                    start,
                    options,
                    cts.Token).ConfigureAwait(false);
                samples.Add(sample);
                WriteCsvRow(csv, sample);
                csv.Flush();
                if (iteration % 10 == 0)
                {
                    Console.WriteLine(FormatProgressLine(sample, deadline));
                }

                iteration++;
                if (cts.IsCancellationRequested || DateTime.UtcNow >= deadline)
                {
                    break;
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(options.IdleSeconds), cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        try { File.Delete(wavPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }

        SoakReport report = SoakAnalyzer.Analyze(samples, options);
        File.WriteAllText(reportTxt, report.Text, new UTF8Encoding(true));
        Console.WriteLine();
        Console.WriteLine(report.Text);
        Console.WriteLine("samples csv: " + samplesCsv);
        Console.WriteLine("report file: " + reportTxt);

        return report.AllPassed ? 0 : 1;
    }

    private static async Task<SoakSample> RunIterationAsync(
        string runtimeDirectory,
        string wavPath,
        string? mp4Path,
        SoakWorkload workload,
        int iteration,
        DateTime start,
        SoakOptions options,
        CancellationToken cancellationToken)
    {
        string? errorMessage = null;
        bool reachedTarget = false;
        double observedSeconds = 0;
        try
        {
            using (MpvPlayer player = CreatePlayer(runtimeDirectory, loopFile: workload != SoakWorkload.Cancel))
            {
                player.Initialize();

                // 覆蓋 WatchProperty callback path：每 iter subscribe + dispose。
                IDisposable timePosSub = player.WatchProperty<double>("time-pos").Subscribe(new NoopObserver<double>());
                IDisposable pauseSub = player.WatchProperty<bool>("pause").Subscribe(new NoopObserver<bool>());

                try
                {
                    string mediaPath = workload switch
                    {
                        SoakWorkload.Mp4 when mp4Path != null => mp4Path,
                        _ => wavPath,
                    };
                    player.LoadFile(mediaPath);

                    if (workload == SoakWorkload.Cancel)
                    {
                        // cancel-in-flight：LoadFile 後立刻 Stop，覆蓋 cancel / abort 路徑。
                        try { player.Stop(); } catch (MpvException) { /* expected race */ }
                        reachedTarget = true;
                    }
                    else
                    {
                        Stopwatch sw = Stopwatch.StartNew();
                        while (sw.Elapsed.TotalSeconds < options.PlaybackSeconds)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                break;
                            }

                            try
                            {
                                observedSeconds = player.GetPropertyDouble("time-pos");

                                // 每 ~1 秒讀一次多項 property，覆蓋 property cache / event 通路。
                                if (((int)sw.Elapsed.TotalMilliseconds) % 1000 < 220)
                                {
                                    _ = player.GetPropertyDouble("duration");
                                    _ = player.GetPropertyString("audio-codec");
                                    _ = player.GetPropertyString("video-codec");
                                    _ = player.GetPropertyFlag("pause");
                                }
                            }
                            catch (MpvException)
                            {
                                // 部分屬性在 file-loaded 前不可用，忽略。
                            }

                            try
                            {
                                await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false);
                            }
                            catch (OperationCanceledException)
                            {
                                break;
                            }
                        }

                        reachedTarget = sw.Elapsed.TotalSeconds >= options.PlaybackSeconds;
                        player.Stop();
                    }
                }
                finally
                {
                    timePosSub.Dispose();
                    pauseSub.Dispose();
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            errorMessage = ex.GetType().Name + ": " + ex.Message;
        }

        // 強制 full GC + finalizer 收尾後取樣，避免 GC 噪音被當成成長。
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long gcHeap = GC.GetTotalMemory(forceFullCollection: true);
        long workingSet;
        long privateMemory;
        int handleCount;
        using (Process process = Process.GetCurrentProcess())
        {
            process.Refresh();
            workingSet = process.WorkingSet64;
            privateMemory = process.PrivateMemorySize64;
            handleCount = process.HandleCount;
        }

        TimeSpan elapsed = DateTime.UtcNow - start;
        return new SoakSample(
            Iteration: iteration,
            ElapsedSeconds: elapsed.TotalSeconds,
            Workload: workload,
            GcHeapBytes: gcHeap,
            WorkingSetBytes: workingSet,
            PrivateMemoryBytes: privateMemory,
            HandleCount: handleCount,
            PlaybackReachedTarget: reachedTarget,
            ObservedPlaybackSeconds: observedSeconds,
            ErrorMessage: errorMessage);
    }

    private static SoakWorkload PickWorkload(int iteration, bool mp4Available)
    {
        // 三路循環：WAV → MP4 → Cancel。若無 MP4 則退化為 WAV / Cancel 兩路。
        int mod = mp4Available ? 3 : 2;
        int slot = iteration % mod;
        if (mp4Available)
        {
            return slot switch
            {
                0 => SoakWorkload.Wav,
                1 => SoakWorkload.Mp4,
                _ => SoakWorkload.Cancel,
            };
        }

        return slot == 0 ? SoakWorkload.Wav : SoakWorkload.Cancel;
    }

    private static string? TryGenerateTestMp4(string runtimeDirectory, string outputDirectory)
    {
        string ffmpegPath = Path.Combine(runtimeDirectory, "ffmpeg.exe");
        if (!File.Exists(ffmpegPath))
        {
            return null;
        }

        string mp4Path = Path.Combine(outputDirectory, "soak-video.mp4");
        if (File.Exists(mp4Path))
        {
            try { File.Delete(mp4Path); } catch (IOException) { return mp4Path; } catch (UnauthorizedAccessException) { return mp4Path; }
        }

        ProcessStartInfo psi = new ProcessStartInfo(ffmpegPath)
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-y");
        psi.ArgumentList.Add("-loglevel");
        psi.ArgumentList.Add("error");
        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add("lavfi");
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add("testsrc=duration=5:size=320x240:rate=30");
        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add("lavfi");
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add("sine=frequency=440:duration=5");
        psi.ArgumentList.Add("-c:v");
        psi.ArgumentList.Add("libx264");
        psi.ArgumentList.Add("-preset");
        psi.ArgumentList.Add("ultrafast");
        psi.ArgumentList.Add("-tune");
        psi.ArgumentList.Add("zerolatency");
        psi.ArgumentList.Add("-pix_fmt");
        psi.ArgumentList.Add("yuv420p");
        psi.ArgumentList.Add("-c:a");
        psi.ArgumentList.Add("aac");
        psi.ArgumentList.Add("-shortest");
        psi.ArgumentList.Add(mp4Path);

        using (Process? process = Process.Start(psi))
        {
            if (process == null)
            {
                return null;
            }

            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit(60000);
            if (process.ExitCode != 0 || !File.Exists(mp4Path))
            {
                Console.Error.WriteLine("ffmpeg 產生測試 MP4 失敗（exit=" + process.ExitCode + "）：" + stderr);
                return null;
            }
        }

        return mp4Path;
    }

    private sealed class NoopObserver<T> : IObserver<T>
    {
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(T value) { }
    }

    private static MpvPlayer CreatePlayer(string runtimeDirectory, bool loopFile = false)
    {
        MpvPlayerOptions options = MpvRuntimeInstaller.CreatePlayerOptions(runtimeDirectory);
        options.EnableYtdlp = false;
        options.EnableOsc = false;
        options.EnableKeyboardInput = false;
        options.EnableDefaultInputBindings = false;
        options.LogLevel = "warn";
        options.InitialOptions["vo"] = "null";
        options.InitialOptions["ao"] = "null";
        options.InitialOptions["terminal"] = "no";
        options.InitialOptions["idle"] = "yes";
        options.InitialOptions["keep-open"] = "no";
        if (loopFile)
        {
            options.InitialOptions["loop-file"] = "inf";
        }

        return new MpvPlayer(options);
    }

    private static async Task<string> ResolveRuntimeDirectoryAsync(string? overridePath)
    {
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            string resolved = Path.GetFullPath(overridePath!);
            EnsureLibMpvExists(resolved);
            return resolved;
        }

        string? configured = Environment.GetEnvironmentVariable("MEDIAEMBEDKIT_MPV_RUNTIME_DIR");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            string resolved = Path.GetFullPath(configured!);
            EnsureLibMpvExists(resolved);
            return resolved;
        }

        string runtimeDirectory = Path.Combine(AppContext.BaseDirectory, "runtime");
        string libraryPath = Path.Combine(runtimeDirectory, MpvLibraryLoader.GetDefaultLibraryFileName());
        if (!File.Exists(libraryPath))
        {
            MpvRuntimeInstallOptions opts = new MpvRuntimeInstallOptions();
            opts.Windows.IncludeYtDlp = false;
            opts.Windows.IncludeDeno = false;
            opts.Windows.IncludeFFmpeg = false;
            opts.Windows.LoadLibMpv = false;
            MpvRuntimeInstallResult result = await MpvRuntimeInstaller.InstallOrUpdateAsync(runtimeDirectory, opts).ConfigureAwait(false);
            if (!result.IsSupported || string.IsNullOrWhiteSpace(result.LibMpvPath))
            {
                throw new InvalidOperationException("無法準備 Windows libmpv 執行階段：" + result.Message);
            }
        }

        EnsureLibMpvExists(runtimeDirectory);
        return runtimeDirectory;
    }

    private static void EnsureLibMpvExists(string runtimeDirectory)
    {
        string libraryPath = Path.Combine(runtimeDirectory, MpvLibraryLoader.GetDefaultLibraryFileName());
        if (!File.Exists(libraryPath))
        {
            throw new FileNotFoundException("找不到 libmpv：" + libraryPath, libraryPath);
        }
    }

    private static void WriteCsvHeader(StreamWriter writer)
    {
        writer.WriteLine("iteration,elapsed_seconds,workload,gc_heap_bytes,working_set_bytes,private_memory_bytes,handle_count,playback_reached,observed_playback_seconds,error");
    }

    private static void WriteCsvRow(StreamWriter writer, SoakSample sample)
    {
        writer.Write(sample.Iteration.ToString(CultureInfo.InvariantCulture));
        writer.Write(',');
        writer.Write(sample.ElapsedSeconds.ToString("F3", CultureInfo.InvariantCulture));
        writer.Write(',');
        writer.Write(sample.Workload.ToString().ToLowerInvariant());
        writer.Write(',');
        writer.Write(sample.GcHeapBytes.ToString(CultureInfo.InvariantCulture));
        writer.Write(',');
        writer.Write(sample.WorkingSetBytes.ToString(CultureInfo.InvariantCulture));
        writer.Write(',');
        writer.Write(sample.PrivateMemoryBytes.ToString(CultureInfo.InvariantCulture));
        writer.Write(',');
        writer.Write(sample.HandleCount.ToString(CultureInfo.InvariantCulture));
        writer.Write(',');
        writer.Write(sample.PlaybackReachedTarget ? "1" : "0");
        writer.Write(',');
        writer.Write(sample.ObservedPlaybackSeconds.ToString("F3", CultureInfo.InvariantCulture));
        writer.Write(',');
        if (!string.IsNullOrEmpty(sample.ErrorMessage))
        {
            writer.Write('"');
            writer.Write(sample.ErrorMessage!.Replace("\"", "\"\""));
            writer.Write('"');
        }

        writer.WriteLine();
    }

    private static string FormatProgressLine(SoakSample sample, DateTime deadline)
    {
        TimeSpan remaining = deadline - DateTime.UtcNow;
        if (remaining < TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
        }

        StringBuilder sb = new StringBuilder();
        sb.Append("[soak] iter=");
        sb.Append(sample.Iteration);
        sb.Append(" elapsed=");
        sb.Append(TimeSpan.FromSeconds(sample.ElapsedSeconds).ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture));
        sb.Append(" remaining=");
        sb.Append(remaining.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture));
        sb.Append(" gc=");
        sb.Append(FormatBytes(sample.GcHeapBytes));
        sb.Append(" ws=");
        sb.Append(FormatBytes(sample.WorkingSetBytes));
        sb.Append(" handles=");
        sb.Append(sample.HandleCount);
        if (!string.IsNullOrEmpty(sample.ErrorMessage))
        {
            sb.Append(" err=");
            sb.Append(sample.ErrorMessage);
        }

        return sb.ToString();
    }

    internal static string FormatBytes(long bytes)
    {
        double value = bytes;
        string[] units = new[] { "B", "KiB", "MiB", "GiB" };
        int unitIndex = 0;
        while (value >= 1024.0 && unitIndex < units.Length - 1)
        {
            value /= 1024.0;
            unitIndex++;
        }

        return value.ToString("F1", CultureInfo.InvariantCulture) + " " + units[unitIndex];
    }

    private static byte[] CreateSineWave(TimeSpan duration)
    {
        int sampleCount = Math.Max(1, (int)(duration.TotalSeconds * SampleRate));
        short[] samples = new short[sampleCount];
        for (int index = 0; index < samples.Length; index++)
        {
            double angle = 2.0 * Math.PI * 440.0 * index / SampleRate;
            samples[index] = (short)(Math.Sin(angle) * short.MaxValue * 0.20);
        }

        using (MemoryStream stream = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(stream, Encoding.ASCII, true))
        {
            int dataLength = samples.Length * sizeof(short);
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataLength);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(SampleRate);
            writer.Write(SampleRate * sizeof(short));
            writer.Write((short)sizeof(short));
            writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataLength);
            for (int index = 0; index < samples.Length; index++)
            {
                writer.Write(samples[index]);
            }

            writer.Flush();
            return stream.ToArray();
        }
    }
}

internal enum SoakWorkload
{
    Wav,
    Mp4,
    Cancel,
}

internal sealed record SoakSample(
    int Iteration,
    double ElapsedSeconds,
    SoakWorkload Workload,
    long GcHeapBytes,
    long WorkingSetBytes,
    long PrivateMemoryBytes,
    int HandleCount,
    bool PlaybackReachedTarget,
    double ObservedPlaybackSeconds,
    string? ErrorMessage);

internal sealed class SoakOptions
{
    public TimeSpan Duration { get; init; } = TimeSpan.FromHours(24);
    public double PlaybackSeconds { get; init; } = 60.0;
    public double IdleSeconds { get; init; } = 5.0;
    public string OutputDirectory { get; init; } = string.Empty;
    public string? RuntimeDirectoryOverride { get; init; }
    public long GcHeapAbsoluteGrowthLimitBytes { get; init; } = 100L * 1024L * 1024L;
    public long WorkingSetAbsoluteGrowthLimitBytes { get; init; } = 300L * 1024L * 1024L;
    public int HandleCountAbsoluteGrowthLimit { get; init; } = 100;
    public int WarmupIterations { get; init; } = 10;
    public bool ShowHelp { get; init; }

    public static SoakOptions Parse(string[] args)
    {
        double hours = 24.0;
        double playbackSeconds = 60.0;
        double idleSeconds = 5.0;
        string outputDirectory = "";
        string? runtimeDirectoryOverride = null;
        long gcLimit = 100L * 1024L * 1024L;
        long wsLimit = 300L * 1024L * 1024L;
        int handleLimit = 100;
        int warmup = 10;
        bool help = false;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "--hours":
                    hours = double.Parse(RequireValue(args, ref i, arg), CultureInfo.InvariantCulture);
                    break;
                case "--playback-seconds":
                    playbackSeconds = double.Parse(RequireValue(args, ref i, arg), CultureInfo.InvariantCulture);
                    break;
                case "--idle-seconds":
                    idleSeconds = double.Parse(RequireValue(args, ref i, arg), CultureInfo.InvariantCulture);
                    break;
                case "--output-dir":
                    outputDirectory = RequireValue(args, ref i, arg);
                    break;
                case "--runtime-directory":
                    runtimeDirectoryOverride = RequireValue(args, ref i, arg);
                    break;
                case "--gc-heap-growth-mb":
                    gcLimit = (long)(double.Parse(RequireValue(args, ref i, arg), CultureInfo.InvariantCulture) * 1024.0 * 1024.0);
                    break;
                case "--working-set-growth-mb":
                    wsLimit = (long)(double.Parse(RequireValue(args, ref i, arg), CultureInfo.InvariantCulture) * 1024.0 * 1024.0);
                    break;
                case "--handle-growth":
                    handleLimit = int.Parse(RequireValue(args, ref i, arg), CultureInfo.InvariantCulture);
                    break;
                case "--warmup-iterations":
                    warmup = int.Parse(RequireValue(args, ref i, arg), CultureInfo.InvariantCulture);
                    break;
                case "--help":
                case "-h":
                case "/?":
                    help = true;
                    break;
                default:
                    throw new ArgumentException("未知引數：" + arg);
            }
        }

        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            outputDirectory = Path.Combine(
                Directory.GetCurrentDirectory(),
                ".tmp",
                "soak-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
        }

        return new SoakOptions
        {
            Duration = TimeSpan.FromHours(hours),
            PlaybackSeconds = playbackSeconds,
            IdleSeconds = idleSeconds,
            OutputDirectory = outputDirectory,
            RuntimeDirectoryOverride = runtimeDirectoryOverride,
            GcHeapAbsoluteGrowthLimitBytes = gcLimit,
            WorkingSetAbsoluteGrowthLimitBytes = wsLimit,
            HandleCountAbsoluteGrowthLimit = handleLimit,
            WarmupIterations = warmup,
            ShowHelp = help,
        };
    }

    private static string RequireValue(string[] args, ref int index, string flag)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException(flag + " 需要值。");
        }

        index++;
        return args[index];
    }

    public static void PrintUsage(TextWriter writer)
    {
        writer.WriteLine("MediaEmbedKit.Mpv.SoakTests — 連續播放 soak harness");
        writer.WriteLine();
        writer.WriteLine("用法：");
        writer.WriteLine("  dotnet run --project tests/MediaEmbedKit.Mpv.SoakTests -- [選項]");
        writer.WriteLine();
        writer.WriteLine("選項：");
        writer.WriteLine("  --hours <h>                  總跑多久（小時，預設 24）");
        writer.WriteLine("  --playback-seconds <s>       每回合播放秒數（預設 60）");
        writer.WriteLine("  --idle-seconds <s>           回合間 idle（預設 5）");
        writer.WriteLine("  --output-dir <path>          樣本與報告輸出資料夾（預設 .tmp/soak-{時戳}/）");
        writer.WriteLine("  --runtime-directory <path>   指定執行階段資料夾（覆寫環境變數）");
        writer.WriteLine("  --gc-heap-growth-mb <mb>     GC heap 整段（--hours）累計成長上限（MB，預設 100）");
        writer.WriteLine("  --working-set-growth-mb <mb> Working Set 整段累計成長上限（MB，預設 300）");
        writer.WriteLine("  --handle-growth <n>          Handle Count 整段累計成長上限（預設 100）");
        writer.WriteLine("  --warmup-iterations <n>      回歸分析跳過前 n 回合作為 warmup（預設 10）");
        writer.WriteLine("  --help                       顯示此說明");
    }
}

internal sealed record SoakReport(string Text, bool AllPassed);

internal static class SoakAnalyzer
{
    public static SoakReport Analyze(IReadOnlyList<SoakSample> samples, SoakOptions options)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("== Soak 分析 ==");
        sb.AppendLine("樣本數：" + samples.Count);

        if (samples.Count == 0)
        {
            sb.AppendLine("沒有取得任何樣本，無法分析。");
            return new SoakReport(sb.ToString(), false);
        }

        int errors = 0;
        foreach (SoakSample s in samples)
        {
            if (!string.IsNullOrEmpty(s.ErrorMessage))
            {
                errors++;
            }
        }

        sb.AppendLine("錯誤回合數：" + errors);

        SoakSample first = samples[0];
        SoakSample last = samples[samples.Count - 1];
        TimeSpan totalElapsed = TimeSpan.FromSeconds(last.ElapsedSeconds - first.ElapsedSeconds);
        sb.AppendLine("實際時長：" + totalElapsed);

        if (samples.Count <= options.WarmupIterations + 2)
        {
            sb.AppendLine("樣本不足以做有意義回歸（需 warmup + 至少 2 個樣本）。僅輸出 baseline / 終點數值，無法判斷洩漏。");
            sb.AppendLine();
            AppendSummary(sb, "GC heap", first.GcHeapBytes, last.GcHeapBytes);
            AppendSummary(sb, "Working Set", first.WorkingSetBytes, last.WorkingSetBytes);
            AppendSummary(sb, "Private Memory", first.PrivateMemoryBytes, last.PrivateMemoryBytes);
            AppendSummary(sb, "Handle Count", first.HandleCount, last.HandleCount);
            return new SoakReport(sb.ToString(), false);
        }

        // 跳過 warmup 後做線性回歸。
        List<SoakSample> analysisSet = new List<SoakSample>(samples.Count);
        for (int i = options.WarmupIterations; i < samples.Count; i++)
        {
            if (string.IsNullOrEmpty(samples[i].ErrorMessage))
            {
                analysisSet.Add(samples[i]);
            }
        }

        if (analysisSet.Count < 2)
        {
            sb.AppendLine("warmup 之後沒有足夠成功樣本可分析。");
            return new SoakReport(sb.ToString(), false);
        }

        double baseSeconds = analysisSet[0].ElapsedSeconds;
        double endSeconds = analysisSet[analysisSet.Count - 1].ElapsedSeconds;
        double analyzedSpanSeconds = endSeconds - baseSeconds;
        double targetSpanSeconds = options.Duration.TotalSeconds;

        LinearRegression gcReg = LinearRegression.Compute(analysisSet, baseSeconds, s => s.GcHeapBytes);
        LinearRegression wsReg = LinearRegression.Compute(analysisSet, baseSeconds, s => s.WorkingSetBytes);
        LinearRegression pmReg = LinearRegression.Compute(analysisSet, baseSeconds, s => s.PrivateMemoryBytes);
        LinearRegression hcReg = LinearRegression.Compute(analysisSet, baseSeconds, s => s.HandleCount);

        long projectedGcGrowth = (long)(gcReg.SlopePerSecond * targetSpanSeconds);
        long projectedWsGrowth = (long)(wsReg.SlopePerSecond * targetSpanSeconds);
        long projectedPmGrowth = (long)(pmReg.SlopePerSecond * targetSpanSeconds);
        long projectedHandleGrowth = (long)(hcReg.SlopePerSecond * targetSpanSeconds);

        // Mann-Kendall 趨勢檢定（無母數），α=0.05 → |Z| > 1.96 即顯著趨勢。
        MannKendallResult gcMk = MannKendall.Compute(analysisSet, s => s.GcHeapBytes);
        MannKendallResult wsMk = MannKendall.Compute(analysisSet, s => s.WorkingSetBytes);
        MannKendallResult pmMk = MannKendall.Compute(analysisSet, s => s.PrivateMemoryBytes);
        MannKendallResult hcMk = MannKendall.Compute(analysisSet, s => s.HandleCount);

        // 工作負載分布。
        int wavCount = 0;
        int mp4Count = 0;
        int cancelCount = 0;
        foreach (SoakSample s in samples)
        {
            switch (s.Workload)
            {
                case SoakWorkload.Wav: wavCount++; break;
                case SoakWorkload.Mp4: mp4Count++; break;
                case SoakWorkload.Cancel: cancelCount++; break;
            }
        }

        bool gcPass = Math.Abs(projectedGcGrowth) <= options.GcHeapAbsoluteGrowthLimitBytes;
        bool wsPass = Math.Abs(projectedWsGrowth) <= options.WorkingSetAbsoluteGrowthLimitBytes;
        bool handlePass = Math.Abs(projectedHandleGrowth) <= options.HandleCountAbsoluteGrowthLimit;
        // MK 是趨勢檢定但對 magnitude 無感（1 byte/iter 累積也判顯著）。
        // 業界做法：gate 仍以絕對量門檻為準；MK 純資訊用，用來描述「上升 / 平穩 / 下降」。
        bool gcMkPass = gcMk.ZScore <= 1.96;
        bool wsMkPass = wsMk.ZScore <= 1.96;
        bool hcMkPass = hcMk.ZScore <= 1.96;
        bool allPass = gcPass && wsPass && handlePass && errors == 0;

        sb.AppendLine("warmup 跳過：" + options.WarmupIterations);
        sb.AppendLine("回歸樣本數：" + analysisSet.Count);
        sb.AppendLine("回歸區間：" + TimeSpan.FromSeconds(analyzedSpanSeconds));
        sb.AppendLine(CultureInfo.InvariantCulture, $"工作負載分布：wav={wavCount} mp4={mp4Count} cancel={cancelCount}");
        sb.AppendLine();
        AppendRegression(sb, "GC heap", first.GcHeapBytes, last.GcHeapBytes, gcReg, targetSpanSeconds, projectedGcGrowth, options.GcHeapAbsoluteGrowthLimitBytes, gcPass, gcMk, gcMkPass);
        AppendRegression(sb, "Working Set", first.WorkingSetBytes, last.WorkingSetBytes, wsReg, targetSpanSeconds, projectedWsGrowth, options.WorkingSetAbsoluteGrowthLimitBytes, wsPass, wsMk, wsMkPass);
        AppendRegression(sb, "Private Memory", first.PrivateMemoryBytes, last.PrivateMemoryBytes, pmReg, targetSpanSeconds, projectedPmGrowth, 0, true, pmMk, true);
        AppendHandleRegression(sb, first.HandleCount, last.HandleCount, hcReg, targetSpanSeconds, projectedHandleGrowth, options.HandleCountAbsoluteGrowthLimit, handlePass, hcMk, hcMkPass);

        sb.AppendLine();
        sb.AppendLine("註：MK z-score 為趨勢方向描述（純資訊），實際 pass/fail 以絕對量門檻為準。");
        sb.AppendLine(allPass ? "結論：PASS" : "結論：FAIL（任一項超出絕對量門檻或有錯誤回合）");

        return new SoakReport(sb.ToString(), allPass);
    }

    private static void AppendSummary(StringBuilder sb, string name, long baseline, long end)
    {
        long delta = end - baseline;
        sb.Append(name);
        sb.Append("：baseline=");
        sb.Append(Program.FormatBytes(baseline));
        sb.Append(" end=");
        sb.Append(Program.FormatBytes(end));
        sb.Append(" delta=");
        sb.AppendLine(Program.FormatBytes(delta));
    }

    private static void AppendSummary(StringBuilder sb, string name, int baseline, int end)
    {
        int delta = end - baseline;
        sb.AppendFormat(CultureInfo.InvariantCulture, "{0}：baseline={1} end={2} delta={3}", name, baseline, end, delta);
        sb.AppendLine();
    }

    private static void AppendRegression(
        StringBuilder sb,
        string name,
        long baseline,
        long end,
        LinearRegression regression,
        double targetSpanSeconds,
        long projectedGrowth,
        long limit,
        bool pass,
        MannKendallResult mk,
        bool mkPass)
    {
        sb.Append(name).Append("：");
        sb.Append("baseline=").Append(Program.FormatBytes(baseline));
        sb.Append(" end=").Append(Program.FormatBytes(end));
        sb.Append(" slope=");
        sb.Append((regression.SlopePerSecond * 3600.0).ToString("F0", CultureInfo.InvariantCulture));
        sb.Append(" B/hr");
        sb.Append(" 整段投射=");
        sb.Append((projectedGrowth >= 0 ? "+" : "") + Program.FormatBytes(projectedGrowth));
        if (limit > 0)
        {
            sb.Append(" 門檻=±").Append(Program.FormatBytes(limit));
            sb.Append(' ').Append(pass ? "PASS" : "FAIL");
        }
        else
        {
            sb.Append(" (僅參考)");
        }

        sb.Append(" MK z=").Append(mk.ZScore.ToString("F2", CultureInfo.InvariantCulture));
        sb.Append(' ').Append(DescribeMk(mk.ZScore));
        sb.AppendLine();
    }

    private static void AppendHandleRegression(
        StringBuilder sb,
        int baseline,
        int end,
        LinearRegression regression,
        double targetSpanSeconds,
        long projectedGrowth,
        int limit,
        bool pass,
        MannKendallResult mk,
        bool mkPass)
    {
        sb.AppendFormat(
            CultureInfo.InvariantCulture,
            "Handle Count：baseline={0} end={1} slope={2:F2}/hr 整段投射={3:+#;-#;0} 門檻=±{4} {5} MK z={6:F2} {7}",
            baseline,
            end,
            regression.SlopePerSecond * 3600.0,
            projectedGrowth,
            limit,
            pass ? "PASS" : "FAIL",
            mk.ZScore,
            DescribeMk(mk.ZScore));
        sb.AppendLine();
    }

    private static string DescribeMk(double zScore)
    {
        if (zScore > 1.96) return "(MK 上升顯著)";
        if (zScore < -1.96) return "(MK 下降顯著)";
        return "(MK 平穩)";
    }
}

internal sealed record MannKendallResult(double S, double Variance, double ZScore);

internal static class MannKendall
{
    /// <summary>
    /// 對序列做 Mann-Kendall 趨勢檢定（無母數）。
    /// </summary>
    /// <remarks>
    /// S = Σ_{i&lt;j} sign(y_j - y_i)；Var(S) = n(n-1)(2n+5)/18；
    /// Z = (S - sign(S)) / sqrt(Var(S))。|Z| &gt; 1.96 表示在 α=0.05 顯著趨勢。
    /// 正 Z 表示上升、負 Z 表示下降。樣本數 &lt; 10 時忽略連續性修正以免分母過小。
    /// </remarks>
    public static MannKendallResult Compute(IReadOnlyList<SoakSample> samples, Func<SoakSample, double> selector)
    {
        int n = samples.Count;
        if (n < 3)
        {
            return new MannKendallResult(0, 0, 0);
        }

        long s = 0;
        for (int i = 0; i < n - 1; i++)
        {
            double yi = selector(samples[i]);
            for (int j = i + 1; j < n; j++)
            {
                double yj = selector(samples[j]);
                if (yj > yi) s++;
                else if (yj < yi) s--;
            }
        }

        double variance = (double)n * (n - 1) * (2 * n + 5) / 18.0;
        if (variance <= 0)
        {
            return new MannKendallResult(s, 0, 0);
        }

        double zScore;
        if (s > 0)
        {
            zScore = (s - 1) / Math.Sqrt(variance);
        }
        else if (s < 0)
        {
            zScore = (s + 1) / Math.Sqrt(variance);
        }
        else
        {
            zScore = 0;
        }

        return new MannKendallResult(s, variance, zScore);
    }
}

internal sealed record LinearRegression(double SlopePerSecond, double Intercept)
{
    public static LinearRegression Compute(IReadOnlyList<SoakSample> samples, double baseSeconds, Func<SoakSample, double> selector)
    {
        int n = samples.Count;
        if (n < 2)
        {
            return new LinearRegression(0, 0);
        }

        double sumX = 0;
        double sumY = 0;
        double sumXy = 0;
        double sumXx = 0;
        for (int i = 0; i < n; i++)
        {
            double x = samples[i].ElapsedSeconds - baseSeconds;
            double y = selector(samples[i]);
            sumX += x;
            sumY += y;
            sumXy += x * y;
            sumXx += x * x;
        }

        double denominator = n * sumXx - sumX * sumX;
        if (denominator == 0)
        {
            return new LinearRegression(0, sumY / n);
        }

        double slope = (n * sumXy - sumX * sumY) / denominator;
        double intercept = (sumY - slope * sumX) / n;
        return new LinearRegression(slope, intercept);
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaEmbedKit.Mpv.Downloads;

namespace MediaEmbedKit.Mpv.StressTests;

/// <summary>
/// 執行第一階段自動化壓力測試。
/// </summary>
internal static class Program
{
    /// <summary>
    /// 播放器重複建立與釋放次數。
    /// </summary>
    private const int PlayerLifecycleIterations = 12;
    /// <summary>
    /// 長時間 leak 檢查中重複建立／釋放 player 的回合數。
    /// </summary>
    private const int LongRunLeakIterations = 50;
    /// <summary>
    /// 長時間 leak 檢查可容忍的相對成長百分比（依 baseline GC heap）。
    /// </summary>
    private const double LongRunLeakRelativeGrowthLimit = 0.25;
    /// <summary>
    /// 長時間 leak 檢查可容忍的絕對成長下限（位元組）；小於此值的成長視為 GC 噪音，不算 leak。
    /// </summary>
    private const long LongRunLeakAbsoluteGrowthFloorBytes = 8L * 1024L * 1024L;

    /// <summary>
    /// 自訂串流重複播放次數。
    /// </summary>
    private const int StreamPlaybackIterations = 8;

    /// <summary>
    /// 串流取消重複測試次數。
    /// </summary>
    private const int StreamCancellationIterations = 4;

    /// <summary>
    /// 壓力測試進入點。
    /// </summary>
    /// <param name="args">命令列引數；目前未使用。</param>
    /// <returns>全部測試通過時傳回 0。</returns>
    private static async Task<int> Main(string[] args)
    {
        _ = args;
        string runtimeDirectory = await RuntimeResolver.ResolveAsync().ConfigureAwait(false);
        Console.WriteLine("使用執行階段資料夾：" + runtimeDirectory);

        StressTestRunner runner = new StressTestRunner();
        runner.Add("播放器重複建立與釋放", delegate
        {
            return VerifyRepeatedCreateDisposeAsync(runtimeDirectory);
        });
        runner.Add("多 client 重複建立與 shutdown", delegate
        {
            return VerifyRepeatedClientLifecycleAsync(runtimeDirectory);
        });
        runner.Add("自訂 stream callback 重複播放", delegate
        {
            return VerifyRepeatedStreamPlaybackAsync(runtimeDirectory);
        });
        runner.Add("stream callback 重複取消", delegate
        {
            return VerifyRepeatedStreamCancellationAsync(runtimeDirectory);
        });
        runner.Add("外部工具大量輸出與逾時", VerifyExternalToolOutputAndTimeoutAsync);
        runner.Add("runtime helper 失敗與已載入更新路徑", VerifyRuntimeHelperFailurePathsAsync);
        runner.Add("播放器長時間建立／釋放記憶體 leak 檢查", delegate
        {
            return VerifyLongRunMemoryLeakAsync(runtimeDirectory);
        });
        runner.Add("WatchProperty 跨 thread 同時訂閱／取消", delegate
        {
            return VerifyWatchPropertyConcurrentAsync(runtimeDirectory);
        });

        await runner.RunAsync().ConfigureAwait(false);
        return runner.FailedCount == 0 ? 0 : 1;
    }

    /// <summary>
    /// 對同一 player 同屬性同時開多個訂閱／取消執行緒，驗證 WatchProperty 內部
    /// 觀察者註冊機制在並發場景下不擲未處理例外、不卡死。
    /// </summary>
    /// <param name="runtimeDirectory">包含 libmpv 的執行階段資料夾。</param>
    /// <returns>代表測試流程的工作。</returns>
    private static async Task VerifyWatchPropertyConcurrentAsync(string runtimeDirectory)
    {
        using (MpvPlayer player = CreatePlayer(runtimeDirectory))
        {
            player.Initialize();

            const int subscribeThreads = 4;
            const int iterationsPerThread = 50;
            using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            {
                Task[] tasks = new Task[subscribeThreads];
                for (int threadIndex = 0; threadIndex < subscribeThreads; threadIndex++)
                {
                    tasks[threadIndex] = Task.Run(() =>
                    {
                        for (int iteration = 0; iteration < iterationsPerThread; iteration++)
                        {
                            if (cts.IsCancellationRequested)
                            {
                                return;
                            }

                            IObservable<double> obs = player.WatchProperty<double>("time-pos");
                            IDisposable subscription = obs.Subscribe(new NoopObserver<double>());
                            // 立即取消模擬「短訂閱壽命」競爭情境
                            subscription.Dispose();
                        }
                    }, cts.Token);
                }

                await Task.WhenAll(tasks).ConfigureAwait(false);
            }

            // 再做一個長壽訂閱確認 player 仍可用
            IDisposable finalSub = player.WatchProperty<double>("time-pos").Subscribe(new NoopObserver<double>());
            finalSub.Dispose();
            StressAssert.True(player.IsInitialized, "並發測試後 player 仍應可用。");
        }
    }

    /// <summary>
    /// 長時間反覆建立／釋放播放器並追蹤受控堆積成長，作為簡易 leak 哨兵。
    /// 失敗條件：經過 <see cref="LongRunLeakIterations"/> 次後，相對 baseline 的成長
    /// 超過 <see cref="LongRunLeakRelativeGrowthLimit"/> 且絕對成長大於
    /// <see cref="LongRunLeakAbsoluteGrowthFloorBytes"/>（避免把 GC 噪音當 leak）。
    /// </summary>
    /// <param name="runtimeDirectory">包含 libmpv 的執行階段資料夾。</param>
    /// <returns>代表測試流程的工作。</returns>
    private static Task VerifyLongRunMemoryLeakAsync(string runtimeDirectory)
    {
        // Warm-up：先跑 3 次建立／釋放讓 JIT、libmpv 內部 cache 等到達穩態。
        for (int warmup = 0; warmup < 3; warmup++)
        {
            using (MpvPlayer warm = CreatePlayer(runtimeDirectory))
            {
                warm.Initialize();
            }
        }

        // Baseline 取樣（強制完整 GC）。
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long baselineBytes = GC.GetTotalMemory(forceFullCollection: true);

        // 寫一段測試 WAV 給 LoadFile + WatchProperty 路徑使用。
        string wavPath = Path.Combine(Path.GetTempPath(), "leak-probe-" + Guid.NewGuid().ToString("N") + ".wav");
        File.WriteAllBytes(wavPath, WaveGenerator.CreateSineWave(TimeSpan.FromMilliseconds(500)));
        try
        {
            for (int iteration = 0; iteration < LongRunLeakIterations; iteration++)
            {
                using (MpvPlayer player = CreatePlayer(runtimeDirectory))
                {
                    player.Initialize();
                    StressAssert.True(player.IsInitialized, "iteration " + iteration + " 應完成初始化。");

                    // 觸發若干屬性讀取，確保 property 路徑也被覆蓋。
                    _ = player.GetPropertyString("mpv-version");
                    _ = player.GetPropertyDouble("volume");

                    // 加 WatchProperty 訂閱 / 取消，涵蓋 IObservable 路徑。
                    IDisposable timePosSub = player.WatchProperty<double>("time-pos").Subscribe(new NoopObserver<double>());
                    IDisposable pauseSub = player.WatchProperty<bool>("pause").Subscribe(new NoopObserver<bool>());

                    // LoadFile + 短暫播放 + Stop，覆蓋媒體生命週期路徑。
                    player.LoadFile(wavPath);
                    // 不 await 完整播放（會慢），只允許進入 file-loaded 狀態。
                    player.Stop();

                    timePosSub.Dispose();
                    pauseSub.Dispose();
                }
            }
        }
        finally
        {
            try { File.Delete(wavPath); } catch (IOException) { }
        }

        // 終點取樣。
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long endBytes = GC.GetTotalMemory(forceFullCollection: true);

        long growthBytes = endBytes - baselineBytes;
        double growthRelative = baselineBytes <= 0 ? 0 : (double)growthBytes / baselineBytes;
        Console.WriteLine(
            "[leak] baseline=" + baselineBytes
            + " end=" + endBytes
            + " growth=" + growthBytes
            + " relative=" + growthRelative.ToString("P1", System.Globalization.CultureInfo.InvariantCulture));

        bool relativeOver = growthRelative > LongRunLeakRelativeGrowthLimit;
        bool absoluteOver = growthBytes > LongRunLeakAbsoluteGrowthFloorBytes;
        StressAssert.True(
            !(relativeOver && absoluteOver),
            "受控堆積在 " + LongRunLeakIterations + " 次 build/dispose 後同時超出相對與絕對門檻；"
            + "growth=" + growthBytes + " bytes ("
            + growthRelative.ToString("P1", System.Globalization.CultureInfo.InvariantCulture) + ")");

        return Task.CompletedTask;
    }

    /// <summary>
    /// 驗證播放器可重複建立、初始化與釋放。
    /// </summary>
    /// <param name="runtimeDirectory">包含 libmpv 的執行階段資料夾。</param>
    /// <returns>代表測試流程的工作。</returns>
    private static Task VerifyRepeatedCreateDisposeAsync(string runtimeDirectory)
    {
        for (int iteration = 0; iteration < PlayerLifecycleIterations; iteration++)
        {
            using (MpvPlayer player = CreatePlayer(runtimeDirectory))
            {
                player.Initialize();
                StressAssert.True(player.IsInitialized, "播放器應完成初始化。");
                string? version = player.GetPropertyString("mpv-version");
                StressAssert.True(!string.IsNullOrWhiteSpace(version), "應可讀取 mpv-version。");
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 驗證額外 client 與 shutdown 可重複建立和釋放。
    /// </summary>
    /// <param name="runtimeDirectory">包含 libmpv 的執行階段資料夾。</param>
    /// <returns>代表測試流程的工作。</returns>
    private static async Task VerifyRepeatedClientLifecycleAsync(string runtimeDirectory)
    {
        for (int iteration = 0; iteration < PlayerLifecycleIterations; iteration++)
        {
            using (MpvPlayer player = CreatePlayer(runtimeDirectory))
            {
                TaskCompletionSource<bool> shutdown = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                player.Shutdown += delegate
                {
                    shutdown.TrySetResult(true);
                };

                player.Initialize();
                using (MpvClientHandle strongClient = player.CreateClient("mediaembedkit-stress-strong-" + iteration.ToString(CultureInfo.InvariantCulture)))
                using (MpvClientHandle weakClient = player.CreateWeakClient("mediaembedkit-stress-weak-" + iteration.ToString(CultureInfo.InvariantCulture)))
                {
                    StressAssert.True(strongClient.DangerousHandle != IntPtr.Zero, "強參考 client 不可為零。");
                    StressAssert.True(weakClient.DangerousHandle != IntPtr.Zero, "弱參考 client 不可為零。");
                }

                IntPtr rawClient = player.CreateClientHandle("mediaembedkit-stress-raw-" + iteration.ToString(CultureInfo.InvariantCulture));
                try
                {
                    StressAssert.True(rawClient != IntPtr.Zero, "原生 client 不可為零。");
                }
                finally
                {
                    MpvPlayer.DestroyClientHandle(rawClient);
                }

                player.Quit();
                await StressAssert.WaitAsync(shutdown.Task, "等待 shutdown 事件逾時。").ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// 驗證自訂 stream callback 可重複載入並正常結束。
    /// </summary>
    /// <param name="runtimeDirectory">包含 libmpv 的執行階段資料夾。</param>
    /// <returns>代表測試流程的工作。</returns>
    private static async Task VerifyRepeatedStreamPlaybackAsync(string runtimeDirectory)
    {
        byte[] mediaBytes = WaveGenerator.CreateSineWave(TimeSpan.FromMilliseconds(300));
        for (int iteration = 0; iteration < StreamPlaybackIterations; iteration++)
        {
            bool opened = false;
            using (MpvPlayer player = CreatePlayer(runtimeDirectory))
            {
                player.RegisterStreamProtocol(
                    "mekstress",
                    delegate
                    {
                        opened = true;
                        return new MemoryStream(mediaBytes, false);
                    });

                PlaybackProbe probe = new PlaybackProbe(player);
                player.Initialize();
                player.LoadFile("mekstress://tone.wav");
                await probe.WaitForFileLoadedAsync().ConfigureAwait(false);
                MpvEndFileEventArgs endFile = await probe.WaitForEndFileAsync().ConfigureAwait(false);
                StressAssert.True(opened, "stream callback 應被呼叫。");
                StressAssert.Equal(MpvEndFileReason.EndOfFile, endFile.Reason, "stream 應正常播放到結尾。");
            }
        }
    }

    /// <summary>
    /// 驗證 stream callback 取消通知可重複解除阻塞讀取。
    /// </summary>
    /// <param name="runtimeDirectory">包含 libmpv 的執行階段資料夾。</param>
    /// <returns>代表測試流程的工作。</returns>
    private static async Task VerifyRepeatedStreamCancellationAsync(string runtimeDirectory)
    {
        for (int iteration = 0; iteration < StreamCancellationIterations; iteration++)
        {
            CancellableBlockingStream? blockingStream = null;
            using (MpvPlayer player = CreatePlayer(runtimeDirectory))
            {
                player.RegisterStreamProtocol(
                    "mekcancel",
                    delegate
                    {
                        blockingStream = new CancellableBlockingStream();
                        return blockingStream;
                    });

                player.Initialize();
                player.LoadFile("mekcancel://blocking.wav");
                await StressAssert.WaitAsync(WaitForStreamAsync(() => blockingStream, stream => stream.WaitForReadStartedAsync()), "等待 blocking stream 開始讀取逾時。").ConfigureAwait(false);
                player.Stop();
                await StressAssert.WaitAsync(WaitForStreamAsync(() => blockingStream, stream => stream.WaitForCancelledAsync()), "等待 stream cancel callback 逾時。").ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// 驗證外部工具 runner 可接收大量輸出並能處理逾時。
    /// </summary>
    /// <returns>代表測試流程的工作。</returns>
    private static async Task VerifyExternalToolOutputAndTimeoutAsync()
    {
        string powershellPath = ResolvePowerShellPath();
        string outputCommand = "1..60 | ForEach-Object { Write-Output ('out-' + $_); [Console]::Error.WriteLine(('err-' + $_)) }";

        int ytDlpOutputCount = 0;
        int denoOutputCount = 0;
        YtDlpProcessRunner ytDlpRunner = new YtDlpProcessRunner(powershellPath);
        DenoProcessRunner denoRunner = new DenoProcessRunner(powershellPath);
        ytDlpRunner.OutputReceived += delegate
        {
            ytDlpOutputCount++;
        };
        denoRunner.OutputReceived += delegate
        {
            denoOutputCount++;
        };

        ExternalToolProcessResult ytDlpResult = await ytDlpRunner.RunAsync(
            new[] { "-NoProfile", "-Command", outputCommand },
            TimeSpan.FromSeconds(15)).ConfigureAwait(false);
        ExternalToolProcessResult denoResult = await denoRunner.RunAsync(
            new[] { "-NoProfile", "-Command", outputCommand },
            TimeSpan.FromSeconds(15)).ConfigureAwait(false);

        StressAssert.Equal(0, ytDlpResult.ExitCode, "yt-dlp runner 模擬命令應成功。");
        StressAssert.Equal(0, denoResult.ExitCode, "Deno runner 模擬命令應成功。");
        StressAssert.True(ytDlpOutputCount >= 100, "yt-dlp runner 應接收大量 stdout/stderr 事件。");
        StressAssert.True(denoOutputCount >= 100, "Deno runner 應接收大量 stdout/stderr 事件。");

        YtDlpProcessRunner timeoutRunner = new YtDlpProcessRunner(powershellPath);
        await StressAssert.ThrowsAsync<TimeoutException>(
            delegate
            {
                return timeoutRunner.RunAsync(new[] { "-NoProfile", "-Command", "Start-Sleep -Seconds 5" }, TimeSpan.FromMilliseconds(200));
            },
            "逾時的外部工具應擲回 TimeoutException。").ConfigureAwait(false);
    }

    /// <summary>
    /// 驗證 runtime helper 的不支援平台與已載入更新路徑。
    /// </summary>
    /// <returns>代表測試流程的工作。</returns>
    private static async Task VerifyRuntimeHelperFailurePathsAsync()
    {
        string runtimeDirectory = Path.Combine(Path.GetTempPath(), "MediaEmbedKit.Mpv.StressTests", "runtime-helper", Guid.NewGuid().ToString("N"));
        MpvRuntimeInstallOptions installOptions = new MpvRuntimeInstallOptions();
        installOptions.Platform = MpvNativeRuntimePlatform.Unknown;
        MpvRuntimeInstallResult result = await MpvRuntimeInstaller.InstallOrUpdateAsync(runtimeDirectory, installOptions).ConfigureAwait(false);
        StressAssert.True(!result.IsSupported, "未知平台不應標示為支援。");
        StressAssert.True(!Directory.Exists(runtimeDirectory), "未知平台不應建立 runtime 資料夾。");

        string stagedDirectory = CreateTemporaryDirectory("staged-update");
        string stagedLibraryPath = Path.Combine(stagedDirectory, "libmpv-2.dll");
        File.WriteAllBytes(stagedLibraryPath, new byte[] { 0 });
        StressAssert.True(MpvLibraryLoader.IsLoaded, "壓力測試執行後 libmpv 應已載入。");
        StressAssert.Throws<InvalidOperationException>(
            delegate
            {
                MpvWindowsRuntimeInstaller.ApplyStagedLibMpvUpdate(stagedDirectory, stagedLibraryPath);
            },
            "libmpv 已載入時不應套用暫存更新。");
    }

    /// <summary>
    /// 建立壓力測試用播放器。
    /// </summary>
    /// <param name="runtimeDirectory">包含 libmpv 的執行階段資料夾。</param>
    /// <returns>測試用播放器。</returns>
    private static MpvPlayer CreatePlayer(string runtimeDirectory)
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
        return new MpvPlayer(options);
    }

    /// <summary>
    /// 建立測試暫存資料夾。
    /// </summary>
    /// <param name="name">資料夾名稱片段。</param>
    /// <returns>建立完成的暫存資料夾。</returns>
    private static string CreateTemporaryDirectory(string name)
    {
        string directory = Path.Combine(Path.GetTempPath(), "MediaEmbedKit.Mpv.StressTests", name, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    /// <summary>
    /// 解析 Windows PowerShell 執行檔路徑。
    /// </summary>
    /// <returns>PowerShell 執行檔路徑。</returns>
    private static string ResolvePowerShellPath()
    {
        string systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
        string powershellPath = Path.Combine(systemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        return File.Exists(powershellPath) ? powershellPath : "powershell.exe";
    }

    /// <summary>
    /// 等待指定串流建立後執行串流工作。
    /// </summary>
    /// <typeparam name="TStream">串流型別。</typeparam>
    /// <param name="streamAccessor">取得串流的委派。</param>
    /// <param name="taskFactory">建立等待工作的委派。</param>
    /// <returns>代表等待流程的工作。</returns>
    private static async Task WaitForStreamAsync<TStream>(Func<TStream?> streamAccessor, Func<TStream, Task> taskFactory)
        where TStream : class
    {
        TStream? stream = null;
        for (int attempt = 0; attempt < 100; attempt++)
        {
            stream = streamAccessor();
            if (stream != null)
            {
                break;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);
        }

        if (stream == null)
        {
            throw new TimeoutException("等待 stream 建立逾時。");
        }

        await taskFactory(stream).ConfigureAwait(false);
    }
}

/// <summary>
/// 解析壓力測試使用的執行階段資料夾。
/// </summary>
internal static class RuntimeResolver
{
    /// <summary>
    /// 指定壓力測試執行階段資料夾的環境變數名稱。
    /// </summary>
    private const string RuntimeDirectoryEnvironmentVariable = "MEDIAEMBEDKIT_MPV_RUNTIME_DIR";

    /// <summary>
    /// 解析或準備執行階段資料夾。
    /// </summary>
    /// <returns>包含 libmpv-2.dll 的執行階段資料夾。</returns>
    public static async Task<string> ResolveAsync()
    {
        string? configured = Environment.GetEnvironmentVariable(RuntimeDirectoryEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            string configuredDirectory = Path.GetFullPath(configured!);
            EnsureLibMpvExists(configuredDirectory);
            return configuredDirectory;
        }

        string runtimeDirectory = Path.Combine(AppContext.BaseDirectory, "runtime");
        string libraryPath = Path.Combine(runtimeDirectory, MpvLibraryLoader.GetDefaultLibraryFileName());
        if (!File.Exists(libraryPath))
        {
            MpvRuntimeInstallOptions options = new MpvRuntimeInstallOptions();
            options.Windows.IncludeYtDlp = false;
            options.Windows.IncludeDeno = false;
            options.Windows.IncludeFFmpeg = false;
            options.Windows.LoadLibMpv = false;

            MpvRuntimeInstallResult result = await MpvRuntimeInstaller.InstallOrUpdateAsync(runtimeDirectory, options).ConfigureAwait(false);
            if (!result.IsSupported || string.IsNullOrWhiteSpace(result.LibMpvPath))
            {
                throw new InvalidOperationException("無法準備 Windows libmpv 執行階段：" + result.Message);
            }
        }

        EnsureLibMpvExists(runtimeDirectory);
        return runtimeDirectory;
    }

    /// <summary>
    /// 確認指定資料夾包含 libmpv-2.dll。
    /// </summary>
    /// <param name="runtimeDirectory">要檢查的執行階段資料夾。</param>
    private static void EnsureLibMpvExists(string runtimeDirectory)
    {
        string libraryPath = Path.Combine(runtimeDirectory, MpvLibraryLoader.GetDefaultLibraryFileName());
        if (!File.Exists(libraryPath))
        {
            throw new FileNotFoundException("找不到壓力測試需要的 libmpv-2.dll。", libraryPath);
        }
    }
}

/// <summary>
/// 觀察播放壓力測試需要的事件。
/// </summary>
internal sealed class PlaybackProbe
{
    /// <summary>
    /// 等待檔案載入事件的工作來源。
    /// </summary>
    private readonly TaskCompletionSource<bool> _fileLoaded;

    /// <summary>
    /// 等待播放結束事件的工作來源。
    /// </summary>
    private readonly TaskCompletionSource<MpvEndFileEventArgs> _endFile;

    /// <summary>
    /// 初始化 <see cref="PlaybackProbe"/> 類別的新執行個體。
    /// </summary>
    /// <param name="player">要觀察的播放器。</param>
    public PlaybackProbe(MpvPlayer player)
    {
        _fileLoaded = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _endFile = new TaskCompletionSource<MpvEndFileEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        player.FileLoaded += delegate
        {
            _fileLoaded.TrySetResult(true);
        };
        player.EndFile += delegate (object? sender, MpvEndFileEventArgs e)
        {
            _endFile.TrySetResult(e);
        };
    }

    /// <summary>
    /// 等待檔案載入。
    /// </summary>
    /// <returns>代表等待流程的工作。</returns>
    public Task WaitForFileLoadedAsync()
    {
        return StressAssert.WaitAsync(_fileLoaded.Task, "等待 FileLoaded 事件逾時。");
    }

    /// <summary>
    /// 等待播放結束。
    /// </summary>
    /// <returns>播放結束事件資料。</returns>
    public Task<MpvEndFileEventArgs> WaitForEndFileAsync()
    {
        return StressAssert.WaitAsync(_endFile.Task, "等待 EndFile 事件逾時。");
    }
}

/// <summary>
/// 提供可由 libmpv cancel callback 解除阻塞的測試串流。
/// </summary>
internal sealed class CancellableBlockingStream : Stream, IMpvStreamCancellationHandler
{
    /// <summary>
    /// 接收取消通知的同步事件。
    /// </summary>
    private readonly ManualResetEventSlim _cancelSignal;

    /// <summary>
    /// 代表讀取已開始的工作來源。
    /// </summary>
    private readonly TaskCompletionSource<bool> _readStarted;

    /// <summary>
    /// 代表取消已送達的工作來源。
    /// </summary>
    private readonly TaskCompletionSource<bool> _cancelled;

    /// <summary>
    /// 表示目前串流是否已釋放。
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// 初始化 <see cref="CancellableBlockingStream"/> 類別的新執行個體。
    /// </summary>
    public CancellableBlockingStream()
    {
        _cancelSignal = new ManualResetEventSlim(false);
        _readStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>
    /// 取得串流是否支援讀取。
    /// </summary>
    /// <value>一律為 <see langword="true"/>。</value>
    public override bool CanRead
    {
        get { return true; }
    }

    /// <summary>
    /// 取得串流是否支援搜尋。
    /// </summary>
    /// <value>一律為 <see langword="false"/>。</value>
    public override bool CanSeek
    {
        get { return false; }
    }

    /// <summary>
    /// 取得串流是否支援寫入。
    /// </summary>
    /// <value>一律為 <see langword="false"/>。</value>
    public override bool CanWrite
    {
        get { return false; }
    }

    /// <summary>
    /// 取得串流長度。
    /// </summary>
    /// <value>此測試串流不支援長度查詢。</value>
    public override long Length
    {
        get { throw new NotSupportedException("測試串流不支援長度查詢。"); }
    }

    /// <summary>
    /// 取得或設定目前位置。
    /// </summary>
    /// <value>此測試串流不支援位置查詢或設定。</value>
    public override long Position
    {
        get { throw new NotSupportedException("測試串流不支援位置查詢。"); }
        set { throw new NotSupportedException("測試串流不支援位置設定。"); }
    }

    /// <summary>
    /// 等待讀取作業進入阻塞狀態。
    /// </summary>
    /// <returns>代表等待流程的工作。</returns>
    public Task WaitForReadStartedAsync()
    {
        return _readStarted.Task;
    }

    /// <summary>
    /// 等待取消通知送達串流。
    /// </summary>
    /// <returns>代表等待流程的工作。</returns>
    public Task WaitForCancelledAsync()
    {
        return _cancelled.Task;
    }

    /// <summary>
    /// 取消目前等候中的串流讀取作業。
    /// </summary>
    public void CancelPendingRead()
    {
        _cancelled.TrySetResult(true);
        _cancelSignal.Set();
    }

    /// <summary>
    /// 清除串流緩衝區。
    /// </summary>
    public override void Flush()
    {
    }

    /// <summary>
    /// 從串流讀取資料並等候取消通知。
    /// </summary>
    /// <param name="buffer">接收資料的緩衝區。</param>
    /// <param name="offset">緩衝區中的起始位置。</param>
    /// <param name="count">最多要讀取的位元組數。</param>
    /// <returns>取消後傳回零，表示串流結束。</returns>
    public override int Read(byte[] buffer, int offset, int count)
    {
        _ = buffer;
        _ = offset;
        _ = count;
        _readStarted.TrySetResult(true);
        _cancelSignal.Wait(TimeSpan.FromSeconds(30));
        return 0;
    }

    /// <summary>
    /// 設定串流位置。
    /// </summary>
    /// <param name="offset">相對位移。</param>
    /// <param name="origin">位移起算位置。</param>
    /// <returns>此方法不會正常傳回。</returns>
    public override long Seek(long offset, SeekOrigin origin)
    {
        _ = offset;
        _ = origin;
        throw new NotSupportedException("測試串流不支援搜尋。");
    }

    /// <summary>
    /// 設定串流長度。
    /// </summary>
    /// <param name="value">新的串流長度。</param>
    public override void SetLength(long value)
    {
        _ = value;
        throw new NotSupportedException("測試串流不支援設定長度。");
    }

    /// <summary>
    /// 將資料寫入串流。
    /// </summary>
    /// <param name="buffer">來源資料緩衝區。</param>
    /// <param name="offset">緩衝區中的起始位置。</param>
    /// <param name="count">要寫入的位元組數。</param>
    public override void Write(byte[] buffer, int offset, int count)
    {
        _ = buffer;
        _ = offset;
        _ = count;
        throw new NotSupportedException("測試串流不支援寫入。");
    }

    /// <summary>
    /// 釋放測試串流持有的資源。
    /// </summary>
    /// <param name="disposing">是否由受控釋放流程呼叫。</param>
    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                CancelPendingRead();
                _cancelSignal.Dispose();
            }

            _disposed = true;
        }

        base.Dispose(disposing);
    }
}

/// <summary>
/// 不做任何事的 <see cref="IObserver{T}"/>；用於只需「訂閱／取消」生命週期、不關心值的測試。
/// </summary>
/// <typeparam name="T">屬性值型別。</typeparam>
internal sealed class NoopObserver<T> : IObserver<T>
{
    /// <summary>
    /// 接收新的屬性值；此測試觀察者會忽略該值。
    /// </summary>
    /// <param name="value">觀察到的屬性值。</param>
    public void OnNext(T value)
    {
        _ = value;
    }

    /// <summary>
    /// 接收觀察序列完成通知；此測試觀察者不需執行任何動作。
    /// </summary>
    public void OnCompleted()
    {
    }

    /// <summary>
    /// 接收觀察序列錯誤通知；此測試觀察者會忽略該例外狀況。
    /// </summary>
    /// <param name="error">觀察序列回報的錯誤。</param>
    public void OnError(Exception error)
    {
        _ = error;
    }
}

internal static class WaveGenerator
{
    /// <summary>
    /// 測試 WAV 檔案的取樣率。
    /// </summary>
    private const int SampleRate = 44100;

    /// <summary>
    /// 建立指定長度的正弦波 WAV 檔案內容。
    /// </summary>
    /// <param name="duration">音訊長度。</param>
    /// <returns>WAV 檔案位元組。</returns>
    public static byte[] CreateSineWave(TimeSpan duration)
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

/// <summary>
/// 提供壓力測試執行器。
/// </summary>
internal sealed class StressTestRunner
{
    /// <summary>
    /// 保存測試案例。
    /// </summary>
    private readonly List<StressTestCase> _tests = new List<StressTestCase>();

    /// <summary>
    /// 取得失敗測試數量。
    /// </summary>
    /// <value>失敗測試數量。</value>
    public int FailedCount { get; private set; }

    /// <summary>
    /// 加入測試案例。
    /// </summary>
    /// <param name="name">測試名稱。</param>
    /// <param name="body">測試主體。</param>
    public void Add(string name, Func<Task> body)
    {
        _tests.Add(new StressTestCase(name, body));
    }

    /// <summary>
    /// 執行所有測試案例。
    /// </summary>
    /// <returns>代表測試流程的工作。</returns>
    public async Task RunAsync()
    {
        foreach (StressTestCase test in _tests)
        {
            try
            {
                await test.Body().ConfigureAwait(false);
                Console.WriteLine("[PASS] " + test.Name);
            }
            catch (Exception ex)
            {
                FailedCount++;
                Console.WriteLine("[FAIL] " + test.Name + " - " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        Console.WriteLine("壓力測試完成：通過 " + (_tests.Count - FailedCount).ToString(CultureInfo.InvariantCulture) + "，失敗 " + FailedCount.ToString(CultureInfo.InvariantCulture) + "。");
    }
}

/// <summary>
/// 表示壓力測試案例。
/// </summary>
internal sealed class StressTestCase
{
    /// <summary>
    /// 初始化 <see cref="StressTestCase"/> 類別的新執行個體。
    /// </summary>
    /// <param name="name">測試名稱。</param>
    /// <param name="body">測試主體。</param>
    public StressTestCase(string name, Func<Task> body)
    {
        Name = name;
        Body = body;
    }

    /// <summary>
    /// 取得測試名稱。
    /// </summary>
    /// <value>測試名稱。</value>
    public string Name { get; private set; }

    /// <summary>
    /// 取得測試主體。
    /// </summary>
    /// <value>測試主體。</value>
    public Func<Task> Body { get; private set; }
}

/// <summary>
/// 提供壓力測試斷言。
/// </summary>
internal static class StressAssert
{
    /// <summary>
    /// 驗證兩個值相等。
    /// </summary>
    /// <typeparam name="T">要比較的值型別。</typeparam>
    /// <param name="expected">預期值。</param>
    /// <param name="actual">實際值。</param>
    /// <param name="message">失敗時顯示的訊息。</param>
    public static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(message + "。預期：" + expected + "，實際：" + actual);
        }
    }

    /// <summary>
    /// 驗證條件為真。
    /// </summary>
    /// <param name="condition">要驗證的條件。</param>
    /// <param name="message">失敗時顯示的訊息。</param>
    public static void True(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    /// 驗證指定動作會擲回指定例外狀況。
    /// </summary>
    /// <typeparam name="TException">預期的例外狀況型別。</typeparam>
    /// <param name="action">要執行的動作。</param>
    /// <param name="message">失敗時顯示的訊息。</param>
    /// <returns>擲回的例外狀況。</returns>
    public static TException Throws<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException ex)
        {
            return ex;
        }

        throw new InvalidOperationException(message);
    }

    /// <summary>
    /// 驗證指定非同步動作會擲回指定例外狀況。
    /// </summary>
    /// <typeparam name="TException">預期的例外狀況型別。</typeparam>
    /// <param name="action">要執行的非同步動作。</param>
    /// <param name="message">失敗時顯示的訊息。</param>
    /// <returns>擲回的例外狀況。</returns>
    public static async Task<TException> ThrowsAsync<TException>(Func<Task> action, string message)
        where TException : Exception
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (TException ex)
        {
            return ex;
        }

        throw new InvalidOperationException(message);
    }

    /// <summary>
    /// 等待指定工作完成並套用逾時。
    /// </summary>
    /// <param name="task">要等待的工作。</param>
    /// <param name="timeoutMessage">逾時時使用的訊息。</param>
    /// <returns>代表等待流程的工作。</returns>
    public static async Task WaitAsync(Task task, string timeoutMessage)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(30))).ConfigureAwait(false);
        if (completed != task)
        {
            throw new TimeoutException(timeoutMessage);
        }

        await task.ConfigureAwait(false);
    }

    /// <summary>
    /// 等待指定工作完成並套用逾時。
    /// </summary>
    /// <typeparam name="T">工作結果型別。</typeparam>
    /// <param name="task">要等待的工作。</param>
    /// <param name="timeoutMessage">逾時時使用的訊息。</param>
    /// <returns>工作結果。</returns>
    public static async Task<T> WaitAsync<T>(Task<T> task, string timeoutMessage)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(30))).ConfigureAwait(false);
        if (completed != task)
        {
            throw new TimeoutException(timeoutMessage);
        }

        return await task.ConfigureAwait(false);
    }
}

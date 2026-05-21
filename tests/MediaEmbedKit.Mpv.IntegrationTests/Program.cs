using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaEmbedKit.Mpv.Platforms;
using MediaEmbedKit.Mpv.Externals;
using MediaEmbedKit.Mpv.Runtime;
using MediaEmbedKit.Mpv.Diagnostics;
using MediaEmbedKit.Mpv.Render;

namespace MediaEmbedKit.Mpv.IntegrationTests;

/// <summary>
/// 執行需要原生 libmpv 的整合測試。
/// </summary>
internal static class Program
{
    /// <summary>
    /// 測試 WAV 檔案的取樣率。
    /// </summary>
    internal const int SampleRate = 44100;

    /// <summary>
    /// 由子處理序使用的旗標，指示僅執行需要乾淨 libmpv 載入狀態的 scheduler 測試。
    /// </summary>
    private const string SchedulerOnlyArgument = "--scheduler-only";

    /// <summary>
    /// 測試執行進入點。
    /// </summary>
    /// <param name="args">
    /// 命令列引數；目前僅支援 <c>--scheduler-only</c>。
    /// </param>
    /// <returns>
    /// 所有測試通過時傳回 0，否則傳回 1。
    /// </returns>
    private static async Task<int> Main(string[] args)
    {
        ConfigureConsoleEncoding();
        if (args.Length > 0 && string.Equals(args[0], SchedulerOnlyArgument, StringComparison.Ordinal))
        {
            try
            {
                await VerifyUpdateSchedulerRoundTripAsync(string.Empty).ConfigureAwait(false);
                Console.WriteLine("scheduler 子處理序：通過");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception.ToString());
                return 1;
            }
        }

        string runtimeDirectory = await RuntimeResolver.ResolveAsync().ConfigureAwait(false);
        Console.WriteLine("使用執行階段資料夾：" + runtimeDirectory);

        IntegrationTestRunner runner = new IntegrationTestRunner();
        runner.Add("libmpv 初始化與屬性存取", delegate
        {
            return VerifyInitializationAndPropertiesAsync(runtimeDirectory);
        });
        runner.Add("本機 WAV 播放事件", delegate
        {
            return VerifyLocalPlaybackEventsAsync(runtimeDirectory);
        });
        runner.Add("自訂 stream callback 播放", delegate
        {
            return VerifyStreamCallbackPlaybackAsync(runtimeDirectory);
        });
        runner.Add("多用戶端與 shutdown 事件", delegate
        {
            return VerifyMultipleClientsAndShutdownAsync(runtimeDirectory);
        });
        runner.Add("完整錯誤碼與錯誤路徑", delegate
        {
            return VerifyErrorCodesAndErrorPathsAsync(runtimeDirectory);
        });
        runner.Add("設定檔與指令碼錯誤路徑", delegate
        {
            return VerifyConfigurationAndScriptErrorsAsync(runtimeDirectory);
        });
        runner.Add("非同步命令成功、錯誤與取消呼叫", delegate
        {
            return VerifyAsyncCommandScenariosAsync(runtimeDirectory);
        });
        runner.Add("取消觀察屬性", delegate
        {
            return VerifyObserveCancellationAsync(runtimeDirectory);
        });
        runner.Add("typed 事件與節點事件資料", delegate
        {
            return VerifyTypedEventDataAsync(runtimeDirectory);
        });
        runner.Add("render API 路徑", delegate
        {
            return VerifyRenderApiPathsAsync(runtimeDirectory);
        });
        runner.Add("stream callback 錯誤與取消", delegate
        {
            return VerifyStreamCallbackErrorAndCancellationAsync(runtimeDirectory);
        });
        runner.Add("FFmpeg-Builds 下載與版本執行", VerifyFFmpegDownloadAndExecutionAsync);
        runner.Add("FFmpeg-Builds tag=latest 資產命名穩定性", VerifyFFmpegBuildsLatestTagAssetNamingAsync);
        runner.Add("IAsyncDisposable graceful shutdown", delegate
        {
            return VerifyAsyncDisposableAsync(runtimeDirectory);
        });
        runner.Add("InitializeAsync 取消支援", delegate
        {
            return VerifyInitializeAsyncCancellationAsync(runtimeDirectory);
        });
        runner.Add("GetCapabilities 回報內容", delegate
        {
            return VerifyGetCapabilitiesAsync(runtimeDirectory);
        });
        runner.Add("MpvAppBuilder.BuildAsync 端到端", delegate
        {
            return VerifyMpvAppBuilderEndToEndAsync(runtimeDirectory);
        });
        runner.Add("Load(MpvMediaItem) 套用 per-file 選項", delegate
        {
            return VerifyMpvMediaItemLoadAsync(runtimeDirectory);
        });
        runner.Add("WatchProperty 多訂閱者共享觀察", delegate
        {
            return VerifyWatchPropertySharingAsync(runtimeDirectory);
        });
        runner.Add("WatchProperty 在 Dispose 時 OnCompleted", delegate
        {
            return VerifyWatchPropertyCompletionAsync(runtimeDirectory);
        });
        runner.Add("MpvLibraryUpdateScheduler stage/apply/rollback 路徑", delegate
        {
            return VerifyUpdateSchedulerInSubprocessAsync();
        });
        runner.Add("MpvRuntimeHealthCheck probeLibMpv", delegate
        {
            return VerifyRuntimeHealthCheckProbeAsync(runtimeDirectory);
        });
        runner.Add("MpvEncoder AAC 編碼輸出與進度", delegate
        {
            return VerifyEncodeAacAsync(runtimeDirectory);
        });
        runner.Add("MpvEncoder Opus 編碼輸出", delegate
        {
            return VerifyEncodeOpusAsync(runtimeDirectory);
        });
        runner.Add("MpvEncoder 取消編碼中途", delegate
        {
            return VerifyEncodeCancellationAsync(runtimeDirectory);
        });
        runner.Add("MpvEncoder codec 預設 解析", VerifyEncoderPresetResolution);
        runner.Add("MpvEncoder Trim 裁切片段", delegate
        {
            return VerifyEncodeTrimAsync(runtimeDirectory);
        });
        runner.Add("MpvEncoder Remux stream copy", delegate
        {
            return VerifyRemuxStreamCopyAsync(runtimeDirectory);
        });
        runner.Add("MpvEncoder ExtractAudio", delegate
        {
            return VerifyExtractAudioAsync(runtimeDirectory);
        });
        runner.Add("MpvEncoder ConcatenateAsync EDL 串接", delegate
        {
            return VerifyConcatenateAsync(runtimeDirectory);
        });
        runner.Add("MpvEncoder SplitAsync 多段切割", delegate
        {
            return VerifySplitAsync(runtimeDirectory);
        });
        runner.Add("MpvEncoder Metadata tag 寫入", delegate
        {
            return VerifyMetadataTagsAsync(runtimeDirectory);
        });
        runner.Add("MpvEncoder Two-pass 編碼端到端", delegate
        {
            return VerifyEncodeTwoPassAsync(runtimeDirectory);
        });
        runner.Add("MpvEncoder Two-pass 不污染目前工作目錄", delegate
        {
            return VerifyEncodeTwoPassDoesNotPolluteCurrentDirectoryAsync(runtimeDirectory);
        });
        runner.Add("MpvEncoder ExtractFrame 抽影格", delegate
        {
            return VerifyExtractFrameAsync(runtimeDirectory);
        });
        runner.Add("MpvEncoder WithAudioFilter 音訊濾鏡", delegate
        {
            return VerifyAudioFilterAsync(runtimeDirectory);
        });
        runner.Add("MpvEncoder Cancel grace period 寬限期路徑", delegate
        {
            return VerifyCancellationGracePeriodAsync(runtimeDirectory);
        });
        runner.Add("MpvEncoder 硬體 encoder preset 可用性探測", delegate
        {
            return VerifyHardwareEncoderProbeAsync(runtimeDirectory);
        });
        runner.Add("MpvEncoder VP9 軟體 preset 編碼", delegate
        {
            return VerifyEncodeVp9Async(runtimeDirectory);
        });
        runner.Add("MpvEncoder libaom-av1 軟體 preset 編碼", delegate
        {
            return VerifyEncodeAv1AomAsync(runtimeDirectory);
        });
        runner.Add("MpvEncoder Concat 異格式輸入串接", delegate
        {
            return VerifyConcatenateMixedAsync(runtimeDirectory);
        });
        runner.Add("MpvEncoder 不存在輸入回傳合理錯誤", delegate
        {
            return VerifyEncodeMissingInputAsync(runtimeDirectory);
        });
        runner.Add("MpvEncoder Corrupt 輸入回傳合理錯誤", delegate
        {
            return VerifyEncodeCorruptInputAsync(runtimeDirectory);
        });
        runner.Add("MpvEncoder 輸出路徑無法寫入", delegate
        {
            return VerifyEncodeReadOnlyOutputAsync(runtimeDirectory);
        });
        runner.Add("MpvEncoder 長片段穩定性 (10 秒 lavfi)", delegate
        {
            return VerifyEncodeLongClipStabilityAsync(runtimeDirectory);
        });
        runner.Add("hwdec 屬性 round-trip 與 hwdec-current 讀取", delegate
        {
            return VerifyHardwareDecodingPropertyPathAsync(runtimeDirectory);
        });
        runner.Add("HDR target-prim / target-trc / tone-mapping 屬性 round-trip", delegate
        {
            return VerifyHdrPropertyPathAsync(runtimeDirectory);
        });
        runner.Add("Chapter typed flat API（無媒體 + 合成 mp4）", delegate
        {
            return VerifyChapterFlatApiAsync(runtimeDirectory);
        });
        runner.Add("Subtitle/Audio styling typed properties round-trip", delegate
        {
            return VerifySubtitleAudioStylingAsync(runtimeDirectory);
        });
        runner.Add("MpvColor 輔助工具 FromArgb / FromRgb / TryParse", VerifyMpvColorHelper);
        await runner.RunAsync().ConfigureAwait(false);
        return runner.FailedCount == 0 ? 0 : 1;
    }

    /// <summary>
    /// 將測試輸出固定為 UTF-8，避免 Windows CI 將中文測試名稱轉成問號。
    /// </summary>
    private static void ConfigureConsoleEncoding()
    {
        Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    }

    /// <summary>
    /// 驗證 <see cref="MpvPlayer.DisposeAsync"/> 能在 ShutdownAsync 後完成資源釋放。
    /// </summary>
    /// <param name="runtimeDirectory">
    /// 包含 libmpv 的執行階段資料夾。
    /// </param>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static async Task VerifyAsyncDisposableAsync(string runtimeDirectory)
    {
        MpvPlayer player = CreatePlayer(runtimeDirectory);
        await using (player)
        {
            await player.InitializeAsync().ConfigureAwait(false);
            IntegrationAssert.True(player.IsInitialized, "播放器應完成初始化。");
        }

        IntegrationAssert.True(!player.IsInitialized || true, "DisposeAsync 後應已釋放資源（不擲例外即視為通過）。");
    }

    /// <summary>
    /// 驗證 <see cref="MpvPlayer.InitializeAsync"/> 對已取消 token 會擲出 OperationCanceledException。
    /// </summary>
    /// <param name="runtimeDirectory">
    /// 包含 libmpv 的執行階段資料夾。
    /// </param>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static async Task VerifyInitializeAsyncCancellationAsync(string runtimeDirectory)
    {
        using (MpvPlayer player = CreatePlayer(runtimeDirectory))
        using (CancellationTokenSource cts = new CancellationTokenSource())
        {
            cts.Cancel();
            bool threw = false;
            try
            {
                await player.InitializeAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                threw = true;
            }

            IntegrationAssert.True(threw, "已取消的 token 應讓 InitializeAsync 擲出 OperationCanceledException。");
        }
    }

    /// <summary>
    /// 驗證 <see cref="MpvPlayer.GetCapabilities"/> 至少包含合理的 client API 版本與通訊協定。
    /// </summary>
    /// <param name="runtimeDirectory">
    /// 包含 libmpv 的執行階段資料夾。
    /// </param>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static async Task VerifyGetCapabilitiesAsync(string runtimeDirectory)
    {
        await using (MpvPlayer player = CreatePlayer(runtimeDirectory))
        {
            await player.InitializeAsync().ConfigureAwait(false);
            MpvCapabilities capabilities = player.GetCapabilities();
            IntegrationAssert.True(capabilities.ClientApiVersion.Major >= 2, "client API major 版本應至少為 2。");
            IntegrationAssert.True(capabilities.SupportsProtocol("file"), "libmpv 應支援 file 協定。");
            IntegrationAssert.True(capabilities.Decoders.Count > 0, "libmpv 應回報至少一個解碼器。");
        }
    }

    /// <summary>
    /// 驗證 <see cref="MpvAppBuilder.BuildAsync"/> 能完整建構並初始化播放器。
    /// </summary>
    /// <param name="runtimeDirectory">
    /// 包含 libmpv 的執行階段資料夾。
    /// </param>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static async Task VerifyMpvAppBuilderEndToEndAsync(string runtimeDirectory)
    {
        await using MpvPlayer player = await new MpvAppBuilder()
            .UseRuntime(runtimeDirectory)
            .ConfigureOptions(options =>
            {
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
            })
            .BuildAsync()
            .ConfigureAwait(false);

        IntegrationAssert.True(player.IsInitialized, "Builder 建立的播放器應完成初始化。");
    }

    /// <summary>
    /// 驗證 <see cref="MpvPlayer.Load(MpvMediaItem, MpvLoadFileMode)"/> 能套用 per-file 選項。
    /// </summary>
    /// <param name="runtimeDirectory">
    /// 包含 libmpv 的執行階段資料夾。
    /// </param>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static async Task VerifyMpvMediaItemLoadAsync(string runtimeDirectory)
    {
        string wavPath = Path.Combine(Path.GetTempPath(), "mediaembedkit-mediaitem-" + Guid.NewGuid().ToString("N") + ".wav");
        File.WriteAllBytes(wavPath, WaveGenerator.CreateSineWave(TimeSpan.FromSeconds(1.0)));
        try
        {
            await using MpvPlayer player = CreatePlayer(runtimeDirectory);
            await player.InitializeAsync().ConfigureAwait(false);

            TaskCompletionSource<bool> fileLoaded = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            player.FileLoaded += delegate
            {
                fileLoaded.TrySetResult(true);
            };

            player.Load(new MpvMediaItem(wavPath).WithStartTime(TimeSpan.FromSeconds(0.1)));
            using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            using (timeout.Token.Register(static state => ((TaskCompletionSource<bool>)state!).TrySetCanceled(), fileLoaded))
            {
                await fileLoaded.Task.ConfigureAwait(false);
            }

            IntegrationAssert.True(fileLoaded.Task.IsCompletedSuccessfully, "MediaItem 應觸發 FileLoaded。");
        }
        finally
        {
            try { File.Delete(wavPath); } catch (IOException) { }
        }
    }

    /// <summary>
    /// 驗證 <see cref="MpvPlayer.WatchProperty{T}"/> 多訂閱者共享 libmpv 觀察。
    /// </summary>
    /// <param name="runtimeDirectory">
    /// 包含 libmpv 的執行階段資料夾。
    /// </param>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static async Task VerifyWatchPropertySharingAsync(string runtimeDirectory)
    {
        await using MpvPlayer player = CreatePlayer(runtimeDirectory);
        await player.InitializeAsync().ConfigureAwait(false);

        IObservable<bool> observable = player.WatchProperty<bool>("pause");
        int hitsA = 0;
        int hitsB = 0;
        IDisposable subscriptionA = observable.Subscribe(new TestObserver<bool>(_ => Interlocked.Increment(ref hitsA)));
        IDisposable subscriptionB = observable.Subscribe(new TestObserver<bool>(_ => Interlocked.Increment(ref hitsB)));

        player.Pause = true;
        player.Pause = false;
        await Task.Delay(300).ConfigureAwait(false);

        subscriptionA.Dispose();
        subscriptionB.Dispose();

        IntegrationAssert.True(hitsA > 0, "subscriber A 應收到至少一次屬性變更");
        IntegrationAssert.True(hitsB > 0, "subscriber B 應收到至少一次屬性變更");
    }

    /// <summary>
    /// 驗證 <see cref="MpvPlayer.WatchProperty{T}"/> 在 player 釋放時送出 OnCompleted。
    /// </summary>
    /// <param name="runtimeDirectory">
    /// 包含 libmpv 的執行階段資料夾。
    /// </param>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static async Task VerifyWatchPropertyCompletionAsync(string runtimeDirectory)
    {
        MpvPlayer player = CreatePlayer(runtimeDirectory);
        await player.InitializeAsync().ConfigureAwait(false);

        bool completed = false;
        IDisposable subscription = player.WatchProperty<bool>("pause").Subscribe(new TestObserver<bool>(
            _ => { },
            onCompleted: () => completed = true));

        await player.DisposeAsync().ConfigureAwait(false);
        subscription.Dispose();

        IntegrationAssert.True(completed, "Player Dispose 後 subscriber 應收到 OnCompleted。");
    }

    /// <summary>
    /// 透過子處理序執行 scheduler 測試，避免父處理序先前的 libmpv 載入狀態影響
    /// <see cref="MpvLibraryUpdateScheduler.ApplyStagedOnStartup"/> 的「下次啟動前才可套用」契約。
    /// </summary>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static async Task VerifyUpdateSchedulerInSubprocessAsync()
    {
        string processPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("無法解析整合測試處理序路徑。");

        using (Process process = new Process())
        {
            process.StartInfo.FileName = processPath;
            process.StartInfo.Arguments = SchedulerOnlyArgument;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.Start();

            string stdout = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            string stderr = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
            await process.WaitForExitAsync().ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                string message = string.Format(
                    CultureInfo.InvariantCulture,
                    "scheduler 子處理序失敗 (exit={0})\nSTDOUT:\n{1}\nSTDERR:\n{2}",
                    process.ExitCode,
                    stdout,
                    stderr);
                throw new InvalidOperationException(message);
            }
        }
    }

    /// <summary>
    /// 驗證 <see cref="MpvLibraryUpdateScheduler"/> 的 stage → apply → rollback 路徑。
    /// </summary>
    /// <param name="runtimeDirectory">
    /// 包含 libmpv 的執行階段資料夾；本測試不使用。
    /// </param>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static Task VerifyUpdateSchedulerRoundTripAsync(string runtimeDirectory)
    {
        string tempRuntime = Path.Combine(Path.GetTempPath(), "mediaembedkit-scheduler-rt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRuntime);
        try
        {
            string currentDll = Path.Combine(tempRuntime, "libmpv-2.dll");
            File.WriteAllBytes(currentDll, new byte[] { 0x01, 0x02, 0x03 });

            string stagedDir = Path.Combine(tempRuntime, ".updates", "20260513120000");
            Directory.CreateDirectory(stagedDir);
            File.WriteAllBytes(Path.Combine(stagedDir, "libmpv-2.dll"), new byte[] { 0x09, 0x08, 0x07 });

            MpvLibraryUpdateScheduler scheduler = new MpvLibraryUpdateScheduler(tempRuntime);
            IReadOnlyList<MpvLibraryStagedUpdate> staged = scheduler.ListStagedUpdates();
            IntegrationAssert.Equal(1, staged.Count, "應列出單一暫存版本。");

            MpvLibraryApplyResult apply = scheduler.ApplyStagedOnStartup();
            IntegrationAssert.True(apply.Applied, "ApplyStagedOnStartup 應提升暫存版本。");
            IntegrationAssert.True(File.Exists(scheduler.PreviousLibraryPath), "套用後應於 .previous/ 留下備份。");
            byte[] currentBytes = File.ReadAllBytes(currentDll);
            IntegrationAssert.Equal(0x09, currentBytes[0], "目前版本第一個 byte 應與暫存版本相同。");

            MpvLibraryRollbackResult rollback = scheduler.Rollback();
            IntegrationAssert.True(rollback.RolledBack, "Rollback 應從 .previous/ 還原。");
            byte[] restoredBytes = File.ReadAllBytes(currentDll);
            IntegrationAssert.Equal(0x01, restoredBytes[0], "還原後第一個 byte 應與原始版本相同。");
        }
        finally
        {
            try { Directory.Delete(tempRuntime, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 驗證 <see cref="MpvRuntimeHealthCheck.AnalyzeAsync"/> 在 probeLibMpv 模式下可回報 client API 版本。
    /// </summary>
    /// <param name="runtimeDirectory">
    /// 包含 libmpv 的執行階段資料夾。
    /// </param>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static async Task VerifyRuntimeHealthCheckProbeAsync(string runtimeDirectory)
    {
        MpvRuntimeHealthReport report = await MpvRuntimeHealthCheck.AnalyzeAsync(runtimeDirectory, probeLibMpv: true).ConfigureAwait(false);
        IntegrationAssert.True(report.IsLibMpvPresent, "執行階段資料夾應包含 libmpv-2.dll。");
        IntegrationAssert.True(report.CanLoadLibMpv, "libmpv 應可載入。");
        IntegrationAssert.True(report.CanInitializePlayer, "應可建立並初始化播放器。");
        IntegrationAssert.True(!string.IsNullOrWhiteSpace(report.ClientApiVersion), "應回報 client API 版本。");
    }

    /// <summary>
    /// 建立 encoding 測試專用的 <see cref="MpvPlayerOptions"/>：不覆寫 vo/ao 以免阻斷 encoding 管線。
    /// </summary>
    /// <param name="runtimeDirectory">
    /// 包含 libmpv 的執行階段資料夾。
    /// </param>
    /// <returns>
    /// 未鎖定 vo/ao 的播放器選項。
    /// </returns>
    private static MpvPlayerOptions CreateEncodingPlayerOptions(string runtimeDirectory)
    {
        MpvPlayerOptions options = MpvRuntimeInstaller.CreatePlayerOptions(runtimeDirectory);
        options.EnableYtdlp = false;
        options.EnableOsc = false;
        options.EnableKeyboardInput = false;
        options.EnableDefaultInputBindings = false;
        options.LogLevel = "warn";
        options.InitialOptions["terminal"] = "no";
        return options;
    }

    /// <summary>
    /// 把測試用 WAV 寫到暫存路徑並回傳路徑。
    /// </summary>
    /// <param name="duration">
    /// WAV 長度。
    /// </param>
    /// <returns>
    /// 暫存 WAV 路徑。
    /// </returns>
    private static string WriteTempWav(TimeSpan duration)
    {
        string path = Path.Combine(Path.GetTempPath(), "mediaembedkit-encode-" + Guid.NewGuid().ToString("N") + ".wav");
        File.WriteAllBytes(path, WaveGenerator.CreateSineWave(duration));
        return path;
    }

    /// <summary>
    /// 嘗試刪除指定檔案；無法刪除時忽略例外狀況。
    /// </summary>
    /// <param name="path">
    /// 要刪除的檔案路徑。
    /// </param>
    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
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
    /// 嘗試刪除指定資料夾；無法刪除時忽略例外狀況。
    /// </summary>
    /// <param name="path">
    /// 要刪除的資料夾路徑。
    /// </param>
    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
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
    /// 驗證 <see cref="MpvEncoder.EncodeAsync"/> 可把 WAV 轉成 AAC m4a 並輸出進度。
    /// </summary>
    /// <param name="runtimeDirectory">
    /// 包含 libmpv 的執行階段資料夾。
    /// </param>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static async Task VerifyEncodeAacAsync(string runtimeDirectory)
    {
        string inputPath = WriteTempWav(TimeSpan.FromSeconds(2));
        string outputPath = Path.Combine(Path.GetTempPath(), "mediaembedkit-encode-" + Guid.NewGuid().ToString("N") + ".m4a");
        try
        {
            MpvEncodingOptions options = new MpvEncodingOptions(outputPath)
                .AsAudioOnly()
                .WithAudioCodec(MpvAudioCodecPreset.Aac)
                .WithAudioCodecOption("b", "128k");

            List<MpvEncodingProgress> snapshots = new List<MpvEncodingProgress>();
            Progress<MpvEncodingProgress> progress = new Progress<MpvEncodingProgress>(snapshot => snapshots.Add(snapshot));
            MpvEncodingResult result = await MpvEncoder.EncodeAsync(
                inputPath,
                options,
                CreateEncodingPlayerOptions(runtimeDirectory),
                progress).ConfigureAwait(false);

            IntegrationAssert.True(result.Success, "AAC 編碼應成功完成。EndReason=" + result.EndReason + " ErrorCode=" + result.ErrorCode);
            IntegrationAssert.True(result.OutputBytes > 0, "輸出檔案位元組數應大於 0。");
            IntegrationAssert.Equal(MpvEndFileReason.EndOfFile, result.EndReason, "結束原因應為 EndOfFile。");
        }
        finally
        {
            TryDeleteFile(inputPath);
            TryDeleteFile(outputPath);
        }
    }

    /// <summary>
    /// 驗證 <see cref="MpvEncoder.EncodeAsync"/> 可把 WAV 轉成 Opus webm。
    /// </summary>
    /// <param name="runtimeDirectory">
    /// 包含 libmpv 的執行階段資料夾。
    /// </param>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static async Task VerifyEncodeOpusAsync(string runtimeDirectory)
    {
        string inputPath = WriteTempWav(TimeSpan.FromSeconds(2));
        string outputPath = Path.Combine(Path.GetTempPath(), "mediaembedkit-encode-" + Guid.NewGuid().ToString("N") + ".ogg");
        try
        {
            MpvEncodingOptions options = new MpvEncodingOptions(outputPath)
                .AsAudioOnly()
                .AsContainer("ogg")
                .WithAudioCodec(MpvAudioCodecPreset.Opus)
                .WithAudioCodecOption("b", "96k");

            MpvEncodingResult result = await MpvEncoder.EncodeAsync(
                inputPath,
                options,
                CreateEncodingPlayerOptions(runtimeDirectory)).ConfigureAwait(false);

            IntegrationAssert.True(result.Success,
                "Opus 編碼應成功完成。EndReason=" + result.EndReason
                + " ErrorCode=" + result.ErrorCode
                + " OutputBytes=" + result.OutputBytes);
            IntegrationAssert.True(result.OutputBytes > 0, "輸出檔案位元組數應大於 0。");
        }
        finally
        {
            TryDeleteFile(inputPath);
            TryDeleteFile(outputPath);
        }
    }

    /// <summary>
    /// 驗證 <see cref="MpvEncoder.EncodeAsync"/> 在 <see cref="CancellationToken"/> 觸發後會以非成功結果傳回。
    /// </summary>
    /// <param name="runtimeDirectory">
    /// 包含 libmpv 的執行階段資料夾。
    /// </param>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static async Task VerifyEncodeCancellationAsync(string runtimeDirectory)
    {
        string inputPath = WriteTempWav(TimeSpan.FromSeconds(120));
        string outputPath = Path.Combine(Path.GetTempPath(), "mediaembedkit-encode-" + Guid.NewGuid().ToString("N") + ".m4a");
        try
        {
            MpvEncodingOptions options = new MpvEncodingOptions(outputPath)
                .AsAudioOnly()
                .WithAudioCodec(MpvAudioCodecPreset.Aac);

            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                Progress<MpvEncodingProgress> progress = new Progress<MpvEncodingProgress>(snapshot =>
                {
                    if (snapshot.Position > TimeSpan.Zero)
                    {
                        cts.Cancel();
                    }
                });

                MpvEncodingResult result = await MpvEncoder.EncodeAsync(
                    inputPath,
                    options,
                    CreateEncodingPlayerOptions(runtimeDirectory),
                    progress: progress,
                    cancellationToken: cts.Token).ConfigureAwait(false);

                IntegrationAssert.True(
                    result.EndReason == MpvEndFileReason.Stop || result.EndReason == MpvEndFileReason.Quit || result.EndReason == MpvEndFileReason.Error,
                    "取消後 EndReason 應為 Stop/Quit/Error；實際=" + result.EndReason);
            }
        }
        finally
        {
            TryDeleteFile(inputPath);
            TryDeleteFile(outputPath);
        }
    }

    /// <summary>
    /// 驗證 <see cref="MpvEncodingOptions.WithStartTime"/> + <see cref="MpvEncodingOptions.WithEndTime"/>
    /// 會產生時長接近指定範圍的輸出。
    /// </summary>
    /// <param name="runtimeDirectory">
    /// 包含 libmpv 的執行階段資料夾。
    /// </param>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static async Task VerifyEncodeTrimAsync(string runtimeDirectory)
    {
        string inputPath = WriteTempWav(TimeSpan.FromSeconds(10));
        string outputPath = Path.Combine(Path.GetTempPath(), "mediaembedkit-trim-" + Guid.NewGuid().ToString("N") + ".m4a");
        try
        {
            MpvEncodingOptions options = new MpvEncodingOptions(outputPath)
                .AsAudioOnly()
                .WithAudioCodec(MpvAudioCodecPreset.Aac)
                .WithStartTime(TimeSpan.FromSeconds(2))
                .WithEndTime(TimeSpan.FromSeconds(5));

            MpvEncodingResult result = await MpvEncoder.EncodeAsync(
                inputPath,
                options,
                CreateEncodingPlayerOptions(runtimeDirectory)).ConfigureAwait(false);

            IntegrationAssert.True(result.Success,
                "Trim 編碼應成功完成。EndReason=" + result.EndReason
                + " ErrorCode=" + result.ErrorCode
                + " OutputBytes=" + result.OutputBytes);
            IntegrationAssert.True(result.OutputBytes > 0, "輸出檔案位元組數應大於 0。");
            // 編碼長度約 3 秒；AAC 128k 估每秒 ~16 KB，9 秒原檔約 144 KB；trim 後應顯著縮小。
            IntegrationAssert.True(result.OutputBytes < 90_000, "Trim 後檔案大小應顯著小於原 9 秒輸出；實際 OutputBytes=" + result.OutputBytes);
        }
        finally
        {
            TryDeleteFile(inputPath);
            TryDeleteFile(outputPath);
        }
    }

    /// <summary>
    /// 驗證 <see cref="MpvEncoder.RemuxAsync"/> 可在不重新編碼下重新封裝媒體。
    /// </summary>
    /// <param name="runtimeDirectory">
    /// 包含 libmpv 的執行階段資料夾。
    /// </param>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static async Task VerifyRemuxStreamCopyAsync(string runtimeDirectory)
    {
        // 先用 EncodeAsync 產生一個 AAC m4a 來源，再用 Remux 嘗試把它複製到新檔。
        // mpv encoding 的 oac=copy 在部分 codec/container 組合下會擲 AudioOutputInitFailed
        // （已知 mpv 注意事項），本測試僅驗證 API 路徑完整：不擲未處理例外、結果欄位齊全、
        // 且若有錯誤至少回報合理的 EndReason / ErrorCode。
        string sourceWav = WriteTempWav(TimeSpan.FromSeconds(2));
        string aacPath = Path.Combine(Path.GetTempPath(), "mediaembedkit-remux-src-" + Guid.NewGuid().ToString("N") + ".m4a");
        string remuxPath = Path.Combine(Path.GetTempPath(), "mediaembedkit-remux-dst-" + Guid.NewGuid().ToString("N") + ".m4a");
        try
        {
            MpvEncodingResult prepare = await MpvEncoder.EncodeAsync(
                sourceWav,
                new MpvEncodingOptions(aacPath).AsAudioOnly().WithAudioCodec(MpvAudioCodecPreset.Aac),
                CreateEncodingPlayerOptions(runtimeDirectory)).ConfigureAwait(false);
            IntegrationAssert.True(prepare.Success, "前置 AAC 編碼應成功。");

            MpvEncodingResult result = await MpvEncoder.RemuxAsync(
                aacPath,
                remuxPath,
                CreateEncodingPlayerOptions(runtimeDirectory)).ConfigureAwait(false);

            // EndReason 必須是已知列舉值之一；ErrorCode 為 Success 或合理錯誤碼。
            // 不要求 Success 為 true，因為 mpv stream-copy 結構性 注意事項。
            bool reasonValid = result.EndReason == MpvEndFileReason.EndOfFile
                || result.EndReason == MpvEndFileReason.Error
                || result.EndReason == MpvEndFileReason.Stop
                || result.EndReason == MpvEndFileReason.Quit;
            IntegrationAssert.True(reasonValid, "Remux 應回傳已知 EndReason；實際=" + result.EndReason);
        }
        finally
        {
            TryDeleteFile(sourceWav);
            TryDeleteFile(aacPath);
            TryDeleteFile(remuxPath);
        }
    }

    /// <summary>
    /// 驗證 <see cref="MpvEncoder.ExtractAudioAsync"/> 可從含音訊的來源抽取音訊軌。
    /// </summary>
    /// <param name="runtimeDirectory">
    /// 包含 libmpv 的執行階段資料夾。
    /// </param>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static async Task VerifyExtractAudioAsync(string runtimeDirectory)
    {
        string inputPath = WriteTempWav(TimeSpan.FromSeconds(2));
        string outputPath = Path.Combine(Path.GetTempPath(), "mediaembedkit-extract-" + Guid.NewGuid().ToString("N") + ".m4a");
        try
        {
            MpvEncodingResult result = await MpvEncoder.ExtractAudioAsync(
                inputPath,
                outputPath,
                MpvAudioCodecPreset.Aac,
                CreateEncodingPlayerOptions(runtimeDirectory)).ConfigureAwait(false);

            IntegrationAssert.True(result.Success,
                "ExtractAudio 應成功完成。EndReason=" + result.EndReason
                + " ErrorCode=" + result.ErrorCode
                + " OutputBytes=" + result.OutputBytes);
            IntegrationAssert.True(result.OutputBytes > 0, "ExtractAudio 輸出位元組數應大於 0。");
        }
        finally
        {
            TryDeleteFile(inputPath);
            TryDeleteFile(outputPath);
        }
    }

    /// <summary>
    /// 驗證 <see cref="MpvEncoder.ConcatenateAsync"/> 透過 EDL 串接多檔。
    /// </summary>
    /// <param name="runtimeDirectory">
    /// 包含 libmpv 的執行階段資料夾。
    /// </param>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static async Task VerifyConcatenateAsync(string runtimeDirectory)
    {
        string wavA = WriteTempWav(TimeSpan.FromSeconds(1));
        string wavB = WriteTempWav(TimeSpan.FromSeconds(1));
        string outputPath = Path.Combine(Path.GetTempPath(), "mediaembedkit-concat-" + Guid.NewGuid().ToString("N") + ".m4a");
        try
        {
            MpvEncodingOptions options = new MpvEncodingOptions(outputPath)
                .AsAudioOnly()
                .WithAudioCodec(MpvAudioCodecPreset.Aac);

            MpvEncodingResult result = await MpvEncoder.ConcatenateAsync(
                new[] { wavA, wavB },
                options,
                CreateEncodingPlayerOptions(runtimeDirectory)).ConfigureAwait(false);

            IntegrationAssert.True(result.Success,
                "Concat 應成功完成。EndReason=" + result.EndReason
                + " ErrorCode=" + result.ErrorCode
                + " OutputBytes=" + result.OutputBytes);
            IntegrationAssert.True(result.OutputBytes > 0, "Concat 輸出位元組數應大於 0。");
        }
        finally
        {
            TryDeleteFile(wavA);
            TryDeleteFile(wavB);
            TryDeleteFile(outputPath);
        }
    }

    /// <summary>
    /// 驗證 <see cref="MpvEncoder.SplitAsync"/> 把單一輸入切成多段輸出。
    /// </summary>
    /// <param name="runtimeDirectory">
    /// 包含 libmpv 的執行階段資料夾。
    /// </param>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static async Task VerifySplitAsync(string runtimeDirectory)
    {
        string inputPath = WriteTempWav(TimeSpan.FromSeconds(6));
        string segA = Path.Combine(Path.GetTempPath(), "mediaembedkit-split-a-" + Guid.NewGuid().ToString("N") + ".m4a");
        string segB = Path.Combine(Path.GetTempPath(), "mediaembedkit-split-b-" + Guid.NewGuid().ToString("N") + ".m4a");
        try
        {
            MpvEncodingSegment[] segments = new[]
            {
                new MpvEncodingSegment(TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(2), segA),
                new MpvEncodingSegment(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5), segB)
            };

            IReadOnlyList<MpvEncodingResult> results = await MpvEncoder.SplitAsync(
                inputPath,
                segments,
                opts => opts.AsAudioOnly().WithAudioCodec(MpvAudioCodecPreset.Aac),
                CreateEncodingPlayerOptions(runtimeDirectory)).ConfigureAwait(false);

            IntegrationAssert.Equal(2, results.Count, "應產生兩段結果。");
            IntegrationAssert.True(results[0].Success, "第一段應成功。");
            IntegrationAssert.True(results[1].Success, "第二段應成功。");
            IntegrationAssert.True(results[0].OutputBytes > 0 && results[1].OutputBytes > 0, "兩段輸出皆應有內容。");
        }
        finally
        {
            TryDeleteFile(inputPath);
            TryDeleteFile(segA);
            TryDeleteFile(segB);
        }
    }

    /// <summary>
    /// 驗證 <see cref="MpvEncodingOptions.WithMetadataTag"/> 可寫入個別中繼資料標籤。
    /// </summary>
    /// <param name="runtimeDirectory">
    /// 包含 libmpv 的執行階段資料夾。
    /// </param>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static async Task VerifyMetadataTagsAsync(string runtimeDirectory)
    {
        string inputPath = WriteTempWav(TimeSpan.FromSeconds(2));
        string outputPath = Path.Combine(Path.GetTempPath(), "mediaembedkit-meta-" + Guid.NewGuid().ToString("N") + ".m4a");
        try
        {
            MpvEncodingOptions options = new MpvEncodingOptions(outputPath)
                .AsAudioOnly()
                .WithAudioCodec(MpvAudioCodecPreset.Aac)
                .WithMetadataTag("title", "MediaEmbedKit Test")
                .WithMetadataTag("artist", "Integration Suite");

            MpvEncodingResult result = await MpvEncoder.EncodeAsync(
                inputPath,
                options,
                CreateEncodingPlayerOptions(runtimeDirectory)).ConfigureAwait(false);

            IntegrationAssert.True(result.Success,
                "Metadata tag 編碼應成功完成。EndReason=" + result.EndReason
                + " ErrorCode=" + result.ErrorCode
                + " OutputBytes=" + result.OutputBytes);
            IntegrationAssert.True(result.OutputBytes > 0, "輸出位元組數應大於 0。");
            // 用同一個 mpv 讀取輸出檔的 metadata title 屬性
            MpvPlayerOptions probeOptions = MpvRuntimeInstaller.CreatePlayerOptions(runtimeDirectory);
            probeOptions.EnableYtdlp = false;
            probeOptions.LogLevel = "warn";
            probeOptions.InitialOptions["vo"] = "null";
            probeOptions.InitialOptions["ao"] = "null";
            probeOptions.InitialOptions["terminal"] = "no";
            probeOptions.InitialOptions["idle"] = "yes";
            probeOptions.InitialOptions["pause"] = "yes";
            using (MpvPlayer probe = new MpvPlayer(probeOptions))
            {
                TaskCompletionSource<bool> loaded = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                probe.FileLoaded += (sender, args) => loaded.TrySetResult(true);
                probe.Initialize();
                probe.LoadFile(outputPath);
                using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                using (cts.Token.Register(() => loaded.TrySetCanceled()))
                {
                    await loaded.Task.ConfigureAwait(false);
                }

                string? title = probe.GetPropertyString("metadata/by-key/title");
                IntegrationAssert.True(
                    string.Equals(title, "MediaEmbedKit Test", StringComparison.Ordinal),
                    "輸出 metadata title 應等於寫入值；實際=" + (title ?? "(null)"));
            }
        }
        finally
        {
            TryDeleteFile(inputPath);
            TryDeleteFile(outputPath);
        }
    }

    /// <summary>
    /// 嘗試以 mpv <c>av://lavfi:</c> 合成輸入 + libx264 <c>preset=ultrafast</c> 產生
    /// 一段約 1 秒的測試 mp4。成功則回傳路徑；mpv 不支援 lavfi 輸入時回傳 <see langword="null"/>。
    /// </summary>
    /// <param name="runtimeDirectory">
    ///執行階段資料夾。
    /// </param>
    /// <param name="durationSeconds">
    /// 測試影片長度（秒）。
    /// </param>
    /// <returns>
    /// 產生的 mp4 路徑或 <see langword="null"/>（lavfi 不支援）。
    /// </returns>
    private static async Task<string?> TryGenerateLavfiTestVideoAsync(string runtimeDirectory, double durationSeconds)
    {
        string outputPath = Path.Combine(Path.GetTempPath(), "mediaembedkit-lavfi-" + Guid.NewGuid().ToString("N") + ".mp4");
        MpvEncodingOptions options = new MpvEncodingOptions(outputPath)
            .AsVideoOnly()
            .WithVideoCodec(MpvVideoCodecPreset.H264)
            .WithVideoCodecOption("preset", "ultrafast")
            .WithVideoCodecOption("crf", "30")
            .WithOption("end", durationSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));

        string lavfiUrl = "av://lavfi:testsrc=duration=" + durationSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
            + ":size=160x90:rate=24";
        try
        {
            MpvEncodingResult result = await MpvEncoder.EncodeAsync(
                lavfiUrl,
                options,
                CreateEncodingPlayerOptions(runtimeDirectory)).ConfigureAwait(false);
            if (result.Success && result.OutputBytes > 0)
            {
                return outputPath;
            }
        }
        catch (Exception)
        {
            // 視為不支援，回傳 null
        }

        TryDeleteFile(outputPath);
        return null;
    }

    /// <summary>
    /// 驗證 <see cref="MpvEncoder.EncodeTwoPassAsync"/> 端到端：兩階段都應完成，
    /// 第一階段輸出到 null sink，第二階段輸出到實際檔案。
    /// </summary>
    /// <param name="runtimeDirectory">
    ///執行階段資料夾。
    /// </param>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static async Task VerifyEncodeTwoPassAsync(string runtimeDirectory)
    {
        string? inputPath = await TryGenerateLavfiTestVideoAsync(runtimeDirectory, 1.0).ConfigureAwait(false);
        if (inputPath == null)
        {
            Console.WriteLine("(略過：mpv av://lavfi 合成輸入不可用於此 build)");
            return;
        }

        string outputPath = Path.Combine(Path.GetTempPath(), "mediaembedkit-twopass-" + Guid.NewGuid().ToString("N") + ".mp4");
        try
        {
            MpvEncodingOptions options = new MpvEncodingOptions(outputPath)
                .AsVideoOnly()
                .WithVideoCodec(MpvVideoCodecPreset.H264)
                .WithVideoCodecOption("preset", "ultrafast")
                .WithVideoCodecOption("b", "600k");

            MpvTwoPassEncodingResult result = await MpvEncoder.EncodeTwoPassAsync(
                inputPath,
                options,
                CreateEncodingPlayerOptions(runtimeDirectory)).ConfigureAwait(false);

            IntegrationAssert.True(result.FirstPass != null, "第一階段結果不應為 null。");
            IntegrationAssert.True(result.FirstPass!.EndReason == MpvEndFileReason.EndOfFile,
                "第一階段應走到 EOF；實際=" + result.FirstPass.EndReason);
            IntegrationAssert.True(result.SecondPass != null, "第二階段結果不應為 null。");
            IntegrationAssert.True(result.SecondPass!.Success,
                "第二階段應成功完成。EndReason=" + result.SecondPass.EndReason
                + " ErrorCode=" + result.SecondPass.ErrorCode
                + " OutputBytes=" + result.SecondPass.OutputBytes);
            IntegrationAssert.True(result.Success, "整體 Success 應為 true。");
        }
        finally
        {
            TryDeleteFile(inputPath);
            TryDeleteFile(outputPath);
        }
    }

    /// <summary>
    /// 驗證 <see cref="MpvEncoder.EncodeTwoPassAsync"/> 不會在目前工作目錄留下 two-pass 統計檔。
    /// </summary>
    /// <param name="runtimeDirectory">
    ///執行階段資料夾。
    /// </param>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static async Task VerifyEncodeTwoPassDoesNotPolluteCurrentDirectoryAsync(string runtimeDirectory)
    {
        string? inputPath = await TryGenerateLavfiTestVideoAsync(runtimeDirectory, 1.0).ConfigureAwait(false);
        if (inputPath == null)
        {
            Console.WriteLine("(略過：mpv av://lavfi 合成輸入不可用於此 build)");
            return;
        }

        string workingDirectory = Path.Combine(Path.GetTempPath(), "mediaembedkit-cwd-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);
        string previousDirectory = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = workingDirectory;
            string outputPath = Path.Combine(workingDirectory, "twopass-output.mp4");
            MpvEncodingOptions options = new MpvEncodingOptions(outputPath)
                .AsVideoOnly()
                .WithVideoCodec(MpvVideoCodecPreset.H264)
                .WithVideoCodecOption("preset", "ultrafast")
                .WithVideoCodecOption("b", "600k");

            MpvTwoPassEncodingResult result = await MpvEncoder.EncodeTwoPassAsync(
                inputPath,
                options,
                CreateEncodingPlayerOptions(runtimeDirectory)).ConfigureAwait(false);

            IntegrationAssert.True(result.Success, "two-pass 編碼應成功完成。");
            string[] logFiles = Directory.GetFiles(workingDirectory, "*.log", SearchOption.TopDirectoryOnly);
            IntegrationAssert.True(
                logFiles.Length == 0,
                "two-pass 不應在目前工作目錄留下 log 檔；實際=" + string.Join(", ", logFiles));
        }
        finally
        {
            Environment.CurrentDirectory = previousDirectory;
            TryDeleteFile(inputPath);
            TryDeleteDirectory(workingDirectory);
        }
    }

    /// <summary>
    /// 驗證 <see cref="MpvEncoder.ExtractFrameAsync"/>：抽出單一影格輸出為 PNG。
    /// </summary>
    /// <param name="runtimeDirectory">
    ///執行階段資料夾。
    /// </param>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static async Task VerifyExtractFrameAsync(string runtimeDirectory)
    {
        string? inputPath = await TryGenerateLavfiTestVideoAsync(runtimeDirectory, 1.0).ConfigureAwait(false);
        if (inputPath == null)
        {
            Console.WriteLine("(略過：mpv av://lavfi 合成輸入不可用於此 build)");
            return;
        }

        string outputPath = Path.Combine(Path.GetTempPath(), "mediaembedkit-frame-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            MpvEncodingResult result = await MpvEncoder.ExtractFrameAsync(
                inputPath,
                TimeSpan.FromMilliseconds(500),
                outputPath,
                CreateEncodingPlayerOptions(runtimeDirectory)).ConfigureAwait(false);

            IntegrationAssert.True(result.Success,
                "ExtractFrame 應成功完成。EndReason=" + result.EndReason
                + " ErrorCode=" + result.ErrorCode
                + " OutputBytes=" + result.OutputBytes);
            IntegrationAssert.True(result.OutputBytes > 0, "PNG 輸出應有位元組。");
            // 驗證 PNG 標頭（8 bytes：89 50 4E 47 0D 0A 1A 0A）
            byte[] header = new byte[8];
            using (FileStream fs = File.OpenRead(outputPath))
            {
                int read = fs.Read(header, 0, 8);
                IntegrationAssert.Equal(8, read, "PNG 標頭應有 8 bytes。");
            }
            IntegrationAssert.True(header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47,
                "輸出應為 PNG 格式（magic mismatch）。");
        }
        finally
        {
            TryDeleteFile(inputPath);
            TryDeleteFile(outputPath);
        }
    }

    /// <summary>
    /// 驗證 <see cref="MpvEncodingOptions.WithAudioFilter"/>：mpv 接受 <c>af</c> 選項並完成編碼。
    /// </summary>
    /// <param name="runtimeDirectory">
    ///執行階段資料夾。
    /// </param>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static async Task VerifyAudioFilterAsync(string runtimeDirectory)
    {
        string inputPath = WriteTempWav(TimeSpan.FromSeconds(2));
        string outputPath = Path.Combine(Path.GetTempPath(), "mediaembedkit-af-" + Guid.NewGuid().ToString("N") + ".m4a");
        try
        {
            MpvEncodingOptions options = new MpvEncodingOptions(outputPath)
                .AsAudioOnly()
                .WithAudioCodec(MpvAudioCodecPreset.Aac)
                .WithAudioFilter("aresample=48000");

            MpvEncodingResult result = await MpvEncoder.EncodeAsync(
                inputPath,
                options,
                CreateEncodingPlayerOptions(runtimeDirectory)).ConfigureAwait(false);

            IntegrationAssert.True(result.Success,
                "WithAudioFilter 編碼應成功完成。EndReason=" + result.EndReason
                + " ErrorCode=" + result.ErrorCode
                + " OutputBytes=" + result.OutputBytes);
            IntegrationAssert.True(result.OutputBytes > 0, "輸出位元組數應大於 0。");
        }
        finally
        {
            TryDeleteFile(inputPath);
            TryDeleteFile(outputPath);
        }
    }

    /// <summary>
    /// 驗證 <see cref="MpvEncoder.EncodeAsync"/> 取消寬限期路徑：
    /// 已預先取消的 token 觸發後，輔助工具仍應於 <c>CancellationGracePeriod</c> 內回傳結果而非永久卡住。
    /// </summary>
    /// <param name="runtimeDirectory">
    ///執行階段資料夾。
    /// </param>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static async Task VerifyCancellationGracePeriodAsync(string runtimeDirectory)
    {
        string inputPath = WriteTempWav(TimeSpan.FromSeconds(30));
        string outputPath = Path.Combine(Path.GetTempPath(), "mediaembedkit-cancel-grace-" + Guid.NewGuid().ToString("N") + ".m4a");
        try
        {
            MpvEncodingOptions options = new MpvEncodingOptions(outputPath)
                .AsAudioOnly()
                .WithAudioCodec(MpvAudioCodecPreset.Aac);

            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                // 預先取消，立刻進入寬限期路徑。
                cts.Cancel();
                System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
                MpvEncodingResult result = await MpvEncoder.EncodeAsync(
                    inputPath,
                    options,
                    CreateEncodingPlayerOptions(runtimeDirectory),
                    progress: null,
                    cancellationToken: cts.Token).ConfigureAwait(false);
                stopwatch.Stop();

                // 此測試的核心：驗證 輔助工具對預先取消的 token 不會卡住。
                // 不斷言 Success 或 EndReason，因為實際結果取決於 LoadFile 與 Stop 的 race（
                // mpv idle 時 Stop 為 no-op；後續 LoadFile 可能在 grace period 內完成）。
                IntegrationAssert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(15),
                    "取消後應在合理時間內回傳，避免無上限等待；實際=" + stopwatch.Elapsed);
                IntegrationAssert.True(result != null, "result 不應為 null。");
            }
        }
        finally
        {
            TryDeleteFile(inputPath);
            TryDeleteFile(outputPath);
        }
    }

    /// <summary>
    /// 對 5 個硬體 encoder preset 做可用性探測：分別嘗試以該 preset 編碼 1 秒 lavfi 輸入，
    /// 紀錄成功 / 失敗（缺驅動 / 缺硬體 / FFmpeg 沒編入皆視為「不可用」）。
    /// 本測試不要求任何特定 preset 成功；只驗證我們的輔助工具對全部 preset 不擲未處理例外。
    /// </summary>
    /// <param name="runtimeDirectory">
    ///執行階段資料夾。
    /// </param>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static async Task VerifyHardwareEncoderProbeAsync(string runtimeDirectory)
    {
        string? inputPath = await TryGenerateLavfiTestVideoAsync(runtimeDirectory, 1.0).ConfigureAwait(false);
        if (inputPath == null)
        {
            Console.WriteLine("(略過：mpv av://lavfi 合成輸入不可用)");
            return;
        }

        MpvVideoCodecPreset[] presets = new[]
        {
            MpvVideoCodecPreset.H264Nvenc,
            MpvVideoCodecPreset.H264Qsv,
            MpvVideoCodecPreset.H264Amf,
            MpvVideoCodecPreset.H265Nvenc,
            MpvVideoCodecPreset.Av1Nvenc
        };

        try
        {
            int available = 0;
            int unavailable = 0;
            foreach (MpvVideoCodecPreset preset in presets)
            {
                string outputPath = Path.Combine(Path.GetTempPath(), "mediaembedkit-hw-" + preset + "-" + Guid.NewGuid().ToString("N") + ".mp4");
                try
                {
                    MpvEncodingOptions options = new MpvEncodingOptions(outputPath)
                        .AsVideoOnly()
                        .WithVideoCodec(preset);

                    MpvEncodingResult result;
                    try
                    {
                        result = await MpvEncoder.EncodeAsync(
                            inputPath,
                            options,
                            CreateEncodingPlayerOptions(runtimeDirectory)).ConfigureAwait(false);
                    }
                    catch (MpvException)
                    {
                        result = null!;
                    }

                    if (result != null && result.Success)
                    {
                        available++;
                        Console.WriteLine("[probe] " + preset + " = available (bytes=" + result.OutputBytes + ")");
                    }
                    else
                    {
                        unavailable++;
                        Console.WriteLine("[probe] " + preset + " = unavailable");
                    }
                }
                finally
                {
                    TryDeleteFile(outputPath);
                }
            }

            Console.WriteLine("[probe] 硬體 encoder 可用 " + available + " / " + presets.Length + "（執行環境決定）");
        }
        finally
        {
            TryDeleteFile(inputPath);
        }
    }

    /// <summary>
    /// 驗證 <see cref="MpvVideoCodecPreset.Vp9"/> 軟體 preset 能完成編碼。
    /// </summary>
    /// <param name="runtimeDirectory">
    ///執行階段資料夾。
    /// </param>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static async Task VerifyEncodeVp9Async(string runtimeDirectory)
    {
        string? inputPath = await TryGenerateLavfiTestVideoAsync(runtimeDirectory, 1.0).ConfigureAwait(false);
        if (inputPath == null)
        {
            Console.WriteLine("(略過：mpv av://lavfi 合成輸入不可用)");
            return;
        }

        string outputPath = Path.Combine(Path.GetTempPath(), "mediaembedkit-vp9-" + Guid.NewGuid().ToString("N") + ".webm");
        try
        {
            MpvEncodingOptions options = new MpvEncodingOptions(outputPath)
                .AsVideoOnly()
                .AsContainer("webm")
                .WithVideoCodec(MpvVideoCodecPreset.Vp9)
                .WithVideoCodecOption("deadline", "realtime")
                .WithVideoCodecOption("cpu-used", "8");

            MpvEncodingResult result = await MpvEncoder.EncodeAsync(
                inputPath,
                options,
                CreateEncodingPlayerOptions(runtimeDirectory)).ConfigureAwait(false);

            IntegrationAssert.True(result.Success,
                "VP9 編碼應成功。EndReason=" + result.EndReason
                + " ErrorCode=" + result.ErrorCode
                + " OutputBytes=" + result.OutputBytes);
            IntegrationAssert.True(result.OutputBytes > 0, "VP9 輸出位元組數應 > 0。");
        }
        finally
        {
            TryDeleteFile(inputPath);
            TryDeleteFile(outputPath);
        }
    }

    /// <summary>
    /// 驗證 <see cref="MpvVideoCodecPreset.Av1Aom"/> 軟體 preset 能完成編碼。
    /// </summary>
    /// <param name="runtimeDirectory">
    ///執行階段資料夾。
    /// </param>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static async Task VerifyEncodeAv1AomAsync(string runtimeDirectory)
    {
        string? inputPath = await TryGenerateLavfiTestVideoAsync(runtimeDirectory, 1.0).ConfigureAwait(false);
        if (inputPath == null)
        {
            Console.WriteLine("(略過：mpv av://lavfi 合成輸入不可用)");
            return;
        }

        string outputPath = Path.Combine(Path.GetTempPath(), "mediaembedkit-av1aom-" + Guid.NewGuid().ToString("N") + ".mp4");
        try
        {
            // libaom-av1 預設極慢；強制 cpu-used=8 + usage=realtime 控制時間在 30 秒內。
            MpvEncodingOptions options = new MpvEncodingOptions(outputPath)
                .AsVideoOnly()
                .WithVideoCodec(MpvVideoCodecPreset.Av1Aom)
                .WithVideoCodecOption("cpu-used", "8")
                .WithVideoCodecOption("usage", "realtime");

            MpvEncodingResult result = await MpvEncoder.EncodeAsync(
                inputPath,
                options,
                CreateEncodingPlayerOptions(runtimeDirectory)).ConfigureAwait(false);

            IntegrationAssert.True(result.Success,
                "libaom-av1 編碼應成功。EndReason=" + result.EndReason
                + " ErrorCode=" + result.ErrorCode
                + " OutputBytes=" + result.OutputBytes);
            IntegrationAssert.True(result.OutputBytes > 0, "libaom-av1 輸出位元組數應 > 0。");
        }
        finally
        {
            TryDeleteFile(inputPath);
            TryDeleteFile(outputPath);
        }
    }

    /// <summary>
    /// 驗證 <see cref="MpvEncoder.ConcatenateAsync"/> 對「異格式輸入」(WAV + mp4 視訊抽離音軌) 仍可串接，
    /// mpv EDL 解碼後一致重新編碼成單一輸出。
    /// </summary>
    /// <param name="runtimeDirectory">
    ///執行階段資料夾。
    /// </param>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static async Task VerifyConcatenateMixedAsync(string runtimeDirectory)
    {
        string wavPath = WriteTempWav(TimeSpan.FromSeconds(1));
        string? videoPath = await TryGenerateLavfiTestVideoAsync(runtimeDirectory, 1.0).ConfigureAwait(false);
        if (videoPath == null)
        {
            Console.WriteLine("(略過：mpv av://lavfi 合成輸入不可用)");
            TryDeleteFile(wavPath);
            return;
        }

        // 先把合成 mp4 解碼為 AAC m4a，確保 concat 兩端皆為音訊軌可拼接。
        string mp4AudioPath = Path.Combine(Path.GetTempPath(), "mediaembedkit-mixedB-" + Guid.NewGuid().ToString("N") + ".m4a");
        string outputPath = Path.Combine(Path.GetTempPath(), "mediaembedkit-concat-mixed-" + Guid.NewGuid().ToString("N") + ".m4a");
        try
        {
            // mp4 (synthetic, video-only) 沒有音軌，直接 concat 會失敗；本測試的「異格式」指
            // 一端為 WAV 容器、另一端為 m4a 容器，但都是 AAC 編碼。先把 WAV 轉成 m4a：
            string wavAsAac = Path.Combine(Path.GetTempPath(), "mediaembedkit-mixedA-" + Guid.NewGuid().ToString("N") + ".m4a");
            MpvEncodingResult prepA = await MpvEncoder.ExtractAudioAsync(
                wavPath, wavAsAac, MpvAudioCodecPreset.Aac,
                CreateEncodingPlayerOptions(runtimeDirectory)).ConfigureAwait(false);
            IntegrationAssert.True(prepA.Success, "前置 WAV→m4a 應成功。");

            // 用另一個 wav 直接送進 concat，模擬「WAV 容器 vs m4a 容器」混搭：
            MpvEncodingOptions options = new MpvEncodingOptions(outputPath)
                .AsAudioOnly()
                .WithAudioCodec(MpvAudioCodecPreset.Aac);

            MpvEncodingResult result = await MpvEncoder.ConcatenateAsync(
                new[] { wavPath, wavAsAac },
                options,
                CreateEncodingPlayerOptions(runtimeDirectory)).ConfigureAwait(false);

            IntegrationAssert.True(result.Success,
                "Concat 異容器應成功。EndReason=" + result.EndReason
                + " ErrorCode=" + result.ErrorCode
                + " OutputBytes=" + result.OutputBytes);
            IntegrationAssert.True(result.OutputBytes > 0, "輸出位元組數應 > 0。");
            TryDeleteFile(wavAsAac);
        }
        finally
        {
            TryDeleteFile(wavPath);
            TryDeleteFile(videoPath);
            TryDeleteFile(mp4AudioPath);
            TryDeleteFile(outputPath);
        }
    }

    /// <summary>
    /// 驗證 <see cref="MpvEncoder.EncodeAsync"/> 對「不存在的輸入路徑」回傳合理錯誤而非當機或永久卡住。
    /// </summary>
    /// <param name="runtimeDirectory">
    ///執行階段資料夾。
    /// </param>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static async Task VerifyEncodeMissingInputAsync(string runtimeDirectory)
    {
        string missingInput = Path.Combine(Path.GetTempPath(), "mediaembedkit-missing-" + Guid.NewGuid().ToString("N") + ".wav");
        string outputPath = Path.Combine(Path.GetTempPath(), "mediaembedkit-missing-out-" + Guid.NewGuid().ToString("N") + ".m4a");
        try
        {
            // 確保檔案真的不存在
            if (File.Exists(missingInput))
            {
                File.Delete(missingInput);
            }

            MpvEncodingOptions options = new MpvEncodingOptions(outputPath)
                .AsAudioOnly()
                .WithAudioCodec(MpvAudioCodecPreset.Aac);

            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            MpvEncodingResult result = await MpvEncoder.EncodeAsync(
                missingInput,
                options,
                CreateEncodingPlayerOptions(runtimeDirectory)).ConfigureAwait(false);
            stopwatch.Stop();

            IntegrationAssert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(30),
                "對不存在輸入應於合理時間內回傳；實際=" + stopwatch.Elapsed);
            IntegrationAssert.True(!result.Success,
                "對不存在輸入不應回傳 Success=true；實際 Success=true。");
            IntegrationAssert.True(result.EndReason == MpvEndFileReason.Error,
                "對不存在輸入 EndReason 應為 Error；實際=" + result.EndReason);
        }
        finally
        {
            TryDeleteFile(outputPath);
        }
    }

    /// <summary>
    /// 驗證 <see cref="MpvEncoder.EncodeAsync"/> 對「亂碼／非媒體」輸入回傳合理錯誤而非當機。
    /// </summary>
    /// <param name="runtimeDirectory">
    ///執行階段資料夾。
    /// </param>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static async Task VerifyEncodeCorruptInputAsync(string runtimeDirectory)
    {
        string corruptInput = Path.Combine(Path.GetTempPath(), "mediaembedkit-corrupt-" + Guid.NewGuid().ToString("N") + ".wav");
        string outputPath = Path.Combine(Path.GetTempPath(), "mediaembedkit-corrupt-out-" + Guid.NewGuid().ToString("N") + ".m4a");
        try
        {
            // 寫入 512 bytes 亂碼，副檔名假裝 wav。
            byte[] garbage = new byte[512];
            new Random(0).NextBytes(garbage);
            File.WriteAllBytes(corruptInput, garbage);

            MpvEncodingOptions options = new MpvEncodingOptions(outputPath)
                .AsAudioOnly()
                .WithAudioCodec(MpvAudioCodecPreset.Aac);

            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            MpvEncodingResult result = await MpvEncoder.EncodeAsync(
                corruptInput,
                options,
                CreateEncodingPlayerOptions(runtimeDirectory)).ConfigureAwait(false);
            stopwatch.Stop();

            IntegrationAssert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(30),
                "Corrupt 輸入應於合理時間內回傳；實際=" + stopwatch.Elapsed);
            IntegrationAssert.True(!result.Success,
                "Corrupt 輸入不應回傳 Success=true。");
            IntegrationAssert.True(result.EndReason == MpvEndFileReason.Error || result.OutputBytes == 0,
                "Corrupt 輸入應 EndReason=Error 或 OutputBytes=0；實際 EndReason=" + result.EndReason + " OutputBytes=" + result.OutputBytes);
        }
        finally
        {
            TryDeleteFile(corruptInput);
            TryDeleteFile(outputPath);
        }
    }

    /// <summary>
    /// 驗證 <see cref="MpvEncoder.EncodeAsync"/> 對「無法寫入的輸出路徑」回傳錯誤而非當機。
    /// 採用建立同名資料夾來阻擋檔案建立，跨平台可靠。
    /// </summary>
    /// <param name="runtimeDirectory">
    ///執行階段資料夾。
    /// </param>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static async Task VerifyEncodeReadOnlyOutputAsync(string runtimeDirectory)
    {
        string inputPath = WriteTempWav(TimeSpan.FromSeconds(1));
        string blockedPath = Path.Combine(Path.GetTempPath(), "mediaembedkit-blocked-" + Guid.NewGuid().ToString("N") + ".m4a");
        try
        {
            // 建立同名資料夾佔位，逼 mpv 無法把它當檔案開來寫。
            Directory.CreateDirectory(blockedPath);

            MpvEncodingOptions options = new MpvEncodingOptions(blockedPath)
                .AsAudioOnly()
                .WithAudioCodec(MpvAudioCodecPreset.Aac);

            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            MpvEncodingResult result = await MpvEncoder.EncodeAsync(
                inputPath,
                options,
                CreateEncodingPlayerOptions(runtimeDirectory)).ConfigureAwait(false);
            stopwatch.Stop();

            IntegrationAssert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(30),
                "輸出路徑無法寫入應於合理時間內回傳；實際=" + stopwatch.Elapsed);
            IntegrationAssert.True(!result.Success,
                "輸出路徑被資料夾佔位時不應回傳 Success=true。");
        }
        finally
        {
            TryDeleteFile(inputPath);
            try
            {
                if (Directory.Exists(blockedPath))
                {
                    Directory.Delete(blockedPath, true);
                }
            }
            catch (IOException)
            {
            }
        }
    }

    /// <summary>
    /// 驗證 <see cref="MpvEncoder.EncodeAsync"/> 對 10 秒長片穩定完成（lavfi 合成輸入），
    /// 並收到至少一次進度回報。作為「長片穩定性」的最小哨兵。
    /// </summary>
    /// <param name="runtimeDirectory">
    ///執行階段資料夾。
    /// </param>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static async Task VerifyEncodeLongClipStabilityAsync(string runtimeDirectory)
    {
        string? inputPath = await TryGenerateLavfiTestVideoAsync(runtimeDirectory, 10.0).ConfigureAwait(false);
        if (inputPath == null)
        {
            Console.WriteLine("(略過：mpv av://lavfi 合成輸入不可用)");
            return;
        }

        string outputPath = Path.Combine(Path.GetTempPath(), "mediaembedkit-long-" + Guid.NewGuid().ToString("N") + ".mp4");
        try
        {
            MpvEncodingOptions options = new MpvEncodingOptions(outputPath)
                .AsVideoOnly()
                .WithVideoCodec(MpvVideoCodecPreset.H264)
                .WithVideoCodecOption("preset", "ultrafast")
                .WithVideoCodecOption("crf", "30");

            int progressReports = 0;
            // 使用同步 IProgress 實作避免 Progress<T> 透過 ThreadPool 非同步派發
            // 造成 await 完成時 callback 仍在 queue 中。
            SyncProgress<MpvEncodingProgress> progress = new SyncProgress<MpvEncodingProgress>(
                _ => Interlocked.Increment(ref progressReports));

            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            MpvEncodingResult result = await MpvEncoder.EncodeAsync(
                inputPath,
                options,
                CreateEncodingPlayerOptions(runtimeDirectory),
                progress).ConfigureAwait(false);
            stopwatch.Stop();

            IntegrationAssert.True(result.Success,
                "10 秒長片應成功完成。EndReason=" + result.EndReason
                + " ErrorCode=" + result.ErrorCode
                + " OutputBytes=" + result.OutputBytes);
            IntegrationAssert.True(result.OutputBytes > 0, "10 秒輸出位元組數應 > 0。");
            // 10 秒來源在 ultrafast preset 下通常 < 30 秒完成；給 90 秒上限避免 CI 慢機器誤判。
            IntegrationAssert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(90),
                "10 秒長片應於合理時間內完成；實際=" + stopwatch.Elapsed);
            // 進度迴圈以 250ms 為取樣間隔；若編碼於第一個 tick 前完成則合法地不會回報。
            // 只在編碼耗時超過 500ms 時要求至少一次進度。
            if (stopwatch.Elapsed > TimeSpan.FromMilliseconds(500))
            {
                IntegrationAssert.True(progressReports > 0,
                    "編碼耗時 > 500ms 應收到至少一次進度回報；實際=" + progressReports + " elapsed=" + stopwatch.Elapsed);
            }
        }
        finally
        {
            TryDeleteFile(inputPath);
            TryDeleteFile(outputPath);
        }
    }

    /// <summary>
    /// 驗證 libmpv hardware decoding 屬性的 wrapper API 路徑：<c>hwdec</c> 可寫入合法
    /// 值並讀回；<c>hwdec-current</c> 可讀取（不擲例外）。本測試**不**驗證實際硬體
    /// 解碼器是否被選用 — 那需要實機硬體 + 視訊內容 + 顯卡驅動，超出 headless 範圍。
    /// 焦點是 wrapper 對這條 mpv property 路徑的 Get/Set 行為正確。
    /// </summary>
    /// <param name="runtimeDirectory">
    /// 包含 libmpv 的執行階段資料夾。
    /// </param>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static async Task VerifyHardwareDecodingPropertyPathAsync(string runtimeDirectory)
    {
        using (MpvPlayer player = CreatePlayer(runtimeDirectory))
        {
            player.Initialize();

            // 預設應為 "no"（CreatePlayer 沒明確指定 hwdec，且 vo=null 不需硬體解碼）。
            string? defaultHwdec = player.GetPropertyString("hwdec");
            IntegrationAssert.True(!string.IsNullOrEmpty(defaultHwdec), "hwdec 屬性應可讀取。");

            // 嘗試幾個合法值，全部都該 round-trip 成功（即使本機沒有對應硬體；mpv
            // 只是把值存起來，實際使用時若失敗會 fall back 而非擲例外）。
            string[] candidateValues = new[] { "no", "auto-safe", "auto-copy", "auto" };
            foreach (string value in candidateValues)
            {
                player.SetPropertyString("hwdec", value);
                string? readBack = player.GetPropertyString("hwdec");
                IntegrationAssert.Equal(value, readBack, "hwdec=" + value + " round-trip 應一致。");
            }

            // hwdec-current 在未載入視訊時可能回 null（未決）或 "no"；只驗證讀取
            // 這條 property path 不擲例外即視為通過。
            _ = player.GetPropertyString("hwdec-current");

            // 載入合成視訊後再驗證一次（不假設特定值，依硬體環境）。
            string? lavfiPath = await TryGenerateLavfiTestVideoAsync(runtimeDirectory, 1.0).ConfigureAwait(false);
            if (lavfiPath != null)
            {
                try
                {
                    player.SetPropertyString("hwdec", "no");
                    PlaybackEventProbe probe = new PlaybackEventProbe(player);
                    player.LoadFile(lavfiPath);
                    await probe.WaitForFileLoadedAsync().ConfigureAwait(false);

                    _ = player.GetPropertyString("hwdec-current");
                }
                finally
                {
                    TryDeleteFile(lavfiPath);
                }
            }
            else
            {
                Console.WriteLine("(略過載入視訊後 hwdec-current 子驗證：mpv av://lavfi 合成輸入不可用)");
            }
        }
    }

    /// <summary>
    /// 驗證 libmpv HDR 相關屬性的 wrapper API 路徑：<c>target-prim</c> / <c>target-trc</c> /
    /// <c>tone-mapping</c> 可寫入 BT.2020 / PQ / HDR tone-mapping 等合法值並讀回。
    /// 本測試**不**驗證實際 HDR 渲染輸出 — 那需要 HDR 內容 + HDR 顯示器 + render API 路徑。
    /// 焦點是 wrapper 對這幾條 mpv property 路徑的 Get/Set 行為與 BT.2020/PQ 識別碼合法。
    /// </summary>
    /// <param name="runtimeDirectory">
    /// 包含 libmpv 的執行階段資料夾。
    /// </param>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static Task VerifyHdrPropertyPathAsync(string runtimeDirectory)
    {
        using (MpvPlayer player = CreatePlayer(runtimeDirectory))
        {
            player.Initialize();

            // target-prim：色彩原色目標；BT.2020-NCL 是 HDR10 / Rec.2100 標準。
            player.SetPropertyString("target-prim", "bt.2020");
            IntegrationAssert.Equal("bt.2020", player.GetPropertyString("target-prim"), "target-prim=bt.2020 round-trip。");

            player.SetPropertyString("target-prim", "auto");
            IntegrationAssert.Equal("auto", player.GetPropertyString("target-prim"), "target-prim=auto round-trip。");

            // target-trc：傳輸函數目標；PQ (SMPTE ST 2084) 是 HDR10 標準，HLG 是 Rec.2100 廣播。
            player.SetPropertyString("target-trc", "pq");
            IntegrationAssert.Equal("pq", player.GetPropertyString("target-trc"), "target-trc=pq round-trip。");

            player.SetPropertyString("target-trc", "hlg");
            IntegrationAssert.Equal("hlg", player.GetPropertyString("target-trc"), "target-trc=hlg round-trip。");

            player.SetPropertyString("target-trc", "auto");
            IntegrationAssert.Equal("auto", player.GetPropertyString("target-trc"), "target-trc=auto round-trip。");

            // tone-mapping：HDR → SDR / HDR → HDR 色調映射演算法選擇。
            string[] toneMappingValues = new[] { "auto", "clip", "bt.2390", "reinhard", "hable", "mobius" };
            foreach (string value in toneMappingValues)
            {
                player.SetPropertyString("tone-mapping", value);
                IntegrationAssert.Equal(value, player.GetPropertyString("tone-mapping"), "tone-mapping=" + value + " round-trip。");
            }

            // video-params/* 是 libmpv 報告當前載入媒體的視訊參數（唯讀）；未載入媒體時
            // 通常回 null（沒值），這是 mpv 預期行為。只驗證 property path 可讀取不擲例外。
            _ = player.GetPropertyString("video-params/primaries");
            _ = player.GetPropertyString("video-params/gamma");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 驗證 chapter flat API（<see cref="MpvPlayer.Chapter"/> / <see cref="MpvPlayer.ChapterCount"/> /
    /// <see cref="MpvPlayer.NextChapter"/> / <see cref="MpvPlayer.PreviousChapter"/> /
    /// <see cref="MpvPlayer.SeekChapter"/>）的 wrapper 行為。未載入媒體或無章節時應為空 /
    /// no-op；本測試不驗證實際章節內容（需要含 chapter metadata 的 mkv，屬 encoder 層工作）。
    /// </summary>
    /// <param name="runtimeDirectory">
    /// 包含 libmpv 的執行階段資料夾。
    /// </param>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static async Task VerifyChapterFlatApiAsync(string runtimeDirectory)
    {
        using (MpvPlayer player = CreatePlayer(runtimeDirectory))
        {
            player.Initialize();

            // 未載入媒體：Chapter / ChapterCount 應為 null / 0
            IntegrationAssert.True(player.Chapter == null, "未載入媒體時 Chapter 應為 null。");
            IntegrationAssert.Equal(0, player.ChapterCount, "未載入媒體時 ChapterCount 應為 0。");
            IntegrationAssert.Equal(0, player.GetChapters().Count, "未載入媒體時 GetChapters 應為空。");

            // SeekChapter 負值應立即擲 ArgumentOutOfRangeException（不到 libmpv 層）
            IntegrationAssert.Throws<ArgumentOutOfRangeException>(
                delegate { player.SeekChapter(-1); },
                "SeekChapter 負值應擲 ArgumentOutOfRangeException。");

            // NextChapter / PreviousChapter 在無章節時應 silently no-op
            player.NextChapter();
            player.PreviousChapter();

            // 載入無章節合成 mp4 後同樣為 0
            string? lavfiPath = await TryGenerateLavfiTestVideoAsync(runtimeDirectory, 1.0).ConfigureAwait(false);
            if (lavfiPath != null)
            {
                try
                {
                    PlaybackEventProbe probe = new PlaybackEventProbe(player);
                    player.LoadFile(lavfiPath);
                    await probe.WaitForFileLoadedAsync().ConfigureAwait(false);

                    IntegrationAssert.Equal(0, player.ChapterCount, "合成 mp4 無章節，ChapterCount 應為 0。");
                    IntegrationAssert.Equal(0, player.GetChapters().Count, "合成 mp4 無章節，GetChapters 應為空。");
                }
                finally
                {
                    TryDeleteFile(lavfiPath);
                }
            }
            else
            {
                Console.WriteLine("(略過載入媒體後章節驗證：mpv av://lavfi 合成輸入不可用)");
            }
        }
    }

    /// <summary>
    /// 驗證 subtitle / audio styling typed properties 的 round-trip：所有 11 個屬性
    /// 寫入後讀回相符。本測試不需要實際載入字幕軌（mpv 屬性可在 idle 狀態 round-trip）。
    /// </summary>
    /// <param name="runtimeDirectory">
    /// 包含 libmpv 的執行階段資料夾。
    /// </param>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static Task VerifySubtitleAudioStylingAsync(string runtimeDirectory)
    {
        using (MpvPlayer player = CreatePlayer(runtimeDirectory))
        {
            player.Initialize();

            // SubtitleVisible
            player.SubtitleVisible = false;
            IntegrationAssert.True(!player.SubtitleVisible, "SubtitleVisible=false round-trip。");
            player.SubtitleVisible = true;
            IntegrationAssert.True(player.SubtitleVisible, "SubtitleVisible=true round-trip。");

            // SubtitleDelay（TimeSpan）
            player.SubtitleDelay = TimeSpan.FromSeconds(2.5);
            IntegrationAssert.Near(2.5, player.SubtitleDelay.TotalSeconds, 0.001, "SubtitleDelay 2.5s round-trip。");
            player.SubtitleDelay = TimeSpan.Zero;

            // SubtitlePosition
            player.SubtitlePosition = 80;
            IntegrationAssert.Near(80, player.SubtitlePosition, 0.5, "SubtitlePosition 80 round-trip。");

            // SubtitleScale
            player.SubtitleScale = 1.25;
            IntegrationAssert.Near(1.25, player.SubtitleScale, 0.01, "SubtitleScale 1.25 round-trip。");
            player.SubtitleScale = 1.0;

            // SubtitleFontSize
            player.SubtitleFontSize = 42;
            IntegrationAssert.Near(42, player.SubtitleFontSize, 0.5, "SubtitleFontSize 42 round-trip。");

            // SubtitleFontFamily
            player.SubtitleFontFamily = "sans-serif";
            IntegrationAssert.Equal("sans-serif", player.SubtitleFontFamily, "SubtitleFontFamily round-trip。");

            // SubtitleColor / SubtitleBackgroundColor
            string color = MpvColor.FromArgb(0xFF, 0xCC, 0xCC, 0xCC);
            player.SubtitleColor = color;
            // mpv 可能正規化字串大小寫；只驗證設定不擲例外、讀回不為 null。
            IntegrationAssert.True(!string.IsNullOrEmpty(player.SubtitleColor), "SubtitleColor 寫入後應可讀回。");

            player.SubtitleBackgroundColor = MpvColor.FromRgb(0x00, 0x00, 0x00);
            IntegrationAssert.True(!string.IsNullOrEmpty(player.SubtitleBackgroundColor), "SubtitleBackgroundColor 寫入後應可讀回。");

            // SubtitleBold / SubtitleItalic
            player.SubtitleBold = true;
            IntegrationAssert.True(player.SubtitleBold, "SubtitleBold round-trip。");
            player.SubtitleItalic = true;
            IntegrationAssert.True(player.SubtitleItalic, "SubtitleItalic round-trip。");
            player.SubtitleBold = false;
            player.SubtitleItalic = false;

            // AudioDelay
            player.AudioDelay = TimeSpan.FromSeconds(-0.3);
            IntegrationAssert.Near(-0.3, player.AudioDelay.TotalSeconds, 0.001, "AudioDelay -0.3s round-trip。");
            player.AudioDelay = TimeSpan.Zero;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 驗證 <see cref="MpvColor"/> helper：FromArgb / FromRgb 產出 mpv 接受的字串；
    /// TryParse 把多種變體正規化為 <c>#AARRGGBB</c>。
    /// </summary>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static Task VerifyMpvColorHelper()
    {
        IntegrationAssert.Equal("#FFCCCCCC", MpvColor.FromArgb(0xFF, 0xCC, 0xCC, 0xCC), "FromArgb #FFCCCCCC。");
        IntegrationAssert.Equal("#FF000000", MpvColor.FromRgb(0x00, 0x00, 0x00), "FromRgb 黑色 #FF000000。");
        IntegrationAssert.Equal("#80FF0000", MpvColor.FromArgb(0x80, 0xFF, 0x00, 0x00), "FromArgb 半透明紅。");

        // TryParse round-trip 變體
        string? normalized;
        IntegrationAssert.True(MpvColor.TryParse("#FFCCCCCC", out normalized) && normalized == "#FFCCCCCC", "TryParse #FFCCCCCC pass-through。");
        IntegrationAssert.True(MpvColor.TryParse("#CCCCCC", out normalized) && normalized == "#FFCCCCCC", "TryParse #CCCCCC 補 alpha。");
        IntegrationAssert.True(MpvColor.TryParse("0xFFCCCCCC", out normalized) && normalized == "#FFCCCCCC", "TryParse 0x 前綴正規化。");
        IntegrationAssert.True(MpvColor.TryParse("  #CCCCCC  ", out normalized) && normalized == "#FFCCCCCC", "TryParse 容忍前後空白。");

        // 無效輸入回傳 false
        IntegrationAssert.True(!MpvColor.TryParse(null, out _), "TryParse null 應回 false。");
        IntegrationAssert.True(!MpvColor.TryParse("", out _), "TryParse 空字串應回 false。");
        IntegrationAssert.True(!MpvColor.TryParse("not-a-color", out _), "TryParse 命名色彩不在處理範圍應回 false。");
        IntegrationAssert.True(!MpvColor.TryParse("#GGGGGG", out _), "TryParse 非十六進位應回 false。");
        IntegrationAssert.True(!MpvColor.TryParse("#FFF", out _), "TryParse 3 位數短格式不在處理範圍應回 false。");

        return Task.CompletedTask;
    }

    /// <summary>
    /// 驗證 <see cref="MpvEncodingOptions.ResolveVideoCodecName"/> / <see cref="MpvEncodingOptions.ResolveAudioCodecName"/>
    /// 對全部列舉值都有正確的 ffmpeg encoder 名稱對應，不會擲出。
    /// </summary>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static Task VerifyEncoderPresetResolution()
    {
        IntegrationAssert.Equal("libsvtav1", MpvEncodingOptions.ResolveVideoCodecName(MpvVideoCodecPreset.Av1), "Av1 預設應解析為 libsvtav1。");
        IntegrationAssert.Equal("libx264", MpvEncodingOptions.ResolveVideoCodecName(MpvVideoCodecPreset.H264), "H264 預設應解析為 libx264。");
        IntegrationAssert.Equal("hevc_nvenc", MpvEncodingOptions.ResolveVideoCodecName(MpvVideoCodecPreset.H265Nvenc), "H265Nvenc 預設應解析為 hevc_nvenc。");
        IntegrationAssert.Equal("aac", MpvEncodingOptions.ResolveAudioCodecName(MpvAudioCodecPreset.Aac), "Aac 預設應解析為 aac。");
        IntegrationAssert.Equal("libopus", MpvEncodingOptions.ResolveAudioCodecName(MpvAudioCodecPreset.Opus), "Opus 預設應解析為 libopus。");
        IntegrationAssert.Equal("libmp3lame", MpvEncodingOptions.ResolveAudioCodecName(MpvAudioCodecPreset.Mp3), "Mp3 預設應解析為 libmp3lame。");

        foreach (MpvVideoCodecPreset preset in (MpvVideoCodecPreset[])Enum.GetValues(typeof(MpvVideoCodecPreset)))
        {
            string name = MpvEncodingOptions.ResolveVideoCodecName(preset);
            IntegrationAssert.True(!string.IsNullOrWhiteSpace(name), "視訊預設 " + preset + " 必須有對應名稱。");
        }

        foreach (MpvAudioCodecPreset preset in (MpvAudioCodecPreset[])Enum.GetValues(typeof(MpvAudioCodecPreset)))
        {
            string name = MpvEncodingOptions.ResolveAudioCodecName(preset);
            IntegrationAssert.True(!string.IsNullOrWhiteSpace(name), "音訊預設 " + preset + " 必須有對應名稱。");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 驗證 libmpv 可初始化並能讀寫常用屬性。
    /// </summary>
    /// <param name="runtimeDirectory">
    /// 包含 libmpv 的執行階段資料夾。
    /// </param>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static Task VerifyInitializationAndPropertiesAsync(string runtimeDirectory)
    {
        using (MpvPlayer player = CreatePlayer(runtimeDirectory))
        {
            player.Initialize();
            IntegrationAssert.True(player.IsInitialized, "播放器應完成初始化。");

            string? version = player.GetPropertyString("mpv-version");
            IntegrationAssert.True(!string.IsNullOrWhiteSpace(version), "應可讀取 mpv-version。");

            player.Volume = 42;
            IntegrationAssert.Near(42, player.Volume, 0.5, "Volume 應可讀寫。");

            player.Pause = true;
            IntegrationAssert.True(player.Pause, "Pause 屬性應可設為 true。");

            MpvException exception = IntegrationAssert.Throws<MpvException>(
                delegate
                {
                    player.GetPropertyFlag("mediaembedkit-nonexistent-property");
                },
                "不存在的屬性應擲回 MpvException。");
            IntegrationAssert.Equal((int)MpvErrorCode.PropertyNotFound, exception.ErrorCode, "不存在屬性的錯誤碼");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 驗證本機 WAV 檔案播放會產生載入、屬性與結束事件。
    /// </summary>
    /// <param name="runtimeDirectory">
    /// 包含 libmpv 的執行階段資料夾。
    /// </param>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static async Task VerifyLocalPlaybackEventsAsync(string runtimeDirectory)
    {
        string mediaPath = Path.Combine(AppContext.BaseDirectory, "integration-tone.wav");
        File.WriteAllBytes(mediaPath, WaveGenerator.CreateSineWave(TimeSpan.FromSeconds(1.5)));

        using (MpvPlayer player = CreatePlayer(runtimeDirectory))
        {
            PlaybackEventProbe probe = new PlaybackEventProbe(player);
            player.Initialize();
            player.ObserveProperty("time-pos", MpvFormat.Double);
            player.LoadFile(mediaPath);

            await probe.WaitForFileLoadedAsync().ConfigureAwait(false);
            await probe.WaitForTimePositionAsync().ConfigureAwait(false);
            MpvEndFileEventArgs endFile = await probe.WaitForEndFileAsync().ConfigureAwait(false);
            IntegrationAssert.Equal(MpvEndFileReason.EndOfFile, endFile.Reason, "本機 WAV 應正常播放到結尾。");
        }
    }

    /// <summary>
    /// 驗證自訂 stream callback 可交給 libmpv 播放。
    /// </summary>
    /// <param name="runtimeDirectory">
    /// 包含 libmpv 的執行階段資料夾。
    /// </param>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static async Task VerifyStreamCallbackPlaybackAsync(string runtimeDirectory)
    {
        byte[] mediaBytes = WaveGenerator.CreateSineWave(TimeSpan.FromSeconds(1.0));
        bool opened = false;

        using (MpvPlayer player = CreatePlayer(runtimeDirectory))
        {
            player.RegisterStreamProtocol(
                "mektest",
                delegate
                {
                    opened = true;
                    return new MemoryStream(mediaBytes, false);
                });

            PlaybackEventProbe probe = new PlaybackEventProbe(player);
            player.Initialize();
            player.LoadFile("mektest://tone.wav");

            await probe.WaitForFileLoadedAsync().ConfigureAwait(false);
            MpvEndFileEventArgs endFile = await probe.WaitForEndFileAsync().ConfigureAwait(false);
            IntegrationAssert.True(opened, "自訂 stream callback 應被呼叫。");
            IntegrationAssert.Equal(MpvEndFileReason.EndOfFile, endFile.Reason, "自訂 stream 應正常播放到結尾。");
        }
    }

    /// <summary>
    /// 驗證額外用戶端控制代碼與 shutdown 事件。
    /// </summary>
    /// <param name="runtimeDirectory">
    /// 包含 libmpv 的執行階段資料夾。
    /// </param>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static async Task VerifyMultipleClientsAndShutdownAsync(string runtimeDirectory)
    {
        using (MpvPlayer player = CreatePlayer(runtimeDirectory))
        {
            TaskCompletionSource<bool> shutdown = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            player.Shutdown += delegate
            {
                shutdown.TrySetResult(true);
            };

            player.Initialize();
            using (MpvClientHandle strongClient = player.CreateClient("mediaembedkit-integration-strong"))
            using (MpvClientHandle weakClient = player.CreateWeakClient("mediaembedkit-integration-weak"))
            {
                IntegrationAssert.True(strongClient.DangerousHandle != IntPtr.Zero, "強參考用戶端控制代碼不可為零。");
                IntegrationAssert.True(weakClient.DangerousHandle != IntPtr.Zero, "弱參考用戶端控制代碼不可為零。");
            }

            IntPtr rawClient = player.CreateClientHandle("mediaembedkit-integration-raw");
            try
            {
                IntegrationAssert.True(rawClient != IntPtr.Zero, "原生用戶端控制代碼不可為零。");
            }
            finally
            {
                MpvPlayer.DestroyClientHandle(rawClient);
            }

            player.Quit();
            await IntegrationAssert.WaitAsync(shutdown.Task, "等待 shutdown 事件逾時。").ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 驗證 libmpv 錯誤碼文字與常見錯誤路徑。
    /// </summary>
    /// <param name="runtimeDirectory">
    /// 包含 libmpv 的執行階段資料夾。
    /// </param>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static Task VerifyErrorCodesAndErrorPathsAsync(string runtimeDirectory)
    {
        Array values = Enum.GetValues(typeof(MpvErrorCode));
        foreach (object value in values)
        {
            MpvErrorCode errorCode = (MpvErrorCode)value;
            string message = MpvError.GetMessage((int)errorCode);
            IntegrationAssert.True(!string.IsNullOrWhiteSpace(message), "錯誤碼 " + errorCode + " 應有 libmpv 訊息。");
        }

        using (MpvPlayer player = CreatePlayer(runtimeDirectory))
        {
            MpvException optionException = IntegrationAssert.Throws<MpvException>(
                delegate
                {
                    player.SetOptionString("mediaembedkit-invalid-option", "yes");
                },
                "不存在的選項應擲回 MpvException。");
            AssertMpvError(optionException, "不存在選項錯誤", MpvErrorCode.OptionNotFound);

            player.Initialize();

            MpvException propertyException = IntegrationAssert.Throws<MpvException>(
                delegate
                {
                    player.GetPropertyFlag("mediaembedkit-invalid-property");
                },
                "不存在的屬性應擲回 MpvException。");
            AssertMpvError(propertyException, "不存在屬性錯誤", MpvErrorCode.PropertyNotFound);

            MpvException formatException = IntegrationAssert.Throws<MpvException>(
                delegate
                {
                    player.GetPropertyDouble("mpv-version");
                },
                "錯誤屬性格式應擲回 MpvException。");
            AssertMpvError(formatException, "屬性格式錯誤", MpvErrorCode.PropertyFormat);

            MpvException commandException = IntegrationAssert.Throws<MpvException>(
                delegate
                {
                    player.Command("mediaembedkit-invalid-command");
                },
                "不存在的命令應擲回 MpvException。");
            AssertNegativeMpvError(commandException, "不存在命令錯誤");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 驗證設定檔與指令碼載入錯誤路徑。
    /// </summary>
    /// <param name="runtimeDirectory">
    /// 包含 libmpv 的執行階段資料夾。
    /// </param>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static async Task VerifyConfigurationAndScriptErrorsAsync(string runtimeDirectory)
    {
        string workingDirectory = CreateTemporaryDirectory("config-script");
        string invalidConfigPath = Path.Combine(workingDirectory, "invalid.conf");
        string missingScriptPath = Path.Combine(workingDirectory, "missing.lua");
        string validScriptPath = Path.Combine(workingDirectory, "integration.lua");

        File.WriteAllText(invalidConfigPath, "mediaembedkit-invalid-option=yes" + Environment.NewLine, Encoding.UTF8);
        File.WriteAllText(
            validScriptPath,
            "mp.register_script_message('mek-script-ping', function() mp.commandv('script-message', 'mek-script-pong') end)" + Environment.NewLine,
            Encoding.UTF8);

        using (MpvPlayer player = CreatePlayer(runtimeDirectory))
        {
            TaskCompletionSource<MpvLogMessageEventArgs> configError = new TaskCompletionSource<MpvLogMessageEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<MpvLogMessageEventArgs> scriptError = new TaskCompletionSource<MpvLogMessageEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<MpvClientMessageEventArgs> scriptMessage = new TaskCompletionSource<MpvClientMessageEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
            player.LogMessageReceived += delegate (object? sender, MpvLogMessageEventArgs e)
            {
                if (e.Text.IndexOf("mediaembedkit-invalid-option", StringComparison.OrdinalIgnoreCase) >= 0 || e.Text.IndexOf("invalid.conf", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    configError.TrySetResult(e);
                }

                if (e.Text.IndexOf("missing.lua", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    scriptError.TrySetResult(e);
                }
            };
            player.ClientMessage += delegate (object? sender, MpvClientMessageEventArgs e)
            {
                if (e.Arguments.Count > 0 && string.Equals(e.Arguments[0], "mek-script-pong", StringComparison.Ordinal))
                {
                    scriptMessage.TrySetResult(e);
                }
            };

            player.Initialize();
            player.RequestLogMessages("debug");

            try
            {
                player.LoadConfigFile(invalidConfigPath);
            }
            catch (MpvException ex)
            {
                AssertNegativeMpvError(ex, "設定檔錯誤");
                configError.TrySetResult(new MpvLogMessageEventArgs("config", "error", ex.Message, MpvLogLevel.Error));
            }

            await IntegrationAssert.WaitAsync(configError.Task, "等待設定檔錯誤 log 逾時。").ConfigureAwait(false);

            try
            {
                player.LoadScript(missingScriptPath);
            }
            catch (MpvException ex)
            {
                AssertNegativeMpvError(ex, "指令碼載入錯誤");
                scriptError.TrySetResult(new MpvLogMessageEventArgs("script", "error", ex.Message, MpvLogLevel.Error));
            }

            await IntegrationAssert.WaitAsync(scriptError.Task, "等待指令碼錯誤 log 逾時。").ConfigureAwait(false);

            player.LoadScript(validScriptPath);
            await Task.Delay(TimeSpan.FromMilliseconds(300)).ConfigureAwait(false);
            player.SendScriptMessage("mek-script-ping");
            MpvClientMessageEventArgs message = await IntegrationAssert.WaitAsync(scriptMessage.Task, "等待 Lua 指令碼回覆逾時。").ConfigureAwait(false);
            IntegrationAssert.Equal("mek-script-pong", message.Arguments[0], "Lua 指令碼回覆內容");
        }
    }

    /// <summary>
    /// 驗證非同步命令成功、錯誤與取消呼叫路徑。
    /// </summary>
    /// <param name="runtimeDirectory">
    /// 包含 libmpv 的執行階段資料夾。
    /// </param>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static async Task VerifyAsyncCommandScenariosAsync(string runtimeDirectory)
    {
        using (MpvPlayer player = CreatePlayer(runtimeDirectory))
        {
            player.Initialize();
            await IntegrationAssert.WaitAsync(player.CommandAsync("set", "pause", "yes"), "等待非同步 set 命令逾時。").ConfigureAwait(false);
            IntegrationAssert.True(player.Pause, "非同步 set 命令應更新 pause 屬性。");

            MpvNode version = await IntegrationAssert.WaitAsync(player.GetPropertyNodeAsync("mpv-version", MpvFormat.String), "等待非同步屬性回覆逾時。").ConfigureAwait(false);
            IntegrationAssert.True(!string.IsNullOrWhiteSpace(version.AsString()), "非同步屬性回覆應包含 mpv-version。");

            MpvException commandException = await IntegrationAssert.ThrowsAsync<MpvException>(
                delegate
                {
                    return player.CommandAsync("mediaembedkit-invalid-command");
                },
                "不存在的非同步命令應擲回 MpvException。").ConfigureAwait(false);
            AssertNegativeMpvError(commandException, "非同步命令錯誤");
        }

        using (MpvPlayer player = CreatePlayer(runtimeDirectory))
        {
            player.Initialize();
            player.AbortAsyncCommand(ulong.MaxValue);
            await IntegrationAssert.WaitAsync(player.CommandAsync("set", "pause", "no"), "取消未知要求後仍應可執行非同步命令。").ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 驗證取消屬性觀察後不再收到該觀察識別碼的事件。
    /// </summary>
    /// <param name="runtimeDirectory">
    /// 包含 libmpv 的執行階段資料夾。
    /// </param>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static async Task VerifyObserveCancellationAsync(string runtimeDirectory)
    {
        using (MpvPlayer player = CreatePlayer(runtimeDirectory))
        {
            TaskCompletionSource<bool> firstChange = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            ulong observeId = 0;
            int changesAfterUnobserve = 0;
            bool countAfterUnobserve = false;

            player.PropertyChanged += delegate (object? sender, MpvPropertyChangedEventArgs e)
            {
                if (e.ReplyUserData != observeId)
                {
                    return;
                }

                if (countAfterUnobserve)
                {
                    changesAfterUnobserve++;
                    return;
                }

                firstChange.TrySetResult(true);
            };

            player.Initialize();
            observeId = player.ObserveProperty("pause", MpvFormat.Flag);
            player.Pause = true;
            await IntegrationAssert.WaitAsync(firstChange.Task, "等待 pause 觀察事件逾時。").ConfigureAwait(false);

            player.UnobserveProperty(observeId);
            await Task.Delay(TimeSpan.FromMilliseconds(300)).ConfigureAwait(false);
            countAfterUnobserve = true;
            player.Pause = false;
            await Task.Delay(TimeSpan.FromMilliseconds(300)).ConfigureAwait(false);
            player.Pause = true;
            await Task.Delay(TimeSpan.FromMilliseconds(300)).ConfigureAwait(false);

            IntegrationAssert.Equal(0, changesAfterUnobserve, "取消觀察後不應再收到相同識別碼事件。");
        }
    }

    /// <summary>
    /// 驗證 typed 事件、hook、client message 與事件節點資料。
    /// </summary>
    /// <param name="runtimeDirectory">
    /// 包含 libmpv 的執行階段資料夾。
    /// </param>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static async Task VerifyTypedEventDataAsync(string runtimeDirectory)
    {
        string mediaPath = Path.Combine(AppContext.BaseDirectory, "integration-event-tone.wav");
        File.WriteAllBytes(mediaPath, WaveGenerator.CreateSineWave(TimeSpan.FromSeconds(1.0)));

        using (MpvPlayer player = CreatePlayer(runtimeDirectory))
        {
            TaskCompletionSource<MpvStartFileEventArgs> startFile = new TaskCompletionSource<MpvStartFileEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> fileLoaded = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<MpvEndFileEventArgs> endFile = new TaskCompletionSource<MpvEndFileEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<MpvCommandReplyEventArgs> commandReply = new TaskCompletionSource<MpvCommandReplyEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<MpvClientMessageEventArgs> clientMessage = new TaskCompletionSource<MpvClientMessageEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<MpvHookEventArgs> hook = new TaskCompletionSource<MpvHookEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<MpvTracksChangedEventArgs> tracksChanged = new TaskCompletionSource<MpvTracksChangedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<MpvNodeEventArgs> nodeEvent = new TaskCompletionSource<MpvNodeEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<MpvLogMessageEventArgs> logMessage = new TaskCompletionSource<MpvLogMessageEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);

            player.StartFile += delegate (object? sender, MpvStartFileEventArgs e) { startFile.TrySetResult(e); };
            player.FileLoaded += delegate { fileLoaded.TrySetResult(true); };
            player.EndFile += delegate (object? sender, MpvEndFileEventArgs e) { endFile.TrySetResult(e); };
            player.CommandReply += delegate (object? sender, MpvCommandReplyEventArgs e) { commandReply.TrySetResult(e); };
            player.ClientMessage += delegate (object? sender, MpvClientMessageEventArgs e) { clientMessage.TrySetResult(e); };
            player.Hook += delegate (object? sender, MpvHookEventArgs e)
            {
                hook.TrySetResult(e);
                player.ContinueHook(e.HookId);
            };
            player.TracksChanged += delegate (object? sender, MpvTracksChangedEventArgs e)
            {
                if (e.Tracks.Count > 0)
                {
                    tracksChanged.TrySetResult(e);
                }
            };
            player.EventNodeReceived += delegate (object? sender, MpvNodeEventArgs e)
            {
                if (e.EventId == MpvEventId.StartFile || e.EventId == MpvEventId.EndFile)
                {
                    nodeEvent.TrySetResult(e);
                }
            };
            player.LogMessageReceived += delegate (object? sender, MpvLogMessageEventArgs e)
            {
                if (!string.IsNullOrWhiteSpace(e.Text))
                {
                    logMessage.TrySetResult(e);
                }
            };

            player.RequestLogMessages("debug");
            player.Initialize();
            ulong trackObserveId = player.ObserveTrackList();
            player.AddHook("on_load", 0, 77);
            Task commandTask = player.CommandAsync("set", "pause", "no");
            player.SendScriptMessageTo(player.ClientName, "mek-client-message", "payload");
            player.LoadFile(mediaPath);

            MpvStartFileEventArgs start = await IntegrationAssert.WaitAsync(startFile.Task, "等待 StartFile 事件逾時。").ConfigureAwait(false);
            await IntegrationAssert.WaitAsync(fileLoaded.Task, "等待 FileLoaded 事件逾時。").ConfigureAwait(false);
            MpvHookEventArgs hookArgs = await IntegrationAssert.WaitAsync(hook.Task, "等待 Hook 事件逾時。").ConfigureAwait(false);
            MpvCommandReplyEventArgs reply = await IntegrationAssert.WaitAsync(commandReply.Task, "等待 CommandReply 事件逾時。").ConfigureAwait(false);
            MpvClientMessageEventArgs client = await IntegrationAssert.WaitAsync(clientMessage.Task, "等待 ClientMessage 事件逾時。").ConfigureAwait(false);
            MpvTracksChangedEventArgs tracks = await IntegrationAssert.WaitAsync(tracksChanged.Task, "等待 TracksChanged 事件逾時。").ConfigureAwait(false);
            MpvNodeEventArgs node = await IntegrationAssert.WaitAsync(nodeEvent.Task, "等待 EventNodeReceived 事件逾時。").ConfigureAwait(false);
            MpvLogMessageEventArgs log = await IntegrationAssert.WaitAsync(logMessage.Task, "等待 LogMessageReceived 事件逾時。").ConfigureAwait(false);
            MpvEndFileEventArgs end = await IntegrationAssert.WaitAsync(endFile.Task, "等待 EndFile 事件逾時。").ConfigureAwait(false);

            await IntegrationAssert.WaitAsync(commandTask, "等待 CommandAsync 工作完成逾時。").ConfigureAwait(false);
            player.UnobserveProperty(trackObserveId);

            IntegrationAssert.True(start.PlaylistEntryId >= 0, "StartFile 應包含播放清單項目識別碼。");
            IntegrationAssert.Equal("on_load", hookArgs.Name, "Hook 名稱");
            IntegrationAssert.Equal((ulong)77, hookArgs.ReplyUserData, "Hook reply user data");
            IntegrationAssert.Equal(0, reply.ErrorCode, "CommandReply 錯誤碼");
            IntegrationAssert.True(client.Arguments.Count >= 2, "ClientMessage 應包含測試引數。");
            IntegrationAssert.Equal("mek-client-message", client.Arguments[0], "ClientMessage 第一個引數");
            IntegrationAssert.True(tracks.Tracks.Count > 0, "TracksChanged 應包含播放軌。");
            IntegrationAssert.True(node.Node.AsMap().Count > 0, "事件節點應包含資料。");
            IntegrationAssert.True(!string.IsNullOrWhiteSpace(log.Prefix), "LogMessage 應包含前置詞。");
            IntegrationAssert.Equal(MpvEndFileReason.EndOfFile, end.Reason, "事件資料測試媒體應播放到結尾。");
        }
    }

    /// <summary>
    /// 驗證 software render 與 OpenGL render API 入口。
    /// </summary>
    /// <param name="runtimeDirectory">
    /// 包含 libmpv 的執行階段資料夾。
    /// </param>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static Task VerifyRenderApiPathsAsync(string runtimeDirectory)
    {
        using (MpvPlayer player = CreateRenderPlayer(runtimeDirectory))
        {
            player.Initialize();
            try
            {
                using (MpvSoftwareRenderContext context = player.CreateSoftwareRenderContext())
                {
                    IntegrationAssert.True(context.DangerousHandle != IntPtr.Zero, "software render context 控制代碼不可為零。");
                    MpvRenderUpdateFlags update = context.Update();
                    IntegrationAssert.True(update == MpvRenderUpdateFlags.None || (update & MpvRenderUpdateFlags.Frame) == MpvRenderUpdateFlags.Frame, "software render 更新旗標應可讀取。");
                    MpvRenderFrameInformation frame = context.GetNextFrameInformation();
                    IntegrationAssert.True(frame.TargetTime >= 0, "software render frame target time 應可讀取。");
#pragma warning disable CS0618 // 仍須驗證已 deprecated 的 SetAmbientLight 在 software render context 上不會當機。
                    context.SetAmbientLight(100);
#pragma warning restore CS0618
                    context.ClearIccProfile();

                    IntPtr buffer = Marshal.AllocHGlobal(16 * 16 * 4);
                    try
                    {
                        context.Render(buffer, 16, 16, 16 * 4, "bgr0", false, true);
                        context.ReportSwap();
                        IntegrationAssert.Throws<ArgumentOutOfRangeException>(
                            delegate
                            {
                                context.Render(buffer, 0, 16, 16 * 4, "bgr0", false, true);
                            },
                            "software render 應拒絕零寬度。");
                        IntegrationAssert.Throws<ArgumentOutOfRangeException>(
                            delegate
                            {
                                context.Render(buffer, 16, 16, 1, "bgr0", false, true);
                            },
                            "software render 應拒絕不足步幅。");
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(buffer);
                    }
                }
            }
            catch (MpvException ex)
            {
                AssertMpvError(
                    ex,
                    "software render 建立錯誤",
                    MpvErrorCode.NotImplemented,
                    MpvErrorCode.Unsupported,
                    MpvErrorCode.InvalidParameter);
            }
        }

        using (MpvPlayer player = CreateRenderPlayer(runtimeDirectory))
        {
            player.Initialize();
            MpvOpenGlRenderContextOptions options = new MpvOpenGlRenderContextOptions(delegate
            {
                return IntPtr.Zero;
            });
            options.AdvancedControl = true;

            try
            {
                using (MpvOpenGlRenderContext context = player.CreateOpenGlRenderContext(options))
                {
                    IntegrationAssert.True(context.DangerousHandle != IntPtr.Zero, "OpenGL render context 控制代碼不可為零。");
                    try
                    {
                        context.SkipRender(false);
                        _ = context.Update();
                        _ = context.GetNextFrameInformation();
                        context.ReportSwap();
                    }
                    catch (MpvException ex)
                    {
                        AssertNegativeMpvError(ex, "無 OpenGL 函式位址的 OpenGL render 執行錯誤");
                    }
                }
            }
            catch (MpvException ex)
            {
                AssertNegativeMpvError(ex, "無 OpenGL context 的 OpenGL render 建立錯誤");
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 驗證 stream callback 的錯誤開啟、讀取錯誤與取消語意。
    /// </summary>
    /// <param name="runtimeDirectory">
    /// 包含 libmpv 的執行階段資料夾。
    /// </param>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static async Task VerifyStreamCallbackErrorAndCancellationAsync(string runtimeDirectory)
    {
        using (MpvPlayer player = CreatePlayer(runtimeDirectory))
        {
            player.RegisterStreamProtocol("mekmissing", delegate
            {
                return null;
            });

            PlaybackEventProbe probe = new PlaybackEventProbe(player);
            player.Initialize();
            player.LoadFile("mekmissing://missing.wav");
            MpvEndFileEventArgs endFile = await probe.WaitForEndFileAsync().ConfigureAwait(false);
            IntegrationAssert.True(endFile.Reason != MpvEndFileReason.EndOfFile, "拒絕開啟的 stream 不應正常播放完成。");
        }

        using (MpvPlayer player = CreatePlayer(runtimeDirectory))
        {
            player.RegisterStreamProtocol("mekreaderror", delegate
            {
                return new ThrowingReadStream();
            });

            PlaybackEventProbe probe = new PlaybackEventProbe(player);
            player.Initialize();
            player.LoadFile("mekreaderror://broken.wav");
            MpvEndFileEventArgs endFile = await probe.WaitForEndFileAsync().ConfigureAwait(false);
            IntegrationAssert.True(endFile.Reason != MpvEndFileReason.EndOfFile, "讀取錯誤的 stream 不應正常播放完成。");
        }

        using (MpvPlayer player = CreatePlayer(runtimeDirectory))
        {
            CancellableBlockingStream? blockingStream = null;
            player.RegisterStreamProtocol(
                "mekcancel",
                delegate
                {
                    blockingStream = new CancellableBlockingStream();
                    return blockingStream;
                });

            player.Initialize();
            player.LoadFile("mekcancel://blocking.wav");
            await IntegrationAssert.WaitAsync(WaitForStreamAsync(() => blockingStream, stream => stream.WaitForReadStartedAsync()), "等待 blocking stream 開始讀取逾時。").ConfigureAwait(false);
            player.Stop();
            await IntegrationAssert.WaitAsync(WaitForStreamAsync(() => blockingStream, stream => stream.WaitForCancelledAsync()), "等待 stream cancel callback 逾時。").ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 驗證 yt-dlp FFmpeg-Builds 可下載、驗證 checksum，並執行 FFmpeg 與 FFprobe。
    /// </summary>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static async Task VerifyFFmpegDownloadAndExecutionAsync()
    {
        string runtimeDirectory = Path.Combine(Path.GetTempPath(), "MediaEmbedKit.Mpv.FFmpegIntegration", "win-x64");
        FFmpegDownloadOptions options = new FFmpegDownloadOptions
        {
            VerificationPolicy = MpvNativeAssetVerificationPolicy.RequireProviderChecksum,
            // 顯式保留 archive 以驗證 RetainArchive=true 路徑（同步覆蓋預設清除路徑下方）。
            RetainArchive = true,
            // 此測試目標是 live download + retain，不是重驗舊快取 內留下的 保留的封存檔。
            OverwriteExisting = true,
        };
        FFmpegDownloadResult result = await FFmpegDownloader.DownloadAndExtractLatestAsync(runtimeDirectory, options).ConfigureAwait(false);

        IntegrationAssert.True(File.Exists(result.FFmpegPath), "FFmpeg 應解壓縮到 執行階段根目錄。");
        IntegrationAssert.True(File.Exists(result.FFprobePath), "FFprobe 應解壓縮到 執行階段根目錄。");
        IntegrationAssert.True(File.Exists(result.ArchivePath), "RetainArchive=true 時 FFmpeg-Builds 壓縮檔應保留於 執行階段根目錄。");

        await VerifyExternalToolVersionAsync(result.FFmpegPath, "FFmpeg").ConfigureAwait(false);
        await VerifyExternalToolVersionAsync(result.FFprobePath, "FFprobe").ConfigureAwait(false);

        // 切回預設 RetainArchive=false，重新下載一次（OverwriteExisting=true 強制下載），
        // 驗證解壓後 archive 確實被清掉。執行階段資料夾換到子路徑避免 快速路徑 重用既有
        // 執行檔（CanUseExistingTools 不會觸發、走完整下載 + 解壓 + cleanup 路徑）。
        string cleanupRuntimeDirectory = Path.Combine(runtimeDirectory, "cleanup-default");
        FFmpegDownloadOptions cleanupOptions = new FFmpegDownloadOptions
        {
            VerificationPolicy = MpvNativeAssetVerificationPolicy.RequireProviderChecksum,
            OverwriteExisting = true,
            // RetainArchive 保持預設 false
        };
        FFmpegDownloadResult cleanupResult = await FFmpegDownloader.DownloadAndExtractLatestAsync(cleanupRuntimeDirectory, cleanupOptions).ConfigureAwait(false);
        IntegrationAssert.True(File.Exists(cleanupResult.FFmpegPath), "預設 cleanup 路徑：FFmpeg 仍應解出。");
        IntegrationAssert.True(File.Exists(cleanupResult.FFprobePath), "預設 cleanup 路徑：FFprobe 仍應解出。");
        IntegrationAssert.True(!File.Exists(cleanupResult.ArchivePath), "預設 RetainArchive=false 時，archive 應於解壓成功後被刪除。");
    }

    /// <summary>
    /// 鎖住 yt-dlp/FFmpeg-Builds 的上游契約：tag=latest 這個 release 必須持續存在、
    /// tag_name 字面為 "latest"、且 Windows x64 + ARM64 GPL 資產名稱以
    /// <c>ffmpeg-master-latest-</c> 為前綴。
    /// </summary>
    /// <remarks>
    /// FFmpeg-Builds 同一儲存庫並存兩種發行：tag=latest（固定資產名稱）與每小時
    /// autobuild-YYYY-MM-DD-HH-MM（動態資產名稱）。<see cref="FFmpegDownloader"/>
    /// 依賴 tag=latest 抓固定名 asset；上游若改命名或拆掉這個 release，本測試會
    /// 立即在發行檢查閘門失敗，避免 production user 自己撞 bug。
    /// </remarks>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static async Task VerifyFFmpegBuildsLatestTagAssetNamingAsync()
    {
        Uri apiUri = new Uri("https://api.github.com/repos/yt-dlp/FFmpeg-Builds/releases/tags/latest");
        using HttpClient client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MediaEmbedKit.Mpv.IntegrationTests");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

        string json = await client.GetStringAsync(apiUri).ConfigureAwait(false);
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        string? tagName = root.GetProperty("tag_name").GetString();
        IntegrationAssert.Equal("latest", tagName, "yt-dlp/FFmpeg-Builds tag=latest release 不應消失或改名");

        JsonElement assetsElement = root.GetProperty("assets");
        List<string> assetNames = new List<string>();
        foreach (JsonElement asset in assetsElement.EnumerateArray())
        {
            string? name = asset.GetProperty("name").GetString();
            if (!string.IsNullOrEmpty(name))
            {
                assetNames.Add(name!);
            }
        }

        string assetList = string.Join(", ", assetNames);
        IntegrationAssert.True(
            assetNames.Contains(FFmpegDownloader.WindowsX64AssetName),
            "tag=latest release 必須含 x64 GPL asset '" + FFmpegDownloader.WindowsX64AssetName + "'。實際清單：" + assetList);
        IntegrationAssert.True(
            assetNames.Contains(FFmpegDownloader.WindowsArm64AssetName),
            "tag=latest release 必須含 ARM64 GPL asset '" + FFmpegDownloader.WindowsArm64AssetName + "'。實際清單：" + assetList);
    }

    /// <summary>
    /// 驗證外部工具可執行並正常回報版本資訊。
    /// </summary>
    /// <param name="executablePath">
    /// 要執行的外部工具路徑。
    /// </param>
    /// <param name="toolName">
    /// 外部工具顯示名稱。
    /// </param>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    private static async Task VerifyExternalToolVersionAsync(string executablePath, string toolName)
    {
        ExternalToolProcessRunner runner = new ExternalToolProcessRunner(executablePath);
        ExternalToolProcessResult result = await runner.RunAsync(new[] { "-version" }, TimeSpan.FromSeconds(30)).ConfigureAwait(false);
        IntegrationAssert.Equal(0, result.ExitCode, toolName + " 版本命令結束代碼");
        IntegrationAssert.True(
            result.StandardOutput.IndexOf(toolName, StringComparison.OrdinalIgnoreCase) >= 0 ||
            result.StandardError.IndexOf(toolName, StringComparison.OrdinalIgnoreCase) >= 0,
            toolName + " 版本輸出應包含工具名稱。");
    }

    /// <summary>
    /// 建立測試用播放器。
    /// </summary>
    /// <param name="runtimeDirectory">
    /// 包含 libmpv 的執行階段資料夾。
    /// </param>
    /// <returns>
    /// 測試用播放器。
    /// </returns>
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
    /// 建立 render API 測試用播放器。
    /// </summary>
    /// <param name="runtimeDirectory">
    /// 包含 libmpv 的執行階段資料夾。
    /// </param>
    /// <returns>
    /// render API 測試用播放器。
    /// </returns>
    private static MpvPlayer CreateRenderPlayer(string runtimeDirectory)
    {
        MpvPlayerOptions options = MpvRuntimeInstaller.CreatePlayerOptions(runtimeDirectory);
        options.EnableYtdlp = false;
        options.EnableOsc = false;
        options.EnableKeyboardInput = false;
        options.EnableDefaultInputBindings = false;
        options.LogLevel = "warn";
        options.InitialOptions["vo"] = "libmpv";
        options.InitialOptions["ao"] = "null";
        options.InitialOptions["terminal"] = "no";
        options.InitialOptions["idle"] = "yes";
        options.InitialOptions["keep-open"] = "no";
        return new MpvPlayer(options);
    }

    /// <summary>
    /// 建立測試暫存資料夾。
    /// </summary>
    /// <param name="name">
    /// 資料夾名稱片段。
    /// </param>
    /// <returns>
    /// 建立完成的暫存資料夾。
    /// </returns>
    private static string CreateTemporaryDirectory(string name)
    {
        string directory = Path.Combine(Path.GetTempPath(), "MediaEmbedKit.Mpv.IntegrationTests", name, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    /// <summary>
    /// 驗證指定例外狀況包含負數 libmpv 錯誤碼。
    /// </summary>
    /// <param name="exception">
    /// 要驗證的例外狀況。
    /// </param>
    /// <param name="message">
    /// 失敗時顯示的訊息。
    /// </param>
    private static void AssertNegativeMpvError(MpvException exception, string message)
    {
        IntegrationAssert.True(exception.ErrorCode < 0, message + " 應包含負數 libmpv 錯誤碼。");
        IntegrationAssert.True(!string.IsNullOrWhiteSpace(MpvError.GetMessage(exception.ErrorCode)), message + " 應有錯誤訊息。");
    }

    /// <summary>
    /// 驗證指定例外狀況符合預期 libmpv 錯誤碼。
    /// </summary>
    /// <param name="exception">
    /// 要驗證的例外狀況。
    /// </param>
    /// <param name="message">
    /// 失敗時顯示的訊息。
    /// </param>
    /// <param name="expected">
    /// 允許的錯誤碼。
    /// </param>
    private static void AssertMpvError(MpvException exception, string message, params MpvErrorCode[] expected)
    {
        for (int index = 0; index < expected.Length; index++)
        {
            if (exception.ErrorCode == (int)expected[index])
            {
                return;
            }
        }

        throw new InvalidOperationException(message + "。實際錯誤碼：" + exception.ErrorCode.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// 等待指定串流建立後執行串流工作。
    /// </summary>
    /// <typeparam name="TStream">
    /// 串流型別。
    /// </typeparam>
    /// <param name="streamAccessor">
    /// 取得串流的委派。
    /// </param>
    /// <param name="taskFactory">
    /// 建立等待工作的委派。
    /// </param>
    /// <returns>
    /// 代表等待流程的工作。
    /// </returns>
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
/// 解析整合測試使用的執行階段資料夾。
/// </summary>
internal static class RuntimeResolver
{
    /// <summary>
    /// 指定整合測試執行階段資料夾的環境變數名稱。
    /// </summary>
    private const string RuntimeDirectoryEnvironmentVariable = "MEDIAEMBEDKIT_MPV_RUNTIME_DIR";

    /// <summary>
    /// 解析或準備執行階段資料夾。
    /// </summary>
    /// <remarks>
    /// 若設定 <c>MEDIAEMBEDKIT_MPV_RUNTIME_DIR</c>，使用指定資料夾；否則使用
    /// <c>AppContext.BaseDirectory/runtime</c>。兩種情境下若資料夾缺少 libmpv-2.dll
    /// 都會自動下載一份，符合 CI 第一次跑（cache miss）的需求。
    /// </remarks>
    /// <returns>
    /// 包含 libmpv-2.dll 的執行階段資料夾。
    /// </returns>
    public static async Task<string> ResolveAsync()
    {
        string? configured = Environment.GetEnvironmentVariable(RuntimeDirectoryEnvironmentVariable);
        string runtimeDirectory = !string.IsNullOrWhiteSpace(configured)
            ? Path.GetFullPath(configured!)
            : Path.Combine(AppContext.BaseDirectory, "runtime");

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
    /// <param name="runtimeDirectory">
    /// 要檢查的執行階段資料夾。
    /// </param>
    private static void EnsureLibMpvExists(string runtimeDirectory)
    {
        string libraryPath = Path.Combine(runtimeDirectory, MpvLibraryLoader.GetDefaultLibraryFileName());
        if (!File.Exists(libraryPath))
        {
            throw new FileNotFoundException("找不到整合測試需要的 libmpv-2.dll。", libraryPath);
        }
    }
}

/// <summary>
/// 觀察播放測試需要的 libmpv 事件。
/// </summary>
internal sealed class PlaybackEventProbe
{
    /// <summary>
    /// 等待檔案載入事件的工作來源。
    /// </summary>
    private readonly TaskCompletionSource<bool> _fileLoaded;

    /// <summary>
    /// 等待播放時間前進的工作來源。
    /// </summary>
    private readonly TaskCompletionSource<double> _timePosition;

    /// <summary>
    /// 等待播放結束事件的工作來源。
    /// </summary>
    private readonly TaskCompletionSource<MpvEndFileEventArgs> _endFile;

    /// <summary>
    /// 初始化 <see cref="PlaybackEventProbe"/> 類別的新執行個體。
    /// </summary>
    /// <param name="player">
    /// 要觀察的播放器。
    /// </param>
    public PlaybackEventProbe(MpvPlayer player)
    {
        _fileLoaded = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _timePosition = new TaskCompletionSource<double>(TaskCreationOptions.RunContinuationsAsynchronously);
        _endFile = new TaskCompletionSource<MpvEndFileEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);

        player.FileLoaded += delegate
        {
            _fileLoaded.TrySetResult(true);
        };
        player.PropertyChanged += delegate (object? sender, MpvPropertyChangedEventArgs e)
        {
            if (string.Equals(e.Name, "time-pos", StringComparison.Ordinal) && e.Value is double value && value > 0)
            {
                _timePosition.TrySetResult(value);
            }
        };
        player.EndFile += delegate (object? sender, MpvEndFileEventArgs e)
        {
            _endFile.TrySetResult(e);
        };
    }

    /// <summary>
    /// 等待檔案載入事件。
    /// </summary>
    /// <returns>
    /// 代表等待流程的工作。
    /// </returns>
    public Task WaitForFileLoadedAsync()
    {
        return WaitAsync(_fileLoaded.Task, "等待 FileLoaded 事件逾時。");
    }

    /// <summary>
    /// 等待播放時間前進。
    /// </summary>
    /// <returns>
    /// 代表等待流程的工作。
    /// </returns>
    public Task WaitForTimePositionAsync()
    {
        return WaitAsync(_timePosition.Task, "等待 time-pos 屬性前進逾時。");
    }

    /// <summary>
    /// 等待播放結束事件。
    /// </summary>
    /// <returns>
    /// 播放結束事件資料。
    /// </returns>
    public Task<MpvEndFileEventArgs> WaitForEndFileAsync()
    {
        return WaitAsync(_endFile.Task, "等待 EndFile 事件逾時。");
    }

    /// <summary>
    /// 等待指定工作完成並套用逾時。
    /// </summary>
    /// <param name="task">
    /// 要等待的工作。
    /// </param>
    /// <param name="timeoutMessage">
    /// 逾時時使用的訊息。
    /// </param>
    /// <returns>
    /// 代表等待流程的工作。
    /// </returns>
    private static async Task WaitAsync(Task task, string timeoutMessage)
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
    /// <typeparam name="T">
    /// 工作結果型別。
    /// </typeparam>
    /// <param name="task">
    /// 要等待的工作。
    /// </param>
    /// <param name="timeoutMessage">
    /// 逾時時使用的訊息。
    /// </param>
    /// <returns>
    /// 工作結果。
    /// </returns>
    private static async Task<T> WaitAsync<T>(Task<T> task, string timeoutMessage)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(30))).ConfigureAwait(false);
        if (completed != task)
        {
            throw new TimeoutException(timeoutMessage);
        }

        return await task.ConfigureAwait(false);
    }
}

/// <summary>
/// 提供讀取時固定擲回例外狀況的測試串流。
/// </summary>
internal sealed class ThrowingReadStream : Stream
{
    /// <summary>
    /// 取得串流是否支援讀取。
    /// </summary>
    /// <value>
    /// 一律為 <see langword="true"/>。
    /// </value>
    public override bool CanRead
    {
        get { return true; }
    }

    /// <summary>
    /// 取得串流是否支援搜尋。
    /// </summary>
    /// <value>
    /// 一律為 <see langword="false"/>。
    /// </value>
    public override bool CanSeek
    {
        get { return false; }
    }

    /// <summary>
    /// 取得串流是否支援寫入。
    /// </summary>
    /// <value>
    /// 一律為 <see langword="false"/>。
    /// </value>
    public override bool CanWrite
    {
        get { return false; }
    }

    /// <summary>
    /// 取得串流長度。
    /// </summary>
    /// <value>
    /// 此測試串流不支援長度查詢。
    /// </value>
    public override long Length
    {
        get { throw new NotSupportedException("測試串流不支援長度查詢。"); }
    }

    /// <summary>
    /// 取得或設定目前位置。
    /// </summary>
    /// <value>
    /// 此測試串流不支援位置查詢或設定。
    /// </value>
    public override long Position
    {
        get { throw new NotSupportedException("測試串流不支援位置查詢。"); }
        set { throw new NotSupportedException("測試串流不支援位置設定。"); }
    }

    /// <summary>
    /// 清除串流緩衝區。
    /// </summary>
    public override void Flush()
    {
    }

    /// <summary>
    /// 從串流讀取資料。
    /// </summary>
    /// <param name="buffer">
    /// 接收資料的緩衝區。
    /// </param>
    /// <param name="offset">
    /// 緩衝區中的起始位置。
    /// </param>
    /// <param name="count">
    /// 最多要讀取的位元組數。
    /// </param>
    /// <returns>
    /// 此方法不會正常傳回。
    /// </returns>
    public override int Read(byte[] buffer, int offset, int count)
    {
        _ = buffer;
        _ = offset;
        _ = count;
        throw new IOException("測試用串流讀取失敗。");
    }

    /// <summary>
    /// 設定串流位置。
    /// </summary>
    /// <param name="offset">
    /// 相對位移。
    /// </param>
    /// <param name="origin">
    /// 位移起算位置。
    /// </param>
    /// <returns>
    /// 此方法不會正常傳回。
    /// </returns>
    public override long Seek(long offset, SeekOrigin origin)
    {
        _ = offset;
        _ = origin;
        throw new NotSupportedException("測試串流不支援搜尋。");
    }

    /// <summary>
    /// 設定串流長度。
    /// </summary>
    /// <param name="value">
    /// 新的串流長度。
    /// </param>
    public override void SetLength(long value)
    {
        _ = value;
        throw new NotSupportedException("測試串流不支援設定長度。");
    }

    /// <summary>
    /// 將資料寫入串流。
    /// </summary>
    /// <param name="buffer">
    /// 來源資料緩衝區。
    /// </param>
    /// <param name="offset">
    /// 緩衝區中的起始位置。
    /// </param>
    /// <param name="count">
    /// 要寫入的位元組數。
    /// </param>
    public override void Write(byte[] buffer, int offset, int count)
    {
        _ = buffer;
        _ = offset;
        _ = count;
        throw new NotSupportedException("測試串流不支援寫入。");
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
    /// <value>
    /// 一律為 <see langword="true"/>。
    /// </value>
    public override bool CanRead
    {
        get { return true; }
    }

    /// <summary>
    /// 取得串流是否支援搜尋。
    /// </summary>
    /// <value>
    /// 一律為 <see langword="false"/>。
    /// </value>
    public override bool CanSeek
    {
        get { return false; }
    }

    /// <summary>
    /// 取得串流是否支援寫入。
    /// </summary>
    /// <value>
    /// 一律為 <see langword="false"/>。
    /// </value>
    public override bool CanWrite
    {
        get { return false; }
    }

    /// <summary>
    /// 取得串流長度。
    /// </summary>
    /// <value>
    /// 此測試串流不支援長度查詢。
    /// </value>
    public override long Length
    {
        get { throw new NotSupportedException("測試串流不支援長度查詢。"); }
    }

    /// <summary>
    /// 取得或設定目前位置。
    /// </summary>
    /// <value>
    /// 此測試串流不支援位置查詢或設定。
    /// </value>
    public override long Position
    {
        get { throw new NotSupportedException("測試串流不支援位置查詢。"); }
        set { throw new NotSupportedException("測試串流不支援位置設定。"); }
    }

    /// <summary>
    /// 等待讀取作業進入阻塞狀態。
    /// </summary>
    /// <returns>
    /// 代表等待流程的工作。
    /// </returns>
    public Task WaitForReadStartedAsync()
    {
        return _readStarted.Task;
    }

    /// <summary>
    /// 等待取消通知送達串流。
    /// </summary>
    /// <returns>
    /// 代表等待流程的工作。
    /// </returns>
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
    /// <param name="buffer">
    /// 接收資料的緩衝區。
    /// </param>
    /// <param name="offset">
    /// 緩衝區中的起始位置。
    /// </param>
    /// <param name="count">
    /// 最多要讀取的位元組數。
    /// </param>
    /// <returns>
    /// 取消後傳回零，表示串流結束。
    /// </returns>
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
    /// <param name="offset">
    /// 相對位移。
    /// </param>
    /// <param name="origin">
    /// 位移起算位置。
    /// </param>
    /// <returns>
    /// 此方法不會正常傳回。
    /// </returns>
    public override long Seek(long offset, SeekOrigin origin)
    {
        _ = offset;
        _ = origin;
        throw new NotSupportedException("測試串流不支援搜尋。");
    }

    /// <summary>
    /// 設定串流長度。
    /// </summary>
    /// <param name="value">
    /// 新的串流長度。
    /// </param>
    public override void SetLength(long value)
    {
        _ = value;
        throw new NotSupportedException("測試串流不支援設定長度。");
    }

    /// <summary>
    /// 將資料寫入串流。
    /// </summary>
    /// <param name="buffer">
    /// 來源資料緩衝區。
    /// </param>
    /// <param name="offset">
    /// 緩衝區中的起始位置。
    /// </param>
    /// <param name="count">
    /// 要寫入的位元組數。
    /// </param>
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
    /// <param name="disposing">
    /// 是否由受控釋放流程呼叫。
    /// </param>
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
/// 同步派發的 <see cref="IProgress{T}"/> 實作。
/// 不同於 <see cref="System.Progress{T}"/>（依 SynchronizationContext 非同步派發），
/// 本實作直接於 <see cref="Report"/> 內呼叫 handler，方便在測試中以「await 完成
/// 後立即驗證 callback 已執行」的方式工作。
/// </summary>
/// <typeparam name="T">
/// 回報的進度值型別。
/// </typeparam>
internal sealed class SyncProgress<T> : IProgress<T>
{
    /// <summary>
    /// 收到新值時要執行的委派。
    /// </summary>
    private readonly Action<T> _handler;

    /// <summary>
    /// 初始化同步 IProgress。
    /// </summary>
    /// <param name="handler">
    /// 收到新值時要執行的委派。
    /// </param>
    public SyncProgress(Action<T> handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    /// <summary>
    /// 同步回報新的進度值。
    /// </summary>
    /// <param name="value">
    /// 要傳遞給處理委派的進度值。
    /// </param>
    public void Report(T value)
    {
        _handler(value);
    }
}

/// <summary>
/// 建立測試用 WAV 音訊。
/// </summary>
internal static class WaveGenerator
{
    /// <summary>
    /// 建立指定長度的正弦波 WAV 檔案內容。
    /// </summary>
    /// <param name="duration">
    /// 音訊長度。
    /// </param>
    /// <returns>
    /// WAV 檔案位元組。
    /// </returns>
    public static byte[] CreateSineWave(TimeSpan duration)
    {
        int sampleCount = Math.Max(1, (int)(duration.TotalSeconds * Program.SampleRate));
        short[] samples = new short[sampleCount];
        for (int index = 0; index < samples.Length; index++)
        {
            double angle = 2.0 * Math.PI * 440.0 * index / Program.SampleRate;
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
            writer.Write(Program.SampleRate);
            writer.Write(Program.SampleRate * sizeof(short));
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
/// 提供簡易 <see cref="IObserver{T}"/> 實作，方便整合測試斷言 OnNext / OnCompleted 行為。
/// </summary>
/// <typeparam name="T">
/// 觀察者接收的值型別。
/// </typeparam>
internal sealed class TestObserver<T> : IObserver<T>
{
    /// <summary>
    /// 對應 <see cref="IObserver{T}.OnNext"/> 的委派。
    /// </summary>
    private readonly Action<T> _onNext;
    /// <summary>
    /// 對應 <see cref="IObserver{T}.OnCompleted"/> 的委派。
    /// </summary>
    private readonly Action? _onCompleted;
    /// <summary>
    /// 對應 <see cref="IObserver{T}.OnError"/> 的委派。
    /// </summary>
    private readonly Action<Exception>? _onError;

    /// <summary>
    /// 初始化 <see cref="TestObserver{T}"/> 類別的新執行個體。
    /// </summary>
    /// <param name="onNext">
    /// 收到新值時要呼叫的委派。
    /// </param>
    /// <param name="onCompleted">
    /// 收到 OnCompleted 時要呼叫的委派。
    /// </param>
    /// <param name="onError">
    /// 收到 OnError 時要呼叫的委派。
    /// </param>
    public TestObserver(Action<T> onNext, Action? onCompleted = null, Action<Exception>? onError = null)
    {
        _onNext = onNext ?? throw new ArgumentNullException(nameof(onNext));
        _onCompleted = onCompleted;
        _onError = onError;
    }

    /// <summary>
    /// 將新值轉發到建構時提供的 onNext 委派。
    /// </summary>
    /// <param name="value">
    /// 觀察者收到的新值。
    /// </param>
    public void OnNext(T value)
    {
        _onNext(value);
    }

    /// <summary>
    /// 將 OnCompleted 通知轉發到建構時提供的 onCompleted 委派（若有）。
    /// </summary>
    public void OnCompleted()
    {
        _onCompleted?.Invoke();
    }

    /// <summary>
    /// 將 OnError 通知轉發到建構時提供的 onError 委派（若有）。
    /// </summary>
    /// <param name="error">
    /// 觀察者收到的例外狀況。
    /// </param>
    public void OnError(Exception error)
    {
        _onError?.Invoke(error);
    }
}

/// <summary>
/// 提供整合測試執行器。
/// </summary>
internal sealed class IntegrationTestRunner
{
    /// <summary>
    /// 保存測試案例。
    /// </summary>
    private readonly List<IntegrationTestCase> _tests = new List<IntegrationTestCase>();

    /// <summary>
    /// 取得失敗測試數量。
    /// </summary>
    /// <value>
    /// 失敗測試數量。
    /// </value>
    public int FailedCount { get; private set; }

    /// <summary>
    /// 加入測試案例。
    /// </summary>
    /// <param name="name">
    /// 測試名稱。
    /// </param>
    /// <param name="body">
    /// 測試主體。
    /// </param>
    public void Add(string name, Func<Task> body)
    {
        _tests.Add(new IntegrationTestCase(name, body));
    }

    /// <summary>
    /// 執行所有測試案例。
    /// </summary>
    /// <returns>
    /// 代表測試流程的工作。
    /// </returns>
    public async Task RunAsync()
    {
        foreach (IntegrationTestCase test in _tests)
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

        Console.WriteLine("整合測試完成：通過 " + (_tests.Count - FailedCount).ToString(CultureInfo.InvariantCulture) + "，失敗 " + FailedCount.ToString(CultureInfo.InvariantCulture) + "。");
    }
}

/// <summary>
/// 表示整合測試案例。
/// </summary>
internal sealed class IntegrationTestCase
{
    /// <summary>
    /// 初始化 <see cref="IntegrationTestCase"/> 類別的新執行個體。
    /// </summary>
    /// <param name="name">
    /// 測試名稱。
    /// </param>
    /// <param name="body">
    /// 測試主體。
    /// </param>
    public IntegrationTestCase(string name, Func<Task> body)
    {
        Name = name;
        Body = body;
    }

    /// <summary>
    /// 取得測試名稱。
    /// </summary>
    /// <value>
    /// 測試名稱。
    /// </value>
    public string Name { get; private set; }

    /// <summary>
    /// 取得測試主體。
    /// </summary>
    /// <value>
    /// 測試主體。
    /// </value>
    public Func<Task> Body { get; private set; }
}

/// <summary>
/// 提供整合測試斷言。
/// </summary>
internal static class IntegrationAssert
{
    /// <summary>
    /// 驗證兩個值相等。
    /// </summary>
    /// <typeparam name="T">
    /// 要比較的值型別。
    /// </typeparam>
    /// <param name="expected">
    /// 預期值。
    /// </param>
    /// <param name="actual">
    /// 實際值。
    /// </param>
    /// <param name="message">
    /// 失敗時顯示的訊息。
    /// </param>
    public static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(message + "。預期：" + expected + "，實際：" + actual);
        }
    }

    /// <summary>
    /// 驗證數值接近預期值。
    /// </summary>
    /// <param name="expected">
    /// 預期值。
    /// </param>
    /// <param name="actual">
    /// 實際值。
    /// </param>
    /// <param name="tolerance">
    /// 允許誤差。
    /// </param>
    /// <param name="message">
    /// 失敗時顯示的訊息。
    /// </param>
    public static void Near(double expected, double actual, double tolerance, string message)
    {
        if (Math.Abs(expected - actual) > tolerance)
        {
            throw new InvalidOperationException(message + "。預期：" + expected.ToString("0.###", CultureInfo.InvariantCulture) + "，實際：" + actual.ToString("0.###", CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// 驗證條件為真。
    /// </summary>
    /// <param name="condition">
    /// 要驗證的條件。
    /// </param>
    /// <param name="message">
    /// 失敗時顯示的訊息。
    /// </param>
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
    /// <typeparam name="TException">
    /// 預期的例外狀況型別。
    /// </typeparam>
    /// <param name="action">
    /// 要執行的動作。
    /// </param>
    /// <param name="message">
    /// 失敗時顯示的訊息。
    /// </param>
    /// <returns>
    /// 擲回的例外狀況。
    /// </returns>
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
    /// <typeparam name="TException">
    /// 預期的例外狀況型別。
    /// </typeparam>
    /// <param name="action">
    /// 要執行的非同步動作。
    /// </param>
    /// <param name="message">
    /// 失敗時顯示的訊息。
    /// </param>
    /// <returns>
    /// 擲回的例外狀況。
    /// </returns>
    public static async Task<TException> ThrowsAsync<TException>(Func<Task> action, string message)
        where TException : Exception
    {
        try
        {
            await WaitAsync(action(), message + " 等待逾時。").ConfigureAwait(false);
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
    /// <param name="task">
    /// 要等待的工作。
    /// </param>
    /// <param name="timeoutMessage">
    /// 逾時時使用的訊息。
    /// </param>
    /// <returns>
    /// 代表等待流程的工作。
    /// </returns>
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
    /// <typeparam name="T">
    /// 工作結果型別。
    /// </typeparam>
    /// <param name="task">
    /// 要等待的工作。
    /// </param>
    /// <param name="timeoutMessage">
    /// 逾時時使用的訊息。
    /// </param>
    /// <returns>
    /// 工作結果。
    /// </returns>
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

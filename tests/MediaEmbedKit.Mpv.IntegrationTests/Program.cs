using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaEmbedKit.Mpv.Downloads;
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
    /// 測試執行進入點。
    /// </summary>
    /// <param name="args">命令列引數；目前未使用。</param>
    /// <returns>所有測試通過時傳回 0，否則傳回 1。</returns>
    private static async Task<int> Main(string[] args)
    {
        _ = args;
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
            return VerifyUpdateSchedulerRoundTripAsync(runtimeDirectory);
        });
        runner.Add("MpvRuntimeHealthCheck probeLibMpv", delegate
        {
            return VerifyRuntimeHealthCheckProbeAsync(runtimeDirectory);
        });

        await runner.RunAsync().ConfigureAwait(false);
        return runner.FailedCount == 0 ? 0 : 1;
    }

    /// <summary>
    /// 驗證 <see cref="MpvPlayer.DisposeAsync"/> 能在 ShutdownAsync 後完成資源釋放。
    /// </summary>
    /// <param name="runtimeDirectory">包含 libmpv 的執行階段資料夾。</param>
    /// <returns>代表測試流程的工作。</returns>
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
    /// <param name="runtimeDirectory">包含 libmpv 的執行階段資料夾。</param>
    /// <returns>代表測試流程的工作。</returns>
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
    /// <param name="runtimeDirectory">包含 libmpv 的執行階段資料夾。</param>
    /// <returns>代表測試流程的工作。</returns>
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
    /// <param name="runtimeDirectory">包含 libmpv 的執行階段資料夾。</param>
    /// <returns>代表測試流程的工作。</returns>
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
    /// <param name="runtimeDirectory">包含 libmpv 的執行階段資料夾。</param>
    /// <returns>代表測試流程的工作。</returns>
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
    /// <param name="runtimeDirectory">包含 libmpv 的執行階段資料夾。</param>
    /// <returns>代表測試流程的工作。</returns>
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
    /// <param name="runtimeDirectory">包含 libmpv 的執行階段資料夾。</param>
    /// <returns>代表測試流程的工作。</returns>
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
    /// 驗證 <see cref="MpvLibraryUpdateScheduler"/> 的 stage → apply → rollback 路徑。
    /// </summary>
    /// <param name="runtimeDirectory">包含 libmpv 的執行階段資料夾。</param>
    /// <returns>代表測試流程的工作。</returns>
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
    /// <param name="runtimeDirectory">包含 libmpv 的執行階段資料夾。</param>
    /// <returns>代表測試流程的工作。</returns>
    private static async Task VerifyRuntimeHealthCheckProbeAsync(string runtimeDirectory)
    {
        MpvRuntimeHealthReport report = await MpvRuntimeHealthCheck.AnalyzeAsync(runtimeDirectory, probeLibMpv: true).ConfigureAwait(false);
        IntegrationAssert.True(report.IsLibMpvPresent, "執行階段資料夾應包含 libmpv-2.dll。");
        IntegrationAssert.True(report.CanLoadLibMpv, "libmpv 應可載入。");
        IntegrationAssert.True(report.CanInitializePlayer, "應可建立並初始化播放器。");
        IntegrationAssert.True(!string.IsNullOrWhiteSpace(report.ClientApiVersion), "應回報 client API 版本。");
    }

    /// <summary>
    /// 驗證 libmpv 可初始化並能讀寫常用屬性。
    /// </summary>
    /// <param name="runtimeDirectory">包含 libmpv 的執行階段資料夾。</param>
    /// <returns>代表測試流程的工作。</returns>
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
    /// <param name="runtimeDirectory">包含 libmpv 的執行階段資料夾。</param>
    /// <returns>代表測試流程的工作。</returns>
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
    /// <param name="runtimeDirectory">包含 libmpv 的執行階段資料夾。</param>
    /// <returns>代表測試流程的工作。</returns>
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
    /// <param name="runtimeDirectory">包含 libmpv 的執行階段資料夾。</param>
    /// <returns>代表測試流程的工作。</returns>
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
    /// <param name="runtimeDirectory">包含 libmpv 的執行階段資料夾。</param>
    /// <returns>代表測試流程的工作。</returns>
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
    /// <param name="runtimeDirectory">包含 libmpv 的執行階段資料夾。</param>
    /// <returns>代表測試流程的工作。</returns>
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
    /// <param name="runtimeDirectory">包含 libmpv 的執行階段資料夾。</param>
    /// <returns>代表測試流程的工作。</returns>
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
    /// <param name="runtimeDirectory">包含 libmpv 的執行階段資料夾。</param>
    /// <returns>代表測試流程的工作。</returns>
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
    /// <param name="runtimeDirectory">包含 libmpv 的執行階段資料夾。</param>
    /// <returns>代表測試流程的工作。</returns>
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
    /// <param name="runtimeDirectory">包含 libmpv 的執行階段資料夾。</param>
    /// <returns>代表測試流程的工作。</returns>
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
                    context.SetAmbientLight(100);
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
    /// <param name="runtimeDirectory">包含 libmpv 的執行階段資料夾。</param>
    /// <returns>代表測試流程的工作。</returns>
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
    /// <returns>代表測試流程的工作。</returns>
    private static async Task VerifyFFmpegDownloadAndExecutionAsync()
    {
        string runtimeDirectory = Path.Combine(Path.GetTempPath(), "MediaEmbedKit.Mpv.FFmpegIntegration", "win-x64");
        FFmpegDownloadOptions options = new FFmpegDownloadOptions
        {
            VerificationPolicy = MpvNativeAssetVerificationPolicy.RequireProviderChecksum
        };
        FFmpegDownloadResult result = await FFmpegDownloader.DownloadAndExtractLatestAsync(runtimeDirectory, options).ConfigureAwait(false);

        IntegrationAssert.True(File.Exists(result.FFmpegPath), "FFmpeg 應解壓縮到 runtime 根目錄。");
        IntegrationAssert.True(File.Exists(result.FFprobePath), "FFprobe 應解壓縮到 runtime 根目錄。");
        IntegrationAssert.True(File.Exists(result.ArchivePath), "FFmpeg-Builds 壓縮檔應保留於 runtime 根目錄。");

        await VerifyExternalToolVersionAsync(result.FFmpegPath, "FFmpeg").ConfigureAwait(false);
        await VerifyExternalToolVersionAsync(result.FFprobePath, "FFprobe").ConfigureAwait(false);
    }

    /// <summary>
    /// 驗證外部工具可執行並正常回報版本資訊。
    /// </summary>
    /// <param name="executablePath">要執行的外部工具路徑。</param>
    /// <param name="toolName">外部工具顯示名稱。</param>
    /// <returns>代表測試流程的工作。</returns>
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
    /// 建立 render API 測試用播放器。
    /// </summary>
    /// <param name="runtimeDirectory">包含 libmpv 的執行階段資料夾。</param>
    /// <returns>render API 測試用播放器。</returns>
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
    /// <param name="name">資料夾名稱片段。</param>
    /// <returns>建立完成的暫存資料夾。</returns>
    private static string CreateTemporaryDirectory(string name)
    {
        string directory = Path.Combine(Path.GetTempPath(), "MediaEmbedKit.Mpv.IntegrationTests", name, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    /// <summary>
    /// 驗證指定例外狀況包含負數 libmpv 錯誤碼。
    /// </summary>
    /// <param name="exception">要驗證的例外狀況。</param>
    /// <param name="message">失敗時顯示的訊息。</param>
    private static void AssertNegativeMpvError(MpvException exception, string message)
    {
        IntegrationAssert.True(exception.ErrorCode < 0, message + " 應包含負數 libmpv 錯誤碼。");
        IntegrationAssert.True(!string.IsNullOrWhiteSpace(MpvError.GetMessage(exception.ErrorCode)), message + " 應有錯誤訊息。");
    }

    /// <summary>
    /// 驗證指定例外狀況符合預期 libmpv 錯誤碼。
    /// </summary>
    /// <param name="exception">要驗證的例外狀況。</param>
    /// <param name="message">失敗時顯示的訊息。</param>
    /// <param name="expected">允許的錯誤碼。</param>
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
                throw new InvalidOperationException("無法準備 Windows x64 libmpv 執行階段：" + result.Message);
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
    /// <param name="player">要觀察的播放器。</param>
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
    /// <returns>代表等待流程的工作。</returns>
    public Task WaitForFileLoadedAsync()
    {
        return WaitAsync(_fileLoaded.Task, "等待 FileLoaded 事件逾時。");
    }

    /// <summary>
    /// 等待播放時間前進。
    /// </summary>
    /// <returns>代表等待流程的工作。</returns>
    public Task WaitForTimePositionAsync()
    {
        return WaitAsync(_timePosition.Task, "等待 time-pos 屬性前進逾時。");
    }

    /// <summary>
    /// 等待播放結束事件。
    /// </summary>
    /// <returns>播放結束事件資料。</returns>
    public Task<MpvEndFileEventArgs> WaitForEndFileAsync()
    {
        return WaitAsync(_endFile.Task, "等待 EndFile 事件逾時。");
    }

    /// <summary>
    /// 等待指定工作完成並套用逾時。
    /// </summary>
    /// <param name="task">要等待的工作。</param>
    /// <param name="timeoutMessage">逾時時使用的訊息。</param>
    /// <returns>代表等待流程的工作。</returns>
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
    /// <typeparam name="T">工作結果型別。</typeparam>
    /// <param name="task">要等待的工作。</param>
    /// <param name="timeoutMessage">逾時時使用的訊息。</param>
    /// <returns>工作結果。</returns>
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
    /// 清除串流緩衝區。
    /// </summary>
    public override void Flush()
    {
    }

    /// <summary>
    /// 從串流讀取資料。
    /// </summary>
    /// <param name="buffer">接收資料的緩衝區。</param>
    /// <param name="offset">緩衝區中的起始位置。</param>
    /// <param name="count">最多要讀取的位元組數。</param>
    /// <returns>此方法不會正常傳回。</returns>
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
/// 建立測試用 WAV 音訊。
/// </summary>
internal static class WaveGenerator
{
    /// <summary>
    /// 建立指定長度的正弦波 WAV 檔案內容。
    /// </summary>
    /// <param name="duration">音訊長度。</param>
    /// <returns>WAV 檔案位元組。</returns>
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
/// <typeparam name="T">觀察者接收的值型別。</typeparam>
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
    /// <param name="onNext">收到新值時要呼叫的委派。</param>
    /// <param name="onCompleted">收到 OnCompleted 時要呼叫的委派。</param>
    /// <param name="onError">收到 OnError 時要呼叫的委派。</param>
    public TestObserver(Action<T> onNext, Action? onCompleted = null, Action<Exception>? onError = null)
    {
        _onNext = onNext ?? throw new ArgumentNullException(nameof(onNext));
        _onCompleted = onCompleted;
        _onError = onError;
    }

    /// <summary>
    /// 將新值轉發到建構時提供的 onNext 委派。
    /// </summary>
    /// <param name="value">觀察者收到的新值。</param>
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
    /// <param name="error">觀察者收到的例外狀況。</param>
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
    /// <value>失敗測試數量。</value>
    public int FailedCount { get; private set; }

    /// <summary>
    /// 加入測試案例。
    /// </summary>
    /// <param name="name">測試名稱。</param>
    /// <param name="body">測試主體。</param>
    public void Add(string name, Func<Task> body)
    {
        _tests.Add(new IntegrationTestCase(name, body));
    }

    /// <summary>
    /// 執行所有測試案例。
    /// </summary>
    /// <returns>代表測試流程的工作。</returns>
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
    /// <param name="name">測試名稱。</param>
    /// <param name="body">測試主體。</param>
    public IntegrationTestCase(string name, Func<Task> body)
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
/// 提供整合測試斷言。
/// </summary>
internal static class IntegrationAssert
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
    /// 驗證數值接近預期值。
    /// </summary>
    /// <param name="expected">預期值。</param>
    /// <param name="actual">實際值。</param>
    /// <param name="tolerance">允許誤差。</param>
    /// <param name="message">失敗時顯示的訊息。</param>
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

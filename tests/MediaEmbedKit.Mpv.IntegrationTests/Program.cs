using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using MediaEmbedKit.Mpv.Downloads;

namespace MediaEmbedKit.Mpv.IntegrationTests
{
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

            await runner.RunAsync().ConfigureAwait(false);
            return runner.FailedCount == 0 ? 0 : 1;
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
    }
}

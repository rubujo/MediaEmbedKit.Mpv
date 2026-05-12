using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaEmbedKit.Mpv.Downloads;

namespace MediaEmbedKit.Mpv.Samples
{
    /// <summary>
    /// 提供範例專案共用的執行階段安裝與播放器選項設定。
    /// </summary>
    internal static class SampleRuntime
    {
        /// <summary>
        /// 範例預設使用的 YouTube 測試網址。
        /// </summary>
        internal const string PlaybackUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";
        /// <summary>
        /// 範例視窗的標準寬度。
        /// </summary>
        internal const int SampleWindowWidth = 1200;
        /// <summary>
        /// 範例視窗的標準高度。
        /// </summary>
        internal const int SampleWindowHeight = 720;
        /// <summary>
        /// 範例工具列的標準高度。
        /// </summary>
        internal const int SampleToolbarHeight = 48;
        /// <summary>
        /// 範例進階功能列的標準高度。
        /// </summary>
        internal const int SampleFeaturePanelHeight = 88;
        /// <summary>
        /// 範例事件清單的標準高度。
        /// </summary>
        internal const int SampleEventLogHeight = 168;
        /// <summary>
        /// 範例 AirSpace 對比列的標準高度。
        /// </summary>
        internal const int SampleAirspaceComparisonHeight = 40;
        /// <summary>
        /// 範例命令按鈕的標準寬度。
        /// </summary>
        internal const int SampleButtonWidth = 88;
        /// <summary>
        /// 範例命令按鈕的標準高度。
        /// </summary>
        internal const int SampleButtonHeight = 32;
        /// <summary>
        /// 範例控制項周圍的標準內距。
        /// </summary>
        internal const int SampleControlPadding = 8;
        /// <summary>
        /// 範例控制項之間的標準間距。
        /// </summary>
        internal const int SampleControlSpacing = 8;
        /// <summary>
        /// 範例功能按鈕的標準寬度。
        /// </summary>
        internal const int SampleFeatureButtonWidth = 76;
        /// <summary>
        /// 範例 yt-dlp 更新按鈕的標準寬度。
        /// </summary>
        internal const int SampleYtdlpUpdateButtonWidth = 88;
        /// <summary>
        /// 範例 Deno 更新按鈕的標準寬度。
        /// </summary>
        internal const int SampleDenoUpdateButtonWidth = 120;
        /// <summary>
        /// 範例覆蓋層標籤的標準寬度。
        /// </summary>
        internal const int SampleOverlayBadgeWidth = 340;
        /// <summary>
        /// 範例覆蓋層標籤的標準高度。
        /// </summary>
        internal const int SampleOverlayBadgeHeight = 32;
        /// <summary>
        /// 啟用範例播放冒煙測試的環境變數名稱。
        /// </summary>
        private const string SmokeTestEnvironmentVariable = "MEDIAEMBEDKIT_MPV_SAMPLE_SMOKE";
        /// <summary>
        /// 設定冒煙測試最少播放秒數的環境變數名稱。
        /// </summary>
        private const string SmokeTestMinimumSecondsEnvironmentVariable = "MEDIAEMBEDKIT_MPV_SAMPLE_SMOKE_SECONDS";
        /// <summary>
        /// 啟用範例功能冒煙測試的環境變數名稱。
        /// </summary>
        private const string FeatureSmokeTestEnvironmentVariable = "MEDIAEMBEDKIT_MPV_SAMPLE_FEATURE_SMOKE";
        /// <summary>
        /// 指定範例共用 runtime 資料夾的環境變數名稱。
        /// </summary>
        private const string RuntimeDirectoryEnvironmentVariable = "MEDIAEMBEDKIT_MPV_RUNTIME_DIR";
        /// <summary>
        /// 啟用範例播放冒煙測試的本機哨兵檔名稱。
        /// </summary>
        private const string SmokeTestRequestFileName = "sample-smoke.request";
        /// <summary>
        /// 等待範例進入播放狀態的秒數。
        /// </summary>
        private const int SmokeTimeoutSeconds = 180;
        /// <summary>
        /// 預設判定範例已播放的最少秒數。
        /// </summary>
        private const double DefaultSmokeMinimumPlaybackSeconds = 0.25;
        /// <summary>
        /// 範例播放時使用的 yt-dlp 格式選擇。
        /// </summary>
        internal const string SmoothPlaybackYtdlpFormat = "bestvideo[vcodec^=avc1][height<=720][ext=mp4]+bestaudio[acodec^=mp4a][ext=m4a]/best[height<=720][vcodec^=avc1][ext=mp4]/best[height<=720][ext=mp4]/best[height<=720]/best";
        /// <summary>
        /// 範例 Lua 指令碼的檔案名稱。
        /// </summary>
        private const string SampleLuaScriptFileName = "mediaembedkit-sample.lua";
        /// <summary>
        /// 範例字幕檔案的檔案名稱。
        /// </summary>
        private const string SampleSubtitleFileName = "mediaembedkit-sample.zh-TW.srt";
        /// <summary>
        /// 範例 mpv 設定檔的檔案名稱。
        /// </summary>
        private const string SampleConfigFileName = "mpv.conf";
        /// <summary>
        /// 寫入範例產生檔案時使用的 UTF-8 編碼。
        /// </summary>
        private static readonly Encoding SampleFileEncoding = new UTF8Encoding(false);

        /// <summary>
        /// 保存範例應用程式目前使用的播放器選項。
        /// </summary>
        private static MpvPlayerOptions _playerOptions = new MpvPlayerOptions();
        /// <summary>
        /// 保存範例應用程式本次啟動選用的執行階段資料夾。
        /// </summary>
        private static string? _activeRuntimeDirectory;

        /// <summary>
        /// 取得範例應用程式目前使用的播放器選項。
        /// </summary>
        /// <value>已安裝執行階段後產生的播放器選項。</value>
        internal static MpvPlayerOptions PlayerOptions
        {
            get { return _playerOptions; }
        }

        /// <summary>
        /// 取得範例執行階段資料夾。
        /// </summary>
        /// <value>包含 libmpv、yt-dlp、Deno 與範例輔助檔案的資料夾。</value>
        internal static string RuntimeDirectory
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_activeRuntimeDirectory))
                {
                    return Path.GetFullPath(_activeRuntimeDirectory);
                }

                string? runtimeDirectory = Environment.GetEnvironmentVariable(RuntimeDirectoryEnvironmentVariable);
                if (!string.IsNullOrWhiteSpace(runtimeDirectory))
                {
                    return Path.GetFullPath(runtimeDirectory);
                }

                return Path.Combine(AppContext.BaseDirectory, "runtime");
            }
        }

        /// <summary>
        /// 取得範例使用的 yt-dlp 可執行檔路徑。
        /// </summary>
        /// <value>yt-dlp 可執行檔完整路徑。</value>
        internal static string YtDlpPath
        {
            get { return Path.Combine(RuntimeDirectory, "yt-dlp.exe"); }
        }

        /// <summary>
        /// 取得範例使用的 Deno 可執行檔路徑。
        /// </summary>
        /// <value>Deno 可執行檔完整路徑。</value>
        internal static string DenoPath
        {
            get { return Path.Combine(RuntimeDirectory, "deno.exe"); }
        }

        /// <summary>
        /// 取得範例 mpv 設定檔路徑。
        /// </summary>
        /// <value>範例 mpv 設定檔完整路徑。</value>
        internal static string SampleConfigFilePath
        {
            get { return Path.Combine(RuntimeDirectory, SampleConfigFileName); }
        }

        /// <summary>
        /// 取得範例 Lua 指令碼路徑。
        /// </summary>
        /// <value>範例 Lua 指令碼完整路徑。</value>
        internal static string SampleLuaScriptPath
        {
            get { return Path.Combine(RuntimeDirectory, SampleLuaScriptFileName); }
        }

        /// <summary>
        /// 取得範例字幕檔路徑。
        /// </summary>
        /// <value>範例字幕檔完整路徑。</value>
        internal static string SampleSubtitlePath
        {
            get { return Path.Combine(RuntimeDirectory, SampleSubtitleFileName); }
        }

        /// <summary>
        /// 取得範例截圖輸出資料夾。
        /// </summary>
        /// <value>截圖輸出資料夾完整路徑。</value>
        internal static string ScreenshotDirectory
        {
            get { return Path.Combine(AppContext.BaseDirectory, "screenshots"); }
        }

        /// <summary>
        /// 取得目前是否啟用範例播放冒煙測試。
        /// </summary>
        /// <value>環境變數設定為 <c>1</c> 時為 <see langword="true"/>。</value>
        internal static bool IsSmokeTestEnabled
        {
            get
            {
                return string.Equals(
                    Environment.GetEnvironmentVariable(SmokeTestEnvironmentVariable),
                    "1",
                    StringComparison.OrdinalIgnoreCase)
                    || File.Exists(Path.Combine(AppContext.BaseDirectory, SmokeTestRequestFileName));
            }
        }

        /// <summary>
        /// 取得目前是否啟用範例功能冒煙測試。
        /// </summary>
        /// <value>環境變數設定為 <c>1</c> 時為 <see langword="true"/>。</value>
        internal static bool IsFeatureSmokeTestEnabled
        {
            get
            {
                return string.Equals(
                    Environment.GetEnvironmentVariable(FeatureSmokeTestEnvironmentVariable),
                    "1",
                    StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// 安裝或更新範例需要的原生執行階段與外部工具。
        /// </summary>
        /// <param name="cancellationToken">可取消非同步作業的語彙基元。</param>
        /// <returns>代表安裝或更新流程的工作。</returns>
        internal static async Task InstallOrUpdateAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            string runtimeDirectory = await PrepareCoreRuntimeAsync(cancellationToken).ConfigureAwait(false);
            ConfigurePlayerOptions(runtimeDirectory);
            EnsureSampleFiles();
        }

        /// <summary>
        /// 準備核心播放器範例需要的 Windows x64 runtime。
        /// </summary>
        /// <param name="cancellationToken">可取消非同步作業的語彙基元。</param>
        /// <returns>可提供給播放器選項的 runtime 資料夾完整路徑。</returns>
        internal static async Task<string> PrepareCoreRuntimeAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            string runtimeDirectory = RuntimeDirectory;
            if (HasCompleteRuntime(runtimeDirectory))
            {
                _activeRuntimeDirectory = Path.GetFullPath(runtimeDirectory);
                return _activeRuntimeDirectory;
            }

            string? repositoryRuntimeDirectory = FindRepositoryRuntimeDirectory();
            if (repositoryRuntimeDirectory != null && HasCompleteRuntime(repositoryRuntimeDirectory))
            {
                _activeRuntimeDirectory = Path.GetFullPath(repositoryRuntimeDirectory);
                return _activeRuntimeDirectory;
            }

            MpvRuntimeInstallOptions options = new MpvRuntimeInstallOptions();
            options.Windows.LoadLibMpv = false;

            MpvRuntimeInstallResult result = await MpvRuntimeInstaller.InstallOrUpdateAsync(
                runtimeDirectory,
                options,
                cancellationToken).ConfigureAwait(false);

            if (result.IsSupported)
            {
                _activeRuntimeDirectory = Path.GetFullPath(runtimeDirectory);
                return _activeRuntimeDirectory;
            }

            throw new PlatformNotSupportedException("目前範例只支援 Windows x64 runtime 自動安裝：" + result.Message);
        }

        /// <summary>
        /// 判斷指定資料夾是否已包含範例播放需要的 Windows 執行階段檔案。
        /// </summary>
        /// <param name="runtimeDirectory">要檢查的執行階段資料夾。</param>
        /// <returns>資料夾包含 libmpv、yt-dlp 與 Deno 時為 <see langword="true"/>。</returns>
        private static bool HasCompleteRuntime(string runtimeDirectory)
        {
            return File.Exists(Path.Combine(runtimeDirectory, "libmpv-2.dll"))
                && File.Exists(Path.Combine(runtimeDirectory, "yt-dlp.exe"))
                && File.Exists(Path.Combine(runtimeDirectory, "deno.exe"));
        }

        /// <summary>
        /// 從建置輸出往上尋找儲存庫共用的範例執行階段資料夾。
        /// </summary>
        /// <returns>找到完整共用執行階段資料夾時傳回其完整路徑；否則傳回 <see langword="null"/>。</returns>
        private static string? FindRepositoryRuntimeDirectory()
        {
            DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
            for (int depth = 0; directory != null && depth < 12; depth++)
            {
                string candidate = Path.Combine(directory.FullName, ".tmp", "gui-playback-runtime");
                if (HasCompleteRuntime(candidate))
                {
                    return Path.GetFullPath(candidate);
                }

                directory = directory.Parent;
            }

            return null;
        }

        /// <summary>
        /// 依指定的執行階段資料夾建立範例播放器選項。
        /// </summary>
        /// <param name="runtimeDirectory">包含原生執行階段與外部工具的資料夾。</param>
        private static void ConfigurePlayerOptions(string runtimeDirectory)
        {
            _playerOptions = MpvRuntimeInstaller.CreatePlayerOptions(runtimeDirectory);
            _playerOptions.YtdlpFormat = SmoothPlaybackYtdlpFormat;
            _playerOptions.InitialOptions["ytdl-format"] = SmoothPlaybackYtdlpFormat;
            _playerOptions.InitialOptions["hwdec"] = "auto-safe";
            _playerOptions.InitialOptions["panscan"] = "1.0";
            _playerOptions.InitialOptions["keepaspect"] = "no";
        }

        /// <summary>
        /// 建立範例展示常用 API 所需的設定檔、指令碼、字幕與截圖資料夾。
        /// </summary>
        internal static void EnsureSampleFiles()
        {
            Directory.CreateDirectory(RuntimeDirectory);
            Directory.CreateDirectory(ScreenshotDirectory);
            File.WriteAllText(SampleConfigFilePath, CreateSampleConfigText(), SampleFileEncoding);
            File.WriteAllText(SampleLuaScriptPath, CreateSampleLuaScriptText(), SampleFileEncoding);
            File.WriteAllText(SampleSubtitlePath, CreateSampleSubtitleText(), SampleFileEncoding);
        }

        /// <summary>
        /// 將來源播放器選項複製到目標播放器選項。
        /// </summary>
        /// <param name="source">要複製的播放器選項。</param>
        /// <param name="target">要套用設定的播放器選項。</param>
        internal static void CopyTo(MpvPlayerOptions source, MpvPlayerOptions target)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            target.MpvLibraryPath = source.MpvLibraryPath;
            target.EnableDefaultInputBindings = source.EnableDefaultInputBindings;
            target.EnableKeyboardInput = source.EnableKeyboardInput;
            target.EnableOsc = source.EnableOsc;
            target.EnableYtdlp = source.EnableYtdlp;
            target.YtdlpPath = source.YtdlpPath;
            target.YtdlpFormatPreset = source.YtdlpFormatPreset;
            target.YtdlpFormat = source.YtdlpFormat;
            target.ConfigDirectory = source.ConfigDirectory;
            target.InputConfigFile = source.InputConfigFile;
            target.LoadScripts = source.LoadScripts;
            target.ToolDirectory = source.ToolDirectory;
            target.LoadUserConfig = source.LoadUserConfig;
            target.LogLevel = source.LogLevel;

            target.ConfigFiles.Clear();
            foreach (string configFile in source.ConfigFiles)
            {
                target.ConfigFiles.Add(configFile);
            }

            target.ScriptFiles.Clear();
            foreach (string scriptFile in source.ScriptFiles)
            {
                target.ScriptFiles.Add(scriptFile);
            }

            target.InitialOptions.Clear();
            foreach (KeyValuePair<string, string> option in source.InitialOptions)
            {
                target.InitialOptions[option.Key] = option.Value;
            }
        }

        /// <summary>
        /// 建立範例 mpv 設定檔內容。
        /// </summary>
        /// <returns>可寫入 mpv 設定檔的文字。</returns>
        private static string CreateSampleConfigText()
        {
            return "osd-duration=2000" + Environment.NewLine
                + "screenshot-format=png" + Environment.NewLine
                + "screenshot-directory=" + ScreenshotDirectory.Replace("\\", "/") + Environment.NewLine;
        }

        /// <summary>
        /// 建立範例 Lua 指令碼內容。
        /// </summary>
        /// <returns>可寫入 Lua 指令碼的文字。</returns>
        private static string CreateSampleLuaScriptText()
        {
            return "local mp = require 'mp'" + Environment.NewLine
                + "mp.register_script_message('sample-ping', function(text)" + Environment.NewLine
                + "    mp.osd_message('MediaEmbedKit.Mpv Lua: ' .. (text or ''), 2)" + Environment.NewLine
                + "    mp.commandv('script-message', 'sample-pong', text or '')" + Environment.NewLine
                + "end)" + Environment.NewLine;
        }

        /// <summary>
        /// 建立範例字幕檔內容。
        /// </summary>
        /// <returns>可寫入 SRT 字幕檔的文字。</returns>
        private static string CreateSampleSubtitleText()
        {
            return "1" + Environment.NewLine
                + "00:00:00,000 --> 00:00:05,000" + Environment.NewLine
                + "MediaEmbedKit.Mpv 範例字幕：外部字幕已載入。" + Environment.NewLine
                + Environment.NewLine
                + "2" + Environment.NewLine
                + "00:00:05,000 --> 00:00:10,000" + Environment.NewLine
                + "這段字幕由 SampleRuntime 產生。" + Environment.NewLine;
        }

        /// <summary>
        /// 等待播放器實際開始播放，完成後關閉範例應用程式。
        /// </summary>
        /// <param name="sampleName">正在測試的範例名稱。</param>
        /// <param name="getPlayer">取得目前播放器執行個體的委派。</param>
        /// <param name="closeApplication">關閉範例應用程式的委派。</param>
        /// <returns>代表冒煙測試流程的工作。</returns>
        internal static async Task RunSmokeUntilPlaybackAsync(
            string sampleName,
            Func<MpvPlayer?> getPlayer,
            Action closeApplication)
        {
            try
            {
                await WaitForPlaybackAsync(sampleName, getPlayer).ConfigureAwait(true);
                Environment.ExitCode = 0;
            }
            catch (Exception ex)
            {
                Environment.ExitCode = 1;
                WriteSmokeLine(sampleName, "FAILED " + ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                closeApplication();
                await Task.Delay(1000).ConfigureAwait(true);
                Environment.Exit(Environment.ExitCode);
            }
        }

        /// <summary>
        /// 等待播放器播放時間開始前進。
        /// </summary>
        /// <param name="sampleName">正在測試的範例名稱。</param>
        /// <param name="getPlayer">取得目前播放器執行個體的委派。</param>
        /// <returns>代表等待播放狀態的工作。</returns>
        internal static async Task WaitForPlaybackAsync(string sampleName, Func<MpvPlayer?> getPlayer)
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(SmokeTimeoutSeconds);
            Exception? lastException = null;
            bool sawPlayer = false;
            bool sawInitializedPlayer = false;
            double? lastTimePosition = null;
            double minimumPlaybackSeconds = GetMinimumPlaybackSeconds();

            while (DateTimeOffset.UtcNow < deadline)
            {
                MpvPlayer? player = getPlayer();
                sawPlayer = sawPlayer || player != null;
                if (player != null && player.IsInitialized)
                {
                    sawInitializedPlayer = true;
                    try
                    {
                        double timePosition = player.GetPropertyDouble("time-pos");
                        lastTimePosition = timePosition;
                        if (timePosition >= minimumPlaybackSeconds)
                        {
                            WriteSmokeLine(
                                sampleName,
                                "PLAYING time-pos=" + timePosition.ToString("0.000", CultureInfo.InvariantCulture)
                                + " required="
                                + minimumPlaybackSeconds.ToString("0.000", CultureInfo.InvariantCulture));
                            return;
                        }
                    }
                    catch (MpvException ex) when (IsTransientPlaybackException(ex))
                    {
                        lastException = ex;
                    }
                }

                await Task.Delay(500).ConfigureAwait(true);
            }

            string detail = " PlayerSeen=" + sawPlayer
                + " Initialized=" + sawInitializedPlayer
                + " LastTime=" + (lastTimePosition.HasValue ? lastTimePosition.Value.ToString("0.000", CultureInfo.InvariantCulture) : "null")
                + (lastException == null ? string.Empty : " Last mpv error: " + lastException.Message);
            throw new TimeoutException("等待影片播放逾時。" + detail);
        }

        /// <summary>
        /// 取得冒煙測試需要等待的最少播放秒數。
        /// </summary>
        /// <returns>播放器時間必須達到的秒數。</returns>
        private static double GetMinimumPlaybackSeconds()
        {
            string? environmentValue = Environment.GetEnvironmentVariable(SmokeTestMinimumSecondsEnvironmentVariable);
            if (TryParsePositiveSeconds(environmentValue, out double seconds))
            {
                return seconds;
            }

            string requestPath = Path.Combine(AppContext.BaseDirectory, SmokeTestRequestFileName);
            if (File.Exists(requestPath))
            {
                string fileValue = File.ReadAllText(requestPath).Trim();
                if (TryParsePositiveSeconds(fileValue, out seconds))
                {
                    return seconds;
                }
            }

            return DefaultSmokeMinimumPlaybackSeconds;
        }

        /// <summary>
        /// 嘗試將文字轉換為正數秒數。
        /// </summary>
        /// <param name="value">要轉換的秒數文字。</param>
        /// <param name="seconds">接收轉換後秒數的變數。</param>
        /// <returns>文字包含有效正數秒數時為 <see langword="true"/>。</returns>
        private static bool TryParsePositiveSeconds(string? value, out double seconds)
        {
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds) && seconds > 0)
            {
                return true;
            }

            seconds = 0;
            return false;
        }

        /// <summary>
        /// 判斷指定的 libmpv 例外狀況是否屬於播放載入期間可重試的狀態。
        /// </summary>
        /// <param name="exception">libmpv 擲回的例外狀況。</param>
        /// <returns>例外狀況可於等待播放期間重試時為 <see langword="true"/>。</returns>
        private static bool IsTransientPlaybackException(MpvException exception)
        {
            return exception.ErrorCode == (int)MpvErrorCode.PropertyUnavailable
                || exception.ErrorCode == (int)MpvErrorCode.PropertyNotFound
                || exception.ErrorCode == (int)MpvErrorCode.PropertyError;
        }

        /// <summary>
        /// 將範例冒煙測試訊息寫入標準輸出。
        /// </summary>
        /// <param name="sampleName">正在測試的範例名稱。</param>
        /// <param name="message">要輸出的測試訊息。</param>
        internal static void WriteSmokeLine(string sampleName, string message)
        {
            string line = "[sample-smoke] " + sampleName + " " + message;
            Console.WriteLine(line);
            File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "sample-smoke.log"), line + Environment.NewLine);
        }
    }
}

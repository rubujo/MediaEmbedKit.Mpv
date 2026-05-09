using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MediaEmbedKit.Mpv.Downloads
{
    /// <summary>
    /// 提供 Windows 執行階段資料夾的安裝與更新 helper。
    /// </summary>
    public static class MpvWindowsRuntimeInstaller
    {
        /// <summary>
        /// 更新執行階段資料夾中的 libmpv-2.dll。
        /// </summary>
        /// <param name="runtimeDirectory">包含 libmpv 與外部工具的執行階段資料夾。</param>
        /// <param name="options">Windows libmpv 建置下載選項；未指定時使用預設選項。</param>
        /// <param name="cancellationToken">可取消非同步作業的語彙基元。</param>
        /// <returns>表示 libmpv 更新結果的工作。</returns>
        public static async Task<LibMpvUpdateResult> UpdateLibMpvAsync(
            string runtimeDirectory,
            MpvWindowsBuildDownloadOptions? options = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(runtimeDirectory))
            {
                throw new ArgumentException("執行階段資料夾不可為空白。", nameof(runtimeDirectory));
            }

            options = options ?? new MpvWindowsBuildDownloadOptions();
            options.OverwriteExisting = true;

            Directory.CreateDirectory(runtimeDirectory);
            bool loaded = MpvLibraryLoader.IsLoaded;
            string extractDirectory = loaded
                ? Path.Combine(runtimeDirectory, ".updates", DateTime.UtcNow.ToString("yyyyMMddHHmmss"))
                : runtimeDirectory;
            options.ExtractDirectory = extractDirectory;

            MpvWindowsBuildDownloadResult mpv = await MpvWindowsBuildDownloader.DownloadAndExtractLatestLibMpvAsync(
                runtimeDirectory,
                options,
                cancellationToken).ConfigureAwait(false);

            string libMpvPath = Path.Combine(runtimeDirectory, "libmpv-2.dll");
            string updatedLibraryPath = mpv.LibraryPath!;
            if (!loaded && !string.Equals(updatedLibraryPath, libMpvPath, StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(updatedLibraryPath, libMpvPath, true);
                updatedLibraryPath = libMpvPath;
            }

            return new LibMpvUpdateResult(
                runtimeDirectory,
                libMpvPath,
                updatedLibraryPath,
                mpv,
                !loaded,
                loaded,
                loaded
                    ? "libmpv-2.dll is already loaded in the current process. The update was staged; call ApplyStagedLibMpvUpdate before loading libmpv on next startup, then restart the application."
                    : "libmpv-2.dll was updated before it was loaded. No process restart is required for this update.");
        }

        /// <summary>
        /// 在 libmpv 載入前套用先前暫存的 libmpv 更新。
        /// </summary>
        /// <param name="runtimeDirectory">要放置 libmpv-2.dll 的執行階段資料夾。</param>
        /// <param name="stagedLibraryPath">先前暫存的 libmpv-2.dll 路徑。</param>
        public static void ApplyStagedLibMpvUpdate(string runtimeDirectory, string stagedLibraryPath)
        {
            if (MpvLibraryLoader.IsLoaded)
            {
                throw new InvalidOperationException("請先套用暫存的 libmpv 更新，再載入 libmpv。");
            }

            if (string.IsNullOrWhiteSpace(runtimeDirectory))
            {
                throw new ArgumentException("執行階段資料夾不可為空白。", nameof(runtimeDirectory));
            }

            if (string.IsNullOrWhiteSpace(stagedLibraryPath) || !File.Exists(stagedLibraryPath))
            {
                throw new FileNotFoundException("找不到暫存的 libmpv-2.dll。", stagedLibraryPath);
            }

            Directory.CreateDirectory(runtimeDirectory);
            File.Copy(stagedLibraryPath, Path.Combine(runtimeDirectory, "libmpv-2.dll"), true);
        }

        /// <summary>
        /// 安裝或更新 Windows 執行階段資料夾中的 libmpv、yt-dlp 與 Deno。
        /// </summary>
        /// <param name="runtimeDirectory">要建立或更新的執行階段資料夾。</param>
        /// <param name="options">Windows 執行階段下載選項；未指定時使用預設選項。</param>
        /// <param name="cancellationToken">可取消非同步作業的語彙基元。</param>
        /// <returns>表示 Windows 執行階段安裝或更新結果的工作。</returns>
        public static async Task<MpvWindowsRuntimeDownloadResult> InstallOrUpdateAsync(
            string runtimeDirectory,
            MpvWindowsRuntimeDownloadOptions? options = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(runtimeDirectory))
            {
                throw new ArgumentException("執行階段資料夾不可為空白。", nameof(runtimeDirectory));
            }

            options = options ?? new MpvWindowsRuntimeDownloadOptions();
            Directory.CreateDirectory(runtimeDirectory);

            LibMpvUpdateResult libMpv = await UpdateLibMpvAsync(runtimeDirectory, options.Mpv, cancellationToken).ConfigureAwait(false);
            MpvWindowsBuildDownloadResult mpv = libMpv.Download;
            string libMpvPath = libMpv.TargetLibraryPath;

            YtDlpDownloadResult? ytDlp = null;
            if (options.IncludeYtDlp)
            {
                string assetName = options.YtDlp.Architecture.ToAssetName();
                string targetName = options.YtDlp.Architecture == YtDlpWindowsArchitecture.X64 ? "yt-dlp.exe" : assetName;
                ytDlp = await YtDlpDownloader.InstallOrUpdateLatestExecutableAsync(
                    Path.Combine(runtimeDirectory, targetName),
                    options.YtDlp,
                    cancellationToken).ConfigureAwait(false);
            }

            DenoDownloadResult? deno = null;
            if (options.IncludeDeno)
            {
                deno = await DenoDownloader.DownloadAndExtractLatestAsync(
                    runtimeDirectory,
                    options.Deno,
                    cancellationToken).ConfigureAwait(false);
            }

            if (options.LoadLibMpv)
            {
                MpvLibraryLoader.Load(libMpvPath);
            }

            return new MpvWindowsRuntimeDownloadResult(
                runtimeDirectory,
                libMpvPath,
                ytDlp == null ? null : ytDlp.ExecutablePath,
                deno == null ? null : deno.ExecutablePath,
                mpv,
                ytDlp,
                deno);
        }

        /// <summary>
        /// 建立指向指定執行階段資料夾的播放器選項。
        /// </summary>
        /// <param name="runtimeDirectory">包含 libmpv-2.dll 與外部工具的執行階段資料夾。</param>
        /// <returns>可用於 <see cref="MpvPlayer"/> 的播放器選項。</returns>
        public static MpvPlayerOptions CreatePlayerOptions(string runtimeDirectory)
        {
            if (string.IsNullOrWhiteSpace(runtimeDirectory))
            {
                throw new ArgumentException("執行階段資料夾不可為空白。", nameof(runtimeDirectory));
            }

            return new MpvPlayerOptions
            {
                MpvLibraryPath = Path.Combine(runtimeDirectory, "libmpv-2.dll"),
                ToolDirectory = runtimeDirectory,
                YtdlpPath = Path.Combine(runtimeDirectory, "yt-dlp.exe") + ";yt-dlp;youtube-dl",
                EnableYtdlp = true
            };
        }

        /// <summary>
        /// 建立指向指定執行階段資料夾的播放器選項，並可選擇載入同一資料夾中的 mpv 設定。
        /// </summary>
        /// <param name="runtimeDirectory">包含 libmpv-2.dll、外部工具與 mpv 設定檔的執行階段資料夾。</param>
        /// <param name="loadRuntimeConfiguration">是否將執行階段資料夾設為 mpv 設定資料夾。</param>
        /// <returns>可用於 <see cref="MpvPlayer"/> 的播放器選項。</returns>
        public static MpvPlayerOptions CreatePlayerOptions(string runtimeDirectory, bool loadRuntimeConfiguration)
        {
            MpvPlayerOptions options = CreatePlayerOptions(runtimeDirectory);
            if (loadRuntimeConfiguration)
            {
                options.ConfigDirectory = runtimeDirectory;
                options.LoadUserConfig = true;
            }

            return options;
        }
    }
}

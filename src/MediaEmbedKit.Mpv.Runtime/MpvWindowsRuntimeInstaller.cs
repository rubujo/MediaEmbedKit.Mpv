using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using MediaEmbedKit.Mpv.Externals;

namespace MediaEmbedKit.Mpv.Runtime;

/// <summary>
/// 提供 Windows 執行階段資料夾的安裝與更新輔助工具。
/// </summary>
public static class MpvWindowsRuntimeInstaller
{
    /// <summary>
    /// 更新執行階段資料夾中的 libmpv-2.dll。
    /// </summary>
    /// <param name="runtimeDirectory">
    /// 包含 libmpv 與外部工具的執行階段資料夾。
    /// </param>
    /// <param name="options">
    /// Windows libmpv 建置下載選項；未指定時使用預設選項。
    /// </param>
    /// <param name="cancellationToken">
    /// 可取消非同步作業的語彙基元。
    /// </param>
    /// <returns>
    /// 表示 libmpv 更新結果的工作。
    /// </returns>
    public static async Task<LibMpvUpdateResult> UpdateLibMpvAsync(
        string runtimeDirectory,
        MpvWindowsBuildDownloadOptions? options = null,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        if (string.IsNullOrWhiteSpace(runtimeDirectory))
        {
            throw new ArgumentException("執行階段資料夾不可為空白。", nameof(runtimeDirectory));
        }

        // 跨處理序鎖定：避免兩個 process 共用同執行階段資料夾並發 install /
        // update 寫壞 libmpv-2.dll。整段 download + stage + apply + prune 在 lock 內。
        // lock 非 reentrant；本 public 方法 acquire 後直接做事，不呼叫其他會 acquire
        // lock 的 public 方法（會死結）。
        Directory.CreateDirectory(runtimeDirectory);
        using RuntimeDirectoryLock _ = await RuntimeDirectoryLock.AcquireAsync(runtimeDirectory, cancellationToken: cancellationToken).ConfigureAwait(false);

        // UpdateLibMpvAsync = 「明確強制更新到上游最新」語意：呼叫端設不設
        // OverwriteExisting 都會被強制覆寫並 略過下載器的冪等流程
        // skip path（marker 比對）。但不應 mutate 呼叫端物件 —— 改用內部 clone。
        // 想要「有需要才更新」請改用 InstallOrUpdateAsync（它讓 下載器 內建的
        // 冪等快速路徑 生效）。
        MpvWindowsBuildDownloadOptions effectiveOptions = (options ?? new MpvWindowsBuildDownloadOptions()).Clone();
        effectiveOptions.OverwriteExisting = true;

        Directory.CreateDirectory(runtimeDirectory);
        bool loaded = MpvLibraryLoader.IsLoaded;
        string extractDirectory = loaded
            ? Path.Combine(runtimeDirectory, ".updates", DateTime.UtcNow.ToString("yyyyMMddHHmmss"))
            : runtimeDirectory;
        effectiveOptions.ExtractDirectory = extractDirectory;

        MpvWindowsBuildDownloadResult mpv = await MpvWindowsBuildDownloader.DownloadAndExtractLatestLibMpvAsync(
            runtimeDirectory,
            effectiveOptions,
            cancellationToken).ConfigureAwait(false);

        string libMpvPath = Path.Combine(runtimeDirectory, "libmpv-2.dll");
        string updatedLibraryPath = mpv.LibraryPath!;

        // TOCTOU 防護：UpdateLibMpvAsync 開頭檢查 IsLoaded == false 後走 in-place
        // 解壓路徑；但 download + extract 期間可能有另一執行緒呼叫 MpvLibraryLoader.Load，
        // 此時若直接 File.Copy 會撞 Windows 鎖檔 IOException。File.Copy 前再驗一次
        // IsLoaded —— 已被別人載入則改走 stage 路徑（同步 promote 失效，需 restart）。
        bool actuallyApplied = !loaded;
        bool requiresRestart = loaded;
        if (!loaded)
        {
            if (MpvLibraryLoader.IsLoaded)
            {
                // race 中段：另一執行緒已載入 libmpv。把解出的 dll 改 暫存至
                // .updates/<時戳>/，與 loaded==true 路徑語意一致。
                string stageDirectory = Path.Combine(runtimeDirectory, ".updates", DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
                Directory.CreateDirectory(stageDirectory);
                string stagedLibMpvPath = Path.Combine(stageDirectory, "libmpv-2.dll");
                File.Copy(updatedLibraryPath, stagedLibMpvPath, true);
                updatedLibraryPath = stagedLibMpvPath;
                actuallyApplied = false;
                requiresRestart = true;
            }
            else if (!string.Equals(updatedLibraryPath, libMpvPath, StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(updatedLibraryPath, libMpvPath, true);
                updatedLibraryPath = libMpvPath;
            }
        }

        // .updates/<時戳> 暫存累積問題：每次 暫存更新 都新建一個時戳資料夾，
        // 不主動清就會持續累積（LINQPad 等同 AppDomain 多次呼叫尤其明顯）。
        // 保留「最近 1 個」暫存更新 供必要時 回復 / 稽核，其餘清掉。
        if (loaded || requiresRestart)
        {
            PruneStagedUpdatesDirectory(runtimeDirectory, keepLatest: 1);
        }

        return new LibMpvUpdateResult(
            runtimeDirectory,
            libMpvPath,
            updatedLibraryPath,
            mpv,
            actuallyApplied,
            requiresRestart,
            requiresRestart
                ? "libmpv-2.dll is already loaded in the current process. The update was staged; call ApplyStagedLibMpvUpdate before loading libmpv on next startup, then restart the application."
                : "libmpv-2.dll was updated before it was loaded. No process restart is required for this update.");
    }

    /// <summary>
    /// install-or-update 語意的 libmpv 安裝：不強制 <see cref="MpvWindowsBuildDownloadOptions.OverwriteExisting"/>，
    /// 讓 <see cref="MpvWindowsBuildDownloader.DownloadAndExtractLatestLibMpvAsync"/>
    /// 的 sidecar 標記 冪等快速路徑 能生效（runtime/libmpv-2.dll 已是上游
    /// 最新就跳過下載與解壓）。同樣不 修改呼叫端選項。若 libmpv 已載入（同一
    /// 處理序內），會 暫存至 <c>.updates/&lt;時戳&gt;</c> 並 auto-prune 舊版本。
    /// </summary>
    /// <remarks>
    /// 對比 <see cref="UpdateLibMpvAsync"/>「明確強制更新」：本方法是「有需要才更新」
    /// 語意，適合 startup ensure-runtime-present 流程。<see cref="InstallOrUpdateAsync"/>
    /// 內部即呼叫本方法 —— 但本方法也獨立公開，供呼叫端只裝 libmpv（不裝 yt-dlp /
    /// Deno / FFmpeg）時使用。
    /// </remarks>
    /// <param name="runtimeDirectory">
    ///執行階段資料夾。
    /// </param>
    /// <param name="options">
    /// Windows libmpv 建置下載選項；未指定時使用預設選項。
    /// </param>
    /// <param name="cancellationToken">
    /// 可取消非同步作業的語彙基元。
    /// </param>
    /// <returns>
    /// 表示 libmpv 安裝或更新結果的工作。
    /// </returns>
    public static async Task<LibMpvUpdateResult> InstallOrUpdateLibMpvAsync(
        string runtimeDirectory,
        MpvWindowsBuildDownloadOptions? options = null,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        if (string.IsNullOrWhiteSpace(runtimeDirectory))
        {
            throw new ArgumentException("執行階段資料夾不可為空白。", nameof(runtimeDirectory));
        }

        Directory.CreateDirectory(runtimeDirectory);
        using RuntimeDirectoryLock _ = await RuntimeDirectoryLock.AcquireAsync(runtimeDirectory, cancellationToken: cancellationToken).ConfigureAwait(false);
        return await InstallOrUpdateLibMpvCoreAsync(runtimeDirectory, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// <see cref="InstallOrUpdateLibMpvAsync"/> 的 無鎖內部實作；由呼叫端確保已
    /// 取得 <see cref="RuntimeDirectoryLock"/>（避免 nested re-acquire 死結）。
    /// </summary>
    /// <param name="runtimeDirectory">
    ///執行階段資料夾。
    /// </param>
    /// <param name="options">
    /// Windows libmpv 建置下載選項；未指定時使用預設選項。
    /// </param>
    /// <param name="cancellationToken">
    /// 可取消非同步作業的語彙基元。
    /// </param>
    /// <returns>
    /// 表示 libmpv 安裝或更新結果的工作。
    /// </returns>
    private static async Task<LibMpvUpdateResult> InstallOrUpdateLibMpvCoreAsync(
        string runtimeDirectory,
        MpvWindowsBuildDownloadOptions? options,
        CancellationToken cancellationToken)
    {
        MpvWindowsBuildDownloadOptions effectiveOptions = (options ?? new MpvWindowsBuildDownloadOptions()).Clone();

        bool loaded = MpvLibraryLoader.IsLoaded;
        string extractDirectory = loaded
            ? Path.Combine(runtimeDirectory, ".updates", DateTime.UtcNow.ToString("yyyyMMddHHmmss"))
            : runtimeDirectory;
        effectiveOptions.ExtractDirectory = extractDirectory;

        MpvWindowsBuildDownloadResult mpv = await MpvWindowsBuildDownloader.DownloadAndExtractLatestLibMpvAsync(
            runtimeDirectory,
            effectiveOptions,
            cancellationToken).ConfigureAwait(false);

        string libMpvPath = Path.Combine(runtimeDirectory, "libmpv-2.dll");
        string updatedLibraryPath = mpv.LibraryPath!;

        // TOCTOU 防護：開頭 IsLoaded 檢查與 File.Copy 之間，可能另一執行緒呼叫
        // MpvLibraryLoader.Load → 後續 File.Copy 撞 Windows 鎖檔 IOException。
        // File.Copy 前再驗一次。已被別人載入則改 stage（同 loaded 路徑語意）。
        bool actuallyApplied = !loaded;
        bool requiresRestart = loaded;
        if (!loaded)
        {
            if (MpvLibraryLoader.IsLoaded)
            {
                string stageDirectory = Path.Combine(runtimeDirectory, ".updates", DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
                Directory.CreateDirectory(stageDirectory);
                string stagedLibMpvPath = Path.Combine(stageDirectory, "libmpv-2.dll");
                File.Copy(updatedLibraryPath, stagedLibMpvPath, true);
                updatedLibraryPath = stagedLibMpvPath;
                actuallyApplied = false;
                requiresRestart = true;
            }
            else if (!string.Equals(updatedLibraryPath, libMpvPath, StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(updatedLibraryPath, libMpvPath, true);
                updatedLibraryPath = libMpvPath;
            }
        }

        // libmpv 已載入時走的 staging 路徑會建立 .updates/<時戳>；保留最新 1 個、清掉
        // 其餘累積版本（避免 LINQPad 等同 AppDomain 多次呼叫造成磁碟濫用）。
        if (loaded || requiresRestart)
        {
            PruneStagedUpdatesDirectory(runtimeDirectory, keepLatest: 1);
        }

        return new LibMpvUpdateResult(
            runtimeDirectory,
            libMpvPath,
            updatedLibraryPath,
            mpv,
            actuallyApplied,
            requiresRestart,
            requiresRestart
                ? "libmpv-2.dll is already loaded in the current process. The update was staged; call ApplyStagedLibMpvUpdate before loading libmpv on next startup, then restart the application."
                : "libmpv-2.dll is current or was installed before being loaded. No process restart is required.");
    }

    /// <summary>
    /// 清理 <c>runtime/.updates/</c> 內舊的時戳資料夾，僅保留最新的
    /// <paramref name="keepLatest"/> 個（依資料夾名稱排序，即時戳排序）。
    /// 失敗（檔案被鎖、權限等）會吞掉例外 —— 清理是 best-effort，失敗只是磁碟用量問題。
    /// </summary>
    /// <param name="runtimeDirectory">
    ///執行階段資料夾。
    /// </param>
    /// <param name="keepLatest">
    /// 要保留的最新 暫存更新 數量。
    /// </param>
    private static void PruneStagedUpdatesDirectory(string runtimeDirectory, int keepLatest)
    {
        if (keepLatest < 0)
        {
            keepLatest = 0;
        }

        string updatesDirectory = Path.Combine(runtimeDirectory, ".updates");
        if (!Directory.Exists(updatesDirectory))
        {
            return;
        }

        try
        {
            string[] entries = Directory.GetDirectories(updatesDirectory);
            if (entries.Length <= keepLatest)
            {
                return;
            }

            Array.Sort(entries, StringComparer.Ordinal);
            int toDeleteCount = entries.Length - keepLatest;
            for (int i = 0; i < toDeleteCount; i++)
            {
                try
                {
                    Directory.Delete(entries[i], true);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
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
    /// 已知的 opt-in 工具 壓縮檔檔名（依架構 / 通道列舉所有變體）。使用端切到
    /// <c>Include*=false</c> 後對應 下載器 不會跑、無機會清自家 archive，
    /// 在 <see cref="InstallOrUpdateAsync"/> 收尾統一掃過一次防殘留。
    /// </summary>
    private static readonly string[] OptOutFFmpegArchives =
    {
        "ffmpeg-master-latest-win64-gpl.zip",
        "ffmpeg-master-latest-winarm64-gpl.zip",
    };

    private static readonly string[] OptOutDenoArchives =
    {
        "deno-x86_64-pc-windows-msvc.zip",
        "deno-aarch64-pc-windows-msvc.zip",
    };

    /// <summary>
    /// 掃過 opt-out 工具對應的已知 壓縮檔檔名並清掉殘留：使用端之前曾
    /// <c>Include*=true</c> 下載過、現在改 <c>Include*=false</c>，對應 下載器 永不
    /// 執行也就沒機會跑自家 cleanup。失敗不擲例外（best-effort）。
    /// </summary>
    /// <param name="runtimeDirectory">
    ///執行階段資料夾。
    /// </param>
    /// <param name="options">
    /// 當前安裝選項；以 <c>Include*</c> 旗標判斷哪些 壓縮檔是 orphan。
    /// </param>
    private static void TryPruneOptOutToolArchives(
        string runtimeDirectory,
        MpvWindowsRuntimeDownloadOptions options)
    {
        if (!options.IncludeFFmpeg)
        {
            foreach (string archiveName in OptOutFFmpegArchives)
            {
                TryDeleteFileBestEffort(Path.Combine(runtimeDirectory, archiveName));
            }
        }

        if (!options.IncludeDeno)
        {
            foreach (string archiveName in OptOutDenoArchives)
            {
                TryDeleteFileBestEffort(Path.Combine(runtimeDirectory, archiveName));
            }
        }
    }

    /// <summary>
    /// 嘗試刪除指定檔案；不存在或失敗皆吞掉例外（best-effort）。
    /// </summary>
    /// <param name="filePath">
    /// 要刪除的檔案路徑。
    /// </param>
    private static void TryDeleteFileBestEffort(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
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
    /// 在 libmpv 載入前套用先前暫存的 libmpv 更新。
    /// </summary>
    /// <param name="runtimeDirectory">
    /// 要放置 libmpv-2.dll 的執行階段資料夾。
    /// </param>
    /// <param name="stagedLibraryPath">
    /// 先前暫存的 libmpv-2.dll 路徑。
    /// </param>
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
    /// 安裝或更新 Windows 執行階段資料夾中的 libmpv、yt-dlp、Deno 與 FFmpeg。
    /// </summary>
    /// <param name="runtimeDirectory">
    /// 要建立或更新的執行階段資料夾。
    /// </param>
    /// <param name="options">
    /// Windows 執行階段下載選項；未指定時使用預設選項。
    /// </param>
    /// <param name="cancellationToken">
    /// 可取消非同步作業的語彙基元。
    /// </param>
    /// <returns>
    /// 表示 Windows 執行階段安裝或更新結果的工作。
    /// </returns>
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

        // 跨處理序鎖定：避免兩個 process 共用同執行階段資料夾並發 install 寫壞
        // libmpv-2.dll / yt-dlp.exe / deno.exe / ffmpeg.exe。整段 4 元件 install 在 lock 內。
        // 內部呼叫 InstallOrUpdateLibMpvCoreAsync（no-lock 版）避免 nested 死結。
        using RuntimeDirectoryLock _ = await RuntimeDirectoryLock.AcquireAsync(runtimeDirectory, cancellationToken: cancellationToken).ConfigureAwait(false);

        // 走 install-or-update（不強制 OverwriteExisting）：讓
        // MpvWindowsBuildDownloader.DownloadAndExtractLatestLibMpvAsync 的 idempotency
        // skip path（sidecar 標記 比對）能生效，避免每次呼叫都重新下載 ~30 MB libmpv .7z
        // + 重新解壓。對比 UpdateLibMpvAsync（明確強制更新），InstallOrUpdateAsync 是
        // 「有需要才更新」語意。
        LibMpvUpdateResult libMpv = await InstallOrUpdateLibMpvCoreAsync(runtimeDirectory, options.Mpv, cancellationToken).ConfigureAwait(false);
        MpvWindowsBuildDownloadResult mpv = libMpv.Download;
        string libMpvPath = libMpv.TargetLibraryPath;

        YtDlpDownloadResult? ytDlp = null;
        if (options.IncludeYtDlp)
        {
            // 不論架構皆統一儲存為 yt-dlp.exe，讓 mpv 預設 yt-dlp 路徑解析與
            // MpvPlayerOptions.YtdlpPath 預設值在 x64 與 ARM64 上行為一致。
            // 來源資產為 yt-dlp.exe (x64) 或 yt-dlp_arm64.exe (ARM64)，由
            // YtDlpDownloader 依 options.YtDlp.Architecture 自動選擇。
            ytDlp = await YtDlpDownloader.InstallOrUpdateLatestExecutableAsync(
                Path.Combine(runtimeDirectory, "yt-dlp.exe"),
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

        FFmpegDownloadResult? ffmpeg = null;
        if (options.IncludeFFmpeg)
        {
            ffmpeg = await FFmpegDownloader.DownloadAndExtractLatestAsync(
                runtimeDirectory,
                options.FFmpeg,
                cancellationToken).ConfigureAwait(false);
        }

        // 處理 opt-out 工具的孤立壓縮檔：使用端之前曾 Include=true 下載過
        // 但現在改 Include=false，對應 下載器 不會跑、其內部 cleanup 也沒機會
        // 觸發 → archive 永久殘留。此處在每次 InstallOrUpdateAsync 收尾時掃過一次，
        // 把未啟用工具對應的已知 archive 名清掉。刪除失敗不擲例外（best-effort）。
        TryPruneOptOutToolArchives(runtimeDirectory, options);

        if (options.LoadLibMpv)
        {
            MpvLibraryLoader.Load(libMpvPath);
        }

        return new MpvWindowsRuntimeDownloadResult(
            runtimeDirectory,
            libMpvPath,
            ytDlp == null ? null : ytDlp.ExecutablePath,
            deno == null ? null : deno.ExecutablePath,
            ffmpeg == null ? null : ffmpeg.FFmpegPath,
            ffmpeg == null ? null : ffmpeg.FFprobePath,
            mpv,
            ytDlp,
            deno,
            ffmpeg);
    }

    /// <summary>
    /// 建立指向指定執行階段資料夾的播放器選項。
    /// </summary>
    /// <param name="runtimeDirectory">
    /// 包含 libmpv-2.dll 與外部工具的執行階段資料夾。
    /// </param>
    /// <returns>
    /// 可用於 <see cref="MpvPlayer"/> 的播放器選項。
    /// </returns>
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
    /// <param name="runtimeDirectory">
    /// 包含 libmpv-2.dll、外部工具與 mpv 設定檔的執行階段資料夾。
    /// </param>
    /// <param name="loadRuntimeConfiguration">
    /// 是否將執行階段資料夾設為 mpv 設定資料夾。
    /// </param>
    /// <returns>
    /// 可用於 <see cref="MpvPlayer"/> 的播放器選項。
    /// </returns>
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

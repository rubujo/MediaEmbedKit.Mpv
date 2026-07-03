using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using MediaEmbedKit.Mpv.Platforms;

namespace MediaEmbedKit.Mpv.Externals;

/// <summary>
/// 提供 yt-dlp FFmpeg-Builds Windows x64 / ARM64 下載、版本查詢與解壓縮輔助工具。
/// </summary>
public static class FFmpegDownloader
{
    /// <summary>
    /// 預設 FFmpeg-Builds Windows x64 發行資產名稱。
    /// </summary>
    public const string WindowsX64AssetName = "ffmpeg-master-latest-win64-gpl.zip";

    /// <summary>
    /// 預設 FFmpeg-Builds Windows ARM64 發行資產名稱。
    /// </summary>
    public const string WindowsArm64AssetName = "ffmpeg-master-latest-winarm64-gpl.zip";

    /// <summary>
    /// FFmpeg-Builds 發行資產 總和檢查碼檔案名稱。
    /// </summary>
    public const string ChecksumAssetName = "checksums.sha256";
    /// <summary>
    /// FFmpeg 安裝 sidecar 標記檔名稱。
    /// </summary>
    private const string VersionMarkerFileName = "ffmpeg.exe.version";

    /// <summary>
    /// 取得指定 Windows 架構對應的 FFmpeg-Builds 發行資產檔名。
    /// </summary>
    /// <param name="architecture">
    /// FFmpeg-Builds 目標架構。
    /// </param>
    /// <returns>
    /// 對應的 FFmpeg-Builds 發行資產檔名。
    /// </returns>
    public static string GetWindowsAssetName(MpvWindowsArchitecture architecture)
    {
        switch (architecture)
        {
            case MpvWindowsArchitecture.X64:
                return WindowsX64AssetName;
            case MpvWindowsArchitecture.Arm64:
                return WindowsArm64AssetName;
            default:
                throw new ArgumentOutOfRangeException(nameof(architecture), architecture, "未支援的 FFmpeg-Builds Windows 架構。");
        }
    }

    /// <summary>
    /// 下載並解壓縮最新 yt-dlp FFmpeg-Builds Windows 發行檔（x64 或 ARM64，依 <see cref="FFmpegDownloadOptions.Architecture"/>）。
    /// </summary>
    /// <param name="installDirectory">
    /// FFmpeg 與 FFprobe 要安裝到的資料夾。
    /// </param>
    /// <param name="options">
    /// FFmpeg 下載選項；未指定時使用預設選項。
    /// </param>
    /// <param name="cancellationToken">
    /// 可取消非同步作業的語彙基元。
    /// </param>
    /// <returns>
    /// 表示 FFmpeg 下載與解壓縮結果的工作。
    /// </returns>
    public static async Task<FFmpegDownloadResult> DownloadAndExtractLatestAsync(
        string installDirectory,
        FFmpegDownloadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(installDirectory))
        {
            throw new ArgumentException("安裝資料夾不可為空白。", nameof(installDirectory));
        }

        options = options ?? new FFmpegDownloadOptions();
        Directory.CreateDirectory(installDirectory);

        // 進入時清掉 .ffmpeg-extract/ 內過去失敗殘留的 {guid}/ 子資料夾（防毒鎖檔、
        // 強制中止、斷電等場景會留下解壓中段的 ~300 MB 孤兒）。本次解壓會用新
        // {guid}/，不會踩到正在執行的並發解壓（每次都用 Guid.NewGuid()）。
        TryPruneFFmpegExtractWorkspace(installDirectory);

        // yt-dlp/FFmpeg-Builds 同一儲存庫並存兩種發行：tag=latest（固定資產名稱）
        // 與每小時新增的 autobuild-YYYY-MM-DD-HH-MM（含建置編號 / commit hash 的
        // 動態資產名稱）。GitHub /releases/latest 取「created_at 最新」會吃到 autobuild
        // 而拿到不固定的 資產名稱（ffmpeg-N-{build}-g{commit}-...）→ SelectAsset
        // 找 ffmpeg-master-latest-* 必爆。改用 /releases/tags/latest 明確抓 標籤名稱為
        // latest 的 release，資產名稱穩定為 ffmpeg-master-latest-*-gpl.zip。
        Uri defaultApiUri = new Uri("https://api.github.com/repos/yt-dlp/FFmpeg-Builds/releases/tags/latest");
        Uri apiUri = options.ReleaseApiUriOverride ?? defaultApiUri;
        GitHubRelease release = await DownloadUtility.GetLatestReleaseAsync(
            apiUri,
            options.UserAgent,
            cancellationToken).ConfigureAwait(false);

        GitHubReleaseAsset asset = SelectAsset(release, options.Architecture);
        DownloadUtility.ValidateLockedGitHubSource(
            apiUri,
            defaultApiUri,
            asset.BrowserDownloadUrl,
            "yt-dlp",
            "FFmpeg-Builds",
            options.LockReleaseSource);

        string archivePath = Path.Combine(installDirectory, asset.Name);
        string ffmpegPath = Path.Combine(installDirectory, "ffmpeg.exe");
        string ffprobePath = Path.Combine(installDirectory, "ffprobe.exe");
        string markerPath = Path.Combine(installDirectory, VersionMarkerFileName);
        if (CanUseExistingTools(ffmpegPath, ffprobePath, markerPath, release, asset, options))
        {
            if (File.Exists(archivePath) && options.RetainArchive)
            {
                await VerifyExistingArchiveAsync(release, asset, archivePath, options, cancellationToken).ConfigureAwait(false);
            }

            TryPruneFFmpegArchives(
                installDirectory,
                keepArchiveName: options.RetainArchive ? asset.Name : null);

            return new FFmpegDownloadResult(
                release.TagName,
                asset.Name,
                new Uri(asset.BrowserDownloadUrl),
                archivePath,
                ffmpegPath,
                ffprobePath,
                asset.Digest,
                false);
        }

        if (CanVerifyExistingArchive(ffmpegPath, ffprobePath, archivePath, options))
        {
            try
            {
                await VerifyExistingArchiveAsync(release, asset, archivePath, options, cancellationToken).ConfigureAwait(false);
                TryPruneFFmpegArchives(
                    installDirectory,
                    keepArchiveName: options.RetainArchive ? asset.Name : null);
                WriteVersionMarker(markerPath, release, asset);

                return new FFmpegDownloadResult(
                    release.TagName,
                    asset.Name,
                    new Uri(asset.BrowserDownloadUrl),
                    archivePath,
                    ffmpegPath,
                    ffprobePath,
                    asset.Digest,
                    false);
            }
            catch (InvalidOperationException) when (!options.RetainArchive)
            {
            }
        }

        await DownloadUtility.DownloadFileAsync(
            asset.BrowserDownloadUrl,
            archivePath,
            options.UserAgent,
            true,
            cancellationToken).ConfigureAwait(false);

        DownloadUtility.VerifyDownloadedAsset(
            archivePath,
            asset.Digest,
            options.VerifyDigest,
            options.VerificationPolicy,
            options.ExpectedSha256,
            asset.Name);
        await VerifyProviderChecksumIfRequiredAsync(release, asset, archivePath, options, cancellationToken).ConfigureAwait(false);

        string extractDirectory = Path.Combine(installDirectory, ".ffmpeg-extract", Guid.NewGuid().ToString("N"));
        try
        {
            DownloadUtility.ExtractZipToDirectory(archivePath, extractDirectory, true);
            string extractedFFmpegPath = FindExtractedExecutable(extractDirectory, "ffmpeg.exe");
            string extractedFFprobePath = FindExtractedExecutable(extractDirectory, "ffprobe.exe");
            // 拒絕 archive 內 ffmpeg.exe / ffprobe.exe 為 symlink / reparse point
            // （防 CVE-2025-11001 同類攻擊）。
            ArchiveSafety.RejectIfReparsePoint(extractedFFmpegPath, "FFmpeg-Builds archive extracted ffmpeg.exe");
            ArchiveSafety.RejectIfReparsePoint(extractedFFprobePath, "FFmpeg-Builds archive extracted ffprobe.exe");
            File.Copy(extractedFFmpegPath, ffmpegPath, true);
            File.Copy(extractedFFprobePath, ffprobePath, true);
            WriteVersionMarker(markerPath, release, asset);
        }
        finally
        {
            if (Directory.Exists(extractDirectory))
            {
                Directory.Delete(extractDirectory, true);
            }
        }

        // 解壓成功後，依 options.RetainArchive 決定保留目前 archive 或清掉所有
        // FFmpeg-Builds zip；也順手移除舊架構 / 舊流程留下的同類 archive。
        TryPruneFFmpegArchives(
            installDirectory,
            keepArchiveName: options.RetainArchive ? asset.Name : null);

        return new FFmpegDownloadResult(
            release.TagName,
            asset.Name,
            new Uri(asset.BrowserDownloadUrl),
            archivePath,
            ffmpegPath,
            ffprobePath,
            asset.Digest,
            true);
    }

    /// <summary>
    /// 嘗試刪除下載壓縮檔；刪除失敗不擲例外（檔案無關功能、留下也不會壞）。
    /// </summary>
    /// <param name="archivePath">
    /// 要刪除的壓縮檔路徑。
    /// </param>
    private static void TryDeleteArchive(string archivePath)
    {
        try
        {
            if (File.Exists(archivePath))
            {
                File.Delete(archivePath);
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
    /// 驗證既有 FFmpeg-Builds 壓縮檔。
    /// </summary>
    /// <param name="release">
    /// GitHub Releases 資料。
    /// </param>
    /// <param name="asset">
    /// 發行資產。
    /// </param>
    /// <param name="archivePath">
    /// 既有壓縮檔路徑。
    /// </param>
    /// <param name="options">
    /// 下載選項。
    /// </param>
    /// <param name="cancellationToken">
    /// 可取消非同步作業的語彙基元。
    /// </param>
    /// <returns>
    /// 表示驗證流程的工作。
    /// </returns>
    private static async Task VerifyExistingArchiveAsync(
        GitHubRelease release,
        GitHubReleaseAsset asset,
        string archivePath,
        FFmpegDownloadOptions options,
        CancellationToken cancellationToken)
    {
        DownloadUtility.VerifyDownloadedAsset(
            archivePath,
            asset.Digest,
            options.VerifyDigest,
            options.VerificationPolicy,
            options.ExpectedSha256,
            asset.Name);
        await VerifyProviderChecksumIfRequiredAsync(release, asset, archivePath, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 清理執行階段資料夾中的 FFmpeg-Builds 壓縮檔。
    /// </summary>
    /// <param name="installDirectory">
    /// FFmpeg 安裝資料夾。
    /// </param>
    /// <param name="keepArchiveName">
    /// 要保留的壓縮檔名稱；為 <see langword="null"/> 時全部刪除。
    /// </param>
    private static void TryPruneFFmpegArchives(string installDirectory, string? keepArchiveName)
    {
        try
        {
            if (!Directory.Exists(installDirectory))
            {
                return;
            }

            foreach (string path in Directory.EnumerateFiles(installDirectory, "ffmpeg-master-latest-*-gpl.zip"))
            {
                string fileName = Path.GetFileName(path);
                if (keepArchiveName != null &&
                    string.Equals(fileName, keepArchiveName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                TryDeleteArchive(path);
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
    /// 進入 下載流程 時清掉 <c>installDirectory/.ffmpeg-extract/</c> 下過去失敗
    /// 殘留的 <c>{guid}/</c> 子資料夾。每次解壓用新 <c>Guid.NewGuid()</c>，不會清到
    /// 並行執行中的解壓 workspace；只清過往斷電 / 防毒鎖 / 強制中止留下的孤兒。
    /// </summary>
    /// <param name="installDirectory">
    /// FFmpeg 安裝資料夾。
    /// </param>
    private static void TryPruneFFmpegExtractWorkspace(string installDirectory)
    {
        string extractParent = Path.Combine(installDirectory, ".ffmpeg-extract");
        if (!Directory.Exists(extractParent))
        {
            return;
        }

        try
        {
            foreach (string entry in Directory.GetDirectories(extractParent))
            {
                try
                {
                    Directory.Delete(entry, true);
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
    /// 讀取已安裝 FFmpeg 可執行檔的版本。
    /// </summary>
    /// <param name="executablePath">
    /// FFmpeg 可執行檔路徑。
    /// </param>
    /// <returns>
    /// FFmpeg 版本字串；無法讀取時為 <see langword="null"/>。
    /// </returns>
    public static string? GetInstalledVersion(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            return null;
        }

        return DownloadUtility.RunProcessForFirstLine(executablePath, "-version", TimeSpan.FromSeconds(15));
    }

    /// <summary>
    /// 判斷既有 FFmpeg 與 FFprobe 是否可直接重用。
    /// </summary>
    /// <param name="ffmpegPath">
    /// FFmpeg 可執行檔路徑。
    /// </param>
    /// <param name="ffprobePath">
    /// FFprobe 可執行檔路徑。
    /// </param>
    /// <param name="markerPath">
    /// FFmpeg 安裝 sidecar 標記檔路徑。
    /// </param>
    /// <param name="release">
    /// GitHub Releases 資料。
    /// </param>
    /// <param name="asset">
    /// 發行資產。
    /// </param>
    /// <param name="options">
    /// FFmpeg 下載選項。
    /// </param>
    /// <returns>
    /// 可重用既有工具時為 <see langword="true"/>。
    /// </returns>
    private static bool CanUseExistingTools(
        string ffmpegPath,
        string ffprobePath,
        string markerPath,
        GitHubRelease release,
        GitHubReleaseAsset asset,
        FFmpegDownloadOptions options)
    {
        if (!File.Exists(ffmpegPath) ||
            !File.Exists(ffprobePath) ||
            options.OverwriteExisting ||
            !string.IsNullOrWhiteSpace(options.ExpectedSha256))
        {
            return false;
        }

        ArchiveSafety.RejectIfReparsePoint(ffmpegPath, "FFmpeg runtime ffmpeg.exe");
        ArchiveSafety.RejectIfReparsePoint(ffprobePath, "FFmpeg runtime ffprobe.exe");

        FFmpegVersionMarker? marker = FFmpegVersionMarker.TryRead(markerPath);
        return marker != null &&
            string.Equals(marker.ReleaseTag, release.TagName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(marker.AssetName, asset.Name, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(marker.Digest ?? string.Empty, asset.Digest ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 寫入 FFmpeg 安裝 sidecar 標記。
    /// </summary>
    /// <param name="markerPath">
    /// sidecar 標記檔路徑。
    /// </param>
    /// <param name="release">
    /// GitHub Releases 資料。
    /// </param>
    /// <param name="asset">
    /// 發行資產。
    /// </param>
    private static void WriteVersionMarker(string markerPath, GitHubRelease release, GitHubReleaseAsset asset)
    {
        FFmpegVersionMarker marker = new FFmpegVersionMarker(release.TagName, asset.Name, asset.Digest);
        marker.Write(markerPath);
    }

    /// <summary>
    /// 判斷既有壓縮檔是否可用於嚴格驗證路徑。
    /// </summary>
    /// <param name="ffmpegPath">
    /// FFmpeg 可執行檔路徑。
    /// </param>
    /// <param name="ffprobePath">
    /// FFprobe 可執行檔路徑。
    /// </param>
    /// <param name="archivePath">
    /// FFmpeg-Builds 壓縮檔路徑。
    /// </param>
    /// <param name="options">
    /// FFmpeg 下載選項。
    /// </param>
    /// <returns>
    /// 可嘗試驗證既有壓縮檔時為 <see langword="true"/>。
    /// </returns>
    private static bool CanVerifyExistingArchive(string ffmpegPath, string ffprobePath, string archivePath, FFmpegDownloadOptions options)
    {
        return File.Exists(ffmpegPath) &&
            File.Exists(ffprobePath) &&
            File.Exists(archivePath) &&
            !options.OverwriteExisting;
    }

    /// <summary>
    /// 從 GitHub Releases 資料選取 FFmpeg 指定 Windows 架構的發行資產。
    /// </summary>
    /// <param name="release">
    /// GitHub Releases 資料。
    /// </param>
    /// <param name="architecture">
    /// 要選取的 Windows 架構。
    /// </param>
    /// <returns>
    /// 符合指定 Windows 架構的 GitHub Releases 資產。
    /// </returns>
    private static GitHubReleaseAsset SelectAsset(GitHubRelease release, MpvWindowsArchitecture architecture)
    {
        string expectedName = GetWindowsAssetName(architecture);
        GitHubReleaseAsset? asset = release.Assets.FirstOrDefault(item => string.Equals(item.Name, expectedName, StringComparison.OrdinalIgnoreCase));
        if (asset == null)
        {
            throw new InvalidOperationException("GitHub Releases 資料中找不到 FFmpeg " + expectedName + " 資產：" + release.TagName);
        }

        return asset;
    }

    /// <summary>
    /// 在策略要求時使用 FFmpeg-Builds 發行的 總和檢查碼檔案驗證下載檔案。
    /// </summary>
    /// <param name="release">
    /// GitHub Releases 資料。
    /// </param>
    /// <param name="asset">
    /// 已下載的 FFmpeg 發行資產。
    /// </param>
    /// <param name="filePath">
    /// 已下載壓縮檔路徑。
    /// </param>
    /// <param name="options">
    /// FFmpeg 下載選項。
    /// </param>
    /// <param name="cancellationToken">
    /// 可取消非同步作業的語彙基元。
    /// </param>
    /// <returns>
    /// 表示驗證流程的工作。
    /// </returns>
    private static async Task VerifyProviderChecksumIfRequiredAsync(
        GitHubRelease release,
        GitHubReleaseAsset asset,
        string filePath,
        FFmpegDownloadOptions options,
        CancellationToken cancellationToken)
    {
        if (options.VerificationPolicy != MpvNativeAssetVerificationPolicy.RequireProviderChecksum)
        {
            return;
        }

        GitHubReleaseAsset checksumAsset = SelectChecksumAsset(release);
        byte[] checksumBytes = await DownloadUtility.DownloadBytesAsync(
            checksumAsset.BrowserDownloadUrl,
            options.UserAgent,
            cancellationToken).ConfigureAwait(false);
        DownloadUtility.VerifyDownloadedBytes(checksumBytes, checksumAsset.Digest, true, checksumAsset.Name);

        string checksumText = Encoding.UTF8.GetString(checksumBytes);
        string expectedSha256 = DownloadUtility.FindSha256InChecksumText(checksumText, asset.Name);
        DownloadUtility.VerifySha256(filePath, expectedSha256, asset.Name);
    }

    /// <summary>
    /// 從 GitHub Releases 資料選取 FFmpeg-Builds checksum 資產。
    /// </summary>
    /// <param name="release">
    /// GitHub Releases 資料。
    /// </param>
    /// <returns>
    /// checksum 發行資產。
    /// </returns>
    private static GitHubReleaseAsset SelectChecksumAsset(GitHubRelease release)
    {
        GitHubReleaseAsset? asset = release.Assets.FirstOrDefault(item => string.Equals(item.Name, ChecksumAssetName, StringComparison.OrdinalIgnoreCase));
        if (asset == null)
        {
            throw new InvalidOperationException("GitHub Releases 資料中找不到 FFmpeg checksum 資產：" + release.TagName);
        }

        return asset;
    }

    /// <summary>
    /// 從解壓縮資料夾尋找指定可執行檔。
    /// </summary>
    /// <param name="extractDirectory">
    /// 解壓縮資料夾。
    /// </param>
    /// <param name="executableName">
    /// 要尋找的可執行檔名稱。
    /// </param>
    /// <returns>
    /// 找到的可執行檔完整路徑。
    /// </returns>
    private static string FindExtractedExecutable(string extractDirectory, string executableName)
    {
        string? executablePath = Directory.GetFiles(extractDirectory, executableName, SearchOption.AllDirectories).FirstOrDefault();
        if (executablePath == null)
        {
            throw new FileNotFoundException("FFmpeg-Builds 壓縮檔已解壓縮，但找不到 " + executableName + "。", executableName);
        }

        return executablePath;
    }
}

/// <summary>
/// 描述 FFmpeg-Builds 安裝來源的 sidecar 標記。
/// </summary>
internal sealed class FFmpegVersionMarker
{
    /// <summary>
    /// 標記檔 schema 版本。
    /// </summary>
    private const string SchemaVersion = "1";

    /// <summary>
    /// 初始化 <see cref="FFmpegVersionMarker"/> 類別的新執行個體。
    /// </summary>
    /// <param name="releaseTag">
    /// GitHub Releases 標籤。
    /// </param>
    /// <param name="assetName">
    /// 發行資產名稱。
    /// </param>
    /// <param name="digest">
    /// GitHub Releases digest 欄位。
    /// </param>
    public FFmpegVersionMarker(string releaseTag, string assetName, string? digest)
    {
        ReleaseTag = releaseTag ?? string.Empty;
        AssetName = assetName ?? string.Empty;
        Digest = digest;
    }

    /// <summary>
    /// 取得 GitHub Releases 標籤。
    /// </summary>
    /// <value>
    /// GitHub Releases 標籤。
    /// </value>
    public string ReleaseTag { get; private set; }

    /// <summary>
    /// 取得發行資產名稱。
    /// </summary>
    /// <value>
    /// 發行資產名稱。
    /// </value>
    public string AssetName { get; private set; }

    /// <summary>
    /// 取得 GitHub Releases digest 欄位。
    /// </summary>
    /// <value>
    /// GitHub Releases digest 欄位。
    /// </value>
    public string? Digest { get; private set; }

    /// <summary>
    /// 嘗試讀取 sidecar 標記。
    /// </summary>
    /// <param name="markerPath">
    /// sidecar 標記檔路徑。
    /// </param>
    /// <returns>
    /// 可辨識的標記；讀取失敗或 schema 不符時為 <see langword="null"/>。
    /// </returns>
    public static FFmpegVersionMarker? TryRead(string markerPath)
    {
        if (string.IsNullOrWhiteSpace(markerPath) || !File.Exists(markerPath))
        {
            return null;
        }

        try
        {
            string schemaVersion = string.Empty;
            string releaseTag = string.Empty;
            string assetName = string.Empty;
            string? digest = null;
            string[] lines = File.ReadAllLines(markerPath);
            foreach (string line in lines)
            {
                int separator = line.IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }

                string key = line.Substring(0, separator);
                string value = line.Substring(separator + 1);
                switch (key)
                {
                    case "schemaVersion":
                        schemaVersion = value;
                        break;
                    case "releaseTag":
                        releaseTag = value;
                        break;
                    case "assetName":
                        assetName = value;
                        break;
                    case "digest":
                        digest = value;
                        break;
                }
            }

            if (!string.Equals(schemaVersion, SchemaVersion, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(releaseTag) ||
                string.IsNullOrWhiteSpace(assetName))
            {
                return null;
            }

            return new FFmpegVersionMarker(releaseTag, assetName, digest);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// 寫入 sidecar 標記。
    /// </summary>
    /// <param name="markerPath">
    /// sidecar 標記檔路徑。
    /// </param>
    public void Write(string markerPath)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(markerPath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string[] lines = new[]
        {
            "schemaVersion=" + SchemaVersion,
            "releaseTag=" + ReleaseTag,
            "assetName=" + AssetName,
            "digest=" + (Digest ?? string.Empty)
        };
        File.WriteAllLines(markerPath, lines, Encoding.UTF8);
    }
}

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MediaEmbedKit.Mpv.Downloads;

/// <summary>
/// 提供 yt-dlp Windows 可執行檔下載、版本查詢與自我更新 helper。
/// </summary>
public static class YtDlpDownloader
{
    /// <summary>
    /// 下載最新 yt-dlp Windows 可執行檔到指定資料夾。
    /// </summary>
    /// <param name="downloadDirectory">yt-dlp 可執行檔要下載到的資料夾。</param>
    /// <param name="options">yt-dlp 下載選項；未指定時使用預設選項。</param>
    /// <param name="cancellationToken">可取消非同步作業的語彙基元。</param>
    /// <returns>表示 yt-dlp 下載結果的工作。</returns>
    public static async Task<YtDlpDownloadResult> DownloadLatestExecutableAsync(
        string downloadDirectory,
        YtDlpDownloadOptions? options = null,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        if (string.IsNullOrWhiteSpace(downloadDirectory))
        {
            throw new ArgumentException("下載資料夾不可為空白。", nameof(downloadDirectory));
        }

        options = options ?? new YtDlpDownloadOptions();
        Directory.CreateDirectory(downloadDirectory);

        Uri defaultApiUri = GetReleaseApiUri(options.Channel);
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
            GetRepositoryName(options.Channel),
            options.LockReleaseSource);

        string executablePath = Path.Combine(downloadDirectory, asset.Name);
        bool existed = File.Exists(executablePath);

        await DownloadUtility.DownloadFileAsync(
            asset.BrowserDownloadUrl,
            executablePath,
            options.UserAgent,
            options.OverwriteExisting,
            cancellationToken).ConfigureAwait(false);

        DownloadUtility.VerifyDownloadedAsset(
            executablePath,
            asset.Digest,
            options.VerifyDigest,
            options.VerificationPolicy,
            options.ExpectedSha256,
            asset.Name);
        await VerifyProviderChecksumIfRequiredAsync(release, asset, executablePath, options, cancellationToken).ConfigureAwait(false);

        return new YtDlpDownloadResult(
            options.Channel,
            release.TagName,
            asset.Name,
            new Uri(asset.BrowserDownloadUrl),
            executablePath,
            asset.Digest,
            !existed || options.OverwriteExisting);
    }

    /// <summary>
    /// 安裝或更新指定路徑的 yt-dlp Windows 可執行檔。
    /// </summary>
    /// <param name="executablePath">yt-dlp 可執行檔目標路徑。</param>
    /// <param name="options">yt-dlp 下載選項；未指定時使用預設選項。</param>
    /// <param name="cancellationToken">可取消非同步作業的語彙基元。</param>
    /// <returns>表示 yt-dlp 安裝或更新結果的工作。</returns>
    public static async Task<YtDlpDownloadResult> InstallOrUpdateLatestExecutableAsync(
        string executablePath,
        YtDlpDownloadOptions? options = null,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("可執行檔路徑不可為空白。", nameof(executablePath));
        }

        options = options ?? new YtDlpDownloadOptions();
        string directory = Path.GetDirectoryName(Path.GetFullPath(executablePath))!;
        Directory.CreateDirectory(directory);

        Uri defaultApiUri = GetReleaseApiUri(options.Channel);
        Uri apiUri = options.ReleaseApiUriOverride ?? defaultApiUri;
        GitHubRelease release = await DownloadUtility.GetLatestReleaseAsync(
            apiUri,
            options.UserAgent,
            cancellationToken).ConfigureAwait(false);

        string? currentVersion = File.Exists(executablePath) ? GetInstalledVersion(executablePath) : null;
        GitHubReleaseAsset asset = SelectAsset(release, options.Architecture);
        DownloadUtility.ValidateLockedGitHubSource(
            apiUri,
            defaultApiUri,
            asset.BrowserDownloadUrl,
            "yt-dlp",
            GetRepositoryName(options.Channel),
            options.LockReleaseSource);

        if (string.Equals(currentVersion, release.TagName, StringComparison.OrdinalIgnoreCase))
        {
            DownloadUtility.VerifyDownloadedAsset(
                executablePath,
                asset.Digest,
                options.VerifyDigest,
                options.VerificationPolicy,
                options.ExpectedSha256,
                asset.Name);
            await VerifyProviderChecksumIfRequiredAsync(release, asset, executablePath, options, cancellationToken).ConfigureAwait(false);

            return new YtDlpDownloadResult(
                options.Channel,
                release.TagName,
                asset.Name,
                new Uri(asset.BrowserDownloadUrl),
                executablePath,
                asset.Digest,
                false);
        }

        string tempPath = executablePath + ".download";
        await DownloadUtility.DownloadFileAsync(
            asset.BrowserDownloadUrl,
            tempPath,
            options.UserAgent,
            true,
            cancellationToken).ConfigureAwait(false);

        DownloadUtility.VerifyDownloadedAsset(
            tempPath,
            asset.Digest,
            options.VerifyDigest,
            options.VerificationPolicy,
            options.ExpectedSha256,
            asset.Name);
        await VerifyProviderChecksumIfRequiredAsync(release, asset, tempPath, options, cancellationToken).ConfigureAwait(false);

        DownloadUtility.ReplaceFile(tempPath, executablePath);

        return new YtDlpDownloadResult(
            options.Channel,
            release.TagName,
            asset.Name,
            new Uri(asset.BrowserDownloadUrl),
            executablePath,
            asset.Digest,
            true);
    }

    /// <summary>
    /// 讀取已安裝 yt-dlp 可執行檔的版本。
    /// </summary>
    /// <param name="executablePath">yt-dlp 可執行檔路徑。</param>
    /// <returns>yt-dlp 版本字串；無法讀取時為 <see langword="null"/>。</returns>
    public static string? GetInstalledVersion(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            return null;
        }

        return DownloadUtility.RunProcessForFirstLine(executablePath, "--version", TimeSpan.FromSeconds(15));
    }

    /// <summary>
    /// 執行 yt-dlp 內建的自我更新命令。
    /// </summary>
    /// <param name="executablePath">yt-dlp 可執行檔路徑。</param>
    /// <param name="updateTo">yt-dlp 更新目標；未指定時更新到通道預設版本。</param>
    /// <param name="timeout">等待更新命令完成的逾時時間。</param>
    /// <param name="cancellationToken">可取消非同步作業的語彙基元。</param>
    /// <returns>表示 yt-dlp 自我更新命令結果的工作。</returns>
    public static Task<ToolUpdateResult> RunSelfUpdateAsync(
        string executablePath,
        string? updateTo = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            throw new FileNotFoundException("找不到 yt-dlp 可執行檔。", executablePath);
        }

        string arguments = string.IsNullOrWhiteSpace(updateTo)
            ? "--update"
            : "--update-to " + Quote(updateTo!);

        return DownloadUtility.RunProcessAsync(
            executablePath,
            arguments,
            timeout ?? TimeSpan.FromMinutes(5),
            cancellationToken);
    }

    /// <summary>
    /// 將命令列引數值加上引號並逸出內含引號。
    /// </summary>
    /// <param name="value">要加入命令列的原始值。</param>
    /// <returns>可放入命令列的引號字串。</returns>
    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    /// <summary>
    /// 取得指定 yt-dlp 發行通道的 GitHub 最新發行 API URI。
    /// </summary>
    /// <param name="channel">要查詢的 yt-dlp 發行通道。</param>
    /// <returns>對應通道的 GitHub 最新發行 API URI。</returns>
    private static Uri GetReleaseApiUri(YtDlpReleaseChannel channel)
    {
        switch (channel)
        {
            case YtDlpReleaseChannel.Nightly:
                return new Uri("https://api.github.com/repos/yt-dlp/yt-dlp-nightly-builds/releases/latest");
            case YtDlpReleaseChannel.Master:
                return new Uri("https://api.github.com/repos/yt-dlp/yt-dlp-master-builds/releases/latest");
            default:
                return new Uri("https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest");
        }
    }

    /// <summary>
    /// 取得指定 yt-dlp 發行通道對應的 GitHub repository 名稱。
    /// </summary>
    /// <param name="channel">要查詢的 yt-dlp 發行通道。</param>
    /// <returns>對應通道的 GitHub repository 名稱。</returns>
    private static string GetRepositoryName(YtDlpReleaseChannel channel)
    {
        switch (channel)
        {
            case YtDlpReleaseChannel.Nightly:
                return "yt-dlp-nightly-builds";
            case YtDlpReleaseChannel.Master:
                return "yt-dlp-master-builds";
            default:
                return "yt-dlp";
        }
    }

    /// <summary>
    /// 從 GitHub 發行資料選取符合架構的 yt-dlp 發行資產。
    /// </summary>
    /// <param name="release">GitHub 發行資料。</param>
    /// <param name="architecture">要選取的 yt-dlp Windows 架構。</param>
    /// <returns>符合架構的 GitHub 發行資產。</returns>
    private static GitHubReleaseAsset SelectAsset(GitHubRelease release, YtDlpWindowsArchitecture architecture)
    {
        string assetName = architecture.ToAssetName();
        GitHubReleaseAsset? asset = release.Assets.FirstOrDefault(item => string.Equals(item.Name, assetName, StringComparison.OrdinalIgnoreCase));
        if (asset == null)
        {
            throw new InvalidOperationException("GitHub 發行資料中找不到符合 " + architecture + " 的 yt-dlp Windows 資產：" + release.TagName);
        }

        return asset;
    }

    /// <summary>
    /// 在策略要求時使用 yt-dlp 發行的 SHA2-256SUMS 驗證下載檔案。
    /// </summary>
    /// <param name="release">GitHub 發行資料。</param>
    /// <param name="asset">已下載的 yt-dlp 發行資產。</param>
    /// <param name="filePath">已下載檔案路徑。</param>
    /// <param name="options">yt-dlp 下載選項。</param>
    /// <param name="cancellationToken">可取消非同步作業的語彙基元。</param>
    /// <returns>表示驗證流程的工作。</returns>
    private static async Task VerifyProviderChecksumIfRequiredAsync(
        GitHubRelease release,
        GitHubReleaseAsset asset,
        string filePath,
        YtDlpDownloadOptions options,
        CancellationToken cancellationToken)
    {
        if (options.VerificationPolicy != MpvNativeAssetVerificationPolicy.RequireProviderChecksum)
        {
            return;
        }

        GitHubReleaseAsset checksumAsset = SelectChecksumAsset(release, "SHA2-256SUMS");
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
    /// 從 GitHub 發行資料選取指定名稱的 checksum 資產。
    /// </summary>
    /// <param name="release">GitHub 發行資料。</param>
    /// <param name="assetName">要選取的 checksum 資產名稱。</param>
    /// <returns>符合名稱的 GitHub 發行資產。</returns>
    private static GitHubReleaseAsset SelectChecksumAsset(GitHubRelease release, string assetName)
    {
        GitHubReleaseAsset? asset = release.Assets.FirstOrDefault(item => string.Equals(item.Name, assetName, StringComparison.OrdinalIgnoreCase));
        if (asset == null)
        {
            throw new InvalidOperationException("GitHub 發行資料中找不到 " + assetName + " checksum 資產：" + release.TagName);
        }

        return asset;
    }
}

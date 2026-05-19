using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MediaEmbedKit.Mpv.Externals;

/// <summary>
/// 提供 Deno Windows 可執行檔下載、版本查詢與自我更新 helper。
/// </summary>
public static class DenoDownloader
{
    /// <summary>
    /// 下載並解壓縮最新 Deno Windows 發行檔。
    /// </summary>
    /// <param name="installDirectory">Deno 可執行檔要安裝到的資料夾。</param>
    /// <param name="options">Deno 下載選項；未指定時使用預設選項。</param>
    /// <param name="cancellationToken">可取消非同步作業的語彙基元。</param>
    /// <returns>表示 Deno 下載與解壓縮結果的工作。</returns>
    public static async Task<DenoDownloadResult> DownloadAndExtractLatestAsync(
        string installDirectory,
        DenoDownloadOptions? options = null,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        if (string.IsNullOrWhiteSpace(installDirectory))
        {
            throw new ArgumentException("安裝資料夾不可為空白。", nameof(installDirectory));
        }

        options = options ?? new DenoDownloadOptions();
        Directory.CreateDirectory(installDirectory);
        TryPruneDenoExtractWorkspace(installDirectory);

        Uri defaultApiUri = new Uri("https://api.github.com/repos/denoland/deno/releases/latest");
        Uri apiUri = options.ReleaseApiUriOverride ?? defaultApiUri;
        GitHubRelease release = await DownloadUtility.GetLatestReleaseAsync(
            apiUri,
            options.UserAgent,
            cancellationToken).ConfigureAwait(false);

        GitHubReleaseAsset asset = SelectAsset(release, options.Architecture);

        string archivePath = Path.Combine(installDirectory, asset.Name);
        string executablePath = Path.Combine(installDirectory, "deno.exe");
        string? currentVersion = File.Exists(executablePath) ? GetInstalledVersion(executablePath) : null;

        // Idempotency skip：本機 deno.exe 已是上游最新版且呼叫端未要求覆寫或釘版 SHA →
        // 跳過下載 + 解壓。先前是 BestEffort 才允許 skip，但 OverwriteExisting=false +
        // 沒 ExpectedSha256 就足夠保證「使用者沒要求重新驗證」—— 信任 disk 上的檔是
        // 我們上次自己安裝完成寫入的（裝完即用為主流情境）。要強制重抓請設
        // OverwriteExisting=true。
        bool canSkipExisting = !options.OverwriteExisting &&
            string.IsNullOrWhiteSpace(options.ExpectedSha256);
        if (canSkipExisting && string.Equals(NormalizeVersion(currentVersion), NormalizeVersion(release.TagName), StringComparison.OrdinalIgnoreCase))
        {
            // Skip path 仍須遵守 RetainArchive=false 的「裝完即用情境清掉壓縮檔」設計：
            // 上次完整下載成功後留下的 47 MB Deno zip 應該清掉。
            if (!options.RetainArchive)
            {
                TryDeleteArchive(archivePath);
            }

            return new DenoDownloadResult(
                release.TagName,
                asset.Name,
                new Uri(asset.BrowserDownloadUrl),
                archivePath,
                executablePath,
                asset.Digest,
                false);
        }

        await DownloadUtility.DownloadFileAsync(
            asset.BrowserDownloadUrl,
            archivePath,
            options.UserAgent,
            options.OverwriteExisting || !File.Exists(archivePath),
            cancellationToken).ConfigureAwait(false);

        DownloadUtility.VerifyDownloadedAsset(
            archivePath,
            asset.Digest,
            options.VerifyDigest,
            options.VerificationPolicy,
            options.ExpectedSha256,
            asset.Name);
        await VerifyProviderChecksumIfRequiredAsync(release, asset, archivePath, options, cancellationToken).ConfigureAwait(false);

        string extractParent = Path.Combine(installDirectory, ".deno-extract");
        string extractDirectory = Path.Combine(extractParent, Guid.NewGuid().ToString("N"));
        try
        {
            DownloadUtility.ExtractZipToDirectory(archivePath, extractDirectory, true);
            string? found = Directory.GetFiles(extractDirectory, "deno.exe", SearchOption.AllDirectories).FirstOrDefault();
            if (found == null)
            {
                throw new FileNotFoundException("Deno 壓縮檔已解壓縮，但找不到 deno.exe。", "deno.exe");
            }

            // 拒絕 archive 內 deno.exe 為 symlink / reparse point。
            ArchiveSafety.RejectIfReparsePoint(found, "Deno archive extracted deno.exe");
            File.Copy(found, executablePath, true);
        }
        finally
        {
            TryDeleteDirectory(extractDirectory);
            TryDeleteEmptyDirectory(extractParent);
        }

        // 拒絕最終 runtime/deno.exe 為 reparse point。
        ArchiveSafety.RejectIfReparsePoint(executablePath, "Deno runtime deno.exe");

        // 解壓成功後，依 options.RetainArchive 決定是否清掉壓縮檔。Deno zip 約 30 MB；
        // 裝完即用情境留著只佔磁碟。warm restart 強驗證需求請設 RetainArchive=true。
        if (!options.RetainArchive)
        {
            TryDeleteArchive(archivePath);
        }

        return new DenoDownloadResult(
            release.TagName,
            asset.Name,
            new Uri(asset.BrowserDownloadUrl),
            archivePath,
            executablePath,
            asset.Digest,
            true);
    }

    /// <summary>
    /// 嘗試刪除下載壓縮檔；刪除失敗不擲例外（檔案無關功能、留下也不會壞）。
    /// </summary>
    /// <param name="archivePath">要刪除的壓縮檔路徑。</param>
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
    /// 清理過去 Deno 解壓中止留下的暫存資料夾。
    /// </summary>
    /// <param name="installDirectory">Deno 安裝資料夾。</param>
    private static void TryPruneDenoExtractWorkspace(string installDirectory)
    {
        string extractParent = Path.Combine(installDirectory, ".deno-extract");
        if (!Directory.Exists(extractParent))
        {
            return;
        }

        try
        {
            foreach (string entry in Directory.GetDirectories(extractParent))
            {
                TryDeleteDirectory(entry);
            }

            TryDeleteEmptyDirectory(extractParent);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// 嘗試刪除指定資料夾與其內容；失敗時不擲例外。
    /// </summary>
    /// <param name="directoryPath">要刪除的資料夾。</param>
    private static void TryDeleteDirectory(string directoryPath)
    {
        try
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, true);
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
    /// 嘗試刪除空資料夾；資料夾不存在、非空或刪除失敗皆忽略。
    /// </summary>
    /// <param name="directoryPath">要刪除的空資料夾。</param>
    private static void TryDeleteEmptyDirectory(string directoryPath)
    {
        try
        {
            if (Directory.Exists(directoryPath) && !Directory.EnumerateFileSystemEntries(directoryPath).Any())
            {
                Directory.Delete(directoryPath, false);
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
    /// 讀取已安裝 Deno 可執行檔的版本。
    /// </summary>
    /// <param name="executablePath">Deno 可執行檔路徑。</param>
    /// <returns>Deno 版本字串；無法讀取時為 <see langword="null"/>。</returns>
    public static string? GetInstalledVersion(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            return null;
        }

        string? firstLine = DownloadUtility.RunProcessForFirstLine(executablePath, "--version", TimeSpan.FromSeconds(15));
        if (string.IsNullOrWhiteSpace(firstLine))
        {
            return null;
        }

        const string prefix = "deno ";
        return firstLine!.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? firstLine.Substring(prefix.Length).Trim()
            : firstLine.Trim();
    }

    /// <summary>
    /// 執行 Deno 內建的自我升級命令。
    /// </summary>
    /// <param name="executablePath">Deno 可執行檔路徑。</param>
    /// <param name="version">要升級到的 Deno 版本；未指定時升級到最新版本。</param>
    /// <param name="timeout">等待升級命令完成的逾時時間。</param>
    /// <param name="cancellationToken">可取消非同步作業的語彙基元。</param>
    /// <returns>表示 Deno 自我升級命令結果的工作。</returns>
    public static Task<ToolUpdateResult> RunSelfUpgradeAsync(
        string executablePath,
        string? version = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            throw new FileNotFoundException("找不到 Deno 可執行檔。", executablePath);
        }

        string arguments = string.IsNullOrWhiteSpace(version)
            ? "upgrade"
            : "upgrade --version " + Quote(version!);

        return DownloadUtility.RunProcessAsync(
            executablePath,
            arguments,
            timeout ?? TimeSpan.FromMinutes(5),
            cancellationToken);
    }

    /// <summary>
    /// 執行 Deno 內建的自我升級命令，並要求 Deno 使用指定 SHA-256 checksum 驗證下載內容。
    /// </summary>
    /// <param name="executablePath">Deno 可執行檔路徑。</param>
    /// <param name="checksum">Deno 升級壓縮檔的預期 SHA-256 值。</param>
    /// <param name="version">要升級到的 Deno 版本；未指定時升級到最新版本。</param>
    /// <param name="timeout">等待升級命令完成的逾時時間。</param>
    /// <param name="cancellationToken">可取消非同步作業的語彙基元。</param>
    /// <returns>表示 Deno 自我升級命令結果的工作。</returns>
    public static Task<ToolUpdateResult> RunSelfUpgradeWithChecksumAsync(
        string executablePath,
        string checksum,
        string? version = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            throw new FileNotFoundException("找不到 Deno 可執行檔。", executablePath);
        }

        if (string.IsNullOrWhiteSpace(checksum))
        {
            throw new ArgumentException("checksum 不可為空白。", nameof(checksum));
        }

        string arguments = string.IsNullOrWhiteSpace(version)
            ? "upgrade --checksum=" + checksum.Trim()
            : "upgrade --checksum=" + checksum.Trim() + " " + Quote(version!);

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
    /// 從 GitHub 發行資料選取符合架構的 Deno 發行資產。
    /// </summary>
    /// <param name="release">GitHub 發行資料。</param>
    /// <param name="architecture">要選取的 Deno Windows 架構。</param>
    /// <returns>符合架構的 GitHub 發行資產。</returns>
    private static GitHubReleaseAsset SelectAsset(GitHubRelease release, DenoWindowsArchitecture architecture)
    {
        string assetName = architecture.ToAssetName();
        GitHubReleaseAsset? asset = release.Assets.FirstOrDefault(item => string.Equals(item.Name, assetName, StringComparison.OrdinalIgnoreCase));
        if (asset == null)
        {
            throw new InvalidOperationException("GitHub 發行資料中找不到符合 " + architecture + " 的 Deno Windows 資產：" + release.TagName);
        }

        return asset;
    }

    /// <summary>
    /// 在策略要求時使用 Deno 發行的 sha256sum 檔案驗證下載檔案。
    /// </summary>
    /// <param name="release">GitHub 發行資料。</param>
    /// <param name="asset">已下載的 Deno 發行資產。</param>
    /// <param name="filePath">已下載壓縮檔路徑。</param>
    /// <param name="options">Deno 下載選項。</param>
    /// <param name="cancellationToken">可取消非同步作業的語彙基元。</param>
    /// <returns>表示驗證流程的工作。</returns>
    private static async Task VerifyProviderChecksumIfRequiredAsync(
        GitHubRelease release,
        GitHubReleaseAsset asset,
        string filePath,
        DenoDownloadOptions options,
        CancellationToken cancellationToken)
    {
        if (options.VerificationPolicy != MpvNativeAssetVerificationPolicy.RequireProviderChecksum)
        {
            return;
        }

        GitHubReleaseAsset checksumAsset = SelectChecksumAsset(release, asset.Name + ".sha256sum");
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

    /// <summary>
    /// 將 Deno 版本文字正規化為可比較格式。
    /// </summary>
    /// <param name="value">原始版本文字。</param>
    /// <returns>正規化後的版本文字；無有效內容時為 <see langword="null"/>。</returns>
    private static string? NormalizeVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value!.Trim().TrimStart('v', 'V');
    }
}

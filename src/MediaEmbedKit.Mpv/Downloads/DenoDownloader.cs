using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MediaEmbedKit.Mpv.Downloads
{
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

            GitHubRelease release = await DownloadUtility.GetLatestReleaseAsync(
                options.ReleaseApiUriOverride ?? new Uri("https://api.github.com/repos/denoland/deno/releases/latest"),
                options.UserAgent,
                cancellationToken).ConfigureAwait(false);

            GitHubReleaseAsset asset = SelectAsset(release, options.Architecture);
            string archivePath = Path.Combine(installDirectory, asset.Name);
            string executablePath = Path.Combine(installDirectory, "deno.exe");
            string? currentVersion = File.Exists(executablePath) ? GetInstalledVersion(executablePath) : null;

            if (string.Equals(NormalizeVersion(currentVersion), NormalizeVersion(release.TagName), StringComparison.OrdinalIgnoreCase))
            {
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

            if (options.VerifyDigest)
            {
                DownloadUtility.VerifyDigestIfAvailable(archivePath, asset.Digest);
            }

            DownloadUtility.ExtractZipToDirectory(archivePath, installDirectory, true);

            if (!File.Exists(executablePath))
            {
                string? found = Directory.GetFiles(installDirectory, "deno.exe", SearchOption.AllDirectories).FirstOrDefault();
                if (found == null)
                {
                    throw new FileNotFoundException("Deno 壓縮檔已解壓縮，但找不到 deno.exe。", "deno.exe");
                }

                File.Copy(found, executablePath, true);
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
}

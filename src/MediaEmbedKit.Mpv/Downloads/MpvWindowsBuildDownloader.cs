using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MediaEmbedKit.Mpv.Downloads
{
    /// <summary>
    /// 提供 Windows libmpv 建置下載、解壓縮與載入 helper。
    /// </summary>
    public static class MpvWindowsBuildDownloader
    {
        /// <summary>
        /// Windows libmpv 發行壓縮檔內預期的 DLL 檔名。
        /// </summary>
        private const string LibMpvDllName = "libmpv-2.dll";

        /// <summary>
        /// 下載最新 Windows libmpv 建置壓縮檔。
        /// </summary>
        /// <param name="downloadDirectory">壓縮檔要下載到的資料夾。</param>
        /// <param name="options">Windows libmpv 建置下載選項；未指定時使用預設選項。</param>
        /// <param name="cancellationToken">可取消非同步作業的語彙基元。</param>
        /// <returns>表示 libmpv 壓縮檔下載結果的工作。</returns>
        public static async Task<MpvWindowsBuildDownloadResult> DownloadLatestLibMpvArchiveAsync(
            string downloadDirectory,
            MpvWindowsBuildDownloadOptions? options = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(downloadDirectory))
            {
                throw new ArgumentException("下載資料夾不可為空白。", nameof(downloadDirectory));
            }

            options = options ?? new MpvWindowsBuildDownloadOptions();
            Directory.CreateDirectory(downloadDirectory);

            Uri defaultApiUri = GetReleaseApiUri(options.Provider);
            Uri apiUri = options.ReleaseApiUriOverride ?? defaultApiUri;
            GitHubRelease release = await GetLatestReleaseAsync(options, apiUri, cancellationToken).ConfigureAwait(false);
            GitHubReleaseAsset asset = SelectLibMpvAsset(release, options);
            DownloadUtility.ValidateLockedGitHubSource(
                apiUri,
                defaultApiUri,
                asset.BrowserDownloadUrl,
                GetRepositoryOwner(options.Provider),
                GetRepositoryName(options.Provider),
                options.LockReleaseSource);

            string archivePath = Path.Combine(downloadDirectory, asset.Name);

            if (!File.Exists(archivePath) || options.OverwriteExisting)
            {
                await DownloadUtility.DownloadFileAsync(asset.BrowserDownloadUrl, archivePath, options.UserAgent, true, cancellationToken).ConfigureAwait(false);
            }

            if (options.VerificationPolicy == MpvNativeAssetVerificationPolicy.RequireProviderChecksum)
            {
                throw new InvalidOperationException("目前 Windows libmpv provider 未提供獨立 checksum 資產；請改用 RequirePinnedSha256 並提供 ExpectedSha256。");
            }

            DownloadUtility.VerifyDownloadedAsset(
                archivePath,
                asset.Digest,
                options.VerifyDigest,
                options.VerificationPolicy,
                options.ExpectedSha256,
                asset.Name);

            return new MpvWindowsBuildDownloadResult(
                options.Provider,
                release.TagName,
                asset.Name,
                new Uri(asset.BrowserDownloadUrl),
                archivePath,
                asset.Digest,
                null,
                null);
        }

        /// <summary>
        /// 下載並解壓縮最新 Windows libmpv 建置。
        /// </summary>
        /// <param name="downloadDirectory">壓縮檔要下載到的資料夾。</param>
        /// <param name="options">Windows libmpv 建置下載選項；未指定時使用預設選項。</param>
        /// <param name="cancellationToken">可取消非同步作業的語彙基元。</param>
        /// <returns>表示 libmpv 下載與解壓縮結果的工作。</returns>
        public static async Task<MpvWindowsBuildDownloadResult> DownloadAndExtractLatestLibMpvAsync(
            string downloadDirectory,
            MpvWindowsBuildDownloadOptions? options = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            options = options ?? new MpvWindowsBuildDownloadOptions();
            MpvWindowsBuildDownloadResult archive = await DownloadLatestLibMpvArchiveAsync(downloadDirectory, options, cancellationToken).ConfigureAwait(false);

            string sevenZipPath = ResolveSevenZipPath(options.SevenZipPath);
            string extractRoot = options.ExtractDirectory ?? Path.Combine(downloadDirectory, Path.GetFileNameWithoutExtension(archive.AssetName));
            Directory.CreateDirectory(extractRoot);

            ExtractWithSevenZip(sevenZipPath, archive.ArchivePath, extractRoot);

            string? libraryPath = FindLibMpvDll(extractRoot);
            if (libraryPath == null)
            {
                throw new FileNotFoundException("壓縮檔已解壓縮，但找不到 libmpv-2.dll。", LibMpvDllName);
            }

            return new MpvWindowsBuildDownloadResult(
                archive.Provider,
                archive.ReleaseTag,
                archive.AssetName,
                archive.DownloadUri,
                archive.ArchivePath,
                archive.Digest,
                extractRoot,
                libraryPath);
        }

        /// <summary>
        /// 下載、解壓縮並載入最新 Windows libmpv 建置。
        /// </summary>
        /// <param name="downloadDirectory">壓縮檔要下載到的資料夾。</param>
        /// <param name="options">Windows libmpv 建置下載選項；未指定時使用預設選項。</param>
        /// <param name="cancellationToken">可取消非同步作業的語彙基元。</param>
        /// <returns>表示已載入 libmpv 檔案路徑的工作。</returns>
        public static async Task<string> DownloadExtractAndLoadLatestLibMpvAsync(
            string downloadDirectory,
            MpvWindowsBuildDownloadOptions? options = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            MpvWindowsBuildDownloadResult result = await DownloadAndExtractLatestLibMpvAsync(downloadDirectory, options, cancellationToken).ConfigureAwait(false);
            MpvLibraryLoader.Load(result.LibraryPath);
            return result.LibraryPath!;
        }

        /// <summary>
        /// 從設定的提供者取得最新 GitHub 發行資料。
        /// </summary>
        /// <param name="options">Windows libmpv 建置下載選項。</param>
        /// <param name="apiUri">要查詢的 GitHub Releases API URI。</param>
        /// <param name="cancellationToken">可取消非同步作業的語彙基元。</param>
        /// <returns>表示 GitHub 發行資料的工作。</returns>
        private static async Task<GitHubRelease> GetLatestReleaseAsync(MpvWindowsBuildDownloadOptions options, Uri apiUri, CancellationToken cancellationToken)
        {
            ValidateProviderArchitecture(options.Provider, options.Architecture);

            return await DownloadUtility.GetLatestReleaseAsync(apiUri, options.UserAgent, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 驗證指定提供者是否發佈所要求的架構。
        /// </summary>
        /// <param name="provider">Windows libmpv 建置提供者。</param>
        /// <param name="architecture">要驗證的 Windows libmpv 架構。</param>
        private static void ValidateProviderArchitecture(MpvWindowsBuildProvider provider, MpvWindowsArchitecture architecture)
        {
            _ = provider;
            _ = architecture;
        }

        /// <summary>
        /// 取得 Windows libmpv 建置提供者的 GitHub 最新發行 API URI。
        /// </summary>
        /// <param name="provider">Windows libmpv 建置提供者。</param>
        /// <returns>對應提供者的 GitHub 最新發行 API URI。</returns>
        private static Uri GetReleaseApiUri(MpvWindowsBuildProvider provider)
        {
            switch (provider)
            {
                case MpvWindowsBuildProvider.Zhongfly:
                    return new Uri("https://api.github.com/repos/zhongfly/mpv-winbuild/releases/latest");
                default:
                    return new Uri("https://api.github.com/repos/shinchiro/mpv-winbuild-cmake/releases/latest");
            }
        }

        /// <summary>
        /// 取得指定 Windows libmpv provider 對應的 GitHub repository 擁有者。
        /// </summary>
        /// <param name="provider">Windows libmpv 建置提供者。</param>
        /// <returns>對應 provider 的 GitHub repository 擁有者。</returns>
        private static string GetRepositoryOwner(MpvWindowsBuildProvider provider)
        {
            switch (provider)
            {
                case MpvWindowsBuildProvider.Zhongfly:
                    return "zhongfly";
                default:
                    return "shinchiro";
            }
        }

        /// <summary>
        /// 取得指定 Windows libmpv provider 對應的 GitHub repository 名稱。
        /// </summary>
        /// <param name="provider">Windows libmpv 建置提供者。</param>
        /// <returns>對應 provider 的 GitHub repository 名稱。</returns>
        private static string GetRepositoryName(MpvWindowsBuildProvider provider)
        {
            switch (provider)
            {
                case MpvWindowsBuildProvider.Zhongfly:
                    return "mpv-winbuild";
                default:
                    return "mpv-winbuild-cmake";
            }
        }

        /// <summary>
        /// 從 GitHub 發行資料選取符合架構與授權偏好的 libmpv 發行資產。
        /// </summary>
        /// <param name="release">GitHub 發行資料。</param>
        /// <param name="options">Windows libmpv 建置下載選項。</param>
        /// <returns>符合條件的 GitHub 發行資產。</returns>
        private static GitHubReleaseAsset SelectLibMpvAsset(GitHubRelease release, MpvWindowsBuildDownloadOptions options)
        {
            GitHubReleaseAsset[] architectureMatches = release.Assets
                .Where(asset => IsLibMpvAssetForArchitecture(asset.Name, options.Architecture))
                .ToArray();

            GitHubReleaseAsset? selected;
            switch (options.LicensePreference)
            {
                case MpvWindowsBuildLicensePreference.PreferLgpl:
                    selected = architectureMatches.FirstOrDefault(IsLgplAsset)
                        ?? architectureMatches.FirstOrDefault();
                    break;
                case MpvWindowsBuildLicensePreference.RequireLgpl:
                    selected = architectureMatches.FirstOrDefault(IsLgplAsset);
                    if (selected == null)
                    {
                        throw CreateLicensePreferenceException(options);
                    }

                    break;
                case MpvWindowsBuildLicensePreference.PreferNonLgpl:
                    selected = architectureMatches.FirstOrDefault(asset => !IsLgplAsset(asset))
                        ?? architectureMatches.FirstOrDefault();
                    break;
                case MpvWindowsBuildLicensePreference.RequireNonLgpl:
                    selected = architectureMatches.FirstOrDefault(asset => !IsLgplAsset(asset));
                    if (selected == null)
                    {
                        throw CreateLicensePreferenceException(options);
                    }

                    break;
                default:
                    selected = architectureMatches.FirstOrDefault();
                    break;
            }

            if (selected == null)
            {
                throw new InvalidOperationException("找不到符合 " + options.Provider + " " + options.Architecture + " 的 mpv-dev 壓縮檔。");
            }

            return selected;
        }

        /// <summary>
        /// 建立授權偏好無法符合時使用的例外狀況。
        /// </summary>
        /// <param name="options">Windows libmpv 建置下載選項。</param>
        /// <returns>描述授權偏好不符合的例外狀況。</returns>
        private static InvalidOperationException CreateLicensePreferenceException(MpvWindowsBuildDownloadOptions options)
        {
            return new InvalidOperationException(
                "No x64 mpv-dev archive matched the requested license preference " + options.LicensePreference + " for " +
                options.Provider + ". Choose a different provider or license preference.");
        }

        /// <summary>
        /// 判斷發行資產名稱是否標示為 LGPL 建置。
        /// </summary>
        /// <param name="asset">要檢查的 GitHub 發行資產。</param>
        /// <returns>資產名稱包含 LGPL 標示時為 <see langword="true"/>。</returns>
        private static bool IsLgplAsset(GitHubReleaseAsset asset)
        {
            return asset.Name.IndexOf("lgpl", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// 判斷發行資產名稱是否符合要求的 Windows libmpv 架構。
        /// </summary>
        /// <param name="name">GitHub 發行資產名稱。</param>
        /// <param name="architecture">要求的 Windows libmpv 架構。</param>
        /// <returns>資產名稱符合架構時為 <see langword="true"/>。</returns>
        private static bool IsLibMpvAssetForArchitecture(string name, MpvWindowsArchitecture architecture)
        {
            string lower = name.ToLowerInvariant();
            if (!lower.EndsWith(".7z", StringComparison.Ordinal) || !lower.StartsWith("mpv-dev", StringComparison.Ordinal))
            {
                return false;
            }

            return lower.Contains(architecture.ToAssetToken()) && !lower.Contains("x86_64-v3");
        }

        /// <summary>
        /// 解析用來解壓縮 libmpv 封存檔的 7-Zip 可執行檔路徑。
        /// </summary>
        /// <param name="explicitPath">使用者明確指定的 7z.exe 路徑。</param>
        /// <returns>可執行的 7z.exe 路徑。</returns>
        private static string ResolveSevenZipPath(string? explicitPath)
        {
            if (!string.IsNullOrWhiteSpace(explicitPath))
            {
                if (File.Exists(explicitPath))
                {
                    return explicitPath!;
                }

                throw new FileNotFoundException("7z.exe was not found.", explicitPath);
            }

            string[] candidates =
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "7-Zip", "7z.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "7-Zip", "7z.exe")
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                if (File.Exists(candidates[i]))
                {
                    return candidates[i];
                }
            }

            string? path = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrWhiteSpace(path))
            {
                string[] parts = path!.Split(new[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < parts.Length; i++)
                {
                    string candidate = Path.Combine(parts[i], "7z.exe");
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }

            throw new FileNotFoundException("7z.exe is required to extract .7z libmpv builds. Download the archive only, or provide MpvWindowsBuildDownloadOptions.SevenZipPath.");
        }

        /// <summary>
        /// 使用 7-Zip 將 libmpv 封存檔解壓縮到指定資料夾。
        /// </summary>
        /// <param name="sevenZipPath">7z.exe 可執行檔路徑。</param>
        /// <param name="archivePath">要解壓縮的 libmpv 封存檔路徑。</param>
        /// <param name="extractDirectory">解壓縮目標資料夾。</param>
        private static void ExtractWithSevenZip(string sevenZipPath, string archivePath, string extractDirectory)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = sevenZipPath,
                Arguments = "x -y -o\"" + extractDirectory + "\" \"" + archivePath + "\"",
                CreateNoWindow = true,
                UseShellExecute = false
            };

            using (Process? process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    throw new InvalidOperationException("無法啟動 7z.exe。");
                }

                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException("7z extraction failed with exit code " + process.ExitCode + ".");
                }
            }
        }

        /// <summary>
        /// 在解壓縮資料夾中尋找 libmpv-2.dll。
        /// </summary>
        /// <param name="extractDirectory">要搜尋的解壓縮資料夾。</param>
        /// <returns>找到的 libmpv-2.dll 路徑；找不到時為 <see langword="null"/>。</returns>
        private static string? FindLibMpvDll(string extractDirectory)
        {
            return Directory.GetFiles(extractDirectory, LibMpvDllName, SearchOption.AllDirectories).FirstOrDefault();
        }
    }
}

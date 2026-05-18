using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using MediaEmbedKit.Mpv.Externals;

namespace MediaEmbedKit.Mpv.Runtime.ArchiveExtraction;

/// <summary>
/// 在系統未安裝任何相容 7z 工具、且 Windows 內建 tar.exe 也無法處理時，從 ip7z/7zip
/// 官方 GitHub release 下載 standalone <c>7zr.exe</c>（588 KB 32-bit x86）當終極 fallback。
/// </summary>
/// <remarks>
/// <para>
/// <c>7zr.exe</c> 是 7-Zip 原作者 Igor Pavlov 官方 repository
/// （<see href="https://github.com/ip7z/7zip"/>）發佈的 standalone CLI 解壓工具，
/// 僅支援 .7z 格式但無 unrar 限制（純 LGPL-2.1+，可自由 redistribute）。它也是 ip7z
/// release 唯一可直接下載的 standalone CLI（其餘 <c>7za.exe</c> 在 <c>7z2601-extra.7z</c>
/// 內，需先有 7z 工具才能取出 —— chicken-and-egg）。
/// </para>
/// <para>
/// 抓最新版（<c>/releases/latest</c>，ip7z 用版本 tag 如 26.01，無 dual-release 陷阱），
/// 既有 <c>7zr.exe</c> 存在時直接重用、不重複下載。下載完用專案既有的
/// <see cref="MpvNativeAssetVerificationPolicy.RequireGitHubDigest"/> 政策驗證
/// asset 的 SHA-256 與 GitHub API 回傳 digest 一致。
/// </para>
/// <para>
/// <strong>ARM64 注意</strong>：<c>7zr.exe</c> 是 32-bit x86 binary。在 Windows on ARM64
/// 透過 x86 emulation 執行：Windows 11 ARM64 效能 OK；Windows 10 ARM64 emulation
/// 仍是 preview、較慢。ARM64 使用者若預先安裝 7-Zip ARM64 native 版（從
/// <see href="https://www.7-zip.org/download.html"/>），fallback chain 會優先走第 3 段
/// （系統 7-Zip）而不會落到此 bootstrap。
/// </para>
/// </remarks>
internal static class SevenZipBootstrapper
{
    /// <summary>ip7z/7zip 最新 release API。</summary>
    private static readonly Uri LatestReleaseApiUri = new Uri("https://api.github.com/repos/ip7z/7zip/releases/latest");

    /// <summary>要下載的 asset 名稱。</summary>
    private const string AssetName = "7zr.exe";

    /// <summary>
    /// 確保 <paramref name="downloadDirectory"/> 內有可用的 <c>7zr.exe</c>，必要時自動下載。
    /// </summary>
    /// <param name="downloadDirectory">7zr.exe 要放置的資料夾（同 libmpv 下載資料夾）。</param>
    /// <param name="userAgent">下載要求使用的 User-Agent；<see langword="null"/> 用 helper 預設。</param>
    /// <param name="cancellationToken">可取消非同步作業的語彙基元。</param>
    /// <returns>本機可執行的 <c>7zr.exe</c> 完整路徑。</returns>
    public static async Task<string> EnsureAvailableAsync(
        string downloadDirectory,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(downloadDirectory))
        {
            throw new ArgumentException("下載資料夾不可為空白。", nameof(downloadDirectory));
        }

        Directory.CreateDirectory(downloadDirectory);
        string targetPath = Path.Combine(downloadDirectory, AssetName);

        // 既有檔存在直接重用：避免每次 install 都打 GitHub API + 重複下載。
        // 想強制重抓最新版的使用者可自行刪除此檔再呼叫，或日後改裝系統 7-Zip 走第 3 段 fallback。
        if (File.Exists(targetPath))
        {
            return targetPath;
        }

        GitHubRelease release = await DownloadUtility.GetLatestReleaseAsync(
            LatestReleaseApiUri,
            userAgent,
            cancellationToken).ConfigureAwait(false);

        GitHubReleaseAsset? asset = release.Assets.FirstOrDefault(
            a => string.Equals(a.Name, AssetName, StringComparison.Ordinal));
        if (asset == null)
        {
            throw new InvalidOperationException(
                "ip7z/7zip release " + release.TagName + " 沒有名為 " + AssetName + " 的 asset；" +
                "上游可能改了發佈內容，請更新 SevenZipBootstrapper 或回報 issue。");
        }

        DownloadUtility.ValidateLockedGitHubSource(
            LatestReleaseApiUri,
            LatestReleaseApiUri,
            asset.BrowserDownloadUrl,
            "ip7z",
            "7zip",
            lockReleaseSource: true);

        await DownloadUtility.DownloadFileAsync(
            asset.BrowserDownloadUrl,
            targetPath,
            userAgent,
            true,
            cancellationToken).ConfigureAwait(false);

        // 用專案既有的 RequireGitHubDigest 政策驗證下載內容 SHA-256 與 GitHub API
        // 提供的 asset.Digest 一致（跟 libmpv / FFmpeg / Deno 同一道防線）。
        DownloadUtility.VerifyDownloadedAsset(
            targetPath,
            asset.Digest,
            verifyDigestWhenAvailable: true,
            MpvNativeAssetVerificationPolicy.RequireGitHubDigest,
            expectedSha256: null,
            asset.Name);

        return targetPath;
    }
}

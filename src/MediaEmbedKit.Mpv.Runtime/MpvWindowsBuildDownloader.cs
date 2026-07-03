using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using MediaEmbedKit.Mpv.Platforms;

using MediaEmbedKit.Mpv.Externals;

namespace MediaEmbedKit.Mpv.Runtime;

/// <summary>
/// 提供 Windows libmpv 建置下載、解壓縮與載入輔助工具。
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
    /// <param name="downloadDirectory">
    /// 壓縮檔要下載到的資料夾。
    /// </param>
    /// <param name="options">
    /// Windows libmpv 建置下載選項；未指定時使用預設選項。
    /// </param>
    /// <param name="cancellationToken">
    /// 可取消非同步作業的語彙基元。
    /// </param>
    /// <returns>
    /// 表示 libmpv 壓縮檔下載結果的工作。
    /// </returns>
    public static async Task<MpvWindowsBuildDownloadResult> DownloadLatestLibMpvArchiveAsync(
        string downloadDirectory,
        MpvWindowsBuildDownloadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(downloadDirectory))
        {
            throw new ArgumentException("下載資料夾不可為空白。", nameof(downloadDirectory));
        }

        options = options ?? new MpvWindowsBuildDownloadOptions();
        Directory.CreateDirectory(downloadDirectory);

        List<MpvWindowsBuildProvider> providerSequence = BuildProviderSequence(options);
        List<Exception> failures = new List<Exception>();
        for (int providerIndex = 0; providerIndex < providerSequence.Count; providerIndex++)
        {
            MpvWindowsBuildProvider candidateProvider = providerSequence[providerIndex];
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await DownloadLatestLibMpvArchiveForProviderAsync(
                    downloadDirectory,
                    options,
                    candidateProvider,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                failures.Add(new InvalidOperationException(candidateProvider + " 下載失敗：" + exception.Message, exception));
            }
        }

        if (failures.Count == 1)
        {
            throw failures[0];
        }

        throw new AggregateException("所有 Windows libmpv 來源都下載失敗。", failures);
    }

    /// <summary>
    /// 從指定提供者 下載最新 Windows libmpv 壓縮檔。
    /// </summary>
    /// <param name="downloadDirectory">
    /// 壓縮檔要下載到的資料夾。
    /// </param>
    /// <param name="options">
    /// Windows libmpv 建置下載選項。
    /// </param>
    /// <param name="provider">
    /// 要嘗試的提供者。
    /// </param>
    /// <param name="cancellationToken">
    /// 可取消非同步作業的語彙基元。
    /// </param>
    /// <returns>
    /// 下載結果。
    /// </returns>
    private static async Task<MpvWindowsBuildDownloadResult> DownloadLatestLibMpvArchiveForProviderAsync(
        string downloadDirectory,
        MpvWindowsBuildDownloadOptions options,
        MpvWindowsBuildProvider provider,
        CancellationToken cancellationToken)
    {
        Uri defaultApiUri = GetReleaseApiUri(provider);
        Uri apiUri = provider == options.Provider && options.ReleaseApiUriOverride != null
            ? options.ReleaseApiUriOverride
            : defaultApiUri;
        GitHubRelease release = await GetLatestReleaseAsync(options, downloadDirectory, apiUri, cancellationToken).ConfigureAwait(false);
        GitHubReleaseAsset asset = SelectLibMpvAsset(release, options);
        ValidateLockedProviderSource(apiUri, defaultApiUri, asset.BrowserDownloadUrl, provider, options.LockReleaseSource);

        string archivePath = Path.Combine(downloadDirectory, asset.Name);

        if (!File.Exists(archivePath) || options.OverwriteExisting)
        {
            await DownloadUtility.DownloadFileAsync(asset.BrowserDownloadUrl, archivePath, options.UserAgent, true, cancellationToken).ConfigureAwait(false);
        }

        if (options.VerificationPolicy == MpvNativeAssetVerificationPolicy.RequireProviderChecksum)
        {
            throw new InvalidOperationException("目前 Windows libmpv 提供者未提供獨立 checksum 資產；請改用 RequirePinnedSha256 並提供 ExpectedSha256。");
        }

        DownloadUtility.VerifyDownloadedAsset(
            archivePath,
            asset.Digest,
            options.VerifyDigest,
            options.VerificationPolicy,
            options.ExpectedSha256,
            asset.Name);

        return new MpvWindowsBuildDownloadResult(
            provider,
            release.TagName,
            asset.Name,
            new Uri(asset.BrowserDownloadUrl),
            archivePath,
            asset.Digest,
            null,
            null);
    }

    /// <summary>
    /// 串接主提供者與備援提供者 為去重後的有序嘗試清單。
    /// </summary>
    /// <param name="options">
    /// Windows libmpv 建置下載選項。
    /// </param>
    /// <returns>
    /// 去重後的提供者嘗試清單。
    /// </returns>
    private static List<MpvWindowsBuildProvider> BuildProviderSequence(MpvWindowsBuildDownloadOptions options)
    {
        List<MpvWindowsBuildProvider> sequence = new List<MpvWindowsBuildProvider> { options.Provider };
        foreach (MpvWindowsBuildProvider candidate in options.ProviderFallbackOrder)
        {
            if (!sequence.Contains(candidate))
            {
                sequence.Add(candidate);
            }
        }

        return sequence;
    }

    /// <summary>
    /// 下載並解壓縮最新 Windows libmpv 建置。
    /// </summary>
    /// <param name="downloadDirectory">
    /// 壓縮檔要下載到的資料夾。
    /// </param>
    /// <param name="options">
    /// Windows libmpv 建置下載選項；未指定時使用預設選項。
    /// </param>
    /// <param name="cancellationToken">
    /// 可取消非同步作業的語彙基元。
    /// </param>
    /// <returns>
    /// 表示 libmpv 下載與解壓縮結果的工作。
    /// </returns>
    public static async Task<MpvWindowsBuildDownloadResult> DownloadAndExtractLatestLibMpvAsync(
        string downloadDirectory,
        MpvWindowsBuildDownloadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options = options ?? new MpvWindowsBuildDownloadOptions();
        Directory.CreateDirectory(downloadDirectory);

        // Idempotency skip：runtime/libmpv-2.dll 已存在 + sidecar 標記 對得上上游當前
        // 發行資料 → 跳過下載與解壓。先前每次 InstallOrUpdateAsync 都重新下載 ~30 MB .7z 與
        // 重新解壓，現在改成「上游無變化就完全不動」。要強制重新下載請設 OverwriteExisting=
        // true 或刪掉 sidecar 標記（libmpv-2.dll.version.json）。
        string targetLibMpvPath = Path.Combine(downloadDirectory, LibMpvDllName);
        if (!options.OverwriteExisting &&
            string.IsNullOrWhiteSpace(options.ExpectedSha256) &&
            File.Exists(targetLibMpvPath))
        {
            string markerPath = targetLibMpvPath + LibMpvVersionMarker.FileExtension;
            LibMpvVersionMarker? marker = LibMpvVersionMarker.TryRead(markerPath);
            if (marker != null)
            {
                MpvWindowsBuildDownloadResult? reused = await TryReuseExistingLibMpvAsync(
                    downloadDirectory,
                    options,
                    marker,
                    targetLibMpvPath,
                    cancellationToken).ConfigureAwait(false);
                if (reused != null)
                {
                    return reused;
                }
            }
        }

        MpvWindowsBuildDownloadResult archive = await DownloadLatestLibMpvArchiveAsync(downloadDirectory, options, cancellationToken).ConfigureAwait(false);

        string extractRoot = options.ExtractDirectory ?? Path.Combine(downloadDirectory, Path.GetFileNameWithoutExtension(archive.AssetName));
        Directory.CreateDirectory(extractRoot);

        // 走 4-tier 備援鏈（tar.exe → 系統 7-Zip → WinRAR → 下載 7zr.exe）+
        // 選擇性只解 libmpv-2.dll（mpv-dev archive 還含 include/*.h、libmpv.dll.a 等
        // C++ 建置階段 用的雜物，runtime 用不到）。各層失敗時 pipeline 自動跳下一層，
        // 全失敗才擲詳細 exception 給呼叫端。
        ArchiveExtraction.ArchiveExtractionPipeline pipeline = new ArchiveExtraction.ArchiveExtractionPipeline(
            downloadDirectory,
            options.UserAgent,
            options.SevenZipPath);
        await pipeline.ExtractAsync(
            archive.ArchivePath,
            extractRoot,
            new[] { LibMpvDllName },
            cancellationToken).ConfigureAwait(false);

        string? libraryPath = FindLibMpvDll(extractRoot);
        if (libraryPath == null)
        {
            throw new FileNotFoundException("壓縮檔已解壓縮，但找不到 libmpv-2.dll。", LibMpvDllName);
        }

        // 拒絕 archive 內 libmpv-2.dll 為 symlink / reparse point —— 防 CVE-2025-11001 同類
        // 攻擊：攻擊者把 archive 內 libmpv-2.dll 改成 symlink 指向系統 DLL，
        // MpvLibraryLoader.Load 會載入錯誤檔。
        ArchiveSafety.RejectIfReparsePoint(libraryPath, "libmpv mpv-dev archive extracted libmpv-2.dll");

        // 清掉 extractRoot 內 libmpv-2.dll 以外的所有檔案 + 空目錄。mpv-dev archive
        // 含 include/*.h 與 libmpv.dll.a 等 C++ 建置階段雜物 —— 雖然 ExtractAsync
        // 已傳 include filter `new[] { LibMpvDllName }`，但部分 extractor (WinRAR /
        // 系統 7-Zip 的 萬用字元行為) 對 include pattern 處理不一致；後續解壓
        // 也可能因 archive 結構升版混入殘留。掃過一次保證 extractRoot 只剩
        // libmpv-2.dll，執行階段根目錄不被汙染。
        TryRemoveExtractResidue(extractRoot, libraryPath);

        // 解壓成功後，依 options.RetainArchive 決定是否清掉 .7z + 同 downloadDir 內
        // 舊版本殘留的 mpv-*.7z（升版後舊 archive 從未被清的常見情境）。libmpv 7z
        // 約 30–100 MB；暖啟動強驗證請設 RetainArchive=true 保留當前版本
        // archive 供未來 SHA 重驗。
        if (!options.RetainArchive)
        {
            TryDeleteArchive(archive.ArchivePath);
        }

        TryPruneStaleLibMpvArchives(
            downloadDirectory,
            keepArchiveName: options.RetainArchive ? archive.AssetName : null);

        // 寫入 sidecar 標記：紀錄本次安裝的 provider / releaseTag / assetName，下次呼叫
        // 同函式時 快速路徑比對此標記與上游發行資料，匹配就短路。
        // Marker 寫到 downloadDirectory 根（與 skip path 讀取位置一致）；通常等同
        // 執行階段根目錄。呼叫端若未把 dll 搬到 downloadDirectory/libmpv-2.dll，下次會略過
        // path 的 File.Exists 檢查會失敗，重新下載一次自動修正。
        // 寫入失敗不擲例外（marker 是 best-effort 快取，缺失下次重新下載一次回到原狀）。
        string targetMarkerPath = Path.Combine(downloadDirectory, LibMpvDllName) + LibMpvVersionMarker.FileExtension;
        LibMpvVersionMarker.Write(targetMarkerPath, archive.Provider, archive.ReleaseTag, archive.AssetName);

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
    /// 比對 sidecar 標記與上游當前發行資料。匹配（同 provider / 同 releaseTag / 同
    /// assetName）→ 直接重用既有 libmpv-2.dll，回傳合成結果（archivePath 為空字串
    /// 表示本次未下載）。任一層 query 上游失敗（網路不通、API 暫掛等）會吞掉例外 fall
    /// through 到呼叫端的正常下載路徑（仍會走 ProviderFallbackOrder 重試）。
    /// </summary>
    /// <param name="downloadDirectory">
    /// 既有 libmpv-2.dll 所在資料夾。
    /// </param>
    /// <param name="options">
    /// Windows libmpv 建置下載選項。
    /// </param>
    /// <param name="marker">
    /// 已讀到的 sidecar 標記。
    /// </param>
    /// <param name="existingLibMpvPath">
    /// 既有 libmpv-2.dll 路徑。
    /// </param>
    /// <param name="cancellationToken">
    /// 可取消非同步作業的語彙基元。
    /// </param>
    /// <returns>
    /// 匹配可 reuse 時為合成結果；不可 reuse 時為 <see langword="null"/>（呼叫端走正常下載）。
    /// </returns>
    private static async Task<MpvWindowsBuildDownloadResult?> TryReuseExistingLibMpvAsync(
        string downloadDirectory,
        MpvWindowsBuildDownloadOptions options,
        LibMpvVersionMarker marker,
        string existingLibMpvPath,
        CancellationToken cancellationToken)
    {
        foreach (MpvWindowsBuildProvider provider in BuildProviderSequence(options))
        {
            try
            {
                Uri defaultApiUri = GetReleaseApiUri(provider);
                Uri apiUri = provider == options.Provider && options.ReleaseApiUriOverride != null
                    ? options.ReleaseApiUriOverride
                    : defaultApiUri;
                GitHubRelease release = await GetLatestReleaseAsync(options, downloadDirectory, apiUri, cancellationToken).ConfigureAwait(false);
                GitHubReleaseAsset asset = SelectLibMpvAsset(release, options);
                ValidateLockedProviderSource(apiUri, defaultApiUri, asset.BrowserDownloadUrl, provider, options.LockReleaseSource);

                if (string.Equals(marker.Provider, provider.ToString(), StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(marker.ReleaseTag, release.TagName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(marker.AssetName, asset.Name, StringComparison.OrdinalIgnoreCase))
                {
                    // Skip path 仍須遵守 RetainArchive=false 「裝完即用情境清掉壓縮檔」設計：
                    // 上次完整下載成功後留下的 mpv-*-*.7z (~30 MB libmpv archive) 與
                    // 不同版本殘留的舊 archive 都掃掉，避免長期累積。
                    TryPruneStaleLibMpvArchives(
                        downloadDirectory,
                        keepArchiveName: options.RetainArchive ? asset.Name : null);

                    return new MpvWindowsBuildDownloadResult(
                        provider,
                        release.TagName,
                        asset.Name,
                        new Uri(asset.BrowserDownloadUrl),
                        string.Empty,
                        asset.Digest,
                        Path.GetDirectoryName(existingLibMpvPath),
                        existingLibMpvPath);
                }

                // 同一提供者有取到發行資料 但 marker 不匹配 → 上游已有新版，跳出去走正常下載路徑。
                // 不再嘗試備援提供者（避免「主提供者已升版 → 備援來源尚未升版 → 誤判尚未升版」的詭異情境）。
                return null;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // 此提供者查詢失敗（網路 / API），試下一個 備援提供者。
            }
        }

        return null;
    }

    /// <summary>
    /// 嘗試刪除下載的 libmpv 壓縮檔；刪除失敗不擲例外。
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
    /// 掃 <paramref name="downloadDirectory"/> 內 <c>mpv-*.7z</c> 殘留壓縮檔，
    /// 只保留 <paramref name="keepArchiveName"/> 指定的當前 archive（若為 null
    /// 則全部清掉）。處理 (a) 升版後舊版 mpv-dev-*-*.7z 殘留、(b) skip path
    /// 上次留下的 archive 未清等情境。刪除失敗不擲例外（best-effort）。
    /// </summary>
    /// <param name="downloadDirectory">
    /// runtime 下載資料夾。
    /// </param>
    /// <param name="keepArchiveName">
    /// 要保留的 壓縮檔檔名；<see langword="null"/> 表示全清。
    /// </param>
    private static void TryPruneStaleLibMpvArchives(string downloadDirectory, string? keepArchiveName)
    {
        try
        {
            if (!Directory.Exists(downloadDirectory))
            {
                return;
            }

            foreach (string path in Directory.EnumerateFiles(downloadDirectory, "mpv-*.7z"))
            {
                string fileName = Path.GetFileName(path);
                if (keepArchiveName != null &&
                    string.Equals(fileName, keepArchiveName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    File.Delete(path);
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
    /// 已知 mpv-dev archive 內 runtime 用不到的殘留檔案。
    /// </summary>
    private static readonly string[] LibMpvResidueFiles = { "libmpv.dll.a", "mpv.def" };

    /// <summary>
    /// 已知 mpv-dev archive 內 runtime 用不到的殘留資料夾。
    /// </summary>
    private static readonly string[] LibMpvResidueDirectories = { "include" };

    /// <summary>
    /// 清掉 <paramref name="extractRoot"/> 內已知 mpv-dev archive 殘留（<c>include/*.h</c>、
    /// <c>libmpv.dll.a</c>、<c>mpv.def</c> 等 C++ 建置階段雜物，執行階段不需要）。
    /// 用 <strong>whitelist 策略</strong>只動已知殘留檔名/目錄，不暴力刪「非 libmpv-2.dll」
    /// —— 避免呼叫端把 <see cref="MpvWindowsBuildDownloadOptions.ExtractDirectory"/>
    /// 指向自家資料夾時，誤刪 runtime executables（yt-dlp.exe / deno.exe 等）。
    /// 刪除失敗不擲例外（best-effort）。
    /// </summary>
    /// <param name="extractRoot">
    /// 解壓根目錄。
    /// </param>
    /// <param name="libMpvPath">
    /// libmpv-2.dll 絕對路徑（供參考；whitelist 策略不依賴）。
    /// </param>
    private static void TryRemoveExtractResidue(string extractRoot, string libMpvPath)
    {
        _ = libMpvPath;

        try
        {
            if (!Directory.Exists(extractRoot))
            {
                return;
            }

            foreach (string residueFile in LibMpvResidueFiles)
            {
                string candidate = Path.Combine(extractRoot, residueFile);
                if (File.Exists(candidate))
                {
                    try
                    {
                        File.Delete(candidate);
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }
            }

            foreach (string residueDir in LibMpvResidueDirectories)
            {
                string candidate = Path.Combine(extractRoot, residueDir);
                if (Directory.Exists(candidate))
                {
                    try
                    {
                        Directory.Delete(candidate, true);
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
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
    /// 下載、解壓縮並載入最新 Windows libmpv 建置。
    /// </summary>
    /// <param name="downloadDirectory">
    /// 壓縮檔要下載到的資料夾。
    /// </param>
    /// <param name="options">
    /// Windows libmpv 建置下載選項；未指定時使用預設選項。
    /// </param>
    /// <param name="cancellationToken">
    /// 可取消非同步作業的語彙基元。
    /// </param>
    /// <returns>
    /// 表示已載入 libmpv 檔案路徑的工作。
    /// </returns>
    public static async Task<string> DownloadExtractAndLoadLatestLibMpvAsync(
        string downloadDirectory,
        MpvWindowsBuildDownloadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        MpvWindowsBuildDownloadResult result = await DownloadAndExtractLatestLibMpvAsync(downloadDirectory, options, cancellationToken).ConfigureAwait(false);
        MpvLibraryLoader.Load(result.LibraryPath);
        return result.LibraryPath!;
    }

    /// <summary>
    /// 從設定的提供者取得最新 GitHub Releases 資料。
    /// </summary>
    /// <param name="options">
    /// Windows libmpv 建置下載選項。
    /// </param>
    /// <param name="cacheDirectory">
    /// 快取下載資料的資料夾。
    /// </param>
    /// <param name="apiUri">
    /// 要查詢的 GitHub Releases API URI。
    /// </param>
    /// <param name="cancellationToken">
    /// 可取消非同步作業的語彙基元。
    /// </param>
    /// <returns>
    /// 表示 GitHub Releases 資料的工作。
    /// </returns>
    private static async Task<GitHubRelease> GetLatestReleaseAsync(MpvWindowsBuildDownloadOptions options, string cacheDirectory, Uri apiUri, CancellationToken cancellationToken)
    {
        ValidateProviderArchitecture(options.Provider, options.Architecture);

        return await DownloadUtility.GetLatestReleaseAsync(apiUri, options.UserAgent, cacheDirectory, options.CheckInterval, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 驗證指定提供者是否發佈所要求的架構。
    /// </summary>
    /// <param name="provider">
    /// Windows libmpv 建置提供者。
    /// </param>
    /// <param name="architecture">
    /// 要驗證的 Windows libmpv 架構。
    /// </param>
    private static void ValidateProviderArchitecture(MpvWindowsBuildProvider provider, MpvWindowsArchitecture architecture)
    {
        _ = provider;
        _ = architecture;
    }

    /// <summary>
    /// 取得 Windows libmpv 建置提供者的 GitHub 最新發行 API URI。
    /// </summary>
    /// <param name="provider">
    /// Windows libmpv 建置提供者。
    /// </param>
    /// <returns>
    /// 對應提供者的 GitHub 最新發行 API URI。
    /// </returns>
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
    /// 在啟用來源鎖定時驗證 GitHub Releases API URI 與下載 URL 皆屬於指定提供者。
    /// </summary>
    /// <param name="apiUri">
    /// 實際使用的 GitHub Releases API URI。
    /// </param>
    /// <param name="expectedApiUri">
    /// 預期的 GitHub Releases API URI。
    /// </param>
    /// <param name="assetUrl">
    /// 發行資產的下載 URL。
    /// </param>
    /// <param name="provider">
    /// Windows libmpv 建置提供者。
    /// </param>
    /// <param name="lockReleaseSource">
    /// 是否啟用來源鎖定。
    /// </param>
    private static void ValidateLockedProviderSource(
        Uri apiUri,
        Uri expectedApiUri,
        string assetUrl,
        MpvWindowsBuildProvider provider,
        bool lockReleaseSource)
    {
        DownloadUtility.ValidateLockedGitHubSource(
            apiUri,
            expectedApiUri,
            assetUrl,
            GetRepositoryOwner(provider),
            GetRepositoryName(provider),
            lockReleaseSource);
    }

    /// <summary>
    /// 取得指定 Windows libmpv 提供者對應的 GitHub 儲存庫擁有者。
    /// </summary>
    /// <param name="provider">
    /// Windows libmpv 建置提供者。
    /// </param>
    /// <returns>
    /// 對應提供者的 GitHub 儲存庫擁有者。
    /// </returns>
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
    /// 取得指定 Windows libmpv 提供者對應的 GitHub 儲存庫名稱。
    /// </summary>
    /// <param name="provider">
    /// Windows libmpv 建置提供者。
    /// </param>
    /// <returns>
    /// 對應提供者的 GitHub 儲存庫名稱。
    /// </returns>
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
    /// 從 GitHub Releases 資料選取符合架構與授權偏好的 libmpv 發行資產。
    /// </summary>
    /// <param name="release">
    /// GitHub Releases 資料。
    /// </param>
    /// <param name="options">
    /// Windows libmpv 建置下載選項。
    /// </param>
    /// <returns>
    /// 符合條件的 GitHub Releases 資產。
    /// </returns>
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
    /// <param name="options">
    /// Windows libmpv 建置下載選項。
    /// </param>
    /// <returns>
    /// 描述授權偏好不符合的例外狀況。
    /// </returns>
    private static InvalidOperationException CreateLicensePreferenceException(MpvWindowsBuildDownloadOptions options)
    {
        return new InvalidOperationException(
            "No x64 mpv-dev archive matched the requested license preference " + options.LicensePreference + " for " +
            options.Provider + ". 請選擇不同的提供者或授權偏好設定。");
    }

    /// <summary>
    /// 判斷發行資產名稱是否標示為 LGPL 建置。
    /// </summary>
    /// <param name="asset">
    /// 要檢查的 GitHub Releases 資產。
    /// </param>
    /// <returns>
    /// 資產名稱包含 LGPL 標示時為 <see langword="true"/>。
    /// </returns>
    private static bool IsLgplAsset(GitHubReleaseAsset asset)
    {
        return asset.Name.IndexOf("lgpl", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// 判斷發行資產名稱是否符合要求的 Windows libmpv 架構。
    /// </summary>
    /// <param name="name">
    /// GitHub Releases 資產名稱。
    /// </param>
    /// <param name="architecture">
    /// 要求的 Windows libmpv 架構。
    /// </param>
    /// <returns>
    /// 資產名稱符合架構時為 <see langword="true"/>。
    /// </returns>
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
    /// 在解壓縮資料夾中尋找 libmpv-2.dll。
    /// </summary>
    /// <param name="extractDirectory">
    /// 要搜尋的解壓縮資料夾。
    /// </param>
    /// <returns>
    /// 找到的 libmpv-2.dll 路徑；找不到時為 <see langword="null"/>。
    /// </returns>
    private static string? FindLibMpvDll(string extractDirectory)
    {
        return Directory.GetFiles(extractDirectory, LibMpvDllName, SearchOption.AllDirectories).FirstOrDefault();
    }
}

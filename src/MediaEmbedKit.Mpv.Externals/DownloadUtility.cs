using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MediaEmbedKit.Mpv.Externals;

/// <summary>
/// 提供下載、解壓縮、驗證與處理序執行的共用工具。
/// </summary>
internal static class DownloadUtility
{
    /// <summary>
    /// 每個伺服器允許同時建立的預設 HTTP 連線數。
    /// </summary>
    private const int DefaultMaxConnectionsPerServer = 8;
    /// <summary>
    /// 長生命週期 HTTP 連線在重新建立前可保留的時間。
    /// </summary>
    private static readonly TimeSpan PooledConnectionLifetime = TimeSpan.FromMinutes(15);
    /// <summary>
    /// 供所有下載作業重複使用的 HTTP 用戶端。
    /// </summary>
    private static readonly HttpClient SharedHttpClient = CreateHttpClient();

    /// <summary>
    /// 取得或設定是否啟用 HTTPS 憑證釘選防護。
    /// </summary>
    public static bool EnableCertPinning { get; set; } = true;

    /// <summary>
    /// 取得可用於 HTTPS 憑證釘選驗證的可信任公鑰 SHA-256 Base64 雜湊集合。
    /// 預設包含 GitHub.com 與 AWS S3 託管資產所使用的主流根/中繼 CA。
    /// </summary>
    public static HashSet<string> TrustedPinnedPublicKeys { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // Sectigo Public Server Authentication Root E46 (根 CA)
        "EdsvlytFf4a/O+hCPwBXFFi46RKXqivCAF+mO7s+5Ng=",
        // Sectigo Public Server Authentication CA DV E36 (中繼 CA)
        "VqePxH3EcFwZuYK3CCOMz5HKMoeIZpZcEyBf4diPGSA=",
        // ISRG Root X1 (Let's Encrypt 根 CA)
        "9Fk6HgfMnM7/vtnBHcUhg1b3gU2bIpSd50XmKZkMbGA=",
        // Root YR (Let's Encrypt 備用根 CA)
        "3udbYNAibUAofT8NAf6ktVK0UZSjEhF99kRyhtyJ2yM=",
        // DigiCert Global Root G2 (備用根 CA)
        "yzy7t2Ax5eATj43TmiP53kf/w15DwRRMyifUalqxy18=",
        // Amazon Root CA 1 (AWS 根 CA 1)
        "UAJ/9yOqq6nk4CX2QtZgDmyT6JHYlkBfihOzezH/8cs=",
        // Starfield Services Root Certificate Authority - G2 (AWS S3 上層根 CA)
        "IluA1v9TE2XcrcYkqkmDGqLd+EHxHKR9D8TXUk4Et0A=",
        // Amazon RSA 2048 M04 (AWS 中繼 CA)
        "6nxPsa2kTA3VkIjhZo/4AwlOJ2QHhMEFG2KpZqkgNGk="
    };

    /// <summary>
    /// 從 GitHub Releases API 取得最新發行資料，支援 ETag 快取與本地冷卻 TTL 檢查。
    /// </summary>
    /// <param name="apiUri">
    /// GitHub Releases API URI。
    /// </param>
    /// <param name="userAgent">
    /// 下載要求使用的使用者代理字串。
    /// </param>
    /// <param name="cacheDirectory">
    /// 可選的快取資料夾；若指定則啟用 ETag 與本地冷卻快取。
    /// </param>
    /// <param name="checkInterval">
    /// 本地檢查的冷卻時間。
    /// </param>
    /// <param name="cancellationToken">
    /// 可取消非同步作業的語彙基元。
    /// </param>
    /// <returns>
    /// 表示最新 GitHub Releases 資料的工作。
    /// </returns>
    public static async Task<GitHubRelease> GetLatestReleaseAsync(
        Uri apiUri,
        string? userAgent,
        string? cacheDirectory,
        TimeSpan checkInterval,
        CancellationToken cancellationToken)
    {
        string? cacheKey = apiUri.ToString();
        Dictionary<string, GitHubReleaseCacheEntry>? cache = null;
        GitHubReleaseCacheEntry? cachedEntry = null;

        if (!string.IsNullOrWhiteSpace(cacheDirectory))
        {
            cache = LoadCache(cacheDirectory!);
            cache.TryGetValue(cacheKey, out cachedEntry);
        }

        // 1. 本地 TTL 檢查：如果在冷卻期內，且快取中有資料，直接回傳快取
        if (cachedEntry != null && (DateTime.UtcNow - cachedEntry.LastCheckTime) < checkInterval && cachedEntry.Release != null)
        {
            return cachedEntry.Release;
        }

        // 對 5xx / 429 走指數 backoff retry（500ms / 2s / 8s），共 3 輪。
        string? etag = cachedEntry?.ETag;
        TimeSpan[] backoffs = { TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(8) };
        for (int attempt = 0; attempt <= backoffs.Length; attempt++)
        {
            try
            {
                (GitHubRelease? release, string? newEtag, bool isNotModified) = await GetLatestReleaseOnceAsync(apiUri, userAgent, etag, cancellationToken).ConfigureAwait(false);

                if (isNotModified && cachedEntry != null && cachedEntry.Release != null)
                {
                    // 3. ETag 命中 304：更新 lastCheckTime，儲存並重用快取
                    cachedEntry.LastCheckTime = DateTime.UtcNow;
                    if (cache != null)
                    {
                        SaveCache(cacheDirectory!, cache);
                    }
                    return cachedEntry.Release;
                }

                if (release != null)
                {
                    // 4. 取得新資料 200 OK：寫入快取
                    if (cache != null)
                    {
                        cache[cacheKey] = new GitHubReleaseCacheEntry
                        {
                            ETag = newEtag ?? string.Empty,
                            LastCheckTime = DateTime.UtcNow,
                            Release = release
                        };
                        SaveCache(cacheDirectory!, cache);
                    }
                    return release;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (attempt < backoffs.Length && IsTransient(ex))
            {
                // 網路層失敗（DNS / connection refused / TLS 等）或 5xx/429
                System.Diagnostics.Trace.WriteLine(
                    "GetLatestReleaseAsync: Transient error on " + apiUri + ", retry in " +
                    backoffs[attempt].TotalSeconds + "s (attempt " + (attempt + 1) + "/" + backoffs.Length + "). Error: " + ex.Message);
                await Task.Delay(backoffs[attempt], cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // 5. 容錯降級：若發生 403 Rate Limit 或是其他網路錯誤，且快取中有資料，直接降級重用快取
                if (cachedEntry != null && cachedEntry.Release != null)
                {
                    System.Diagnostics.Trace.WriteLine(
                        $"[DownloadUtility] GitHub API query failed for {apiUri}: {ex.Message}. Falling back to cached release metadata.");
                    return cachedEntry.Release;
                }

                // 若為最後一輪且無快取可降級，直接讓 Exception 冒出
                if (attempt == backoffs.Length)
                {
                    throw;
                }
            }
        }

        // 重試耗盡且無快取：再呼一次，這次直接讓例外冒出（不再 catch）。
        (GitHubRelease? release, string? etag, bool isNotModified) finalResult = await GetLatestReleaseOnceAsync(apiUri, userAgent, etag, cancellationToken).ConfigureAwait(false);
        if (finalResult.isNotModified && cachedEntry != null && cachedEntry.Release != null)
        {
            return cachedEntry.Release;
        }
        if (finalResult.release != null)
        {
            return finalResult.release;
        }
        throw new InvalidOperationException("GitHub Releases 查詢失敗。");
    }

    private static bool IsTransient(Exception ex)
    {
        return ex is HttpRequestException || ex is TransientHttpStatusException;
    }

    /// <summary>
    /// 單次嘗試取 GitHub Releases 資料（不含 retry / backoff，支援 ETag）。
    /// </summary>
    private static async Task<(GitHubRelease? release, string? etag, bool isNotModified)> GetLatestReleaseOnceAsync(
        Uri apiUri,
        string? userAgent,
        string? requestEtag,
        CancellationToken cancellationToken)
    {
        ConfigureServicePoint(apiUri);
        using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, apiUri))
        {
            // api.github.com 用誠實標識性 UA，不發瀏覽器 sec-ch-ua client hints
            // （JSON API 不需要、GitHub ToS 偏好標識性 UA）。CDN download URL 走另一條
            // 路徑（DownloadFileAsync），用 Apply() 完整 Chrome 標頭避免 anti-bot。
            BrowserRequestHeaders.ApplyForGitHubApi(request.Headers, userAgent);
            request.Headers.Accept.ParseAdd("application/vnd.github+json");

            if (!string.IsNullOrEmpty(requestEtag))
            {
                request.Headers.IfNoneMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue(requestEtag));
            }

            using (HttpResponseMessage response = await SharedHttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
            {
                int statusCode = (int)response.StatusCode;
                if (statusCode == 304)
                {
                    return (null, requestEtag, true);
                }

                if (statusCode >= 500 || statusCode == 429)
                {
                    // 包成可被 重試迴圈捕捉 的型別。429 額外帶 X-RateLimit-Remaining 讓呼叫端知道。
                    string detail = string.Empty;
                    if (statusCode == 429 && response.Headers.TryGetValues("X-RateLimit-Remaining", out IEnumerable<string>? values))
                    {
                        detail = " (X-RateLimit-Remaining=" + string.Join(",", values) + ")";
                    }

                    throw new TransientHttpStatusException(response.StatusCode, "GitHub API HTTP " + statusCode + detail);
                }

                response.EnsureSuccessStatusCode();

                string? responseEtag = response.Headers.ETag?.ToString();

#if NET5_0_OR_GREATER
                Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
#else
                Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
#endif
                GitHubRelease? release = await JsonSerializer.DeserializeAsync(
                    stream,
                    GitHubReleaseJsonContext.Default.GitHubRelease,
                    cancellationToken).ConfigureAwait(false);
                if (release == null || release.Assets == null || release.Assets.Length == 0)
                {
                    throw new InvalidOperationException("GitHub Releases 資料未包含可下載資產。");
                }

                return (release, responseEtag, false);
            }
        }
    }

    private static readonly object CacheLock = new object();

    private static Dictionary<string, GitHubReleaseCacheEntry> LoadCache(string cacheDirectory)
    {
        string cachePath = Path.Combine(cacheDirectory, ".releases-cache.json");
        if (!File.Exists(cachePath))
        {
            return new Dictionary<string, GitHubReleaseCacheEntry>(StringComparer.OrdinalIgnoreCase);
        }

        lock (CacheLock)
        {
            try
            {
                string json = File.ReadAllText(cachePath);
                Dictionary<string, GitHubReleaseCacheEntry>? dict = JsonSerializer.Deserialize(json, GitHubReleaseJsonContext.Default.DictionaryStringGitHubReleaseCacheEntry);
                return dict ?? new Dictionary<string, GitHubReleaseCacheEntry>(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new Dictionary<string, GitHubReleaseCacheEntry>(StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    private static void SaveCache(string cacheDirectory, Dictionary<string, GitHubReleaseCacheEntry> cache)
    {
        string cachePath = Path.Combine(cacheDirectory, ".releases-cache.json");
        lock (CacheLock)
        {
            try
            {
                Directory.CreateDirectory(cacheDirectory);
                string json = JsonSerializer.Serialize(cache, GitHubReleaseJsonContext.Default.DictionaryStringGitHubReleaseCacheEntry);
                File.WriteAllText(cachePath, json);
            }
            catch
            {
                // ignore
            }
        }
    }

    /// <summary>
    /// 標示 HTTP 回應為「暫時性、可 retry」（5xx / 429）。
    /// </summary>
    private sealed class TransientHttpStatusException : Exception
    {
        public TransientHttpStatusException(System.Net.HttpStatusCode statusCode, string message)
            : base(message)
        {
            StatusCode = statusCode;
        }

        public System.Net.HttpStatusCode StatusCode { get; }
    }

    /// <summary>
    /// 將指定 URL 的內容下載到目標檔案。
    /// </summary>
    /// <param name="url">
    /// 要下載的檔案 URL。
    /// </param>
    /// <param name="targetPath">
    /// 下載檔案的目標路徑。
    /// </param>
    /// <param name="userAgent">
    /// 下載要求使用的使用者代理字串。
    /// </param>
    /// <param name="overwriteExisting">
    /// 是否覆寫已存在的目標檔案。
    /// </param>
    /// <param name="cancellationToken">
    /// 可取消非同步作業的語彙基元。
    /// </param>
    /// <returns>
    /// 代表下載作業的工作。
    /// </returns>
    public static async Task DownloadFileAsync(string url, string targetPath, string? userAgent, bool overwriteExisting, CancellationToken cancellationToken)
    {
        if (File.Exists(targetPath) && !overwriteExisting)
        {
            return;
        }

        string tempPath = targetPath + ".tmp";
        if (File.Exists(tempPath))
        {
            File.Delete(tempPath);
        }

        bool completed = false;
        try
        {
            Uri uri = new Uri(url);
            ConfigureServicePoint(uri);
            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri))
            {
                BrowserRequestHeaders.Apply(request.Headers, userAgent);

                using (HttpResponseMessage response = await SharedHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
#if NET5_0_OR_GREATER
                    using (Stream remote = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
#else
                    using (Stream remote = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
#endif
                    using (FileStream local = File.Create(tempPath))
                    {
                        // 把 cancellationToken 傳完整 —— 原本只 SendAsync 帶 token，CopyToAsync
                        // 沒帶 → cancel 大檔下載（FFmpeg 200 MB / libmpv 30 MB）後仍會跑完才停。
                        await remote.CopyToAsync(local, 81920, cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            ReplaceFile(tempPath, targetPath);
            completed = true;
        }
        finally
        {
            if (!completed)
            {
                TryDeleteFile(tempPath);
            }
        }
    }

    /// <summary>
    /// 將指定 URL 的內容下載為位元組陣列。
    /// </summary>
    /// <param name="url">
    /// 要下載的檔案 URL。
    /// </param>
    /// <param name="userAgent">
    /// 下載要求使用的使用者代理字串。
    /// </param>
    /// <param name="cancellationToken">
    /// 可取消非同步作業的語彙基元。
    /// </param>
    /// <returns>
    /// 表示已下載位元組內容的工作。
    /// </returns>
    public static async Task<byte[]> DownloadBytesAsync(string url, string? userAgent, CancellationToken cancellationToken)
    {
        Uri uri = new Uri(url);
        ConfigureServicePoint(uri);
        using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri))
        {
            BrowserRequestHeaders.Apply(request.Headers, userAgent);

            using (HttpResponseMessage response = await SharedHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
#if NET5_0_OR_GREATER
                using (Stream remote = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
#else
                using (Stream remote = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
#endif
                using (MemoryStream local = new MemoryStream())
                {
                    await remote.CopyToAsync(local, 81920, cancellationToken).ConfigureAwait(false);
                    return local.ToArray();
                }
            }
        }
    }

    /// <summary>
    /// 建立專案共用的 HTTP 用戶端執行個體。
    /// </summary>
    /// <returns>
    /// 可重複使用的 HTTP 用戶端。
    /// </returns>
    private static HttpClient CreateHttpClient()
    {
#if NETSTANDARD2_0 || NET472 || NET48
        HttpClientHandler handler = new HttpClientHandler
        {
            MaxConnectionsPerServer = DefaultMaxConnectionsPerServer,
            ServerCertificateCustomValidationCallback = (request, cert, chain, errors) => ValidateServerCertificate(request, cert, chain, errors)
        };

        return new HttpClient(handler, true);
#else
        SocketsHttpHandler handler = new SocketsHttpHandler
        {
            MaxConnectionsPerServer = DefaultMaxConnectionsPerServer,
            PooledConnectionLifetime = PooledConnectionLifetime,
            SslOptions = new SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = ValidateServerCertificate
            }
        };

        return new HttpClient(handler);
#endif
    }

    /// <summary>
    /// 自訂的 HTTPS 伺服器憑證驗證回呼，提供業界標準的憑證公鑰釘選（Cert Pinning）防線。
    /// </summary>
    internal static bool ValidateServerCertificate(
        object sender,
        X509Certificate? certificate,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors)
    {
        // 1. 若憑證釘選被關閉，只做一般的預設 SSL 驗證
        if (!EnableCertPinning)
        {
            return sslPolicyErrors == SslPolicyErrors.None;
        }

        // 2. 若標準 SSL 驗證失敗（例如憑證過期或主機名不符），直接拒絕連線，不進行釘選比對
        if (sslPolicyErrors != SslPolicyErrors.None)
        {
            return false;
        }

        // 3. 檢查憑證鏈中的公鑰
        if (chain != null && certificate != null)
        {
            for (int i = 0; i < chain.ChainElements.Count; i++)
            {
                X509Certificate2 cert = chain.ChainElements[i].Certificate;
                if (cert != null)
                {
                    // 取得公鑰 (ASN.1 DER-encoded subject public key without AlgorithmIdentifier header)
                    // 此方法在 netstandard2.0/net472/net48 平台上是唯一安全且相容的公鑰獲取方式
                    byte[] rawPublicKey = cert.PublicKey.EncodedKeyValue.RawData;
                    
                    using (SHA256 sha256 = SHA256.Create())
                    {
                        byte[] hash = sha256.ComputeHash(rawPublicKey);
                        string base64Pin = Convert.ToBase64String(hash);

                        // 只要鏈中至少有一個憑證（如中繼或根 CA）符合可信任的公鑰釘選，即為安全
                        if (TrustedPinnedPublicKeys.Contains(base64Pin))
                        {
                            return true;
                        }
                    }
                }
            }
        }

        // 4. 若走訪整條憑證鏈均無符合的 Pinned 公鑰，攔截連線
        return false;
    }

    /// <summary>
    /// 針對 .NET Framework / .NET Standard 設定與指定 URI 對應的 ServicePoint 屬性，
    /// 藉由套用連線租約過期時間防範 DNS 快取過期 (DNS Stale Cache) 問題。
    /// </summary>
    private static void ConfigureServicePoint(Uri uri)
    {
#if NETSTANDARD2_0 || NET472 || NET48
        try
        {
            System.Net.ServicePoint sp = System.Net.ServicePointManager.FindServicePoint(uri);
            sp.ConnectionLeaseTimeout = (int)PooledConnectionLifetime.TotalMilliseconds;
        }
        catch (PlatformNotSupportedException)
        {
        }
#endif
    }

    /// <summary>
    /// 在 GitHub 提供 SHA-256 摘要時驗證下載檔案。
    /// </summary>
    /// <param name="filePath">
    /// 要驗證的下載檔案路徑。
    /// </param>
    /// <param name="digest">
    /// GitHub Releases 資產提供的摘要值。
    /// </param>
    public static void VerifyDigestIfAvailable(string filePath, string? digest)
    {
        if (string.IsNullOrWhiteSpace(digest))
        {
            return;
        }

        if (!digest!.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
        {
            // 防衛性 log：GitHub 未來若改 digest 格式（例如 "sha-256:" 或新演算法），best-effort
            // 路徑會靜默跳過驗證。明確 trace 出來避免使用者「以為有驗其實沒驗」。strict 政策
            // (RequireGitHubDigest / RequireProviderChecksum) 走 VerifyGitHubDigest，會擲出例外。
            System.Diagnostics.Trace.WriteLine(
                "VerifyDigestIfAvailable: 未知 digest prefix，已跳過 best-effort 驗證。digest=" + digest);
            return;
        }

        VerifySha256(filePath, digest.Substring("sha256:".Length), Path.GetFileName(filePath));
    }

    /// <summary>
    /// 依指定策略驗證 GitHub Releases 資產摘要與呼叫端釘選的 SHA-256 值。
    /// </summary>
    /// <param name="filePath">
    /// 要驗證的下載檔案路徑。
    /// </param>
    /// <param name="digest">
    /// GitHub Releases 資產提供的摘要值。
    /// </param>
    /// <param name="verifyDigestWhenAvailable">
    /// 是否在摘要存在時進行 best-effort 驗證。
    /// </param>
    /// <param name="policy">
    /// 下載資產的完整性驗證策略。
    /// </param>
    /// <param name="expectedSha256">
    /// 呼叫端釘選的預期 SHA-256 值。
    /// </param>
    /// <param name="assetName">
    /// 驗證訊息使用的資產名稱。
    /// </param>
    public static void VerifyDownloadedAsset(
        string filePath,
        string? digest,
        bool verifyDigestWhenAvailable,
        MpvNativeAssetVerificationPolicy policy,
        string? expectedSha256,
        string assetName)
    {
        if (policy == MpvNativeAssetVerificationPolicy.RequirePinnedSha256 && string.IsNullOrWhiteSpace(expectedSha256))
        {
            throw new InvalidOperationException("已要求釘選 SHA-256 驗證，但未提供 " + assetName + " 的預期 SHA-256 值。");
        }

        bool requireGitHubDigest = policy == MpvNativeAssetVerificationPolicy.RequireGitHubDigest ||
            policy == MpvNativeAssetVerificationPolicy.RequireProviderChecksum;
        if (requireGitHubDigest)
        {
            VerifyGitHubDigest(filePath, digest, true, assetName);
        }
        else if (verifyDigestWhenAvailable)
        {
            VerifyGitHubDigest(filePath, digest, false, assetName);
        }

        if (!string.IsNullOrWhiteSpace(expectedSha256))
        {
            VerifySha256(filePath, expectedSha256!, assetName);
        }
    }

    /// <summary>
    /// 依指定策略驗證下載到記憶體中的 GitHub Releases 資產摘要。
    /// </summary>
    /// <param name="content">
    /// 要驗證的下載內容。
    /// </param>
    /// <param name="digest">
    /// GitHub Releases 資產提供的摘要值。
    /// </param>
    /// <param name="requireDigest">
    /// 是否要求摘要必須存在。
    /// </param>
    /// <param name="assetName">
    /// 驗證訊息使用的資產名稱。
    /// </param>
    public static void VerifyDownloadedBytes(byte[] content, string? digest, bool requireDigest, string assetName)
    {
        string? expected = NormalizeGitHubSha256Digest(digest);
        if (string.IsNullOrWhiteSpace(expected))
        {
            if (requireDigest)
            {
                throw new InvalidOperationException("GitHub Releases 資產未提供 SHA-256 摘要：" + assetName);
            }

            return;
        }

        string actual = ComputeSha256Hex(content);
        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("下載內容的 SHA-256 值不符：" + assetName);
        }
    }

    /// <summary>
    /// 驗證下載檔案符合預期 SHA-256 值。
    /// </summary>
    /// <param name="filePath">
    /// 要驗證的下載檔案路徑。
    /// </param>
    /// <param name="expectedSha256">
    /// 預期的 SHA-256 十六進位文字。
    /// </param>
    /// <param name="assetName">
    /// 驗證訊息使用的資產名稱。
    /// </param>
    public static void VerifySha256(string filePath, string expectedSha256, string assetName)
    {
        string normalized = NormalizeSha256(expectedSha256, "預期 SHA-256 值");
        string actual = ComputeSha256Hex(filePath);
        if (!string.Equals(normalized, actual, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("下載檔案的 SHA-256 值不符：" + assetName);
        }
    }

    /// <summary>
    /// 從 GNU 風格 checksum 內容中找出指定資產的 SHA-256 值。
    /// </summary>
    /// <param name="checksumText">
    /// 總和檢查碼檔案內容。
    /// </param>
    /// <param name="assetName">
    /// 要比對的資產檔名。
    /// </param>
    /// <returns>
    /// 符合資產名稱的 SHA-256 十六進位文字。
    /// </returns>
    public static string FindSha256InChecksumText(string checksumText, string assetName)
    {
        if (string.IsNullOrWhiteSpace(checksumText))
        {
            throw new InvalidOperationException("總和檢查碼檔案內容不可為空白。");
        }

        // 剝 UTF-8 BOM (U+FEFF)：呼叫端用 Encoding.UTF8.GetString(bytes) 不會自動剝 BOM，
        // 若上游 總和檢查碼檔以 BOM 開頭，第一個 entry 的解析會壞掉（多了不可見字元）。
        if (checksumText.Length > 0 && checksumText[0] == '﻿')
        {
            checksumText = checksumText.Substring(1);
        }

        using (StringReader reader = new StringReader(checksumText))
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                string trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                string? checksum = TryReadChecksumLine(trimmed, assetName);
                if (checksum != null)
                {
                    return checksum;
                }
            }
        }

        throw new InvalidOperationException("總和檢查碼檔案未包含指定資產的 SHA-256 值：" + assetName);
    }

    /// <summary>
    /// 在啟用來源鎖定時驗證 GitHub Releases API URI 與下載 URL。
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
    /// <param name="owner">
    /// 預期的 GitHub 儲存庫擁有者。
    /// </param>
    /// <param name="repository">
    /// 預期的 GitHub 儲存庫名稱。
    /// </param>
    /// <param name="lockReleaseSource">
    /// 是否啟用來源鎖定。
    /// </param>
    public static void ValidateLockedGitHubSource(
        Uri apiUri,
        Uri expectedApiUri,
        string assetUrl,
        string owner,
        string repository,
        bool lockReleaseSource)
    {
        if (!lockReleaseSource)
        {
            return;
        }

        if (!UriEquals(apiUri, expectedApiUri))
        {
            throw new InvalidOperationException("來源鎖定已啟用，不能使用非預設 GitHub Releases API：" + apiUri);
        }

        Uri downloadUri = new Uri(assetUrl);
        string expectedPrefix = "/" + owner + "/" + repository + "/releases/download/";
        if (!string.Equals(downloadUri.Host, "github.com", StringComparison.OrdinalIgnoreCase) ||
            !downloadUri.AbsolutePath.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("來源鎖定已啟用，下載 URL 不屬於預期的 GitHub 儲存庫：" + assetUrl);
        }
    }

    /// <summary>
    /// 計算檔案內容的 SHA-256 十六進位文字。
    /// </summary>
    /// <param name="filePath">
    /// 要計算雜湊值的檔案路徑。
    /// </param>
    /// <returns>
    /// 小寫 SHA-256 十六進位文字。
    /// </returns>
    public static string ComputeSha256Hex(string filePath)
    {
        using (SHA256 sha256 = SHA256.Create())
        using (FileStream stream = File.OpenRead(filePath))
        {
            return ToHex(sha256.ComputeHash(stream));
        }
    }

    /// <summary>
    /// 計算位元組內容的 SHA-256 十六進位文字。
    /// </summary>
    /// <param name="content">
    /// 要計算雜湊值的位元組內容。
    /// </param>
    /// <returns>
    /// 小寫 SHA-256 十六進位文字。
    /// </returns>
    public static string ComputeSha256Hex(byte[] content)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            return ToHex(sha256.ComputeHash(content));
        }
    }

    /// <summary>
    /// 驗證 GitHub Releases 資產摘要。
    /// </summary>
    /// <param name="filePath">
    /// 要驗證的下載檔案路徑。
    /// </param>
    /// <param name="digest">
    /// GitHub Releases 資產提供的摘要值。
    /// </param>
    /// <param name="requireDigest">
    /// 是否要求摘要必須存在。
    /// </param>
    /// <param name="assetName">
    /// 驗證訊息使用的資產名稱。
    /// </param>
    private static void VerifyGitHubDigest(string filePath, string? digest, bool requireDigest, string assetName)
    {
        string? expected = NormalizeGitHubSha256Digest(digest);
        if (string.IsNullOrWhiteSpace(expected))
        {
            if (requireDigest)
            {
                throw new InvalidOperationException("GitHub Releases 資產未提供 SHA-256 摘要：" + assetName);
            }

            return;
        }

        VerifySha256(filePath, expected!, assetName);
    }

    /// <summary>
    /// 從 GitHub digest 欄位擷取 SHA-256 十六進位文字。
    /// </summary>
    /// <param name="digest">
    /// GitHub Releases 資產摘要值。
    /// </param>
    /// <returns>
    /// SHA-256 十六進位文字；摘要不存在時為 <see langword="null"/>。
    /// </returns>
    private static string? NormalizeGitHubSha256Digest(string? digest)
    {
        if (string.IsNullOrWhiteSpace(digest))
        {
            return null;
        }

        const string prefix = "sha256:";
        if (!digest!.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return NormalizeSha256(digest.Substring(prefix.Length), "GitHub SHA-256 摘要");
    }

    /// <summary>
    /// 將 SHA-256 十六進位文字正規化。
    /// </summary>
    /// <param name="value">
    /// 要正規化的 SHA-256 十六進位文字。
    /// </param>
    /// <param name="description">
    /// 錯誤訊息使用的值描述。
    /// </param>
    /// <returns>
    /// 小寫 SHA-256 十六進位文字。
    /// </returns>
    private static string NormalizeSha256(string value, string description)
    {
        string normalized = value.Trim();
        if (normalized.Length != 64)
        {
            throw new InvalidOperationException(description + " 長度不是 64 個十六進位字元。");
        }

        for (int i = 0; i < normalized.Length; i++)
        {
            char character = normalized[i];
            bool isHex = (character >= '0' && character <= '9') ||
                (character >= 'a' && character <= 'f') ||
                (character >= 'A' && character <= 'F');
            if (!isHex)
            {
                throw new InvalidOperationException(description + " 包含非十六進位字元。");
            }
        }

        return normalized.ToLowerInvariant();
    }

    /// <summary>
    /// 嘗試解析單行 checksum 內容。
    /// </summary>
    /// <param name="line">
    /// 單行 checksum 內容。
    /// </param>
    /// <param name="assetName">
    /// 要比對的資產檔名。
    /// </param>
    /// <returns>
    /// 符合資產名稱的 SHA-256 十六進位文字；不符合時為 <see langword="null"/>。
    /// </returns>
    private static string? TryReadChecksumLine(string line, string assetName)
    {
        int separatorIndex = line.IndexOfAny(new[] { ' ', '\t' });
        string checksum = separatorIndex < 0 ? line : line.Substring(0, separatorIndex);
        if (checksum.Length != 64)
        {
            return null;
        }

        string normalized = NormalizeSha256(checksum, "checksum SHA-256 值");
        if (separatorIndex < 0)
        {
            return normalized;
        }

        string fileName = line.Substring(separatorIndex).Trim();
        if (fileName.StartsWith("*", StringComparison.Ordinal))
        {
            fileName = fileName.Substring(1).Trim();
        }

        if (string.Equals(fileName, assetName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFileName(fileName), assetName, StringComparison.OrdinalIgnoreCase))
        {
            return normalized;
        }

        return null;
    }

    /// <summary>
    /// 判斷兩個 URI 是否代表相同位置。
    /// </summary>
    /// <param name="left">
    /// 第一個 URI。
    /// </param>
    /// <param name="right">
    /// 第二個 URI。
    /// </param>
    /// <returns>
    /// 兩個 URI 相同時為 <see langword="true"/>。
    /// </returns>
    private static bool UriEquals(Uri left, Uri right)
    {
        return string.Equals(left.AbsoluteUri.TrimEnd('/'), right.AbsoluteUri.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 將 ZIP 壓縮檔解壓縮到指定資料夾。
    /// </summary>
    /// <param name="archivePath">
    /// ZIP 壓縮檔路徑。
    /// </param>
    /// <param name="targetDirectory">
    /// 解壓縮目標資料夾。
    /// </param>
    /// <param name="overwriteExisting">
    /// 是否覆寫已存在的解壓縮檔案。
    /// </param>
    public static void ExtractZipToDirectory(string archivePath, string targetDirectory, bool overwriteExisting)
    {
        Directory.CreateDirectory(targetDirectory);
        using (ZipArchive archive = ZipFile.OpenRead(archivePath))
        {
            string targetRoot = EnsureTrailingDirectorySeparator(Path.GetFullPath(targetDirectory));
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string destinationPath = Path.GetFullPath(Path.Combine(targetRoot, entry.FullName));
                if (!destinationPath.StartsWith(targetRoot, GetPathComparison()))
                {
                    throw new InvalidOperationException("ZIP 壓縮檔包含目標資料夾外的項目。");
                }

                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(destinationPath);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                if (File.Exists(destinationPath))
                {
                    if (!overwriteExisting)
                    {
                        continue;
                    }

                    File.Delete(destinationPath);
                }

                entry.ExtractToFile(destinationPath);
            }
        }
    }

    /// <summary>
    /// 執行處理序並傳回標準輸出的第一行。
    /// </summary>
    /// <param name="fileName">
    /// 要執行的可執行檔路徑。
    /// </param>
    /// <param name="arguments">
    /// 傳給處理序的命令列引數。
    /// </param>
    /// <param name="timeout">
    /// 等待處理序完成的逾時時間。
    /// </param>
    /// <returns>
    /// 標準輸出的第一行；無法取得時為 <see langword="null"/>。
    /// </returns>
    public static string? RunProcessForFirstLine(string fileName, string arguments, TimeSpan timeout)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using (Process? process = Process.Start(startInfo))
        {
            if (process == null)
            {
                return null;
            }

            if (!process.WaitForExit((int)timeout.TotalMilliseconds))
            {
                TryKillProcess(process);
                return null;
            }

            string output = process.StandardOutput.ReadToEnd();
            using (StringReader reader = new StringReader(output))
            {
                return reader.ReadLine();
            }
        }
    }

    /// <summary>
    /// 非同步執行處理序並收集標準輸出與標準錯誤。
    /// </summary>
    /// <param name="fileName">
    /// 要執行的可執行檔路徑。
    /// </param>
    /// <param name="arguments">
    /// 傳給處理序的命令列引數。
    /// </param>
    /// <param name="timeout">
    /// 等待處理序完成的逾時時間。
    /// </param>
    /// <param name="cancellationToken">
    /// 可取消非同步作業的語彙基元。
    /// </param>
    /// <returns>
    /// 表示處理序執行結果的工作。
    /// </returns>
    public static async Task<ToolUpdateResult> RunProcessAsync(string fileName, string arguments, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using (Process? process = Process.Start(startInfo))
        {
            if (process == null)
            {
                throw new InvalidOperationException("無法啟動 " + fileName + "。");
            }

            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();

            bool exited = await WaitForExitAsync(process, timeout, cancellationToken).ConfigureAwait(false);
            if (!exited)
            {
                TryKillProcess(process);
                throw new TimeoutException(fileName + " 未在指定時間內結束：" + timeout);
            }

            string stdout = await stdoutTask.ConfigureAwait(false);
            string stderr = await stderrTask.ConfigureAwait(false);
            return new ToolUpdateResult(fileName, arguments, process.ExitCode, stdout, stderr);
        }
    }

    /// <summary>
    /// 非同步等待處理序結束並套用逾時限制。
    /// </summary>
    /// <param name="process">
    /// 要等待的處理序。
    /// </param>
    /// <param name="timeout">
    /// 等待處理序完成的逾時時間。
    /// </param>
    /// <param name="cancellationToken">
    /// 可取消非同步作業的語彙基元。
    /// </param>
    /// <returns>
    /// 處理序在逾時前結束時為 <see langword="true"/>。
    /// </returns>
    private static async Task<bool> WaitForExitAsync(Process process, TimeSpan timeout, CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (!process.HasExited)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (DateTimeOffset.UtcNow >= deadline)
            {
                return false;
            }

            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>
    /// best-effort 終止處理序，吞掉各 platform 可能的例外（Win32Exception / InvalidOperationException /
    /// NotSupportedException 等）。.NET 5+ 路徑同時 kill 整個子處理序樹（避免 deno upgrade
    /// 等場景留下孤兒下載 process）；老 TFM 路徑只 kill top-level。
    /// </summary>
    /// <param name="process">
    /// 要終止的處理序。
    /// </param>
    internal static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
#if NET5_0_OR_GREATER
                process.Kill(entireProcessTree: true);
#else
                process.Kill();
#endif
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
        catch (NotSupportedException)
        {
        }
    }

    /// <summary>
    /// 以來源檔案取代目標檔案。
    /// </summary>
    /// <param name="sourcePath">
    /// 要移動到目標位置的來源檔案。
    /// </param>
    /// <param name="targetPath">
    /// 要被取代的目標檔案。
    /// </param>
    public static void ReplaceFile(string sourcePath, string targetPath)
    {
        if (File.Exists(targetPath))
        {
            File.Delete(targetPath);
        }

        File.Move(sourcePath, targetPath);
    }

    /// <summary>
    /// 嘗試刪除暫存檔；刪除失敗不擲例外。
    /// </summary>
    /// <param name="filePath">
    /// 要刪除的檔案。
    /// </param>
    private static void TryDeleteFile(string filePath)
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
    /// 取得目前平台適用的路徑比較方式。
    /// </summary>
    /// <returns>
    /// 目前平台適用的字串比較方式。
    /// </returns>
    private static StringComparison GetPathComparison()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    }

    /// <summary>
    /// 確保資料夾路徑結尾包含目錄分隔符號。
    /// </summary>
    /// <param name="directoryPath">
    /// 要正規化的資料夾路徑。
    /// </param>
    /// <returns>
    /// 結尾包含目錄分隔符號的資料夾路徑。
    /// </returns>
    private static string EnsureTrailingDirectorySeparator(string directoryPath)
    {
        string directorySeparator = Path.DirectorySeparatorChar.ToString();
        string alternateDirectorySeparator = Path.AltDirectorySeparatorChar.ToString();
        if (directoryPath.EndsWith(directorySeparator, StringComparison.Ordinal) ||
            directoryPath.EndsWith(alternateDirectorySeparator, StringComparison.Ordinal))
        {
            return directoryPath;
        }

        return directoryPath + directorySeparator;
    }

    /// <summary>
    /// 將位元組陣列轉換為小寫十六進位文字。
    /// </summary>
    /// <param name="bytes">
    /// 要轉換的位元組陣列。
    /// </param>
    /// <returns>
    /// 小寫十六進位文字。
    /// </returns>
    private static string ToHex(byte[] bytes)
    {
        StringBuilder builder = new StringBuilder(bytes.Length * 2);
        for (int i = 0; i < bytes.Length; i++)
        {
            builder.Append(bytes[i].ToString("x2"));
        }

        return builder.ToString();
    }
}

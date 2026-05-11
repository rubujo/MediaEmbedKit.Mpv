using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MediaEmbedKit.Mpv.Downloads
{
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
        /// 從 GitHub Releases API 取得最新發行資料。
        /// </summary>
        /// <param name="apiUri">GitHub Releases API URI。</param>
        /// <param name="userAgent">下載要求使用的使用者代理字串。</param>
        /// <param name="cancellationToken">可取消非同步作業的語彙基元。</param>
        /// <returns>表示最新 GitHub 發行資料的工作。</returns>
        public static async Task<GitHubRelease> GetLatestReleaseAsync(Uri apiUri, string? userAgent, CancellationToken cancellationToken)
        {
            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, apiUri))
            {
                BrowserRequestHeaders.Apply(request.Headers, userAgent);
                request.Headers.Accept.ParseAdd("application/vnd.github+json");

                using (HttpResponseMessage response = await SharedHttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                    DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(GitHubRelease));
                    GitHubRelease? release = (GitHubRelease?)serializer.ReadObject(stream);
                    if (release == null || release.Assets == null || release.Assets.Length == 0)
                    {
                        throw new InvalidOperationException("GitHub 發行資料未包含可下載資產。");
                    }

                    return release;
                }
            }
        }

        /// <summary>
        /// 將指定 URL 的內容下載到目標檔案。
        /// </summary>
        /// <param name="url">要下載的檔案 URL。</param>
        /// <param name="targetPath">下載檔案的目標路徑。</param>
        /// <param name="userAgent">下載要求使用的使用者代理字串。</param>
        /// <param name="overwriteExisting">是否覆寫已存在的目標檔案。</param>
        /// <param name="cancellationToken">可取消非同步作業的語彙基元。</param>
        /// <returns>代表下載作業的工作。</returns>
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

            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, new Uri(url)))
            {
                BrowserRequestHeaders.Apply(request.Headers, userAgent);

                using (HttpResponseMessage response = await SharedHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    using (Stream remote = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (FileStream local = File.Create(tempPath))
                    {
                        await remote.CopyToAsync(local).ConfigureAwait(false);
                    }
                }
            }

            ReplaceFile(tempPath, targetPath);
        }

        /// <summary>
        /// 將指定 URL 的內容下載為位元組陣列。
        /// </summary>
        /// <param name="url">要下載的檔案 URL。</param>
        /// <param name="userAgent">下載要求使用的使用者代理字串。</param>
        /// <param name="cancellationToken">可取消非同步作業的語彙基元。</param>
        /// <returns>表示已下載位元組內容的工作。</returns>
        public static async Task<byte[]> DownloadBytesAsync(string url, string? userAgent, CancellationToken cancellationToken)
        {
            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, new Uri(url)))
            {
                BrowserRequestHeaders.Apply(request.Headers, userAgent);

                using (HttpResponseMessage response = await SharedHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    using (Stream remote = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (MemoryStream local = new MemoryStream())
                    {
                        await remote.CopyToAsync(local).ConfigureAwait(false);
                        return local.ToArray();
                    }
                }
            }
        }

        /// <summary>
        /// 建立專案共用的 HTTP 用戶端執行個體。
        /// </summary>
        /// <returns>可重複使用的 HTTP 用戶端。</returns>
        private static HttpClient CreateHttpClient()
        {
#if NETSTANDARD2_0 || NET472 || NET48
            HttpClientHandler handler = new HttpClientHandler
            {
                MaxConnectionsPerServer = DefaultMaxConnectionsPerServer
            };

            return new HttpClient(handler, true);
#else
            SocketsHttpHandler handler = new SocketsHttpHandler
            {
                MaxConnectionsPerServer = DefaultMaxConnectionsPerServer,
                PooledConnectionLifetime = PooledConnectionLifetime
            };

            return new HttpClient(handler);
#endif
        }

        /// <summary>
        /// 在 GitHub 提供 SHA-256 摘要時驗證下載檔案。
        /// </summary>
        /// <param name="filePath">要驗證的下載檔案路徑。</param>
        /// <param name="digest">GitHub 發行資產提供的摘要值。</param>
        public static void VerifyDigestIfAvailable(string filePath, string? digest)
        {
            if (string.IsNullOrWhiteSpace(digest) || !digest!.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            VerifySha256(filePath, digest.Substring("sha256:".Length), Path.GetFileName(filePath));
        }

        /// <summary>
        /// 依指定策略驗證 GitHub 發行資產摘要與呼叫端釘選的 SHA-256 值。
        /// </summary>
        /// <param name="filePath">要驗證的下載檔案路徑。</param>
        /// <param name="digest">GitHub 發行資產提供的摘要值。</param>
        /// <param name="verifyDigestWhenAvailable">是否在摘要存在時進行 best-effort 驗證。</param>
        /// <param name="policy">下載資產的完整性驗證策略。</param>
        /// <param name="expectedSha256">呼叫端釘選的預期 SHA-256 值。</param>
        /// <param name="assetName">驗證訊息使用的資產名稱。</param>
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
        /// 依指定策略驗證下載到記憶體中的 GitHub 發行資產摘要。
        /// </summary>
        /// <param name="content">要驗證的下載內容。</param>
        /// <param name="digest">GitHub 發行資產提供的摘要值。</param>
        /// <param name="requireDigest">是否要求摘要必須存在。</param>
        /// <param name="assetName">驗證訊息使用的資產名稱。</param>
        public static void VerifyDownloadedBytes(byte[] content, string? digest, bool requireDigest, string assetName)
        {
            string? expected = NormalizeGitHubSha256Digest(digest);
            if (string.IsNullOrWhiteSpace(expected))
            {
                if (requireDigest)
                {
                    throw new InvalidOperationException("GitHub 發行資產未提供 SHA-256 摘要：" + assetName);
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
        /// <param name="filePath">要驗證的下載檔案路徑。</param>
        /// <param name="expectedSha256">預期的 SHA-256 十六進位文字。</param>
        /// <param name="assetName">驗證訊息使用的資產名稱。</param>
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
        /// <param name="checksumText">checksum 檔案內容。</param>
        /// <param name="assetName">要比對的資產檔名。</param>
        /// <returns>符合資產名稱的 SHA-256 十六進位文字。</returns>
        public static string FindSha256InChecksumText(string checksumText, string assetName)
        {
            if (string.IsNullOrWhiteSpace(checksumText))
            {
                throw new InvalidOperationException("checksum 檔案內容不可為空白。");
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

            throw new InvalidOperationException("checksum 檔案未包含指定資產的 SHA-256 值：" + assetName);
        }

        /// <summary>
        /// 在啟用來源鎖定時驗證 GitHub Releases API URI 與下載 URL。
        /// </summary>
        /// <param name="apiUri">實際使用的 GitHub Releases API URI。</param>
        /// <param name="expectedApiUri">預期的 GitHub Releases API URI。</param>
        /// <param name="assetUrl">發行資產的下載 URL。</param>
        /// <param name="owner">預期的 GitHub repository 擁有者。</param>
        /// <param name="repository">預期的 GitHub repository 名稱。</param>
        /// <param name="lockReleaseSource">是否啟用來源鎖定。</param>
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
                throw new InvalidOperationException("來源鎖定已啟用，下載 URL 不屬於預期的 GitHub repository：" + assetUrl);
            }
        }

        /// <summary>
        /// 計算檔案內容的 SHA-256 十六進位文字。
        /// </summary>
        /// <param name="filePath">要計算雜湊值的檔案路徑。</param>
        /// <returns>小寫 SHA-256 十六進位文字。</returns>
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
        /// <param name="content">要計算雜湊值的位元組內容。</param>
        /// <returns>小寫 SHA-256 十六進位文字。</returns>
        public static string ComputeSha256Hex(byte[] content)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                return ToHex(sha256.ComputeHash(content));
            }
        }

        /// <summary>
        /// 驗證 GitHub 發行資產摘要。
        /// </summary>
        /// <param name="filePath">要驗證的下載檔案路徑。</param>
        /// <param name="digest">GitHub 發行資產提供的摘要值。</param>
        /// <param name="requireDigest">是否要求摘要必須存在。</param>
        /// <param name="assetName">驗證訊息使用的資產名稱。</param>
        private static void VerifyGitHubDigest(string filePath, string? digest, bool requireDigest, string assetName)
        {
            string? expected = NormalizeGitHubSha256Digest(digest);
            if (string.IsNullOrWhiteSpace(expected))
            {
                if (requireDigest)
                {
                    throw new InvalidOperationException("GitHub 發行資產未提供 SHA-256 摘要：" + assetName);
                }

                return;
            }

            VerifySha256(filePath, expected!, assetName);
        }

        /// <summary>
        /// 從 GitHub digest 欄位擷取 SHA-256 十六進位文字。
        /// </summary>
        /// <param name="digest">GitHub 發行資產摘要值。</param>
        /// <returns>SHA-256 十六進位文字；摘要不存在時為 <see langword="null"/>。</returns>
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
        /// <param name="value">要正規化的 SHA-256 十六進位文字。</param>
        /// <param name="description">錯誤訊息使用的值描述。</param>
        /// <returns>小寫 SHA-256 十六進位文字。</returns>
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
        /// <param name="line">單行 checksum 內容。</param>
        /// <param name="assetName">要比對的資產檔名。</param>
        /// <returns>符合資產名稱的 SHA-256 十六進位文字；不符合時為 <see langword="null"/>。</returns>
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
        /// <param name="left">第一個 URI。</param>
        /// <param name="right">第二個 URI。</param>
        /// <returns>兩個 URI 相同時為 <see langword="true"/>。</returns>
        private static bool UriEquals(Uri left, Uri right)
        {
            return string.Equals(left.AbsoluteUri.TrimEnd('/'), right.AbsoluteUri.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 將 ZIP 壓縮檔解壓縮到指定資料夾。
        /// </summary>
        /// <param name="archivePath">ZIP 壓縮檔路徑。</param>
        /// <param name="targetDirectory">解壓縮目標資料夾。</param>
        /// <param name="overwriteExisting">是否覆寫已存在的解壓縮檔案。</param>
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
        /// <param name="fileName">要執行的可執行檔路徑。</param>
        /// <param name="arguments">傳給處理序的命令列引數。</param>
        /// <param name="timeout">等待處理序完成的逾時時間。</param>
        /// <returns>標準輸出的第一行；無法取得時為 <see langword="null"/>。</returns>
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
                    try
                    {
                        process.Kill();
                    }
                    catch (InvalidOperationException)
                    {
                    }

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
        /// <param name="fileName">要執行的可執行檔路徑。</param>
        /// <param name="arguments">傳給處理序的命令列引數。</param>
        /// <param name="timeout">等待處理序完成的逾時時間。</param>
        /// <param name="cancellationToken">可取消非同步作業的語彙基元。</param>
        /// <returns>表示處理序執行結果的工作。</returns>
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
                    try
                    {
                        process.Kill();
                    }
                    catch (InvalidOperationException)
                    {
                    }

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
        /// <param name="process">要等待的處理序。</param>
        /// <param name="timeout">等待處理序完成的逾時時間。</param>
        /// <param name="cancellationToken">可取消非同步作業的語彙基元。</param>
        /// <returns>處理序在逾時前結束時為 <see langword="true"/>。</returns>
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
        /// 以來源檔案取代目標檔案。
        /// </summary>
        /// <param name="sourcePath">要移動到目標位置的來源檔案。</param>
        /// <param name="targetPath">要被取代的目標檔案。</param>
        public static void ReplaceFile(string sourcePath, string targetPath)
        {
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }

            File.Move(sourcePath, targetPath);
        }

        /// <summary>
        /// 取得目前平台適用的路徑比較方式。
        /// </summary>
        /// <returns>目前平台適用的字串比較方式。</returns>
        private static StringComparison GetPathComparison()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        }

        /// <summary>
        /// 確保資料夾路徑結尾包含目錄分隔符號。
        /// </summary>
        /// <param name="directoryPath">要正規化的資料夾路徑。</param>
        /// <returns>結尾包含目錄分隔符號的資料夾路徑。</returns>
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
        /// <param name="bytes">要轉換的位元組陣列。</param>
        /// <returns>小寫十六進位文字。</returns>
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
}

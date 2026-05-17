using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MediaEmbedKit.Mpv.Downloads.ArchiveExtraction;

/// <summary>
/// 使用 Windows 內建 <c>tar.exe</c>（bsdtar / libarchive 後端）解壓 .7z。
/// </summary>
/// <remarks>
/// Windows 10 1803+ (build 17134, 2018-04) 與 Windows 11 / Server 2019+ 內建
/// <c>%SystemRoot%\System32\tar.exe</c>。libarchive 對 7z 的支援涵蓋 LZMA / LZMA2 /
/// BCJ filter 等 shinchiro / zhongfly 實際使用的壓縮方法。實測 Windows 11 24H2
/// 的 bsdtar 3.8.4 + liblzma 5.8.1 可正確解壓含 LZMA2 的 mpv-dev-*.7z。
/// 舊版 Windows 10 (1803–2004) 的 tar.exe 是否同樣涵蓋 LZMA2 未經完整驗證；
/// <see cref="ExtractAsync"/> 若失敗會把 stderr 帶回，由 fallback chain 跳下一個。
/// </remarks>
internal sealed class TarArchiveExtractor : IArchiveExtractor
{
    /// <summary>Tar 解壓的預設逾時時間。</summary>
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(3);

    /// <inheritdoc />
    public string Name => "Windows tar.exe (bsdtar)";

    /// <inheritdoc />
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(ResolveTarPath() != null);
    }

    /// <inheritdoc />
    public async Task ExtractAsync(
        string archivePath,
        string targetDirectory,
        IReadOnlyList<string>? includePatterns,
        CancellationToken cancellationToken)
    {
        string tarPath = ResolveTarPath() ?? throw new InvalidOperationException("tar.exe not found");
        Directory.CreateDirectory(targetDirectory);

        // bsdtar 對 .7z 的選擇性解壓：檔名直接當位置引數傳入，自動 recursive 搜路徑，
        // 不需要 --wildcards 或 -r 旗標（已實測 Win11 24H2）。
        List<string> arguments = new List<string> { "-xf", archivePath, "-C", targetDirectory };
        if (includePatterns != null)
        {
            foreach (string pattern in includePatterns)
            {
                if (!string.IsNullOrWhiteSpace(pattern))
                {
                    arguments.Add(pattern);
                }
            }
        }

        ExternalToolProcessRunner runner = new ExternalToolProcessRunner(tarPath);
        ExternalToolProcessResult result = await runner.RunAsync(arguments, DefaultTimeout, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "tar.exe exit code " + result.ExitCode +
                (string.IsNullOrWhiteSpace(result.StandardError) ? string.Empty : " — stderr: " + result.StandardError.Trim()));
        }
    }

    /// <summary>
    /// 解析 Windows 內建 tar.exe 路徑（先看 System32 再退到 PATH）。
    /// </summary>
    /// <returns>找到時為絕對路徑；否則為 <see langword="null"/>。</returns>
    private static string? ResolveTarPath()
    {
        try
        {
            string systemDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
            if (!string.IsNullOrEmpty(systemDir))
            {
                string candidate = Path.Combine(systemDir, "tar.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }
        catch (PlatformNotSupportedException)
        {
        }

        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        string[] parts = path!.Split(new[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            string candidate = Path.Combine(parts[i], "tar.exe");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}

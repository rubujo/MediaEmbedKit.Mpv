using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using MediaEmbedKit.Mpv.Externals;

namespace MediaEmbedKit.Mpv.Runtime.ArchiveExtraction;

/// <summary>
/// 使用系統安裝的 7-Zip <c>7z.exe</c>（或呼叫端明確指定的 7z 相容 CLI）解壓 .7z。
/// </summary>
/// <remarks>
/// 偵測順序：建構子明確指定路徑 → <c>Program Files\7-Zip\7z.exe</c> →
/// <c>Program Files (x86)\7-Zip\7z.exe</c> → PATH 內任何 <c>7z.exe</c>。
/// 也可建構時傳入 <c>7zr.exe</c> 路徑（CLI 完全相容；
/// <see cref="SevenZrArchiveExtractor"/> 透過此類別實現解壓）。
/// </remarks>
internal sealed class SystemSevenZipArchiveExtractor : IArchiveExtractor
{
    /// <summary>
    /// 7-Zip 解壓的預設逾時時間。
    /// </summary>
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(3);

    /// <summary>
    /// 使用者明確指定的執行檔路徑；<see langword="null"/> 表示走自動偵測。
    /// </summary>
    private readonly string? _explicitPath;

    /// <summary>
    /// 顯示名稱（用於 fallback chain 的失敗訊息）。
    /// </summary>
    private readonly string _displayName;

    /// <summary>
    /// 初始化 <see cref="SystemSevenZipArchiveExtractor"/> 類別的新執行個體。
    /// </summary>
    /// <param name="explicitPath">
    /// 明確指定的 7z.exe 相容路徑；<see langword="null"/> 走自動偵測。
    /// </param>
    /// <param name="displayName">
    /// 在 fallback 失敗訊息中使用的顯示名稱。
    /// </param>
    public SystemSevenZipArchiveExtractor(string? explicitPath = null, string? displayName = null)
    {
        _explicitPath = explicitPath;
        _displayName = displayName ?? (string.IsNullOrWhiteSpace(explicitPath) ? "System 7-Zip" : "Explicit 7z-compatible tool");
    }

    /// <summary>
    /// 顯示名稱（建構時提供，預設「System 7-Zip」或「Explicit 7z-compatible tool」）。
    /// </summary>
    public string Name => _displayName;

    /// <summary>
    /// 檢查明確指定路徑或 Program Files / PATH 內是否存在 7z.exe。
    /// </summary>
    /// <param name="cancellationToken">
    /// 未使用（路徑檢查為同步操作）。
    /// </param>
    /// <returns>
    /// 找到 7z 相容工具時為 <see langword="true"/>。
    /// </returns>
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(ResolveExecutablePath() != null);
    }

    /// <summary>
    /// 用 <c>7z x -y -o{dir} {archive} [pattern...] -r</c> 解壓 .7z。選擇性解壓需要
    /// <c>-r</c> recursive 旗標以匹配任意深度的檔名。
    /// </summary>
    /// <param name="archivePath">
    /// .7z 壓縮檔路徑。
    /// </param>
    /// <param name="targetDirectory">
    /// 解壓縮目標資料夾。
    /// </param>
    /// <param name="includePatterns">
    /// 要解出的檔名清單；<see langword="null"/> 解整個 archive。
    /// </param>
    /// <param name="cancellationToken">
    /// 可取消非同步作業的語彙基元。
    /// </param>
    /// <returns>
    /// 表示解壓縮流程的工作。
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// 7z.exe 不存在或解壓失敗（exit code 非 0）。
    /// </exception>
    public async Task ExtractAsync(
        string archivePath,
        string targetDirectory,
        IReadOnlyList<string>? includePatterns,
        CancellationToken cancellationToken)
    {
        string? executablePath = ResolveExecutablePath();
        if (executablePath == null)
        {
            throw new InvalidOperationException(_displayName + " not found");
        }

        Directory.CreateDirectory(targetDirectory);

        // 7z / 7zr CLI 選擇性解壓：把檔名當位置引數傳入，配合 -r recursive 路徑匹配
        // 確保即便 archive 結構未來改變（例如 libmpv-2.dll 放在子資料夾）也能抓到。
        List<string> arguments = new List<string>
        {
            "x",
            "-y",
            "-o" + targetDirectory,
            archivePath,
        };
        if (includePatterns != null)
        {
            foreach (string pattern in includePatterns)
            {
                if (!string.IsNullOrWhiteSpace(pattern))
                {
                    arguments.Add(pattern);
                }
            }

            arguments.Add("-r");
        }

        ExternalToolProcessRunner runner = new ExternalToolProcessRunner(executablePath);
        ExternalToolProcessResult result = await runner.RunAsync(arguments, DefaultTimeout, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                Path.GetFileName(executablePath) + " exit code " + result.ExitCode +
                (string.IsNullOrWhiteSpace(result.StandardError) ? string.Empty : " — stderr: " + result.StandardError.Trim()));
        }
    }

    /// <summary>
    /// 依優先序解析 7z 相容工具的路徑。
    /// </summary>
    /// <returns>
    /// 找到時為絕對路徑；否則為 <see langword="null"/>。
    /// </returns>
    private string? ResolveExecutablePath()
    {
        if (!string.IsNullOrWhiteSpace(_explicitPath))
        {
            return File.Exists(_explicitPath) ? _explicitPath : null;
        }

        string[] candidates =
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "7-Zip", "7z.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "7-Zip", "7z.exe"),
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

        return null;
    }
}

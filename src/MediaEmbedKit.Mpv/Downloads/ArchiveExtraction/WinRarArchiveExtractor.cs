using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MediaEmbedKit.Mpv.Downloads.ArchiveExtraction;

/// <summary>
/// 使用使用者已安裝的 WinRAR <c>WinRAR.exe</c> 解壓 .7z。
/// </summary>
/// <remarks>
/// WinRAR 在中文圈裝機率高，是 fallback chain 中對「已有 Windows 解壓工具但不是 7-Zip」
/// 使用者的覆蓋層。使用 <c>-ibck</c> 旗標跑背景模式，理論上不顯示 UI；但 WinRAR 試用期
/// 滿後可能在開啟時顯示提醒視窗，那是 WinRAR 自身行為、本程式無法避免。
/// </remarks>
internal sealed class WinRarArchiveExtractor : IArchiveExtractor
{
    /// <summary>WinRAR 解壓的預設逾時時間。</summary>
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(3);

    /// <summary>顯示名稱「WinRAR」。</summary>
    public string Name => "WinRAR";

    /// <summary>檢查 Program Files / Program Files (x86) 內是否安裝 WinRAR.exe。</summary>
    /// <param name="cancellationToken">未使用（路徑檢查為同步操作）。</param>
    /// <returns>找到 WinRAR.exe 時為 <see langword="true"/>。</returns>
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(ResolveWinRarPath() != null);
    }

    /// <summary>
    /// 用 <c>WinRAR x -ibck -y {archive} [pattern...] {dir}\</c> 解壓 .7z。
    /// 結束代碼 0 / 1（成功 / 警告但完成）皆視為成功。
    /// </summary>
    /// <param name="archivePath">.7z 壓縮檔路徑。</param>
    /// <param name="targetDirectory">解壓縮目標資料夾。</param>
    /// <param name="includePatterns">要解出的檔名清單；<see langword="null"/> 解整個 archive。</param>
    /// <param name="cancellationToken">可取消非同步作業的語彙基元。</param>
    /// <exception cref="InvalidOperationException">WinRAR.exe 不存在或解壓失敗（exit code ≥ 2）。</exception>
    public async Task ExtractAsync(
        string archivePath,
        string targetDirectory,
        IReadOnlyList<string>? includePatterns,
        CancellationToken cancellationToken)
    {
        string winRarPath = ResolveWinRarPath() ?? throw new InvalidOperationException("WinRAR.exe not found");
        Directory.CreateDirectory(targetDirectory);

        // WinRAR CLI 語法：x [switches] archive [files] [destination\]
        // -ibck = 背景模式（不顯示對話框）
        // -y    = 對所有提示回答 Yes（覆寫等）
        // target 路徑結尾要 \ 表示「解到此資料夾下」
        List<string> arguments = new List<string>
        {
            "x",
            "-ibck",
            "-y",
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
        }

        string targetWithTrailingSlash = targetDirectory.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? targetDirectory
            : targetDirectory + Path.DirectorySeparatorChar;
        arguments.Add(targetWithTrailingSlash);

        ExternalToolProcessRunner runner = new ExternalToolProcessRunner(winRarPath);
        ExternalToolProcessResult result = await runner.RunAsync(arguments, DefaultTimeout, cancellationToken).ConfigureAwait(false);

        // WinRAR 結束代碼：0 = 成功、1 = 警告但完成、其他 = 失敗。
        // 我們把警告也視為成功（0 與 1 都接受），但記下 stderr 供 fallback chain 日誌。
        if (result.ExitCode != 0 && result.ExitCode != 1)
        {
            throw new InvalidOperationException(
                "WinRAR exit code " + result.ExitCode +
                (string.IsNullOrWhiteSpace(result.StandardError) ? string.Empty : " — stderr: " + result.StandardError.Trim()));
        }
    }

    /// <summary>解析系統安裝的 WinRAR.exe 路徑。</summary>
    /// <returns>找到時為絕對路徑；否則為 <see langword="null"/>。</returns>
    private static string? ResolveWinRarPath()
    {
        string[] candidates =
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WinRAR", "WinRAR.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "WinRAR", "WinRAR.exe"),
        };
        for (int i = 0; i < candidates.Length; i++)
        {
            if (File.Exists(candidates[i]))
            {
                return candidates[i];
            }
        }

        return null;
    }
}

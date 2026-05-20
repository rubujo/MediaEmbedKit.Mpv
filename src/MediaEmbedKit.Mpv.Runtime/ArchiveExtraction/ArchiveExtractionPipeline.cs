using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using MediaEmbedKit.Mpv.Externals;

namespace MediaEmbedKit.Mpv.Runtime.ArchiveExtraction;

/// <summary>
/// libmpv .7z 多段 fallback 解壓 chain 的 orchestrator。依序嘗試
/// <c>Windows tar.exe → 系統 7-Zip → WinRAR → 下載 7zr.exe</c>；
/// 全部失敗時擲出 <see cref="InvalidOperationException"/>，訊息含每層失敗細節
/// 與使用者可採取的解法。
/// </summary>
/// <remarks>
/// 設計哲學：best-effort + 清楚 exception。每層 extractor 失敗（工具不存在、實際解壓
/// 報錯）會記下原因並跳下一層；全部失敗時不靜默退化，明確告訴使用者「試了哪些 / 為何失敗
/// / 怎麼解」，讓使用者自己決定下一步（裝 7-Zip、升 Windows、手動解壓等）。
/// </remarks>
internal sealed class ArchiveExtractionPipeline
{
    /// <summary>
    /// 7zr bootstrap 與 fallback 下載資料夾。
    /// </summary>
    private readonly string _downloadDirectory;

    /// <summary>
    /// 下載 7zr.exe 時使用的 User-Agent。
    /// </summary>
    private readonly string? _userAgent;

    /// <summary>
    /// 使用者明確指定的 7z 相容工具路徑；<see langword="null"/> 表示走自動 fallback chain。
    /// </summary>
    private readonly string? _explicitExtractorPath;

    /// <summary>
    /// 初始化 <see cref="ArchiveExtractionPipeline"/> 類別的新執行個體。
    /// </summary>
    /// <param name="downloadDirectory">
    /// 7zr.exe bootstrap 下載資料夾。
    /// </param>
    /// <param name="userAgent">
    /// 下載要求使用的 User-Agent。
    /// </param>
    /// <param name="explicitExtractorPath">
    /// 呼叫端明確指定的 7z 相容工具路徑（對應
    /// <see cref="MpvWindowsBuildDownloadOptions.SevenZipPath"/>）；
    /// <see langword="null"/> 走自動 fallback chain。
    /// </param>
    public ArchiveExtractionPipeline(string downloadDirectory, string? userAgent, string? explicitExtractorPath)
    {
        if (string.IsNullOrWhiteSpace(downloadDirectory))
        {
            throw new ArgumentException("下載資料夾不可為空白。", nameof(downloadDirectory));
        }

        _downloadDirectory = downloadDirectory;
        _userAgent = userAgent;
        _explicitExtractorPath = explicitExtractorPath;
    }

    /// <summary>
    /// 依 fallback chain 嘗試解壓 <paramref name="archivePath"/> 到 <paramref name="targetDirectory"/>。
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
    /// 所有 fallback 都失敗。
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// 作業被取消。
    /// </exception>
    public async Task ExtractAsync(
        string archivePath,
        string targetDirectory,
        IReadOnlyList<string>? includePatterns,
        CancellationToken cancellationToken)
    {
        List<IArchiveExtractor> extractors = BuildExtractorOrder();
        List<string> attempts = new List<string>();

        foreach (IArchiveExtractor extractor in extractors)
        {
            bool available;
            try
            {
                available = await extractor.IsAvailableAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception availabilityException)
            {
                attempts.Add("  [" + extractor.Name + "] NotAvailable — " + availabilityException.Message);
                continue;
            }

            if (!available)
            {
                attempts.Add("  [" + extractor.Name + "] NotAvailable");
                continue;
            }

            try
            {
                await extractor.ExtractAsync(archivePath, targetDirectory, includePatterns, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception extractionException)
            {
                attempts.Add("  [" + extractor.Name + "] ExtractionFailed — " + extractionException.Message);
                continue;
            }
        }

        throw new InvalidOperationException(BuildFailureMessage(archivePath, attempts));
    }

    /// <summary>
    /// 建立 fallback chain 順序：使用者顯式工具 → tar.exe → 系統 7-Zip → WinRAR → 下載 7zr.exe。
    /// </summary>
    /// <returns>
    /// 依序嘗試的 extractor 清單。
    /// </returns>
    private List<IArchiveExtractor> BuildExtractorOrder()
    {
        List<IArchiveExtractor> list = new List<IArchiveExtractor>();
        if (!string.IsNullOrWhiteSpace(_explicitExtractorPath))
        {
            list.Add(new SystemSevenZipArchiveExtractor(
                _explicitExtractorPath,
                displayName: "Explicit 7z-compatible tool (" + _explicitExtractorPath + ")"));
        }

        list.Add(new TarArchiveExtractor());
        list.Add(new SystemSevenZipArchiveExtractor());
        list.Add(new WinRarArchiveExtractor());
        list.Add(new SevenZrArchiveExtractor(_downloadDirectory, _userAgent));
        return list;
    }

    /// <summary>
    /// 建構 fallback chain 全失敗時的錯誤訊息，包含每層失敗細節與使用者可採取的解法。
    /// </summary>
    /// <param name="archivePath">
    /// 嘗試解壓的 archive 路徑。
    /// </param>
    /// <param name="attempts">
    /// 各層嘗試的結果摘要。
    /// </param>
    /// <returns>
    /// 給使用者看的多行錯誤訊息。
    /// </returns>
    private static string BuildFailureMessage(string archivePath, IReadOnlyList<string> attempts)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("無法解壓縮 " + Path.GetFileName(archivePath) + " —— 已依序嘗試以下方式，全部失敗：");
        sb.AppendLine();
        for (int i = 0; i < attempts.Count; i++)
        {
            sb.AppendLine(attempts[i]);
        }

        sb.AppendLine();
        sb.AppendLine("建議解法（擇一）：");
        sb.AppendLine("  1. 安裝 7-Zip（https://www.7-zip.org/download.html） —— 多數環境最穩；本程式會自動偵測");
        sb.AppendLine("  2. 升級到 Windows 10 1803+ / Windows 11 / Windows Server 2019+，自動啟用內建 tar.exe");
        sb.AppendLine("  3. 透過 MpvWindowsBuildDownloadOptions.SevenZipPath 指定自訂 7z 相容工具");
        sb.AppendLine("  4. 手動解壓 .7z 取出 libmpv-2.dll，放到 runtime 資料夾根目錄");
        sb.AppendLine();
        sb.AppendLine("詳細說明見 https://github.com/rubujo/MediaEmbedKit.Mpv/blob/main/docs/RUNTIME_ASSETS.md");
        return sb.ToString();
    }
}

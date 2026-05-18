using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using MediaEmbedKit.Mpv.Externals;

namespace MediaEmbedKit.Mpv.Runtime.ArchiveExtraction;

/// <summary>
/// 抽象 archive 解壓縮工具 —— 用於 libmpv 的 .7z 多段 fallback chain
/// （tar.exe / 系統 7-Zip / WinRAR / 下載 7zr.exe）。
/// </summary>
internal interface IArchiveExtractor
{
    /// <summary>顯示用名稱（用於 fallback chain 失敗訊息）。</summary>
    string Name { get; }

    /// <summary>
    /// 檢查此 extractor 的必要工具是否「可立即執行」（路徑檢查層級，不真正執行解壓）。
    /// 對「使用者沒裝 / 工具不存在」這類確定情況可快速 short-circuit。
    /// 對「工具存在但實際解壓 LZMA2 .7z 可能失敗」（如舊版 Windows tar.exe）這類
    /// 不確定情況回傳 <see langword="true"/>；由 <see cref="ExtractAsync"/> 在實際嘗試
    /// 時決定能否成功。
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 解壓 <paramref name="archivePath"/> 到 <paramref name="targetDirectory"/>；
    /// 若 <paramref name="includePatterns"/> 為 <see langword="null"/> 或空，解整個 archive；
    /// 否則只解出符合的檔名（各工具 CLI 翻譯不同，由實作處理）。
    /// </summary>
    /// <param name="archivePath">.7z 壓縮檔路徑。</param>
    /// <param name="targetDirectory">解壓縮目標資料夾。</param>
    /// <param name="includePatterns">要解出的檔名清單；<see langword="null"/> 解整個 archive。</param>
    /// <param name="cancellationToken">可取消非同步作業的語彙基元。</param>
    /// <exception cref="InvalidOperationException">解壓失敗（exit code 非 0、工具回報錯誤等）。</exception>
    /// <exception cref="OperationCanceledException">作業被取消。</exception>
    Task ExtractAsync(
        string archivePath,
        string targetDirectory,
        IReadOnlyList<string>? includePatterns,
        CancellationToken cancellationToken);
}

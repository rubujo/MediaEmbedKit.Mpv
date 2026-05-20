using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using MediaEmbedKit.Mpv.Externals;

namespace MediaEmbedKit.Mpv.Runtime.ArchiveExtraction;

/// <summary>
/// 使用 <see cref="SevenZipBootstrapper"/> 取得 / 重用的 <c>7zr.exe</c> 解壓 .7z。
/// </summary>
/// <remarks>
/// 是備援鏈的終極兜底層 —— 在 tar.exe / 系統 7-Zip / WinRAR 都失敗時，
/// 從 ip7z/7zip 官方下載獨立版 <c>7zr.exe</c> 後解壓。CLI 與
/// <see cref="SystemSevenZipArchiveExtractor"/> 完全相容（7z 與 7zr 共用語法），
/// 此類別本質是「bootstrap + 委派給 SystemSevenZipArchiveExtractor」。
/// </remarks>
internal sealed class SevenZrArchiveExtractor : IArchiveExtractor
{
    /// <summary>
    /// 7zr.exe bootstrap 下載資料夾。
    /// </summary>
    private readonly string _downloadDirectory;

    /// <summary>
    /// 下載要求使用的 User-Agent。
    /// </summary>
    private readonly string? _userAgent;

    /// <summary>
    /// 初始化 <see cref="SevenZrArchiveExtractor"/> 類別的新執行個體。
    /// </summary>
    /// <param name="downloadDirectory">
    /// 7zr.exe bootstrap 下載資料夾。
    /// </param>
    /// <param name="userAgent">
    /// 下載要求使用的 User-Agent；<see langword="null"/> 用 輔助工具預設。
    /// </param>
    public SevenZrArchiveExtractor(string downloadDirectory, string? userAgent)
    {
        _downloadDirectory = downloadDirectory;
        _userAgent = userAgent;
    }

    /// <summary>
    /// 顯示名稱「Downloaded 7zr.exe from ip7z/7zip」。
    /// </summary>
    public string Name => "Downloaded 7zr.exe from ip7z/7zip";

    /// <summary>
    /// 永遠回傳 <see langword="true"/> —— 缺失時會在 <see cref="ExtractAsync"/> 內透過
    /// <see cref="SevenZipBootstrapper"/> 自動下載。真正不可用的場景（無網路、ip7z 改
    /// release 結構等）會在 <see cref="ExtractAsync"/> 內 throw，由備援鏈 視為
    /// 這層失敗（但已是最後一層，會直接 throw 給呼叫端）。
    /// </summary>
    /// <param name="cancellationToken">
    /// 未使用。
    /// </param>
    /// <returns>
    /// 恆為 <see langword="true"/>。
    /// </returns>
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(true);
    }

    /// <summary>
    /// 透過 <see cref="SevenZipBootstrapper"/> 取得 / 重用 7zr.exe，再委派給
    /// <see cref="SystemSevenZipArchiveExtractor"/> 執行實際解壓（CLI 完全相容）。
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
    /// 7zr.exe 下載失敗或解壓失敗。
    /// </exception>
    public async Task ExtractAsync(
        string archivePath,
        string targetDirectory,
        IReadOnlyList<string>? includePatterns,
        CancellationToken cancellationToken)
    {
        string sevenZrPath = await SevenZipBootstrapper.EnsureAvailableAsync(
            _downloadDirectory,
            _userAgent,
            cancellationToken).ConfigureAwait(false);

        // 7zr.exe 與 7z.exe CLI 完全相容（同一個 7-Zip 程式碼基礎，只差 7zr 沒包其他格式），
        // 直接複用 SystemSevenZipArchiveExtractor 的解壓邏輯。
        SystemSevenZipArchiveExtractor delegateExtractor = new SystemSevenZipArchiveExtractor(
            sevenZrPath,
            displayName: Name);
        await delegateExtractor.ExtractAsync(archivePath, targetDirectory, includePatterns, cancellationToken).ConfigureAwait(false);
    }
}

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MediaEmbedKit.Mpv.Downloads.ArchiveExtraction;

/// <summary>
/// 使用 <see cref="SevenZipBootstrapper"/> 取得 / 重用的 <c>7zr.exe</c> 解壓 .7z。
/// </summary>
/// <remarks>
/// 是 fallback chain 的終極兜底層 —— 在 tar.exe / 系統 7-Zip / WinRAR 都失敗時，
/// 從 ip7z/7zip 官方下載 standalone <c>7zr.exe</c> 後解壓。CLI 與
/// <see cref="SystemSevenZipArchiveExtractor"/> 完全相容（7z 與 7zr 共用語法），
/// 此類別本質是「bootstrap + 委派給 SystemSevenZipArchiveExtractor」。
/// </remarks>
internal sealed class SevenZrArchiveExtractor : IArchiveExtractor
{
    /// <summary>7zr.exe bootstrap 下載資料夾。</summary>
    private readonly string _downloadDirectory;

    /// <summary>下載要求使用的 User-Agent。</summary>
    private readonly string? _userAgent;

    /// <summary>
    /// 初始化 <see cref="SevenZrArchiveExtractor"/> 類別的新執行個體。
    /// </summary>
    /// <param name="downloadDirectory">7zr.exe bootstrap 下載資料夾。</param>
    /// <param name="userAgent">下載要求使用的 User-Agent；<see langword="null"/> 用 helper 預設。</param>
    public SevenZrArchiveExtractor(string downloadDirectory, string? userAgent)
    {
        _downloadDirectory = downloadDirectory;
        _userAgent = userAgent;
    }

    /// <inheritdoc />
    public string Name => "Downloaded 7zr.exe from ip7z/7zip";

    /// <inheritdoc />
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        // 此層永遠視為可用 —— 缺失時會在 ExtractAsync 內透過 SevenZipBootstrapper 自動下載。
        // 真正不可用的場景（無網路、ip7z 改 release 結構等）會在 ExtractAsync 內 throw，
        // 由 fallback chain 視為這層失敗（但已是最後一層，會直接 throw 給呼叫端）。
        return Task.FromResult(true);
    }

    /// <inheritdoc />
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

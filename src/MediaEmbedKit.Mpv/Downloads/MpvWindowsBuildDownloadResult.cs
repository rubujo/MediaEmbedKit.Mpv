using System;

namespace MediaEmbedKit.Mpv.Downloads;

/// <summary>
/// 表示 Windows libmpv 建置下載或解壓縮作業的結果。
/// </summary>
public sealed class MpvWindowsBuildDownloadResult
{
    /// <summary>
    /// 初始化 <see cref="MpvWindowsBuildDownloadResult"/> 類別的新執行個體。
    /// </summary>
    /// <param name="provider">Windows libmpv 建置提供者。</param>
    /// <param name="releaseTag">GitHub 發行標籤。</param>
    /// <param name="assetName">已下載的發行資產名稱。</param>
    /// <param name="downloadUri">發行資產下載 URI。</param>
    /// <param name="archivePath">下載後的壓縮檔路徑。</param>
    /// <param name="digest">GitHub 發行資產提供的摘要值。</param>
    /// <param name="extractDirectory">壓縮檔解壓縮資料夾。</param>
    /// <param name="libraryPath">解壓縮後找到的 libmpv 檔案路徑。</param>
    internal MpvWindowsBuildDownloadResult(
        MpvWindowsBuildProvider provider,
        string releaseTag,
        string assetName,
        Uri downloadUri,
        string archivePath,
        string? digest,
        string? extractDirectory,
        string? libraryPath)
    {
        Provider = provider;
        ReleaseTag = releaseTag;
        AssetName = assetName;
        DownloadUri = downloadUri;
        ArchivePath = archivePath;
        Digest = digest;
        ExtractDirectory = extractDirectory;
        LibraryPath = libraryPath;
    }

    /// <summary>
    /// 取得 Windows libmpv 建置提供者。
    /// </summary>
    /// <value>Windows libmpv 建置提供者。</value>
    public MpvWindowsBuildProvider Provider { get; private set; }

    /// <summary>
    /// 取得 GitHub 發行標籤。
    /// </summary>
    /// <value>libmpv 建置發行標籤。</value>
    public string ReleaseTag { get; private set; }

    /// <summary>
    /// 取得已下載的發行資產名稱。
    /// </summary>
    /// <value>libmpv 發行資產名稱。</value>
    public string AssetName { get; private set; }

    /// <summary>
    /// 取得發行資產下載 URI。
    /// </summary>
    /// <value>libmpv 發行資產下載 URI。</value>
    public Uri DownloadUri { get; private set; }

    /// <summary>
    /// 取得下載後的壓縮檔路徑。
    /// </summary>
    /// <value>
    /// libmpv 壓縮檔路徑。<see cref="MpvWindowsBuildDownloadOptions.RetainArchive"/>
    /// 預設為 <see langword="false"/>，解壓成功後 helper 會清掉壓縮檔，此路徑指向
    /// 不存在的檔案；caller 若需確認檔案存在請自行 <see cref="System.IO.File.Exists(string)"/>。
    /// </value>
    public string ArchivePath { get; private set; }

    /// <summary>
    /// 取得 GitHub 發行資產提供的摘要值。
    /// </summary>
    /// <value>GitHub 發行資產摘要；未提供時為 <see langword="null"/>。</value>
    public string? Digest { get; private set; }

    /// <summary>
    /// 取得壓縮檔解壓縮資料夾。
    /// </summary>
    /// <value>解壓縮資料夾；尚未解壓縮時為 <see langword="null"/>。</value>
    public string? ExtractDirectory { get; private set; }

    /// <summary>
    /// 取得解壓縮後找到的 libmpv 檔案路徑。
    /// </summary>
    /// <value>libmpv 檔案路徑；尚未解壓縮時為 <see langword="null"/>。</value>
    public string? LibraryPath { get; private set; }
}

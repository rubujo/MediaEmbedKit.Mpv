using System;

namespace MediaEmbedKit.Mpv.Externals;

/// <summary>
/// 表示 yt-dlp FFmpeg-Builds 下載與解壓縮作業的結果。
/// </summary>
public sealed class FFmpegDownloadResult
{
    /// <summary>
    /// 初始化 <see cref="FFmpegDownloadResult"/> 類別的新執行個體。
    /// </summary>
    /// <param name="releaseTag">
    /// GitHub 發行標籤。
    /// </param>
    /// <param name="assetName">
    /// 已下載的發行資產名稱。
    /// </param>
    /// <param name="downloadUri">
    /// 發行資產下載 URI。
    /// </param>
    /// <param name="archivePath">
    /// 下載後的壓縮檔路徑。
    /// </param>
    /// <param name="ffmpegPath">
    /// 解壓縮後的 FFmpeg 可執行檔路徑。
    /// </param>
    /// <param name="ffprobePath">
    /// 解壓縮後的 FFprobe 可執行檔路徑。
    /// </param>
    /// <param name="digest">
    /// GitHub 發行資產提供的摘要值。
    /// </param>
    /// <param name="updated">
    /// 是否實際更新了本機檔案。
    /// </param>
    internal FFmpegDownloadResult(
        string releaseTag,
        string assetName,
        Uri downloadUri,
        string archivePath,
        string ffmpegPath,
        string ffprobePath,
        string? digest,
        bool updated)
    {
        ReleaseTag = releaseTag;
        AssetName = assetName;
        DownloadUri = downloadUri;
        ArchivePath = archivePath;
        FFmpegPath = ffmpegPath;
        FFprobePath = ffprobePath;
        Digest = digest;
        Updated = updated;
    }

    /// <summary>
    /// 取得 GitHub 發行標籤。
    /// </summary>
    /// <value>
    /// FFmpeg-Builds 發行標籤。
    /// </value>
    public string ReleaseTag { get; private set; }

    /// <summary>
    /// 取得已下載的發行資產名稱。
    /// </summary>
    /// <value>
    /// FFmpeg-Builds 發行資產名稱。
    /// </value>
    public string AssetName { get; private set; }

    /// <summary>
    /// 取得發行資產下載 URI。
    /// </summary>
    /// <value>
    /// FFmpeg-Builds 發行資產下載 URI。
    /// </value>
    public Uri DownloadUri { get; private set; }

    /// <summary>
    /// 取得下載後的壓縮檔路徑。
    /// </summary>
    /// <value>
    /// FFmpeg-Builds 壓縮檔路徑。<see cref="FFmpegDownloadOptions.RetainArchive"/>
    /// 預設為 <see langword="false"/>，解壓成功後 helper 會清掉壓縮檔，此路徑指向
    /// 不存在的檔案；caller 若需確認檔案存在請自行 <see cref="System.IO.File.Exists(string)"/>。
    /// </value>
    public string ArchivePath { get; private set; }

    /// <summary>
    /// 取得 FFmpeg 可執行檔路徑。
    /// </summary>
    /// <value>
    /// FFmpeg 可執行檔路徑。
    /// </value>
    public string FFmpegPath { get; private set; }

    /// <summary>
    /// 取得 FFprobe 可執行檔路徑。
    /// </summary>
    /// <value>
    /// FFprobe 可執行檔路徑。
    /// </value>
    public string FFprobePath { get; private set; }

    /// <summary>
    /// 取得 GitHub 發行資產提供的摘要值。
    /// </summary>
    /// <value>
    /// GitHub 發行資產摘要；未提供時為 <see langword="null"/>。
    /// </value>
    public string? Digest { get; private set; }

    /// <summary>
    /// 取得是否實際更新了本機檔案。
    /// </summary>
    /// <value>
    /// 本機檔案已更新時為 <see langword="true"/>。
    /// </value>
    public bool Updated { get; private set; }
}

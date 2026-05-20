using System;

namespace MediaEmbedKit.Mpv.Externals;

/// <summary>
/// 表示 yt-dlp 下載或更新作業的結果。
/// </summary>
public sealed class YtDlpDownloadResult
{
    /// <summary>
    /// 初始化 <see cref="YtDlpDownloadResult"/> 類別的新執行個體。
    /// </summary>
    /// <param name="channel">
    /// yt-dlp 發行通道。
    /// </param>
    /// <param name="releaseTag">
    /// GitHub 發行標籤。
    /// </param>
    /// <param name="assetName">
    /// 已下載的發行資產名稱。
    /// </param>
    /// <param name="downloadUri">
    /// 發行資產下載 URI。
    /// </param>
    /// <param name="executablePath">
    /// yt-dlp 可執行檔路徑。
    /// </param>
    /// <param name="digest">
    /// GitHub 發行資產提供的摘要值。
    /// </param>
    /// <param name="updated">
    /// 是否實際更新了本機檔案。
    /// </param>
    internal YtDlpDownloadResult(
        YtDlpReleaseChannel channel,
        string releaseTag,
        string assetName,
        Uri downloadUri,
        string executablePath,
        string? digest,
        bool updated)
    {
        Channel = channel;
        ReleaseTag = releaseTag;
        AssetName = assetName;
        DownloadUri = downloadUri;
        ExecutablePath = executablePath;
        Digest = digest;
        Updated = updated;
    }

    /// <summary>
    /// 取得 yt-dlp 發行通道。
    /// </summary>
    /// <value>
    /// yt-dlp 發行通道。
    /// </value>
    public YtDlpReleaseChannel Channel { get; private set; }

    /// <summary>
    /// 取得 GitHub 發行標籤。
    /// </summary>
    /// <value>
    /// yt-dlp 發行標籤。
    /// </value>
    public string ReleaseTag { get; private set; }

    /// <summary>
    /// 取得已下載的發行資產名稱。
    /// </summary>
    /// <value>
    /// yt-dlp 發行資產名稱。
    /// </value>
    public string AssetName { get; private set; }

    /// <summary>
    /// 取得發行資產下載 URI。
    /// </summary>
    /// <value>
    /// yt-dlp 發行資產下載 URI。
    /// </value>
    public Uri DownloadUri { get; private set; }

    /// <summary>
    /// 取得 yt-dlp 可執行檔路徑。
    /// </summary>
    /// <value>
    /// yt-dlp 可執行檔路徑。
    /// </value>
    public string ExecutablePath { get; private set; }

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

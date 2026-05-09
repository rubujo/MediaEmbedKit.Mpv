namespace MediaEmbedKit.Mpv.Downloads
{
    /// <summary>
    /// 定義 yt-dlp 可用的 GitHub 發行通道。
    /// </summary>
    public enum YtDlpReleaseChannel
    {
        /// <summary>
        /// 使用 yt-dlp 穩定發行版。
        /// </summary>
        Stable = 0,
        /// <summary>
        /// 使用 yt-dlp nightly 發行版。
        /// </summary>
        Nightly = 1,
        /// <summary>
        /// 使用 yt-dlp master 建置發行版。
        /// </summary>
        Master = 2
    }
}

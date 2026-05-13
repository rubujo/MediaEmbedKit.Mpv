namespace MediaEmbedKit.Mpv.Downloads;

/// <summary>
/// 提供建立 Windows 執行階段資料夾時使用的下載選項。
/// </summary>
public sealed class MpvWindowsRuntimeDownloadOptions
{
    /// <summary>
    /// 初始化 <see cref="MpvWindowsRuntimeDownloadOptions"/> 類別的新執行個體。
    /// </summary>
    public MpvWindowsRuntimeDownloadOptions()
    {
        Mpv = new MpvWindowsBuildDownloadOptions();
        YtDlp = new YtDlpDownloadOptions();
        Deno = new DenoDownloadOptions();
        FFmpeg = new FFmpegDownloadOptions();
        IncludeYtDlp = true;
        IncludeDeno = true;
        IncludeFFmpeg = true;
        LoadLibMpv = true;
    }

    /// <summary>
    /// 取得 libmpv Windows 建置下載選項。
    /// </summary>
    /// <value>libmpv 下載選項。</value>
    public MpvWindowsBuildDownloadOptions Mpv { get; private set; }

    /// <summary>
    /// 取得 yt-dlp Windows 可執行檔下載選項。
    /// </summary>
    /// <value>yt-dlp 下載選項。</value>
    public YtDlpDownloadOptions YtDlp { get; private set; }

    /// <summary>
    /// 取得 Deno Windows 可執行檔下載選項。
    /// </summary>
    /// <value>Deno 下載選項。</value>
    public DenoDownloadOptions Deno { get; private set; }

    /// <summary>
    /// 取得 yt-dlp 專用 FFmpeg Windows x64 建置下載選項。
    /// </summary>
    /// <value>FFmpeg 下載選項。</value>
    public FFmpegDownloadOptions FFmpeg { get; private set; }

    /// <summary>
    /// 取得或設定是否包含 yt-dlp 可執行檔。
    /// </summary>
    /// <value>下載或更新 yt-dlp 時為 <see langword="true"/>。</value>
    public bool IncludeYtDlp { get; set; }

    /// <summary>
    /// 取得或設定是否包含 Deno 可執行檔。
    /// </summary>
    /// <value>下載或更新 Deno 時為 <see langword="true"/>。</value>
    public bool IncludeDeno { get; set; }

    /// <summary>
    /// 取得或設定是否包含 yt-dlp 專用 FFmpeg 與 FFprobe 可執行檔。
    /// </summary>
    /// <value>下載或更新 FFmpeg 與 FFprobe 時為 <see langword="true"/>。</value>
    public bool IncludeFFmpeg { get; set; }

    /// <summary>
    /// 取得或設定完成下載後是否載入 libmpv。
    /// </summary>
    /// <value>下載完成後立即載入 libmpv 時為 <see langword="true"/>。</value>
    public bool LoadLibMpv { get; set; }
}

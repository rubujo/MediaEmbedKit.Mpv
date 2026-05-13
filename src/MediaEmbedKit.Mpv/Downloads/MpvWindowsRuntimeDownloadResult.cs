namespace MediaEmbedKit.Mpv.Downloads;

/// <summary>
/// 表示 Windows 執行階段資料夾安裝或更新作業的結果。
/// </summary>
public sealed class MpvWindowsRuntimeDownloadResult
{
    /// <summary>
    /// 初始化 <see cref="MpvWindowsRuntimeDownloadResult"/> 類別的新執行個體。
    /// </summary>
    /// <param name="runtimeDirectory">執行階段資料夾路徑。</param>
    /// <param name="libMpvPath">libmpv 檔案路徑。</param>
    /// <param name="ytDlpPath">yt-dlp 可執行檔路徑。</param>
    /// <param name="denoPath">Deno 可執行檔路徑。</param>
    /// <param name="ffmpegPath">FFmpeg 可執行檔路徑。</param>
    /// <param name="ffprobePath">FFprobe 可執行檔路徑。</param>
    /// <param name="mpv">libmpv 下載結果。</param>
    /// <param name="ytDlp">yt-dlp 下載結果。</param>
    /// <param name="deno">Deno 下載結果。</param>
    /// <param name="ffmpeg">FFmpeg 下載結果。</param>
    internal MpvWindowsRuntimeDownloadResult(
        string runtimeDirectory,
        string libMpvPath,
        string? ytDlpPath,
        string? denoPath,
        string? ffmpegPath,
        string? ffprobePath,
        MpvWindowsBuildDownloadResult mpv,
        YtDlpDownloadResult? ytDlp,
        DenoDownloadResult? deno,
        FFmpegDownloadResult? ffmpeg)
    {
        RuntimeDirectory = runtimeDirectory;
        LibMpvPath = libMpvPath;
        YtDlpPath = ytDlpPath;
        DenoPath = denoPath;
        FFmpegPath = ffmpegPath;
        FFprobePath = ffprobePath;
        Mpv = mpv;
        YtDlp = ytDlp;
        Deno = deno;
        FFmpeg = ffmpeg;
    }

    /// <summary>
    /// 取得執行階段資料夾路徑。
    /// </summary>
    /// <value>執行階段資料夾路徑。</value>
    public string RuntimeDirectory { get; private set; }

    /// <summary>
    /// 取得 libmpv 檔案路徑。
    /// </summary>
    /// <value>libmpv 檔案路徑。</value>
    public string LibMpvPath { get; private set; }

    /// <summary>
    /// 取得 yt-dlp 可執行檔路徑。
    /// </summary>
    /// <value>yt-dlp 可執行檔路徑；未安裝時為 <see langword="null"/>。</value>
    public string? YtDlpPath { get; private set; }

    /// <summary>
    /// 取得 Deno 可執行檔路徑。
    /// </summary>
    /// <value>Deno 可執行檔路徑；未安裝時為 <see langword="null"/>。</value>
    public string? DenoPath { get; private set; }

    /// <summary>
    /// 取得 FFmpeg 可執行檔路徑。
    /// </summary>
    /// <value>FFmpeg 可執行檔路徑；未安裝時為 <see langword="null"/>。</value>
    public string? FFmpegPath { get; private set; }

    /// <summary>
    /// 取得 FFprobe 可執行檔路徑。
    /// </summary>
    /// <value>FFprobe 可執行檔路徑；未安裝時為 <see langword="null"/>。</value>
    public string? FFprobePath { get; private set; }

    /// <summary>
    /// 取得 libmpv 下載結果。
    /// </summary>
    /// <value>libmpv 下載結果。</value>
    public MpvWindowsBuildDownloadResult Mpv { get; private set; }

    /// <summary>
    /// 取得 yt-dlp 下載結果。
    /// </summary>
    /// <value>yt-dlp 下載結果；未安裝時為 <see langword="null"/>。</value>
    public YtDlpDownloadResult? YtDlp { get; private set; }

    /// <summary>
    /// 取得 Deno 下載結果。
    /// </summary>
    /// <value>Deno 下載結果；未安裝時為 <see langword="null"/>。</value>
    public DenoDownloadResult? Deno { get; private set; }

    /// <summary>
    /// 取得 FFmpeg 下載結果。
    /// </summary>
    /// <value>FFmpeg 下載結果；未安裝時為 <see langword="null"/>。</value>
    public FFmpegDownloadResult? FFmpeg { get; private set; }
}

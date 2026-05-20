namespace MediaEmbedKit.Mpv.Externals;

/// <summary>
/// 定義執行階段 輔助工具可管理的外部工具種類。
/// </summary>
public enum ExternalToolKind
{
    /// <summary>
    /// yt-dlp 下載工具。
    /// </summary>
    YtDlp = 0,
    /// <summary>
    /// Deno 執行階段工具。
    /// </summary>
    Deno = 1,
    /// <summary>
    /// FFmpeg 與 FFprobe 媒體處理工具。
    /// </summary>
    FFmpeg = 2
}

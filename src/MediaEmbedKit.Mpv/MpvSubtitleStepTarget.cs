namespace MediaEmbedKit.Mpv;

/// <summary>
/// 定義字幕步進命令要操作的字幕軌。
/// </summary>
public enum MpvSubtitleStepTarget
{
    /// <summary>
    /// 操作主要字幕軌。
    /// </summary>
    Primary = 0,
    /// <summary>
    /// 操作次要字幕軌。
    /// </summary>
    Secondary = 1
}

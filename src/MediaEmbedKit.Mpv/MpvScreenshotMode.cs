namespace MediaEmbedKit.Mpv;

/// <summary>
/// 定義 mpv 截圖內容模式。
/// </summary>
public enum MpvScreenshotMode
{
    /// <summary>
    /// 擷取原始解析度並包含字幕。
    /// </summary>
    Subtitles = 0,
    /// <summary>
    /// 擷取視訊影像，通常不包含字幕或 OSD。
    /// </summary>
    Video = 1,
    /// <summary>
    /// 擷取視窗目前顯示內容。
    /// </summary>
    Window = 2
}

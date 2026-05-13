namespace MediaEmbedKit.Mpv;

/// <summary>
/// 定義 mpv 播放軌類型。
/// </summary>
public enum MpvTrackType
{
    /// <summary>
    /// 未知或目前版本尚未辨識的播放軌類型。
    /// </summary>
    Unknown = 0,
    /// <summary>
    /// 視訊播放軌。
    /// </summary>
    Video = 1,
    /// <summary>
    /// 音訊播放軌。
    /// </summary>
    Audio = 2,
    /// <summary>
    /// 字幕播放軌。
    /// </summary>
    Subtitle = 3
}

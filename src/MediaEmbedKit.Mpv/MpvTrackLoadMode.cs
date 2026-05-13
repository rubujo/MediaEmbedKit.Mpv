namespace MediaEmbedKit.Mpv;

/// <summary>
/// 定義外部播放軌載入後的選取模式。
/// </summary>
public enum MpvTrackLoadMode
{
    /// <summary>
    /// 載入後立即選取該播放軌。
    /// </summary>
    Select = 0,
    /// <summary>
    /// 依 mpv 自動選軌規則判斷是否選取。
    /// </summary>
    Auto = 1,
    /// <summary>
    /// 載入但不立即選取，並允許稍後以播放軌清單切換。
    /// </summary>
    Cached = 2
}

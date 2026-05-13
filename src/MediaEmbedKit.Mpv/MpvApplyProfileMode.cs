namespace MediaEmbedKit.Mpv;

/// <summary>
/// 定義套用 mpv profile 的模式。
/// </summary>
public enum MpvApplyProfileMode
{
    /// <summary>
    /// 套用 profile 內容。
    /// </summary>
    Apply = 0,
    /// <summary>
    /// 還原先前由 profile 覆寫的選項。
    /// </summary>
    Restore = 1
}

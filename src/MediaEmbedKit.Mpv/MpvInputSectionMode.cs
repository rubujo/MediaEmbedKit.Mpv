namespace MediaEmbedKit.Mpv;

/// <summary>
/// 定義建立 mpv 輸入 section 時使用的模式。
/// </summary>
public enum MpvInputSectionMode
{
    /// <summary>
    /// 只在使用者尚未綁定相同按鍵時套用 section 綁定。
    /// </summary>
    Default = 0,
    /// <summary>
    /// 強制套用 section 綁定。
    /// </summary>
    Force = 1
}

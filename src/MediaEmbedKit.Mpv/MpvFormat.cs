namespace MediaEmbedKit.Mpv;

/// <summary>
/// 定義 libmpv 屬性與節點資料格式。
/// </summary>
public enum MpvFormat
{
    /// <summary>
    /// 不使用資料格式。
    /// </summary>
    None = 0,
    /// <summary>
    /// 使用 UTF-8 字串格式。
    /// </summary>
    String = 1,
    /// <summary>
    /// 使用螢幕顯示用字串格式。
    /// </summary>
    OsdString = 2,
    /// <summary>
    /// 使用布林旗標格式。
    /// </summary>
    Flag = 3,
    /// <summary>
    /// 使用 64 位元整數格式。
    /// </summary>
    Int64 = 4,
    /// <summary>
    /// 使用雙精確度浮點數格式。
    /// </summary>
    Double = 5,
    /// <summary>
    /// 使用 libmpv 節點格式。
    /// </summary>
    Node = 6,
    /// <summary>
    /// 使用 libmpv 節點陣列格式。
    /// </summary>
    NodeArray = 7,
    /// <summary>
    /// 使用 libmpv 節點對應格式。
    /// </summary>
    NodeMap = 8,
    /// <summary>
    /// 使用位元組陣列格式。
    /// </summary>
    ByteArray = 9
}

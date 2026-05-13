namespace MediaEmbedKit.Mpv;

/// <summary>
/// 定義 libmpv 記錄訊息的嚴重性等級。
/// </summary>
public enum MpvLogLevel
{
    /// <summary>
    /// 未指定記錄等級。
    /// </summary>
    None = 0,
    /// <summary>
    /// 嚴重錯誤等級。
    /// </summary>
    Fatal = 10,
    /// <summary>
    /// 錯誤等級。
    /// </summary>
    Error = 20,
    /// <summary>
    /// 警告等級。
    /// </summary>
    Warn = 30,
    /// <summary>
    /// 資訊等級。
    /// </summary>
    Info = 40,
    /// <summary>
    /// 詳細資訊等級。
    /// </summary>
    Verbose = 50,
    /// <summary>
    /// 偵錯等級。
    /// </summary>
    Debug = 60,
    /// <summary>
    /// 追蹤等級。
    /// </summary>
    Trace = 70
}

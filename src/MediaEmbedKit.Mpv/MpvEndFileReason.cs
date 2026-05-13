namespace MediaEmbedKit.Mpv;

/// <summary>
/// 定義 libmpv 結束目前播放項目的原因。
/// </summary>
public enum MpvEndFileReason
{
    /// <summary>
    /// 播放項目已正常播放到結尾。
    /// </summary>
    EndOfFile = 0,
    /// <summary>
    /// 播放項目因停止命令而結束。
    /// </summary>
    Stop = 2,
    /// <summary>
    /// 播放項目因 mpv 結束而停止。
    /// </summary>
    Quit = 3,
    /// <summary>
    /// 播放項目因錯誤而結束。
    /// </summary>
    Error = 4,
    /// <summary>
    /// 播放項目因重新導向而結束。
    /// </summary>
    Redirect = 5
}

namespace MediaEmbedKit.Mpv;

/// <summary>
/// 表示 <see cref="MpvPlayer"/> 由 libmpv 事件聚合而成的播放狀態。
/// </summary>
public enum MpvPlaybackState
{
    /// <summary>
    /// 播放器尚未初始化、或已收到 <c>Shutdown</c> 事件。
    /// </summary>
    Idle = 0,

    /// <summary>
    /// 已要求載入媒體但 libmpv 尚未發出 <c>FileLoaded</c> 事件。
    /// </summary>
    Loading = 1,

    /// <summary>
    /// 媒體已載入但因 buffer 不足、Seek 等原因仍未開始播放。
    /// </summary>
    Buffering = 2,

    /// <summary>
    /// 媒體正在播放中。
    /// </summary>
    Playing = 3,

    /// <summary>
    /// 媒體已暫停。
    /// </summary>
    Paused = 4,

    /// <summary>
    /// 媒體已正常播放到結束。
    /// </summary>
    Ended = 5,

    /// <summary>
    /// libmpv 回報播放期間錯誤。
    /// </summary>
    Error = 6
}

namespace MediaEmbedKit.Mpv;

/// <summary>
/// 定義 libmpv 事件識別碼，對齊 mpv master <c>include/mpv/client.h</c> 的 <c>mpv_event_id</c>。
/// </summary>
/// <remarks>
/// 數值 9、10、12、13、15 是 mpv 早期版本曾使用、現已從事件列表中移除的 ID
/// （前 <c>TracksChanged</c> / <c>TrackSwitched</c> / <c>Pause</c> / <c>Unpause</c> /
/// <c>ScriptInputDispatch</c>）。新版 mpv 永遠不會發送這些 ID，本列舉刻意留下數值缺口
/// 以對應 mpv header 配置。改以觀察對應屬性（<c>track-list</c> / <c>vid</c> / <c>aid</c> /
/// <c>sid</c> / <c>pause</c>）取得相同資訊。
/// </remarks>
public enum MpvEventId
{
    /// <summary>
    /// 沒有事件。
    /// </summary>
    None = 0,
    /// <summary>
    /// libmpv 用戶端正在關閉。
    /// </summary>
    Shutdown = 1,
    /// <summary>
    /// libmpv 產生記錄訊息。
    /// </summary>
    LogMessage = 2,
    /// <summary>
    /// 非同步取得屬性呼叫已完成。
    /// </summary>
    GetPropertyReply = 3,
    /// <summary>
    /// 非同步設定屬性呼叫已完成。
    /// </summary>
    SetPropertyReply = 4,
    /// <summary>
    /// 非同步命令呼叫已完成。
    /// </summary>
    CommandReply = 5,
    /// <summary>
    /// libmpv 開始載入播放項目。
    /// </summary>
    StartFile = 6,
    /// <summary>
    /// libmpv 已結束目前播放項目。
    /// </summary>
    EndFile = 7,
    /// <summary>
    /// 播放項目已載入完成。
    /// </summary>
    FileLoaded = 8,
    /// <summary>
    /// libmpv 進入閒置狀態。
    /// </summary>
    Idle = 11,
    /// <summary>
    /// libmpv 產生週期性刻度事件。
    /// </summary>
    Tick = 14,
    /// <summary>
    /// libmpv 用戶端訊息已送達。
    /// </summary>
    ClientMessage = 16,
    /// <summary>
    /// 視訊輸出已重新設定。
    /// </summary>
    VideoReconfig = 17,
    /// <summary>
    /// 音訊輸出已重新設定。
    /// </summary>
    AudioReconfig = 18,
    /// <summary>
    /// 播放位置搜尋已開始。
    /// </summary>
    Seek = 20,
    /// <summary>
    /// 播放已在搜尋後重新開始。
    /// </summary>
    PlaybackRestart = 21,
    /// <summary>
    /// 已觀察的屬性值已變更。
    /// </summary>
    PropertyChange = 22,
    /// <summary>
    /// libmpv 事件佇列發生溢位。
    /// </summary>
    QueueOverflow = 24,
    /// <summary>
    /// libmpv 掛鉤已觸發。
    /// </summary>
    Hook = 25
}

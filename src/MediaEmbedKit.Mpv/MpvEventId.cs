namespace MediaEmbedKit.Mpv;

/// <summary>
/// 定義 libmpv 事件識別碼。
/// </summary>
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
    /// 播放軌清單已變更。
    /// </summary>
    /// <remarks>mpv 已從事件列表中移除；改以觀察 <c>track-list</c> 屬性偵測變更。</remarks>
    [System.Obsolete("mpv 已移除此事件；請改觀察 track-list 屬性。", error: false)]
    TracksChanged = 9,
    /// <summary>
    /// 目前播放軌已切換。
    /// </summary>
    /// <remarks>mpv 已從事件列表中移除；改以觀察 <c>vid</c> / <c>aid</c> / <c>sid</c> 屬性偵測變更。</remarks>
    [System.Obsolete("mpv 已移除此事件；請改觀察 vid / aid / sid 屬性。", error: false)]
    TrackSwitched = 10,
    /// <summary>
    /// libmpv 進入閒置狀態。
    /// </summary>
    Idle = 11,
    /// <summary>
    /// 播放已暫停。
    /// </summary>
    /// <remarks>mpv 已從事件列表中移除；改以觀察 <c>pause</c> 屬性偵測變更。</remarks>
    [System.Obsolete("mpv 已移除此事件；請改觀察 pause 屬性。", error: false)]
    Pause = 12,
    /// <summary>
    /// 播放已取消暫停。
    /// </summary>
    /// <remarks>mpv 已從事件列表中移除；改以觀察 <c>pause</c> 屬性偵測變更。</remarks>
    [System.Obsolete("mpv 已移除此事件；請改觀察 pause 屬性。", error: false)]
    Unpause = 13,
    /// <summary>
    /// libmpv 產生週期性刻度事件。
    /// </summary>
    Tick = 14,
    /// <summary>
    /// 指令碼輸入分派事件已發生。
    /// </summary>
    /// <remarks>mpv 已從事件列表中移除；用戶端輸入請改透過 <c>mpv_command</c> 或腳本系統。</remarks>
    [System.Obsolete("mpv 已移除此事件；不會再被觸發。", error: false)]
    ScriptInputDispatch = 15,
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

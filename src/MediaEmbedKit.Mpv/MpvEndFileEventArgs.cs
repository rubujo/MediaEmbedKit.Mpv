namespace MediaEmbedKit.Mpv;

/// <summary>
/// 表示 libmpv 完成目前播放項目時提供的事件資料。
/// </summary>
public sealed class MpvEndFileEventArgs : MpvEventArgs
{
    /// <summary>
    /// 初始化 <see cref="MpvEndFileEventArgs"/> 類別的新執行個體。
    /// </summary>
    /// <param name="reason">目前播放項目結束的原因。</param>
    /// <param name="mpvErrorCode">播放結束事件附帶的 libmpv 錯誤碼。</param>
    /// <param name="playlistEntryId">播放清單項目的 libmpv 識別碼。</param>
    public MpvEndFileEventArgs(MpvEndFileReason reason, int mpvErrorCode, long playlistEntryId)
        : this(reason, mpvErrorCode, playlistEntryId, 0, 0)
    {
    }

    /// <summary>
    /// 初始化 <see cref="MpvEndFileEventArgs"/> 類別的新執行個體。
    /// </summary>
    /// <param name="reason">目前播放項目結束的原因。</param>
    /// <param name="mpvErrorCode">播放結束事件附帶的 libmpv 錯誤碼。</param>
    /// <param name="playlistEntryId">播放清單項目的 libmpv 識別碼。</param>
    /// <param name="playlistInsertId">因重新導向而插入的第一個播放清單項目識別碼。</param>
    /// <param name="playlistInsertNumEntries">因重新導向而插入的播放清單項目數量。</param>
    public MpvEndFileEventArgs(
        MpvEndFileReason reason,
        int mpvErrorCode,
        long playlistEntryId,
        long playlistInsertId,
        int playlistInsertNumEntries)
        : base(MpvEventId.EndFile, mpvErrorCode, 0)
    {
        Reason = reason;
        MpvErrorCode = mpvErrorCode;
        PlaylistEntryId = playlistEntryId;
        PlaylistInsertId = playlistInsertId;
        PlaylistInsertNumEntries = playlistInsertNumEntries;
    }

    /// <summary>
    /// 取得目前播放項目結束的原因。
    /// </summary>
    /// <value>播放項目結束原因。</value>
    public MpvEndFileReason Reason { get; private set; }

    /// <summary>
    /// 取得播放結束事件附帶的 libmpv 錯誤碼。
    /// </summary>
    /// <value>libmpv 錯誤碼。</value>
    public int MpvErrorCode { get; private set; }

    /// <summary>
    /// 取得播放清單項目的 libmpv 識別碼。
    /// </summary>
    /// <value>播放清單項目的識別碼。</value>
    public long PlaylistEntryId { get; private set; }

    /// <summary>
    /// 取得因重新導向而插入的第一個播放清單項目識別碼。
    /// </summary>
    /// <value>插入播放清單的第一個項目識別碼；未插入時通常為 0。</value>
    public long PlaylistInsertId { get; private set; }

    /// <summary>
    /// 取得因重新導向而插入的播放清單項目數量。
    /// </summary>
    /// <value>插入播放清單的項目數量；未插入時為 0。</value>
    public int PlaylistInsertNumEntries { get; private set; }
}

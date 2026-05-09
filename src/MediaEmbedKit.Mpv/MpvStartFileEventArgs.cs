namespace MediaEmbedKit.Mpv
{
    /// <summary>
    /// 表示 libmpv 即將開始載入播放項目時提供的事件資料。
    /// </summary>
    public sealed class MpvStartFileEventArgs : MpvEventArgs
    {
        /// <summary>
        /// 初始化 <see cref="MpvStartFileEventArgs"/> 類別的新執行個體。
        /// </summary>
        /// <param name="playlistEntryId">正在載入的播放清單項目識別碼。</param>
        public MpvStartFileEventArgs(long playlistEntryId)
            : base(MpvEventId.StartFile, 0, 0)
        {
            PlaylistEntryId = playlistEntryId;
        }

        /// <summary>
        /// 取得正在載入的播放清單項目識別碼。
        /// </summary>
        /// <value>播放清單項目識別碼。</value>
        public long PlaylistEntryId { get; private set; }
    }
}

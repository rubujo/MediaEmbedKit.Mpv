namespace MediaEmbedKit.Mpv
{
    /// <summary>
    /// 定義載入播放清單時套用的模式。
    /// </summary>
    public enum MpvLoadListMode
    {
        /// <summary>
        /// 以播放清單內容取代目前播放清單。
        /// </summary>
        Replace = 0,
        /// <summary>
        /// 將播放清單內容附加到目前播放清單。
        /// </summary>
        Append = 1,
        /// <summary>
        /// 將播放清單內容附加到目前播放清單並在適當時播放。
        /// </summary>
        AppendPlay = 2,
        /// <summary>
        /// 將播放清單內容插入目前項目之後但不立即播放。
        /// </summary>
        InsertNext = 3,
        /// <summary>
        /// 將播放清單內容插入目前項目之後並在適當時播放。
        /// </summary>
        InsertNextPlay = 4,
        /// <summary>
        /// 將播放清單內容插入指定索引。
        /// </summary>
        InsertAt = 5,
        /// <summary>
        /// 將播放清單內容插入指定索引並在適當時播放。
        /// </summary>
        InsertAtPlay = 6
    }
}

namespace MediaEmbedKit.Mpv
{
    /// <summary>
    /// 定義載入播放項目時套用到播放清單的模式。
    /// </summary>
    public enum MpvLoadFileMode
    {
        /// <summary>
        /// 以新播放項目取代目前播放清單。
        /// </summary>
        Replace = 0,
        /// <summary>
        /// 將播放項目附加到播放清單但不立即播放。
        /// </summary>
        Append = 1,
        /// <summary>
        /// 將播放項目附加到播放清單並在適當時播放。
        /// </summary>
        AppendPlay = 2,
        /// <summary>
        /// 將播放項目插入目前項目之後但不立即播放。
        /// </summary>
        InsertNext = 3,
        /// <summary>
        /// 將播放項目插入目前項目之後並在適當時播放。
        /// </summary>
        InsertNextPlay = 4,
        /// <summary>
        /// 將播放項目插入指定索引。
        /// </summary>
        InsertAt = 5,
        /// <summary>
        /// 將播放項目插入指定索引並在適當時播放。
        /// </summary>
        InsertAtPlay = 6
    }
}

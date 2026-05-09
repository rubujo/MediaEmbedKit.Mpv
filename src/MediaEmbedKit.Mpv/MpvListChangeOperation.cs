namespace MediaEmbedKit.Mpv
{
    /// <summary>
    /// 定義 mpv 清單型選項或屬性的變更操作。
    /// </summary>
    public enum MpvListChangeOperation
    {
        /// <summary>
        /// 設定整個清單。
        /// </summary>
        Set = 0,
        /// <summary>
        /// 將項目加入清單前端。
        /// </summary>
        Add = 1,
        /// <summary>
        /// 將項目加入清單尾端。
        /// </summary>
        Append = 2,
        /// <summary>
        /// 移除清單項目。
        /// </summary>
        Remove = 3,
        /// <summary>
        /// 清除整個清單。
        /// </summary>
        Clear = 4
    }
}

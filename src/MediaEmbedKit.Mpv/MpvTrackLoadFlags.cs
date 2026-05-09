using System;

namespace MediaEmbedKit.Mpv
{
    /// <summary>
    /// 定義外部播放軌載入時可附加的 mpv 旗標。
    /// </summary>
    [Flags]
    public enum MpvTrackLoadFlags
    {
        /// <summary>
        /// 不附加額外旗標。
        /// </summary>
        None = 0,
        /// <summary>
        /// 將播放軌標示為聽覺障礙輔助內容。
        /// </summary>
        HearingImpaired = 1,
        /// <summary>
        /// 將播放軌標示為視覺障礙輔助內容。
        /// </summary>
        VisualImpaired = 2,
        /// <summary>
        /// 將播放軌標示為強制顯示內容。
        /// </summary>
        Forced = 4,
        /// <summary>
        /// 將播放軌標示為預設內容。
        /// </summary>
        Default = 8,
        /// <summary>
        /// 將視訊播放軌標示為附加圖片。
        /// </summary>
        AttachedPicture = 16
    }
}

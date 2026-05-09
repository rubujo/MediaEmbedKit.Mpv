using System;

namespace MediaEmbedKit.Mpv
{
    /// <summary>
    /// 定義啟用 mpv 輸入 section 時使用的旗標。
    /// </summary>
    [Flags]
    public enum MpvInputSectionFlags
    {
        /// <summary>
        /// 不使用額外旗標。
        /// </summary>
        None = 0,
        /// <summary>
        /// 讓新 section 排除較低層級的既有 section。
        /// </summary>
        Exclusive = 1,
        /// <summary>
        /// 允許 section 隱藏滑鼠游標。
        /// </summary>
        AllowHideCursor = 2,
        /// <summary>
        /// 允許 section 啟動視訊輸出拖曳。
        /// </summary>
        AllowVoDragging = 4
    }
}

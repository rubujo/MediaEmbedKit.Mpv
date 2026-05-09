using System;

namespace MediaEmbedKit.Mpv
{
    /// <summary>
    /// 定義 mpv 截圖命令使用的進階旗標。
    /// </summary>
    [Flags]
    public enum MpvScreenshotFlags
    {
        /// <summary>
        /// 不附加進階旗標。
        /// </summary>
        None = 0,
        /// <summary>
        /// 將 OSD 一併納入截圖。
        /// </summary>
        Osd = 1,
        /// <summary>
        /// 依視窗縮放後尺寸輸出截圖。
        /// </summary>
        Scaled = 2,
        /// <summary>
        /// 對每個影格持續截圖。
        /// </summary>
        EachFrame = 4
    }
}

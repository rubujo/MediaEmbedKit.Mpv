using System;

namespace MediaEmbedKit.Mpv.Render
{
    /// <summary>
    /// 定義 libmpv render API 更新通知的旗標。
    /// </summary>
    [Flags]
    public enum MpvRenderUpdateFlags : ulong
    {
        /// <summary>
        /// 沒有更新。
        /// </summary>
        None = 0,
        /// <summary>
        /// render API 有新影格可處理。
        /// </summary>
        Frame = 1UL << 0
    }
}

using System;

namespace MediaEmbedKit.Mpv.Render;

/// <summary>
/// 定義 libmpv render API 下一個影格資訊的旗標。
/// </summary>
[Flags]
public enum MpvRenderFrameInfoFlags : ulong
{
    /// <summary>
    /// 沒有額外影格資訊。
    /// </summary>
    None = 0,
    /// <summary>
    /// 下一次 render 呼叫會呈現新影格。
    /// </summary>
    Present = 1UL << 0,
    /// <summary>
    /// 應重新繪製目前輸出。
    /// </summary>
    Redraw = 1UL << 1,
    /// <summary>
    /// 下一個影格可能重複目前內容。
    /// </summary>
    Repeat = 1UL << 2,
    /// <summary>
    /// 呼叫端應避免等待垂直同步。
    /// </summary>
    BlockVsync = 1UL << 3
}

using System;

using MediaEmbedKit.Mpv.Externals;

namespace MediaEmbedKit.Mpv.Runtime;

/// <summary>
/// 描述 <c>MpvRuntimeHealthReport.IsHealthyFor</c>（位於 <c>MediaEmbedKit.Mpv.Diagnostics</c> 套件）可指定的「必備附帶工具」集合。
/// 核心 libmpv 一律必備，不需透過此列舉表達。
/// </summary>
[Flags]
public enum MpvRuntimeTools
{
    /// <summary>
    /// 僅要求核心 libmpv。
    /// </summary>
    None = 0,

    /// <summary>
    /// 必備 yt-dlp.exe（URL 解析、線上來源下載）。
    /// </summary>
    YtDlp = 1 << 0,

    /// <summary>
    /// 必備 deno.exe（自訂 JavaScript 後處理腳本）。
    /// </summary>
    Deno = 1 << 1,

    /// <summary>
    /// 必備 ffmpeg.exe（yt-dlp 後處理合併、轉碼）。
    /// </summary>
    FFmpeg = 1 << 2,

    /// <summary>
    /// 必備 ffprobe.exe（媒體探測）。
    /// </summary>
    FFprobe = 1 << 3,

    /// <summary>
    /// 全部附帶工具（<see cref="YtDlp"/> + <see cref="Deno"/> + <see cref="FFmpeg"/> + <see cref="FFprobe"/>）。
    /// </summary>
    All = YtDlp | Deno | FFmpeg | FFprobe
}

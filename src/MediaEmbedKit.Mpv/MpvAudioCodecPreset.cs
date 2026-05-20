namespace MediaEmbedKit.Mpv;

/// <summary>
/// 提供 mpv encoding mode 常用音訊編碼器的預設值。
/// 涵蓋 2026-05 shinchiro / zhongfly Windows x64 build 實際內建的 FFmpeg 編碼器；
/// <c>libfdk_aac</c> 因 GPL 建置版 未編入而不列入，需要時請改用內建 <c>aac</c>。
/// </summary>
public enum MpvAudioCodecPreset
{
    /// <summary>
    /// FFmpeg 內建 AAC — <c>aac</c>，FFmpeg 5+ 之後已可滿足多數用途。
    /// </summary>
    Aac = 0,

    /// <summary>
    /// Opus — <c>libopus</c>，2026 通用音訊壓縮首選。
    /// </summary>
    Opus = 1,

    /// <summary>
    /// MP3 — <c>libmp3lame</c>，向後相容傳統播放器時使用。
    /// </summary>
    Mp3 = 2,

    /// <summary>
    /// Stream copy（不重新編碼）— <c>copy</c>。輸出格式必須能容納來源音訊串流的編碼格式。
    /// </summary>
    Copy = 100
}

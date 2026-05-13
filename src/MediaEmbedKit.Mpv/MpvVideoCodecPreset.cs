namespace MediaEmbedKit.Mpv;

/// <summary>
/// 提供 mpv encoding mode 常用視訊編碼器的預設值。
/// 涵蓋 2026-05 shinchiro / zhongfly Windows x64 build 實際內建的 FFmpeg 編碼器；
/// <c>librav1e</c> 因 build 未編入而不列入。硬體加速 <c>*_nvenc</c> / <c>*_qsv</c> /
/// <c>*_amf</c> 需對應驅動與硬體支援。
/// </summary>
public enum MpvVideoCodecPreset
{
    /// <summary>
    /// 軟體 H.264 — <c>libx264</c>。
    /// </summary>
    H264 = 0,

    /// <summary>
    /// NVIDIA 硬體 H.264 — <c>h264_nvenc</c>。
    /// </summary>
    H264Nvenc = 1,

    /// <summary>
    /// Intel Quick Sync 硬體 H.264 — <c>h264_qsv</c>。
    /// </summary>
    H264Qsv = 2,

    /// <summary>
    /// AMD AMF 硬體 H.264 — <c>h264_amf</c>。
    /// </summary>
    H264Amf = 3,

    /// <summary>
    /// 軟體 H.265 / HEVC — <c>libx265</c>。
    /// </summary>
    H265 = 10,

    /// <summary>
    /// NVIDIA 硬體 H.265 — <c>hevc_nvenc</c>。
    /// </summary>
    H265Nvenc = 11,

    /// <summary>
    /// Intel Quick Sync 硬體 H.265 — <c>hevc_qsv</c>。
    /// </summary>
    H265Qsv = 12,

    /// <summary>
    /// AMD AMF 硬體 H.265 — <c>hevc_amf</c>。
    /// </summary>
    H265Amf = 13,

    /// <summary>
    /// 軟體 VP9 — <c>libvpx-vp9</c>。
    /// </summary>
    Vp9 = 20,

    /// <summary>
    /// 軟體 AV1（2026 預設） — <c>libsvtav1</c>，速度與品質的最佳折衷。
    /// </summary>
    Av1 = 30,

    /// <summary>
    /// 軟體 AV1 參考編碼器 — <c>libaom-av1</c>，適合追求最佳壓縮率的離線編碼。
    /// </summary>
    Av1Aom = 31,

    /// <summary>
    /// NVIDIA 硬體 AV1 — <c>av1_nvenc</c>（Ada Lovelace 起）。
    /// </summary>
    Av1Nvenc = 32,

    /// <summary>
    /// Intel 硬體 AV1 — <c>av1_qsv</c>（Arc 系列起）。
    /// </summary>
    Av1Qsv = 33,

    /// <summary>
    /// AMD 硬體 AV1 — <c>av1_amf</c>（RDNA3 起）。
    /// </summary>
    Av1Amf = 34,

    /// <summary>
    /// Stream copy（不重新編碼）— <c>copy</c>。輸出格式必須能容納來源視訊串流的編碼格式。
    /// </summary>
    Copy = 100
}

namespace MediaEmbedKit.Mpv;

/// <summary>
/// 定義 mpv 記憶體截圖像素格式。
/// </summary>
public enum MpvScreenshotFormat
{
    /// <summary>
    /// 使用 B8G8R8X8 位元組排列。
    /// </summary>
    Bgr0 = 0,
    /// <summary>
    /// 使用 B8G8R8A8 位元組排列。
    /// </summary>
    Bgra = 1,
    /// <summary>
    /// 使用 R8G8B8A8 位元組排列。
    /// </summary>
    Rgba = 2,
    /// <summary>
    /// 使用 R16G16B16A16 位元組排列。
    /// </summary>
    Rgba64 = 3
}

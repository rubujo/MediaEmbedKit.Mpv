namespace MediaEmbedKit.Mpv.Render;

/// <summary>
/// 表示 OpenGL DRM 繪圖表面的像素大小。
/// </summary>
public sealed class MpvOpenGlDrmDrawSurfaceSize
{
    /// <summary>
    /// 初始化 <see cref="MpvOpenGlDrmDrawSurfaceSize"/> 類別的新執行個體。
    /// </summary>
    /// <param name="width">
    /// DRM 繪圖表面寬度。
    /// </param>
    /// <param name="height">
    /// DRM 繪圖表面高度。
    /// </param>
    public MpvOpenGlDrmDrawSurfaceSize(int width, int height)
    {
        Width = width;
        Height = height;
    }

    /// <summary>
    /// 取得 DRM 繪圖表面寬度。
    /// </summary>
    /// <value>
    /// DRM 繪圖表面寬度。
    /// </value>
    public int Width { get; private set; }

    /// <summary>
    /// 取得 DRM 繪圖表面高度。
    /// </summary>
    /// <value>
    /// DRM 繪圖表面高度。
    /// </value>
    public int Height { get; private set; }
}

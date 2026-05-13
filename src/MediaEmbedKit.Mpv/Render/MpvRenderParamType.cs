namespace MediaEmbedKit.Mpv.Render;

/// <summary>
/// 定義 libmpv render API 參數型別。
/// </summary>
public enum MpvRenderParamType
{
    /// <summary>
    /// 表示參數陣列結尾或無效參數。
    /// </summary>
    Invalid = 0,
    /// <summary>
    /// 指定 render API 後端名稱。
    /// </summary>
    ApiType = 1,
    /// <summary>
    /// 指定 OpenGL 初始化參數。
    /// </summary>
    OpenGlInitParams = 2,
    /// <summary>
    /// 指定 OpenGL framebuffer 目標。
    /// </summary>
    OpenGlFbo = 3,
    /// <summary>
    /// 指定是否垂直翻轉輸出。
    /// </summary>
    FlipY = 4,
    /// <summary>
    /// 指定 framebuffer 深度值。
    /// </summary>
    Depth = 5,
    /// <summary>
    /// 指定 ICC 色彩描述檔。
    /// </summary>
    IccProfile = 6,
    /// <summary>
    /// 指定環境光參數。
    /// </summary>
    AmbientLight = 7,
    /// <summary>
    /// 指定 X11 顯示連線。
    /// </summary>
    X11Display = 8,
    /// <summary>
    /// 指定 Wayland 顯示連線。
    /// </summary>
    WaylandDisplay = 9,
    /// <summary>
    /// 指定是否啟用進階控制。
    /// </summary>
    AdvancedControl = 10,
    /// <summary>
    /// 要求下一個影格資訊。
    /// </summary>
    NextFrameInfo = 11,
    /// <summary>
    /// 指定 render 呼叫是否等待目標時間。
    /// </summary>
    BlockForTargetTime = 12,
    /// <summary>
    /// 指定是否略過實際繪製。
    /// </summary>
    SkipRendering = 13,
    /// <summary>
    /// 指定 DRM 顯示資訊。
    /// </summary>
    DrmDisplay = 14,
    /// <summary>
    /// 指定 DRM 繪圖表面大小。
    /// </summary>
    DrmDrawSurfaceSize = 15,
    /// <summary>
    /// 指定第二版 DRM 顯示資訊。
    /// </summary>
    DrmDisplayV2 = 16,
    /// <summary>
    /// 指定軟體 render 目標大小。
    /// </summary>
    SoftwareSize = 17,
    /// <summary>
    /// 指定軟體 render 像素格式。
    /// </summary>
    SoftwareFormat = 18,
    /// <summary>
    /// 指定軟體 render 緩衝區步幅。
    /// </summary>
    SoftwareStride = 19,
    /// <summary>
    /// 指定軟體 render 緩衝區指標。
    /// </summary>
    SoftwarePointer = 20
}

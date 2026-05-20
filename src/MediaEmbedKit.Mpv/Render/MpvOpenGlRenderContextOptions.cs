using System;

namespace MediaEmbedKit.Mpv.Render;

/// <summary>
/// 提供建立 libmpv OpenGL render API 內容時使用的選項。
/// </summary>
public sealed class MpvOpenGlRenderContextOptions
{
    /// <summary>
    /// 初始化 <see cref="MpvOpenGlRenderContextOptions"/> 類別的新執行個體。
    /// </summary>
    /// <param name="getProcAddress">
    /// 用來解析 OpenGL 函式位址的委派。
    /// </param>
    public MpvOpenGlRenderContextOptions(Func<string, IntPtr> getProcAddress)
    {
        GetProcAddress = getProcAddress ?? throw new ArgumentNullException(nameof(getProcAddress));
    }

    /// <summary>
    /// 取得用來解析 OpenGL 函式位址的委派。
    /// </summary>
    /// <value>
    /// OpenGL 函式位址解析委派。
    /// </value>
    public Func<string, IntPtr> GetProcAddress { get; private set; }

    /// <summary>
    /// 取得或設定要傳回 libmpv OpenGL 初始化回呼的內容指標。
    /// </summary>
    /// <value>
    /// OpenGL 函式位址解析內容指標。
    /// </value>
    public IntPtr GetProcAddressContext { get; set; }

    /// <summary>
    /// 取得或設定是否啟用 libmpv 進階 render API 控制。
    /// </summary>
    /// <value>
    /// 啟用進階控制時為 <see langword="true"/>。
    /// </value>
    public bool AdvancedControl { get; set; }

    /// <summary>
    /// 取得或設定 X11 顯示連線指標。
    /// </summary>
    /// <value>
    /// X11 Display 指標；非 X11 後端可使用 <see cref="IntPtr.Zero"/>。
    /// </value>
    public IntPtr X11Display { get; set; }

    /// <summary>
    /// 取得或設定 Wayland 顯示連線指標。
    /// </summary>
    /// <value>
    /// Wayland display 指標；非 Wayland 後端可使用 <see cref="IntPtr.Zero"/>。
    /// </value>
    public IntPtr WaylandDisplay { get; set; }

    /// <summary>
    /// 取得或設定舊版 DRM 顯示參數。
    /// </summary>
    /// <value>
    /// 舊版 DRM 顯示參數；不使用 DRM 時為 <see langword="null"/>。
    /// </value>
    public MpvOpenGlDrmDisplayOptions? DrmDisplay { get; set; }

    /// <summary>
    /// 取得或設定 DRM 繪圖表面大小。
    /// </summary>
    /// <value>
    /// DRM 繪圖表面大小；不使用 DRM 時為 <see langword="null"/>。
    /// </value>
    public MpvOpenGlDrmDrawSurfaceSize? DrmDrawSurfaceSize { get; set; }

    /// <summary>
    /// 取得或設定第二版 DRM 顯示參數。
    /// </summary>
    /// <value>
    /// 第二版 DRM 顯示參數；不使用 DRM 時為 <see langword="null"/>。
    /// </value>
    public MpvOpenGlDrmDisplayOptions? DrmDisplayV2 { get; set; }
}

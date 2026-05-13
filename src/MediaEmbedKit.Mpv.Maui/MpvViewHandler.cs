using Microsoft.Maui.Handlers;
#if WINDOWS
using MediaEmbedKit.Mpv.WinUI;
using Microsoft.Maui.Platform;
using WinUiElement = Microsoft.UI.Xaml.UIElement;
#endif

namespace MediaEmbedKit.Mpv.Maui;

/// <summary>
/// 將 <see cref="MpvView"/> 對應到 Windows 平台的 WinUI AirSpace 安全 mpv 控制項。
/// </summary>
#if WINDOWS
public class MpvViewHandler : ViewHandler<MpvView, MpvWinUiPlayer>
#else
public class MpvViewHandler : ViewHandler<MpvView, object>
#endif
{
    /// <summary>
    /// 取得 MAUI 屬性對應表。
    /// </summary>
    /// <value>將 <see cref="MpvView"/> 屬性同步到平台控制項的對應表。</value>
    public static readonly IPropertyMapper<MpvView, MpvViewHandler> Mapper =
        new PropertyMapper<MpvView, MpvViewHandler>(ViewMapper)
        {
            [nameof(MpvView.Source)] = MapSource,
            [nameof(MpvView.OverlayView)] = MapOverlayView,
            [nameof(MpvView.OverlayContent)] = MapOverlayContent,
            [nameof(MpvView.IsOverlayOpen)] = MapIsOverlayOpen
        };

#if WINDOWS
    /// <summary>
    /// 保存目前由 <see cref="MpvView.OverlayView"/> 轉換的平台覆蓋層來源檢視。
    /// </summary>
    private Microsoft.Maui.Controls.View? _resolvedOverlayView;

    /// <summary>
    /// 保存目前由 <see cref="MpvView.OverlayView"/> 轉換出的 WinUI 覆蓋層元素。
    /// </summary>
    private WinUiElement? _resolvedOverlayPlatformView;

    /// <summary>
    /// 保存目前由 <see cref="MpvView.OverlayView"/> 轉換時使用的 MAUI handler。
    /// </summary>
    private Microsoft.Maui.IElementHandler? _resolvedOverlayHandler;

    /// <summary>
    /// 記錄目前覆蓋層 handler 是否由本 handler 建立並負責中斷連線。
    /// </summary>
    private bool _ownsResolvedOverlayHandler;
#endif

    /// <summary>
    /// 初始化 <see cref="MpvViewHandler"/> 類別的新執行個體。
    /// </summary>
    public MpvViewHandler()
        : base(Mapper)
    {
    }

    /// <summary>
    /// 透過平台控制項載入檔案或網址。
    /// </summary>
    /// <param name="pathOrUrl">要載入的檔案路徑或媒體網址。</param>
    /// <param name="mode">播放項目加入播放清單的方式。</param>
    public void LoadFile(string pathOrUrl, MpvLoadFileMode mode = MpvLoadFileMode.Replace)
    {
#if WINDOWS
        if (Microsoft.Maui.Controls.DesignMode.IsDesignModeEnabled)
        {
            return;
        }

        PlatformView.LoadFile(pathOrUrl, mode);
        VirtualView.SetPlayer(PlatformView.Player);
#else
        throw new PlatformNotSupportedException("此平台尚未提供 MAUI libmpv handler。");
#endif
    }

    /// <summary>
    /// 將 MAUI 覆蓋層內容同步到 Windows 平台控制項。
    /// </summary>
    public void UpdateOverlayContent()
    {
#if WINDOWS
        if (Microsoft.Maui.Controls.DesignMode.IsDesignModeEnabled)
        {
            return;
        }

        PlatformView.OverlayContent = ResolveOverlayContent();
        PlatformView.IsOverlayOpen = VirtualView.IsOverlayOpen;
#endif
    }

    /// <summary>
    /// 建立 Windows 平台使用的 WinUI AirSpace 安全 mpv 控制項。
    /// </summary>
    /// <returns>新建立的平台 mpv 控制項。</returns>
#if WINDOWS
    protected override MpvWinUiPlayer CreatePlatformView()
    {
        return new MpvWinUiPlayer();
    }
#else
    protected override object CreatePlatformView()
    {
        throw new PlatformNotSupportedException("此平台尚未提供 MAUI libmpv handler。");
    }
#endif

    /// <summary>
    /// 連接 MAUI 虛擬檢視與平台控制項。
    /// </summary>
    /// <param name="platformView">要連接的平台 mpv 控制項。</param>
#if WINDOWS
    protected override void ConnectHandler(MpvWinUiPlayer platformView)
#else
    protected override void ConnectHandler(object platformView)
#endif
    {
        base.ConnectHandler(platformView);
#if WINDOWS
        if (Microsoft.Maui.Controls.DesignMode.IsDesignModeEnabled)
        {
            return;
        }

        VirtualView.PlayerOptions.CopyTo(platformView.PlayerOptions);
        AttachPlatformWindow(platformView);
        UpdateOverlayContent();
        platformView.PlayerCreated += OnPlayerCreated;
        VirtualView.SetPlayer(platformView.Player);

        if (!string.IsNullOrWhiteSpace(VirtualView.PendingSource))
        {
            platformView.LoadFile(VirtualView.PendingSource!, VirtualView.PendingMode);
        }
#endif
    }

    /// <summary>
    /// 中斷 MAUI 虛擬檢視與平台控制項的連接。
    /// </summary>
    /// <param name="platformView">要中斷連接的平台 mpv 控制項。</param>
#if WINDOWS
    protected override void DisconnectHandler(MpvWinUiPlayer platformView)
#else
    protected override void DisconnectHandler(object platformView)
#endif
    {
#if WINDOWS
        platformView.PlayerCreated -= OnPlayerCreated;
        platformView.OverlayContent = null;
        ReleaseResolvedOverlayView();
        VirtualView.SetPlayer(null);
        platformView.Dispose();
#endif
        base.DisconnectHandler(platformView);
    }

    /// <summary>
    /// 在平台控制項建立播放器後同步虛擬檢視的播放器參考。
    /// </summary>
    /// <param name="sender">引發事件的物件。</param>
    /// <param name="e">事件資料。</param>
    private void OnPlayerCreated(object? sender, System.EventArgs e)
    {
#if WINDOWS
        VirtualView.SetPlayer(PlatformView.Player);
#endif
    }

    /// <summary>
    /// 將 <see cref="MpvView.Source"/> 屬性變更套用到平台控制項。
    /// </summary>
    /// <param name="handler">正在處理屬性對應的 MAUI handler。</param>
    /// <param name="view">來源屬性變更的 MAUI mpv 檢視。</param>
    private static void MapSource(MpvViewHandler handler, MpvView view)
    {
#if WINDOWS
        if (!string.IsNullOrWhiteSpace(view.Source))
        {
            handler.LoadFile(view.Source!);
        }
#endif
    }

    /// <summary>
    /// 將 <see cref="MpvView.OverlayView"/> 屬性變更套用到平台控制項。
    /// </summary>
    /// <param name="handler">正在處理屬性對應的 MAUI handler。</param>
    /// <param name="view">來源屬性變更的 MAUI mpv 檢視。</param>
    private static void MapOverlayView(MpvViewHandler handler, MpvView view)
    {
        handler.UpdateOverlayContent();
    }

    /// <summary>
    /// 將 <see cref="MpvView.OverlayContent"/> 屬性變更套用到平台控制項。
    /// </summary>
    /// <param name="handler">正在處理屬性對應的 MAUI handler。</param>
    /// <param name="view">來源屬性變更的 MAUI mpv 檢視。</param>
    private static void MapOverlayContent(MpvViewHandler handler, MpvView view)
    {
        handler.UpdateOverlayContent();
    }

    /// <summary>
    /// 將 <see cref="MpvView.IsOverlayOpen"/> 屬性變更套用到平台控制項。
    /// </summary>
    /// <param name="handler">正在處理屬性對應的 MAUI handler。</param>
    /// <param name="view">來源屬性變更的 MAUI mpv 檢視。</param>
    private static void MapIsOverlayOpen(MpvViewHandler handler, MpvView view)
    {
        handler.UpdateOverlayContent();
    }

#if WINDOWS
    /// <summary>
    /// 解析應交給 WinUI 控制項的覆蓋層內容。
    /// </summary>
    /// <returns>WinUI 覆蓋層元素；未設定時為 <see langword="null"/>。</returns>
    private WinUiElement? ResolveOverlayContent()
    {
        if (VirtualView.OverlayContent != null)
        {
            ReleaseResolvedOverlayView();
            return VirtualView.OverlayContent;
        }

        Microsoft.Maui.Controls.View? overlayView = VirtualView.OverlayView;
        if (overlayView == null || MauiContext == null)
        {
            ReleaseResolvedOverlayView();
            return null;
        }

        if (ReferenceEquals(_resolvedOverlayView, overlayView) && _resolvedOverlayPlatformView != null)
        {
            return _resolvedOverlayPlatformView;
        }

        ReleaseResolvedOverlayView();

        Microsoft.Maui.IElementHandler? existingHandler = overlayView.Handler;
        WinUiElement platformView = overlayView.ToPlatform(MauiContext);
        _resolvedOverlayView = overlayView;
        _resolvedOverlayPlatformView = platformView;
        _resolvedOverlayHandler = overlayView.Handler;
        _ownsResolvedOverlayHandler = existingHandler == null;
        return platformView;
    }

    /// <summary>
    /// 中斷並清除目前由 <see cref="MpvView.OverlayView"/> 轉換出的覆蓋層 handler。
    /// </summary>
    private void ReleaseResolvedOverlayView()
    {
        Microsoft.Maui.Controls.View? overlayView = _resolvedOverlayView;
        Microsoft.Maui.IElementHandler? overlayHandler = _resolvedOverlayHandler;
        bool shouldDisconnect = _ownsResolvedOverlayHandler
            && overlayView != null
            && overlayHandler != null
            && ReferenceEquals(overlayView.Handler, overlayHandler);

        _resolvedOverlayView = null;
        _resolvedOverlayPlatformView = null;
        _resolvedOverlayHandler = null;
        _ownsResolvedOverlayHandler = false;

        if (shouldDisconnect)
        {
            overlayHandler!.DisconnectHandler();
        }
    }

    /// <summary>
    /// 將 MAUI 視窗轉接到 WinUI 播放控制項。
    /// </summary>
    /// <param name="platformView">要附加視窗的 Windows 平台控制項。</param>
    private void AttachPlatformWindow(MpvWinUiPlayer platformView)
    {
        Microsoft.Maui.Controls.Window? mauiWindow = VirtualView.Window;
        if (mauiWindow != null && mauiWindow.Handler != null && mauiWindow.Handler.PlatformView is Microsoft.UI.Xaml.Window winUiWindow)
        {
            platformView.Attach(winUiWindow);
        }
    }

#endif
}

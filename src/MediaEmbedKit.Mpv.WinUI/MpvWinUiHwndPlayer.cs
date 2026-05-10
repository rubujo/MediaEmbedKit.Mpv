using System;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using WinRT.Interop;

namespace MediaEmbedKit.Mpv.WinUI
{
    /// <summary>
    /// 提供 WinUI 3 用的高效能 HWND libmpv 播放控制項。
    /// </summary>
    public sealed class MpvWinUiHwndPlayer : Grid, IDisposable
    {
        /// <summary>
        /// 識別 <see cref="OverlayContent"/> 相依性屬性。
        /// </summary>
        public static readonly DependencyProperty OverlayContentProperty = DependencyProperty.Register(
            nameof(OverlayContent),
            typeof(UIElement),
            typeof(MpvWinUiHwndPlayer),
            new PropertyMetadata(null, OverlayContentChanged));

        /// <summary>
        /// 識別 <see cref="IsOverlayOpen"/> 相依性屬性。
        /// </summary>
        public static readonly DependencyProperty IsOverlayOpenProperty = DependencyProperty.Register(
            nameof(IsOverlayOpen),
            typeof(bool),
            typeof(MpvWinUiHwndPlayer),
            new PropertyMetadata(true, OverlayOpenChanged));

        /// <summary>
        /// 控制項附加的 WinUI 視窗。
        /// </summary>
        private Window? _attachedWindow;
        /// <summary>
        /// 可容納原生子視窗的父視窗控制代碼。
        /// </summary>
        private IntPtr _parentHwnd;
        /// <summary>
        /// 傳給 libmpv 的原生子視窗控制代碼。
        /// </summary>
        private IntPtr _videoHwnd;
        /// <summary>
        /// 顯示 WinUI 覆蓋層內容的 XAML Island 來源。
        /// </summary>
        private DesktopWindowXamlSource? _overlaySource;
        /// <summary>
        /// 承載 WinUI 覆蓋層 XAML Island 的子視窗控制代碼。
        /// </summary>
        private IntPtr _overlayHostHwnd;
        /// <summary>
        /// 顯示 WinUI 覆蓋層內容的 XAML Island 視窗控制代碼。
        /// </summary>
        private IntPtr _overlayHwnd;
        /// <summary>
        /// 目前被暫時轉換 Margin 的覆蓋層元素。
        /// </summary>
        private FrameworkElement? _overlayMarginElement;
        /// <summary>
        /// 覆蓋層元素原本的 Margin。
        /// </summary>
        private Thickness _overlayOriginalMargin = new Thickness(0);
        /// <summary>
        /// 由覆蓋層元素 Margin 轉換出的原生子視窗位移。
        /// </summary>
        private Thickness _overlayWindowMargin = new Thickness(0);
        /// <summary>
        /// 目前控制項持有的 mpv 播放器執行個體。
        /// </summary>
        private MpvPlayer? _player;
        /// <summary>
        /// 等待播放器建立後載入的媒體來源。
        /// </summary>
        private string? _pendingSource;
        /// <summary>
        /// 等待播放器建立後套用的載入模式。
        /// </summary>
        private MpvLoadFileMode _pendingMode = MpvLoadFileMode.Replace;
        /// <summary>
        /// 最近一次套用到原生子視窗的 X 座標。
        /// </summary>
        private int _lastWindowX = int.MinValue;
        /// <summary>
        /// 最近一次套用到原生子視窗的 Y 座標。
        /// </summary>
        private int _lastWindowY = int.MinValue;
        /// <summary>
        /// 最近一次套用到原生子視窗的寬度。
        /// </summary>
        private int _lastWindowWidth = int.MinValue;
        /// <summary>
        /// 最近一次套用到原生子視窗的高度。
        /// </summary>
        private int _lastWindowHeight = int.MinValue;
        /// <summary>
        /// 表示目前是否已排入原生視窗邊界同步。
        /// </summary>
        private bool _boundsUpdateQueued;
        /// <summary>
        /// 表示目前控制項是否已釋放。
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 初始化 <see cref="MpvWinUiHwndPlayer"/> 類別的新執行個體。
        /// </summary>
        public MpvWinUiHwndPlayer()
        {
            PlayerOptions = new MpvPlayerOptions();
            Background = new SolidColorBrush(Microsoft.UI.Colors.Black);
            if (MpvWinUiDesignMode.IsEnabled)
            {
                Children.Add(MpvWinUiDesignMode.CreatePlaceholder("MediaEmbedKit.Mpv HWND"));
            }

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            SizeChanged += OnSizeChanged;
            LayoutUpdated += OnLayoutUpdated;
        }

        /// <summary>
        /// 在控制項建立 mpv 播放器後發生。
        /// </summary>
        public event EventHandler? PlayerCreated;

        /// <summary>
        /// 取得控制項建立播放器時使用的選項。
        /// </summary>
        /// <value>播放器建立選項。</value>
        public MpvPlayerOptions PlayerOptions { get; private set; }

        /// <summary>
        /// 取得控制項目前建立的播放器。
        /// </summary>
        /// <value>目前播放器；尚未建立時為 <see langword="null"/>。</value>
        public MpvPlayer? Player
        {
            get { return _player; }
        }

        /// <summary>
        /// 取得傳給 libmpv 的原生子視窗控制代碼。
        /// </summary>
        /// <value>原生子視窗尚未建立時為 <see cref="IntPtr.Zero"/>。</value>
        public IntPtr VideoWindowHandle
        {
            get { return _videoHwnd; }
        }

        /// <summary>
        /// 取得目前是否已附加父視窗控制代碼。
        /// </summary>
        /// <value>父視窗控制代碼已設定時為 <see langword="true"/>。</value>
        public bool IsAttached
        {
            get { return _parentHwnd != IntPtr.Zero; }
        }

        /// <summary>
        /// 取得或設定要由控制項自行放入 AirSpace 覆蓋層的 WinUI 內容。
        /// </summary>
        /// <value>顯示在影片上方的 WinUI 元素；未設定時為 <see langword="null"/>。</value>
        public UIElement? OverlayContent
        {
            get { return (UIElement?)GetValue(OverlayContentProperty); }
            set { SetValue(OverlayContentProperty, value); }
        }

        /// <summary>
        /// 取得或設定控制項管理的 AirSpace 覆蓋層是否開啟。
        /// </summary>
        /// <value>覆蓋層應保持開啟時為 <see langword="true"/>。</value>
        public bool IsOverlayOpen
        {
            get { return (bool)GetValue(IsOverlayOpenProperty); }
            set { SetValue(IsOverlayOpenProperty, value); }
        }

        /// <summary>
        /// 取得控制項是否已建立覆蓋層用的原生子視窗。
        /// </summary>
        /// <value>覆蓋層子視窗已建立時為 <see langword="true"/>。</value>
        public bool IsOverlayHostCreated
        {
            get { return _overlayHwnd != IntPtr.Zero; }
        }

        /// <summary>
        /// 附加 WinUI 視窗，讓控制項可以建立原生子視窗。
        /// </summary>
        /// <param name="window">要附加的 WinUI 視窗。</param>
        public void Attach(Window window)
        {
            if (window == null)
            {
                throw new ArgumentNullException(nameof(window));
            }

            DetachWindowClosedHandler();
            _attachedWindow = window;
            _attachedWindow.Closed += AttachedWindowClosed;
            Attach(WindowNative.GetWindowHandle(window));
        }

        /// <summary>
        /// 附加父視窗控制代碼，讓控制項可以建立原生子視窗。
        /// </summary>
        /// <param name="parentHwnd">可容納原生子視窗的父視窗控制代碼。</param>
        public void Attach(IntPtr parentHwnd)
        {
            EnsureNotDisposed();
            if (parentHwnd == IntPtr.Zero)
            {
                throw new ArgumentException("父視窗控制代碼不可為零。", nameof(parentHwnd));
            }

            if (_parentHwnd == parentHwnd)
            {
                return;
            }

            ReleasePlayer();
            DestroyOverlayWindow();
            DestroyVideoWindow();
            _parentHwnd = parentHwnd;
            EnsureVideoWindow();
            EnsurePlayer();
        }

        /// <summary>
        /// 載入檔案或網址作為播放項目。
        /// </summary>
        /// <param name="pathOrUrl">要載入的檔案路徑或媒體網址。</param>
        /// <param name="mode">播放項目加入播放清單的方式。</param>
        public void LoadFile(string pathOrUrl, MpvLoadFileMode mode = MpvLoadFileMode.Replace)
        {
            if (string.IsNullOrWhiteSpace(pathOrUrl))
            {
                throw new ArgumentException("媒體來源不可為空白。", nameof(pathOrUrl));
            }

            _pendingSource = pathOrUrl;
            _pendingMode = mode;
            if (MpvWinUiDesignMode.IsEnabled)
            {
                return;
            }

            EnsurePlayer();

            if (_player != null && _player.IsInitialized)
            {
                _player.LoadFile(pathOrUrl, mode);
            }
        }

        /// <summary>
        /// 釋放控制項持有的播放器與原生子視窗。
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Loaded -= OnLoaded;
            Unloaded -= OnUnloaded;
            SizeChanged -= OnSizeChanged;
            LayoutUpdated -= OnLayoutUpdated;
            DetachWindowClosedHandler();
            ReleasePlayer();
            DestroyOverlayWindow();
            DestroyVideoWindow();
        }

        /// <summary>
        /// 在控制項載入後建立原生子視窗與播放器。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (MpvWinUiDesignMode.IsEnabled)
            {
                return;
            }

            EnsureVideoWindow();
            EnsurePlayer();
            EnsureOverlayWindow();
        }

        /// <summary>
        /// 在控制項卸載時釋放原生子視窗與播放器。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            ReleasePlayer();
            DestroyOverlayWindow();
            DestroyVideoWindow();
        }

        /// <summary>
        /// 在控制項大小變更時同步原生子視窗位置與大小。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">大小變更事件資料。</param>
        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            ScheduleWindowBoundsUpdate();
        }

        /// <summary>
        /// 在版面配置更新時同步原生子視窗位置與大小。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void OnLayoutUpdated(object? sender, object e)
        {
            ScheduleWindowBoundsUpdate();
        }

        /// <summary>
        /// 在附加的 WinUI 視窗關閉時釋放控制項資源。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="args">視窗關閉事件資料。</param>
        private void AttachedWindowClosed(object sender, WindowEventArgs args)
        {
            Dispose();
        }

        /// <summary>
        /// 確保原生子視窗已建立。
        /// </summary>
        private void EnsureVideoWindow()
        {
            if (_disposed || MpvWinUiDesignMode.IsEnabled || _videoHwnd != IntPtr.Zero || _parentHwnd == IntPtr.Zero || !IsLoaded)
            {
                return;
            }

            _videoHwnd = NativeMethods.CreateWindowEx(
                0,
                "STATIC",
                string.Empty,
                NativeMethods.WS_CHILD | NativeMethods.WS_VISIBLE | NativeMethods.WS_CLIPSIBLINGS | NativeMethods.WS_CLIPCHILDREN,
                0,
                0,
                1,
                1,
                _parentHwnd,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero);

            if (_videoHwnd == IntPtr.Zero)
            {
                throw new InvalidOperationException("無法建立提供 libmpv 使用的 WinUI 原生子視窗。");
            }

            UpdateVideoWindowBounds();
            EnsureOverlayWindow();
        }

        /// <summary>
        /// 確保播放器已建立並指向原生子視窗。
        /// </summary>
        private void EnsurePlayer()
        {
            if (_disposed || MpvWinUiDesignMode.IsEnabled || _player != null)
            {
                return;
            }

            EnsureVideoWindow();
            if (_videoHwnd == IntPtr.Zero)
            {
                return;
            }

            _player = new MpvPlayer(PlayerOptions);
            _player.SetVideoWindow(_videoHwnd);
            _player.Initialize();
            PlayerCreated?.Invoke(this, EventArgs.Empty);

            if (!string.IsNullOrWhiteSpace(_pendingSource))
            {
                _player.LoadFile(_pendingSource!, _pendingMode);
            }
        }

        /// <summary>
        /// 將 XAML 版面位置同步到原生子視窗。
        /// </summary>
        private void UpdateVideoWindowBounds()
        {
            if (_videoHwnd == IntPtr.Zero || XamlRoot == null)
            {
                return;
            }

            Point origin;
            try
            {
                GeneralTransform transform = TransformToVisual(null);
                origin = transform.TransformPoint(new Point(0, 0));
            }
            catch (InvalidOperationException)
            {
                return;
            }

            double scale = XamlRoot.RasterizationScale;
            int x = (int)Math.Round(origin.X * scale);
            int y = (int)Math.Round(origin.Y * scale);
            int width = Math.Max(1, (int)Math.Round(ActualWidth * scale));
            int height = Math.Max(1, (int)Math.Round(ActualHeight * scale));

            if (x == _lastWindowX && y == _lastWindowY && width == _lastWindowWidth && height == _lastWindowHeight)
            {
                return;
            }

            NativeMethods.SetWindowPos(
                _videoHwnd,
                NativeMethods.HWND_TOP,
                x,
                y,
                width,
                height,
                NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);

            _lastWindowX = x;
            _lastWindowY = y;
            _lastWindowWidth = width;
            _lastWindowHeight = height;
            UpdateOverlayWindowBounds();
        }

        /// <summary>
        /// 合併排程原生子視窗邊界同步，避免版面配置與拖曳期間過度更新。
        /// </summary>
        private void ScheduleWindowBoundsUpdate()
        {
            if (_disposed || _boundsUpdateQueued)
            {
                return;
            }

            _boundsUpdateQueued = true;
            if (!DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, ProcessQueuedWindowBoundsUpdate))
            {
                _boundsUpdateQueued = false;
                UpdateVideoWindowBounds();
            }
        }

        /// <summary>
        /// 執行已排程的原生子視窗邊界同步。
        /// </summary>
        private void ProcessQueuedWindowBoundsUpdate()
        {
            _boundsUpdateQueued = false;
            UpdateVideoWindowBounds();
        }

        /// <summary>
        /// 在覆蓋層內容變更時同步 XAML Island 內容。
        /// </summary>
        /// <param name="dependencyObject">相依性屬性所屬物件。</param>
        /// <param name="e">相依性屬性變更資料。</param>
        private static void OverlayContentChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            MpvWinUiHwndPlayer player = (MpvWinUiHwndPlayer)dependencyObject;
            player.ApplyOverlayContent(e.NewValue as UIElement);
        }

        /// <summary>
        /// 在覆蓋層開啟狀態變更時同步原生子視窗顯示狀態。
        /// </summary>
        /// <param name="dependencyObject">相依性屬性所屬物件。</param>
        /// <param name="e">相依性屬性變更資料。</param>
        private static void OverlayOpenChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            MpvWinUiHwndPlayer player = (MpvWinUiHwndPlayer)dependencyObject;
            player.UpdateOverlayWindowBounds();
        }

        /// <summary>
        /// 套用要顯示在影片上方的 WinUI 內容。
        /// </summary>
        /// <param name="content">新的 WinUI 覆蓋層內容。</param>
        private void ApplyOverlayContent(UIElement? content)
        {
            RestoreOverlayContentMargin();
            if (content == null)
            {
                DestroyOverlayWindow();
                return;
            }

            CaptureOverlayContentMargin(content);
            EnsureOverlayWindow();
            if (_overlaySource != null)
            {
                _overlaySource.Content = content;
                UpdateOverlayWindowBounds();
            }
        }

        /// <summary>
        /// 確保覆蓋層 XAML Island 子視窗已建立。
        /// </summary>
        private void EnsureOverlayWindow()
        {
            if (_disposed || MpvWinUiDesignMode.IsEnabled || _overlaySource != null || OverlayContent == null || _parentHwnd == IntPtr.Zero || !IsLoaded)
            {
                return;
            }

            CaptureOverlayContentMargin(OverlayContent);
            _overlaySource = new DesktopWindowXamlSource();
            _overlayHostHwnd = NativeMethods.CreateWindowEx(
                0,
                "STATIC",
                string.Empty,
                NativeMethods.WS_CHILD | NativeMethods.WS_VISIBLE | NativeMethods.WS_CLIPSIBLINGS | NativeMethods.WS_CLIPCHILDREN,
                0,
                0,
                1,
                1,
                _parentHwnd,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero);

            if (_overlayHostHwnd == IntPtr.Zero)
            {
                _overlaySource.Dispose();
                _overlaySource = null;
                throw new InvalidOperationException("無法建立 WinUI 覆蓋層子視窗。");
            }

            WindowId overlayWindowId = Win32Interop.GetWindowIdFromWindow(_overlayHostHwnd);
            _overlaySource.Initialize(overlayWindowId);
            _overlaySource.Content = OverlayContent;
            _overlayHwnd = Win32Interop.GetWindowFromWindowId(_overlaySource.SiteBridge.WindowId);
            UpdateOverlayWindowBounds();
        }

        /// <summary>
        /// 將覆蓋層原生子視窗同步到視訊子視窗的位置與大小。
        /// </summary>
        private void UpdateOverlayWindowBounds()
        {
            if (_overlayHostHwnd == IntPtr.Zero || _overlayHwnd == IntPtr.Zero)
            {
                EnsureOverlayWindow();
                return;
            }

            int x = _lastWindowX == int.MinValue ? 0 : _lastWindowX;
            int y = _lastWindowY == int.MinValue ? 0 : _lastWindowY;
            int width = _lastWindowWidth <= 0 ? 1 : _lastWindowWidth;
            int height = _lastWindowHeight <= 0 ? 1 : _lastWindowHeight;
            double scale = XamlRoot == null ? 1 : XamlRoot.RasterizationScale;
            int leftMargin = ConvertDeviceIndependentPixel(_overlayWindowMargin.Left, scale);
            int topMargin = ConvertDeviceIndependentPixel(_overlayWindowMargin.Top, scale);
            int rightMargin = ConvertDeviceIndependentPixel(_overlayWindowMargin.Right, scale);
            int bottomMargin = ConvertDeviceIndependentPixel(_overlayWindowMargin.Bottom, scale);
            int overlayMaximumWidth = Math.Max(1, width - Math.Max(0, leftMargin) - Math.Max(0, rightMargin));
            int overlayMaximumHeight = Math.Max(1, height - Math.Max(0, topMargin) - Math.Max(0, bottomMargin));
            ResolveOverlayContentSize(overlayMaximumWidth, overlayMaximumHeight, out int overlayWidth, out int overlayHeight);
            int flags = NativeMethods.SWP_NOACTIVATE;
            if (IsOverlayOpen && OverlayContent != null && IsLoaded)
            {
                flags |= NativeMethods.SWP_SHOWWINDOW;
            }
            else
            {
                flags |= NativeMethods.SWP_HIDEWINDOW;
            }

            bool hostMoved = NativeMethods.SetWindowPos(
                _overlayHostHwnd,
                NativeMethods.HWND_TOP,
                x + leftMargin,
                y + topMargin,
                overlayWidth,
                overlayHeight,
                flags);

            if (hostMoved)
            {
                NativeMethods.SetWindowPos(
                    _overlayHwnd,
                    NativeMethods.HWND_TOP,
                    0,
                    0,
                    overlayWidth,
                    overlayHeight,
                    NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
            }
        }

        /// <summary>
        /// 將最上層覆蓋層元素的 Margin 轉成原生子視窗位移，避免 XAML Island 透明留白顯示成黑底。
        /// </summary>
        /// <param name="content">覆蓋層內容。</param>
        private void CaptureOverlayContentMargin(UIElement content)
        {
            FrameworkElement? element = content as FrameworkElement;
            if (element == null)
            {
                _overlayWindowMargin = new Thickness(0);
                return;
            }

            if (_overlayMarginElement == element)
            {
                _overlayWindowMargin = _overlayOriginalMargin;
                return;
            }

            RestoreOverlayContentMargin();
            Thickness margin = element.Margin;
            _overlayWindowMargin = margin;
            if (margin.Left == 0 && margin.Top == 0 && margin.Right == 0 && margin.Bottom == 0)
            {
                return;
            }

            _overlayMarginElement = element;
            _overlayOriginalMargin = margin;
            element.Margin = new Thickness(0);
        }

        /// <summary>
        /// 還原先前為了原生子視窗定位而暫時清除的覆蓋層 Margin。
        /// </summary>
        private void RestoreOverlayContentMargin()
        {
            if (_overlayMarginElement != null)
            {
                _overlayMarginElement.Margin = _overlayOriginalMargin;
                _overlayMarginElement = null;
            }

            _overlayOriginalMargin = new Thickness(0);
            _overlayWindowMargin = new Thickness(0);
        }

        /// <summary>
        /// 將裝置獨立像素轉成實際像素。
        /// </summary>
        /// <param name="value">裝置獨立像素值。</param>
        /// <param name="scale">目前 XAML rasterization scale。</param>
        /// <returns>四捨五入後的實際像素值。</returns>
        private static int ConvertDeviceIndependentPixel(double value, double scale)
        {
            return (int)Math.Round(value * scale);
        }

        /// <summary>
        /// 解析覆蓋層內容所需的原生子視窗大小。
        /// </summary>
        /// <param name="maximumWidth">覆蓋層可用的最大寬度。</param>
        /// <param name="maximumHeight">覆蓋層可用的最大高度。</param>
        /// <param name="width">解析後的覆蓋層寬度。</param>
        /// <param name="height">解析後的覆蓋層高度。</param>
        private void ResolveOverlayContentSize(int maximumWidth, int maximumHeight, out int width, out int height)
        {
            width = maximumWidth;
            height = maximumHeight;
            if (OverlayContent is not FrameworkElement element || XamlRoot == null)
            {
                return;
            }

            double scale = XamlRoot.RasterizationScale;
            Size availableSize = new Size(maximumWidth / scale, maximumHeight / scale);
            element.Measure(availableSize);
            double desiredWidth = element.DesiredSize.Width;
            double desiredHeight = element.DesiredSize.Height;
            if (double.IsNaN(desiredWidth)
                || double.IsNaN(desiredHeight)
                || double.IsInfinity(desiredWidth)
                || double.IsInfinity(desiredHeight)
                || desiredWidth <= 0
                || desiredHeight <= 0)
            {
                return;
            }

            width = Math.Min(maximumWidth, Math.Max(1, (int)Math.Ceiling(desiredWidth * scale)));
            height = Math.Min(maximumHeight, Math.Max(1, (int)Math.Ceiling(desiredHeight * scale)));
        }

        /// <summary>
        /// 釋放目前播放器。
        /// </summary>
        private void ReleasePlayer()
        {
            if (_player == null)
            {
                return;
            }

            _player.Dispose();
            _player = null;
        }

        /// <summary>
        /// 銷毀目前原生子視窗。
        /// </summary>
        private void DestroyVideoWindow()
        {
            if (_videoHwnd != IntPtr.Zero)
            {
                NativeMethods.DestroyWindow(_videoHwnd);
            }

            _videoHwnd = IntPtr.Zero;
            _lastWindowX = int.MinValue;
            _lastWindowY = int.MinValue;
            _lastWindowWidth = int.MinValue;
            _lastWindowHeight = int.MinValue;
            _boundsUpdateQueued = false;
        }

        /// <summary>
        /// 銷毀目前覆蓋層 XAML Island 子視窗。
        /// </summary>
        private void DestroyOverlayWindow()
        {
            RestoreOverlayContentMargin();
            if (_overlaySource != null)
            {
                _overlaySource.Content = null;
                _overlaySource.Dispose();
                _overlaySource = null;
            }

            _overlayHwnd = IntPtr.Zero;
            if (_overlayHostHwnd != IntPtr.Zero)
            {
                NativeMethods.DestroyWindow(_overlayHostHwnd);
                _overlayHostHwnd = IntPtr.Zero;
            }
        }

        /// <summary>
        /// 移除附加視窗的關閉事件處理常式。
        /// </summary>
        private void DetachWindowClosedHandler()
        {
            if (_attachedWindow != null)
            {
                _attachedWindow.Closed -= AttachedWindowClosed;
                _attachedWindow = null;
            }
        }

        /// <summary>
        /// 確認目前控制項尚未釋放。
        /// </summary>
        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        /// <summary>
        /// 宣告 WinUI HWND 播放控制項使用的 Win32 API。
        /// </summary>
        private static class NativeMethods
        {
            /// <summary>
            /// 將視窗移到 Z 順序最上層的 SetWindowPos 參考值。
            /// </summary>
            internal static readonly IntPtr HWND_TOP = IntPtr.Zero;
            /// <summary>
            /// 建立子視窗的 Win32 樣式。
            /// </summary>
            internal const int WS_CHILD = 0x40000000;
            /// <summary>
            /// 顯示視窗的 Win32 樣式。
            /// </summary>
            internal const int WS_VISIBLE = 0x10000000;
            /// <summary>
            /// 裁切子視窗的 Win32 樣式。
            /// </summary>
            internal const int WS_CLIPCHILDREN = 0x02000000;
            /// <summary>
            /// 裁切同層視窗的 Win32 樣式。
            /// </summary>
            internal const int WS_CLIPSIBLINGS = 0x04000000;
            /// <summary>
            /// 保持目前 Z 順序的 SetWindowPos 旗標。
            /// </summary>
            internal const int SWP_NOZORDER = 0x0004;
            /// <summary>
            /// 避免啟用視窗的 SetWindowPos 旗標。
            /// </summary>
            internal const int SWP_NOACTIVATE = 0x0010;
            /// <summary>
            /// 顯示視窗的 SetWindowPos 旗標。
            /// </summary>
            internal const int SWP_SHOWWINDOW = 0x0040;
            /// <summary>
            /// 隱藏視窗的 SetWindowPos 旗標。
            /// </summary>
            internal const int SWP_HIDEWINDOW = 0x0080;
            /// <summary>
            /// 建立 Win32 視窗。
            /// </summary>
            /// <param name="dwExStyle">延伸視窗樣式。</param>
            /// <param name="lpClassName">視窗類別名稱。</param>
            /// <param name="lpWindowName">視窗名稱。</param>
            /// <param name="dwStyle">視窗樣式。</param>
            /// <param name="x">視窗左上角 X 座標。</param>
            /// <param name="y">視窗左上角 Y 座標。</param>
            /// <param name="nWidth">視窗寬度。</param>
            /// <param name="nHeight">視窗高度。</param>
            /// <param name="hWndParent">父視窗控制代碼。</param>
            /// <param name="hMenu">功能表控制代碼。</param>
            /// <param name="hInstance">執行個體控制代碼。</param>
            /// <param name="lpParam">建立參數指標。</param>
            /// <returns>新建立視窗的控制代碼。</returns>
            [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            internal static extern IntPtr CreateWindowEx(
                int dwExStyle,
                string lpClassName,
                string lpWindowName,
                int dwStyle,
                int x,
                int y,
                int nWidth,
                int nHeight,
                IntPtr hWndParent,
                IntPtr hMenu,
                IntPtr hInstance,
                IntPtr lpParam);

            /// <summary>
            /// 銷毀指定的 Win32 視窗。
            /// </summary>
            /// <param name="hwnd">要銷毀的視窗控制代碼。</param>
            /// <returns>作業成功時為 <see langword="true"/>。</returns>
            [DllImport("user32.dll", SetLastError = true)]
            internal static extern bool DestroyWindow(IntPtr hwnd);

            /// <summary>
            /// 設定指定 Win32 視窗的位置與大小。
            /// </summary>
            /// <param name="hwnd">要調整的視窗控制代碼。</param>
            /// <param name="hwndInsertAfter">Z 順序參考視窗控制代碼。</param>
            /// <param name="x">新的 X 座標。</param>
            /// <param name="y">新的 Y 座標。</param>
            /// <param name="cx">新的寬度。</param>
            /// <param name="cy">新的高度。</param>
            /// <param name="flags">SetWindowPos 旗標。</param>
            /// <returns>作業成功時為 <see langword="true"/>。</returns>
            [DllImport("user32.dll", SetLastError = true)]
            internal static extern bool SetWindowPos(
                IntPtr hwnd,
                IntPtr hwndInsertAfter,
                int x,
                int y,
                int cx,
                int cy,
                int flags);

        }
    }
}

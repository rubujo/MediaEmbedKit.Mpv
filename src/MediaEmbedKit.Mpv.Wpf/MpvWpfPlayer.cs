using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MediaEmbedKit.Mpv.Wpf
{
    /// <summary>
    /// 提供 WPF 用的 libmpv 播放主控項。
    /// </summary>
    [ToolboxItem(true)]
    [DesignTimeVisible(true)]
    [DefaultProperty(nameof(OverlayContent))]
    public class MpvWpfPlayer : HwndHost
    {
        /// <summary>
        /// WPF 主控項建立的原生子視窗控制代碼。
        /// </summary>
        private IntPtr _hwnd;
        /// <summary>
        /// 目前主控項持有的 mpv 播放器執行個體。
        /// </summary>
        private MpvPlayer? _player;
        /// <summary>
        /// 目前由控制項管理的 AirSpace 覆蓋層 Popup。
        /// </summary>
        private MpvAirspacePopup? _overlayPopup;

        /// <summary>
        /// 識別 <see cref="OverlayContent"/> 相依性屬性。
        /// </summary>
        public static readonly DependencyProperty OverlayContentProperty = DependencyProperty.Register(
            nameof(OverlayContent),
            typeof(UIElement),
            typeof(MpvWpfPlayer),
            new PropertyMetadata(null, OverlayContentChanged));

        /// <summary>
        /// 識別 <see cref="IsOverlayOpen"/> 相依性屬性。
        /// </summary>
        public static readonly DependencyProperty IsOverlayOpenProperty = DependencyProperty.Register(
            nameof(IsOverlayOpen),
            typeof(bool),
            typeof(MpvWpfPlayer),
            new PropertyMetadata(true, OverlayOpenChanged));

        /// <summary>
        /// 初始化 <see cref="MpvWpfPlayer"/> 類別的新執行個體。
        /// </summary>
        public MpvWpfPlayer()
        {
            PlayerOptions = new MpvPlayerOptions();
            Loaded += PlayerLoaded;
            Unloaded += PlayerUnloaded;
            IsVisibleChanged += PlayerIsVisibleChanged;
        }

        /// <summary>
        /// 在主控項建立 mpv 播放器後發生。
        /// </summary>
        public event EventHandler? PlayerCreated;

        /// <summary>
        /// 取得主控項建立播放器時使用的選項。
        /// </summary>
        /// <value>播放器建立選項。</value>
        public MpvPlayerOptions PlayerOptions { get; private set; }

        /// <summary>
        /// 取得主控項目前建立的播放器。
        /// </summary>
        /// <value>目前播放器；尚未建立時為 <see langword="null"/>。</value>
        public MpvPlayer? Player
        {
            get { return _player; }
        }

        /// <summary>
        /// 取得或設定要由控制項自行放入 AirSpace 覆蓋層的 WPF 內容。
        /// </summary>
        /// <value>顯示在影片上方的 WPF 元素；未設定時為 <see langword="null"/>。</value>
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
        /// 載入檔案或網址作為播放項目。
        /// </summary>
        /// <param name="pathOrUrl">要載入的檔案路徑或媒體網址。</param>
        /// <param name="mode">播放項目加入播放清單的方式。</param>
        public void LoadFile(string pathOrUrl, MpvLoadFileMode mode = MpvLoadFileMode.Replace)
        {
            if (IsInDesignMode())
            {
                return;
            }

            EnsurePlayer();
            MpvPlayer? player = _player;
            if (player == null)
            {
                throw new InvalidOperationException("播放器尚未建立。");
            }

            player.LoadFile(pathOrUrl, mode);
        }

        /// <summary>
        /// 建立可傳給 libmpv 的原生子視窗。
        /// </summary>
        /// <param name="hwndParent">WPF 提供的父視窗控制代碼。</param>
        /// <returns>新建立的原生子視窗控制代碼包裝。</returns>
        protected override HandleRef BuildWindowCore(HandleRef hwndParent)
        {
            bool isInDesignMode = IsInDesignMode();
            int windowStyle = NativeMethods.WS_CHILD | NativeMethods.WS_VISIBLE | NativeMethods.WS_CLIPSIBLINGS | NativeMethods.WS_CLIPCHILDREN;
            if (isInDesignMode)
            {
                windowStyle |= NativeMethods.SS_CENTER | NativeMethods.SS_CENTERIMAGE;
            }

            _hwnd = NativeMethods.CreateWindowEx(
                0,
                "STATIC",
                isInDesignMode ? "MediaEmbedKit.Mpv" : string.Empty,
                windowStyle,
                0,
                0,
                Math.Max(1, (int)ActualWidth),
                Math.Max(1, (int)ActualHeight),
                hwndParent.Handle,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero);

            if (_hwnd == IntPtr.Zero)
            {
                throw new InvalidOperationException("Unable to create the native child window used by libmpv.");
            }

            if (!isInDesignMode)
            {
                try
                {
                    EnsurePlayer();
                }
                catch
                {
                    NativeMethods.DestroyWindow(_hwnd);
                    _hwnd = IntPtr.Zero;
                    throw;
                }
            }

            return new HandleRef(this, _hwnd);
        }

        /// <summary>
        /// 銷毀先前建立的原生子視窗。
        /// </summary>
        /// <param name="hwnd">要銷毀的原生子視窗控制代碼包裝。</param>
        protected override void DestroyWindowCore(HandleRef hwnd)
        {
            DisposePlayer();

            if (hwnd.Handle != IntPtr.Zero)
            {
                NativeMethods.DestroyWindow(hwnd.Handle);
            }

            _hwnd = IntPtr.Zero;
        }

        /// <summary>
        /// 在 WPF 版面配置尺寸變更時同步更新原生子視窗大小。
        /// </summary>
        /// <param name="sizeInfo">WPF 提供的大小變更資訊。</param>
        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            if (_hwnd != IntPtr.Zero)
            {
                NativeMethods.SetWindowPos(
                    _hwnd,
                    IntPtr.Zero,
                    0,
                    0,
                    Math.Max(1, (int)ActualWidth),
                    Math.Max(1, (int)ActualHeight),
                    NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
            }

            _overlayPopup?.UpdateBounds();
        }

        /// <summary>
        /// 釋放主控項使用的 mpv 播放器資源。
        /// </summary>
        /// <param name="disposing">由受控程式碼釋放時為 <see langword="true"/>。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Loaded -= PlayerLoaded;
                Unloaded -= PlayerUnloaded;
                IsVisibleChanged -= PlayerIsVisibleChanged;
                DisposeOverlayPopup();
                DisposePlayer();
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// 在覆蓋層內容相依性屬性變更時重建 Popup。
        /// </summary>
        /// <param name="dependencyObject">屬性所屬的相依性物件。</param>
        /// <param name="e">相依性屬性變更資料。</param>
        private static void OverlayContentChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            MpvWpfPlayer player = (MpvWpfPlayer)dependencyObject;
            player.ReplaceOverlayContent(e.NewValue as UIElement);
        }

        /// <summary>
        /// 在覆蓋層開啟狀態變更時同步 Popup。
        /// </summary>
        /// <param name="dependencyObject">屬性所屬的相依性物件。</param>
        /// <param name="e">相依性屬性變更資料。</param>
        private static void OverlayOpenChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            MpvWpfPlayer player = (MpvWpfPlayer)dependencyObject;
            player.ApplyOverlayState();
        }

        /// <summary>
        /// 在控制項載入時開啟需要顯示的 AirSpace 覆蓋層。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void PlayerLoaded(object sender, RoutedEventArgs e)
        {
            ApplyOverlayState();
        }

        /// <summary>
        /// 在控制項卸載時關閉 AirSpace 覆蓋層。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void PlayerUnloaded(object sender, RoutedEventArgs e)
        {
            if (_overlayPopup != null)
            {
                _overlayPopup.IsOpen = false;
            }
        }

        /// <summary>
        /// 在控制項可見度變更時同步 AirSpace 覆蓋層。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">相依性屬性變更資料。</param>
        private void PlayerIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            ApplyOverlayState();
        }

        /// <summary>
        /// 使用新的 WPF 元素重建控制項內建 AirSpace 覆蓋層。
        /// </summary>
        /// <param name="content">要顯示在影片上方的 WPF 元素。</param>
        private void ReplaceOverlayContent(UIElement? content)
        {
            DisposeOverlayPopup();
            if (content != null)
            {
                _overlayPopup = new MpvAirspacePopup(this, content);
            }

            ApplyOverlayState();
        }

        /// <summary>
        /// 依目前控制項狀態套用 AirSpace 覆蓋層開啟狀態。
        /// </summary>
        private void ApplyOverlayState()
        {
            if (_overlayPopup == null)
            {
                return;
            }

            _overlayPopup.IsOpen = IsOverlayOpen && IsLoaded && IsVisible;
        }

        /// <summary>
        /// 釋放控制項內建的 AirSpace 覆蓋層。
        /// </summary>
        private void DisposeOverlayPopup()
        {
            if (_overlayPopup == null)
            {
                return;
            }

            _overlayPopup.Dispose();
            _overlayPopup = null;
        }

        /// <summary>
        /// 確保主控項已建立並初始化 mpv 播放器。
        /// </summary>
        private void EnsurePlayer()
        {
            if (IsInDesignMode())
            {
                return;
            }

            if (_player != null)
            {
                return;
            }

            if (_hwnd == IntPtr.Zero)
            {
                throw new InvalidOperationException("The WPF host window has not been created yet.");
            }

            MpvPlayer player = new MpvPlayer(PlayerOptions);
            try
            {
                player.SetVideoWindow(_hwnd);
                player.Initialize();
                _player = player;
            }
            catch
            {
                player.Dispose();
                throw;
            }

            PlayerCreated?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 判斷主控項目前是否在 XAML 設計工具中執行。
        /// </summary>
        /// <returns>主控項位於設計階段時為 <see langword="true"/>。</returns>
        private bool IsInDesignMode()
        {
            return DesignerProperties.GetIsInDesignMode(this);
        }

        /// <summary>
        /// 釋放目前主控項持有的 mpv 播放器。
        /// </summary>
        private void DisposePlayer()
        {
            if (_player == null)
            {
                return;
            }

            _player.Dispose();
            _player = null;
        }

        /// <summary>
        /// 宣告 WPF HwndHost 控制項使用的 Win32 API。
        /// </summary>
        private static class NativeMethods
        {
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
            /// 讓設計階段替代文字水平置中的 STATIC 樣式。
            /// </summary>
            internal const int SS_CENTER = 0x00000001;
            /// <summary>
            /// 讓設計階段替代文字垂直置中的 STATIC 樣式。
            /// </summary>
            internal const int SS_CENTERIMAGE = 0x00000200;
            /// <summary>
            /// 保持目前 Z 順序的 SetWindowPos 旗標。
            /// </summary>
            internal const int SWP_NOZORDER = 0x0004;
            /// <summary>
            /// 避免啟用視窗的 SetWindowPos 旗標。
            /// </summary>
            internal const int SWP_NOACTIVATE = 0x0010;

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

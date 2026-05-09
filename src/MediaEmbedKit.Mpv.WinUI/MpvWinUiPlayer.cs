using System;
using System.Collections.Generic;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;

namespace MediaEmbedKit.Mpv.WinUI
{
    /// <summary>
    /// 提供 WinUI 3 用的 HWND libmpv 播放控制項。
    /// </summary>
    public sealed class MpvWinUiPlayer : Grid, IDisposable
    {
        /// <summary>
        /// 識別 <see cref="OverlayContent"/> 相依性屬性。
        /// </summary>
        public static readonly DependencyProperty OverlayContentProperty = DependencyProperty.Register(
            nameof(OverlayContent),
            typeof(UIElement),
            typeof(MpvWinUiPlayer),
            new PropertyMetadata(null, OverlayContentChanged));

        /// <summary>
        /// 識別 <see cref="IsOverlayOpen"/> 相依性屬性。
        /// </summary>
        public static readonly DependencyProperty IsOverlayOpenProperty = DependencyProperty.Register(
            nameof(IsOverlayOpen),
            typeof(bool),
            typeof(MpvWinUiPlayer),
            new PropertyMetadata(true, OverlayOpenChanged));

        /// <summary>
        /// HWND 播放後端。
        /// </summary>
        private MpvWinUiHwndPlayer? _hwndPlayer;
        /// <summary>
        /// 控制項記錄的 WinUI 視窗。
        /// </summary>
        private Window? _hostWindow;
        /// <summary>
        /// 可容納原生子視窗的父視窗控制代碼。
        /// </summary>
        private IntPtr _parentHwnd;
        /// <summary>
        /// 等待播放器建立後載入的媒體來源。
        /// </summary>
        private string? _pendingSource;
        /// <summary>
        /// 等待播放器建立後套用的載入模式。
        /// </summary>
        private MpvLoadFileMode _pendingMode = MpvLoadFileMode.Replace;
        /// <summary>
        /// 表示目前控制項是否已釋放。
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 初始化 <see cref="MpvWinUiPlayer"/> 類別的新執行個體。
        /// </summary>
        public MpvWinUiPlayer()
        {
            PlayerOptions = new MpvPlayerOptions();
            if (MpvWinUiDesignMode.IsEnabled)
            {
                Children.Add(MpvWinUiDesignMode.CreatePlaceholder("MediaEmbedKit.Mpv WinUI"));
            }

            Loaded += OnLoaded;
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
            get { return _hwndPlayer == null ? null : _hwndPlayer.Player; }
        }

        /// <summary>
        /// 取得控制項目前記錄的 WinUI 視窗。
        /// </summary>
        /// <value>最近一次附加的 WinUI 視窗；尚未附加時為 <see langword="null"/>。</value>
        public Window? HostWindow
        {
            get { return _hostWindow; }
        }

        /// <summary>
        /// 取得此控制項是否已建立原生子視窗。
        /// </summary>
        /// <value>HWND 後端已建立原生子視窗時為 <see langword="true"/>。</value>
        public bool IsNativeHostCreated
        {
            get { return _hwndPlayer != null && _hwndPlayer.VideoWindowHandle != IntPtr.Zero; }
        }

        /// <summary>
        /// 取得控制項是否已取得可用的父視窗控制代碼。
        /// </summary>
        /// <value>父視窗控制代碼已設定時為 <see langword="true"/>。</value>
        public bool IsAttached
        {
            get { return _parentHwnd != IntPtr.Zero; }
        }

        /// <summary>
        /// 取得最近一次建立 HWND 後端時發生的錯誤。
        /// </summary>
        /// <value>最近一次錯誤；未發生錯誤時為 <see langword="null"/>。</value>
        public Exception? LastBackendError { get; private set; }

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
        /// 附加 WinUI 視窗，讓控制項可以建立 HWND 後端。
        /// </summary>
        /// <param name="hostWindow">控制項所在的 WinUI 視窗。</param>
        public void Attach(Window hostWindow)
        {
            if (hostWindow == null)
            {
                throw new ArgumentNullException(nameof(hostWindow));
            }

            DetachHostWindowClosedHandler();
            _hostWindow = hostWindow;
            _hostWindow.Closed += HostWindowClosed;
            Attach(WindowNative.GetWindowHandle(hostWindow));
        }

        /// <summary>
        /// 附加父視窗控制代碼，讓控制項可以建立 HWND 後端。
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

            _parentHwnd = parentHwnd;
            if (IsLoaded)
            {
                EnsureHwndBackend();
            }
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

            EnsureHwndBackend();
        }

        /// <summary>
        /// 釋放控制項持有的播放後端。
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Loaded -= OnLoaded;
            DetachHostWindowClosedHandler();
            DisposeHwndBackend();
            Children.Clear();
        }

        /// <summary>
        /// 在控制項載入後建立 HWND 播放後端。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (MpvWinUiDesignMode.IsEnabled)
            {
                return;
            }

            EnsureHwndBackend();
        }

        /// <summary>
        /// 在覆蓋層內容變更時同步目前後端。
        /// </summary>
        /// <param name="dependencyObject">相依性屬性所屬物件。</param>
        /// <param name="e">相依性屬性變更資料。</param>
        private static void OverlayContentChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            MpvWinUiPlayer player = (MpvWinUiPlayer)dependencyObject;
            player.UpdateOverlayContent();
        }

        /// <summary>
        /// 在覆蓋層開啟狀態變更時同步目前後端。
        /// </summary>
        /// <param name="dependencyObject">相依性屬性所屬物件。</param>
        /// <param name="e">相依性屬性變更資料。</param>
        private static void OverlayOpenChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            MpvWinUiPlayer player = (MpvWinUiPlayer)dependencyObject;
            player.UpdateOverlayContent();
        }

        /// <summary>
        /// 確保 HWND 後端已建立。
        /// </summary>
        /// <returns>成功建立或已存在 HWND 後端時為 <see langword="true"/>。</returns>
        private bool EnsureHwndBackend()
        {
            if (_disposed || MpvWinUiDesignMode.IsEnabled)
            {
                return false;
            }

            if (!TryResolveParentHwnd())
            {
                return false;
            }

            try
            {
                if (_hwndPlayer == null)
                {
                    _hwndPlayer = new MpvWinUiHwndPlayer();
                    _hwndPlayer.PlayerCreated += BackendPlayerCreated;
                }

                CopyPlayerOptions(PlayerOptions, _hwndPlayer.PlayerOptions);
                _hwndPlayer.OverlayContent = OverlayContent;
                _hwndPlayer.IsOverlayOpen = IsOverlayOpen;
                if (!Children.Contains(_hwndPlayer))
                {
                    Children.Clear();
                    Children.Add(_hwndPlayer);
                }

                _hwndPlayer.Attach(_parentHwnd);
                LastBackendError = null;
                if (!string.IsNullOrWhiteSpace(_pendingSource) && _hwndPlayer.Player != null)
                {
                    _hwndPlayer.LoadFile(_pendingSource!, _pendingMode);
                }

                return true;
            }
            catch (Exception exception)
            {
                LastBackendError = exception;
                DisposeHwndBackend();
                throw;
            }
        }

        /// <summary>
        /// 同步覆蓋層內容到目前使用中的後端。
        /// </summary>
        private void UpdateOverlayContent()
        {
            if (_hwndPlayer != null)
            {
                _hwndPlayer.OverlayContent = OverlayContent;
                _hwndPlayer.IsOverlayOpen = IsOverlayOpen;
            }
        }

        /// <summary>
        /// 嘗試取得可建立 HWND 後端的父視窗控制代碼。
        /// </summary>
        /// <returns>已取得父視窗控制代碼時為 <see langword="true"/>。</returns>
        private bool TryResolveParentHwnd()
        {
            if (_parentHwnd != IntPtr.Zero)
            {
                return true;
            }

            if (_hostWindow != null)
            {
                _parentHwnd = WindowNative.GetWindowHandle(_hostWindow);
                return _parentHwnd != IntPtr.Zero;
            }

            if (XamlRoot == null || XamlRoot.ContentIslandEnvironment == null)
            {
                return false;
            }

            WindowId windowId = XamlRoot.ContentIslandEnvironment.AppWindowId;
            _parentHwnd = Win32Interop.GetWindowFromWindowId(windowId);
            return _parentHwnd != IntPtr.Zero;
        }

        /// <summary>
        /// 處理內部後端建立播放器的事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void BackendPlayerCreated(object? sender, EventArgs e)
        {
            PlayerCreated?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 在附加的 WinUI 視窗關閉時釋放控制項資源。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="args">視窗關閉事件資料。</param>
        private void HostWindowClosed(object sender, WindowEventArgs args)
        {
            Dispose();
        }

        /// <summary>
        /// 釋放 HWND 播放後端。
        /// </summary>
        private void DisposeHwndBackend()
        {
            if (_hwndPlayer == null)
            {
                return;
            }

            _hwndPlayer.OverlayContent = null;
            _hwndPlayer.PlayerCreated -= BackendPlayerCreated;
            Children.Remove(_hwndPlayer);
            _hwndPlayer.Dispose();
            _hwndPlayer = null;
        }

        /// <summary>
        /// 移除附加視窗的關閉事件處理常式。
        /// </summary>
        private void DetachHostWindowClosedHandler()
        {
            if (_hostWindow == null)
            {
                return;
            }

            _hostWindow.Closed -= HostWindowClosed;
            _hostWindow = null;
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
        /// 將播放器建立選項複製到指定目標。
        /// </summary>
        /// <param name="source">來源播放器建立選項。</param>
        /// <param name="target">目標播放器建立選項。</param>
        private static void CopyPlayerOptions(MpvPlayerOptions source, MpvPlayerOptions target)
        {
            target.MpvLibraryPath = source.MpvLibraryPath;
            target.EnableDefaultInputBindings = source.EnableDefaultInputBindings;
            target.EnableKeyboardInput = source.EnableKeyboardInput;
            target.EnableOsc = source.EnableOsc;
            target.EnableYtdlp = source.EnableYtdlp;
            target.YtdlpPath = source.YtdlpPath;
            target.YtdlpFormatPreset = source.YtdlpFormatPreset;
            target.YtdlpFormat = source.YtdlpFormat;
            target.ConfigDirectory = source.ConfigDirectory;
            target.InputConfigFile = source.InputConfigFile;
            target.LoadScripts = source.LoadScripts;
            target.ToolDirectory = source.ToolDirectory;
            target.LoadUserConfig = source.LoadUserConfig;
            target.LogLevel = source.LogLevel;

            target.ConfigFiles.Clear();
            foreach (string configFile in source.ConfigFiles)
            {
                target.ConfigFiles.Add(configFile);
            }

            target.ScriptFiles.Clear();
            foreach (string scriptFile in source.ScriptFiles)
            {
                target.ScriptFiles.Add(scriptFile);
            }

            target.InitialOptions.Clear();
            foreach (KeyValuePair<string, string> option in source.InitialOptions)
            {
                target.InitialOptions[option.Key] = option.Value;
            }
        }
    }
}

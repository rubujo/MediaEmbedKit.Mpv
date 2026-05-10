using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace MediaEmbedKit.Mpv.Wpf
{
    /// <summary>
    /// 提供可覆蓋在 WPF HWND 視訊主控項上的 Popup 輔助類別。
    /// </summary>
    public sealed class MpvAirspacePopup : IDisposable
    {
        /// <summary>
        /// Popup 要對齊的目標 WPF 元素。
        /// </summary>
        private readonly FrameworkElement _target;
        /// <summary>
        /// 目前附加事件的 WPF 視窗。
        /// </summary>
        private Window? _window;
        /// <summary>
        /// 表示目前是否已排入 Popup 邊界同步。
        /// </summary>
        private bool _boundsUpdateQueued;
        /// <summary>
        /// 表示輔助類別是否已釋放。
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 初始化 <see cref="MpvAirspacePopup"/> 類別的新執行個體。
        /// </summary>
        /// <param name="target">Popup 要覆蓋並對齊的 WPF 元素。</param>
        /// <param name="child">要顯示在 Popup 中的 WPF 內容。</param>
        public MpvAirspacePopup(FrameworkElement target, UIElement child)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (child == null)
            {
                throw new ArgumentNullException(nameof(child));
            }

            _target = target;
            Popup = new Popup
            {
                PlacementTarget = target,
                Placement = PlacementMode.Relative,
                AllowsTransparency = true,
                StaysOpen = true,
                Child = child
            };

            Attach();
            UpdateBounds();
        }

        /// <summary>
        /// 取得實際用來顯示覆蓋內容的 WPF Popup。
        /// </summary>
        /// <value>對齊目標元素的 Popup 執行個體。</value>
        public Popup Popup { get; private set; }

        /// <summary>
        /// 取得或設定 Popup 是否開啟。
        /// </summary>
        /// <value>Popup 目前開啟時為 <see langword="true"/>。</value>
        public bool IsOpen
        {
            get { return Popup.IsOpen; }
            set
            {
                Popup.IsOpen = value && _target.IsVisible;
                UpdateBounds();
            }
        }

        /// <summary>
        /// 釋放 Popup 與事件訂閱。
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _boundsUpdateQueued = false;
            Detach();
            Popup.IsOpen = false;
            Popup.Child = null;
        }

        /// <summary>
        /// 依目標元素目前大小與位置更新 Popup 邊界。
        /// </summary>
        public void UpdateBounds()
        {
            if (Popup.Child is FrameworkElement element)
            {
                element.Width = Math.Max(0, _target.ActualWidth);
                element.Height = Math.Max(0, _target.ActualHeight);
            }

            Popup.HorizontalOffset = 0.01;
            Popup.HorizontalOffset = 0;
            Popup.VerticalOffset = 0;
        }

        /// <summary>
        /// 附加目標元素與視窗事件。
        /// </summary>
        private void Attach()
        {
            _target.Loaded += TargetLoaded;
            _target.Unloaded += TargetUnloaded;
            _target.SizeChanged += TargetChanged;
            _target.IsVisibleChanged += TargetIsVisibleChanged;
            _target.LayoutUpdated += TargetLayoutUpdated;

            AttachWindow(Window.GetWindow(_target));
        }

        /// <summary>
        /// 中斷目標元素與視窗事件訂閱。
        /// </summary>
        private void Detach()
        {
            _target.Loaded -= TargetLoaded;
            _target.Unloaded -= TargetUnloaded;
            _target.SizeChanged -= TargetChanged;
            _target.IsVisibleChanged -= TargetIsVisibleChanged;
            _target.LayoutUpdated -= TargetLayoutUpdated;
            AttachWindow(null);
        }

        /// <summary>
        /// 在目標元素載入時重新附加所屬視窗並更新邊界。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void TargetLoaded(object sender, RoutedEventArgs e)
        {
            AttachWindow(Window.GetWindow(_target));
            UpdateBounds();
        }

        /// <summary>
        /// 在目標元素卸載時關閉 Popup。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void TargetUnloaded(object sender, RoutedEventArgs e)
        {
            Popup.IsOpen = false;
        }

        /// <summary>
        /// 在目標元素大小變更時更新 Popup 邊界。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">大小變更事件資料。</param>
        private void TargetChanged(object sender, SizeChangedEventArgs e)
        {
            RequestUpdateBounds();
        }

        /// <summary>
        /// 在目標元素可見度變更時同步 Popup 開啟狀態。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">相依性屬性變更事件資料。</param>
        private void TargetIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!_target.IsVisible)
            {
                Popup.IsOpen = false;
            }
        }

        /// <summary>
        /// 在目標元素版面配置更新時同步 Popup 邊界。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void TargetLayoutUpdated(object? sender, EventArgs e)
        {
            if (Popup.IsOpen)
            {
                RequestUpdateBounds();
            }
        }

        /// <summary>
        /// 附加或切換 Popup 追蹤的 WPF 視窗。
        /// </summary>
        /// <param name="window">要附加的 WPF 視窗；傳入 <see langword="null"/> 會解除目前視窗。</param>
        private void AttachWindow(Window? window)
        {
            if (_window != null)
            {
                _window.LocationChanged -= WindowChanged;
                _window.SizeChanged -= WindowChanged;
                _window.StateChanged -= WindowChanged;
            }

            _window = window;

            if (_window != null)
            {
                _window.LocationChanged += WindowChanged;
                _window.SizeChanged += WindowChanged;
                _window.StateChanged += WindowChanged;
            }
        }

        /// <summary>
        /// 在所屬視窗位置或大小變更時更新 Popup 邊界。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void WindowChanged(object? sender, EventArgs e)
        {
            RequestUpdateBounds();
        }

        /// <summary>
        /// 將 Popup 邊界同步排入 WPF UI 執行緒背景佇列。
        /// </summary>
        private void RequestUpdateBounds()
        {
            if (_disposed || _boundsUpdateQueued)
            {
                return;
            }

            _boundsUpdateQueued = true;
            _ = _target.Dispatcher.BeginInvoke(new Action(ProcessQueuedUpdateBounds), DispatcherPriority.Background);
        }

        /// <summary>
        /// 執行已排程的 Popup 邊界同步。
        /// </summary>
        private void ProcessQueuedUpdateBounds()
        {
            _boundsUpdateQueued = false;
            if (!_disposed)
            {
                UpdateBounds();
            }
        }
    }
}

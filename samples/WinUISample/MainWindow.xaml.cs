using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using MediaEmbedKit.Mpv.Samples;
using Windows.Graphics;
using WinRT.Interop;

namespace MediaEmbedKit.Mpv.Samples.WinUI
{
    /// <summary>
    /// 表示 WinUI 3 範例的主要視窗。
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        /// <summary>
        /// 表示預設媒體載入是否已排程。
        /// </summary>
        private bool _playbackStarted;
        /// <summary>
        /// 表示冒煙測試是否已啟動。
        /// </summary>
        private bool _smokeStarted;

        /// <summary>
        /// 初始化 <see cref="MainWindow"/> 類別的新執行個體。
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            ResizeWindow();
            SourceBox.Text = SampleRuntime.PlaybackUrl;
            SampleRuntime.CopyTo(SampleRuntime.PlayerOptions, PlayerHost.PlayerOptions);
            PlayerHost.Attach(this);
            PlayerHost.Loaded += PlayerHostLoaded;
        }

        /// <summary>
        /// 處理載入按鈕點選事件並載入輸入的媒體來源。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void OnLoadClicked(object sender, RoutedEventArgs e)
        {
            LoadCurrentSource();
        }

        /// <summary>
        /// 處理暫停按鈕點選事件並切換播放器暫停狀態。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void OnPauseClicked(object sender, RoutedEventArgs e)
        {
            if (PlayerHost.Player != null)
            {
                PlayerHost.Player.Pause = !PlayerHost.Player.Pause;
            }
        }

        /// <summary>
        /// 處理停止按鈕點選事件並停止目前播放項目。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void OnStopClicked(object sender, RoutedEventArgs e)
        {
            PlayerHost.Player?.Stop();
        }

        /// <summary>
        /// 在 WinUI 播放控制項載入後載入預設媒體並執行冒煙測試。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private async void PlayerHostLoaded(object sender, RoutedEventArgs e)
        {
            PlayerHost.Loaded -= PlayerHostLoaded;
            StartPlayback();
            if (SampleRuntime.IsSmokeTestEnabled && !_smokeStarted)
            {
                _smokeStarted = true;
                await RunSmokeAsync().ConfigureAwait(true);
            }
        }

        /// <summary>
        /// 從應用程式啟動流程排程 WinUI 3 範例播放冒煙測試。
        /// </summary>
        internal void StartSmokePlayback()
        {
            if (_smokeStarted)
            {
                return;
            }

            _smokeStarted = true;
            if (!DispatcherQueue.TryEnqueue(async () =>
            {
                StartPlayback();
                await RunSmokeAsync().ConfigureAwait(true);
            }))
            {
                StartPlayback();
                _ = RunSmokeAsync();
            }
        }

        /// <summary>
        /// 載入預設媒體來源。
        /// </summary>
        private void StartPlayback()
        {
            if (_playbackStarted)
            {
                return;
            }

            _playbackStarted = true;
            LoadCurrentSource();
        }

        /// <summary>
        /// 載入目前輸入的媒體來源。
        /// </summary>
        private void LoadCurrentSource()
        {
            if (!string.IsNullOrWhiteSpace(SourceBox.Text))
            {
                PlayerHost.LoadFile(SourceBox.Text);
            }
        }

        /// <summary>
        /// 執行 WinUI 3 範例播放冒煙測試。
        /// </summary>
        /// <returns>代表冒煙測試流程的工作。</returns>
        private Task RunSmokeAsync()
        {
            return SampleRuntime.RunSmokeUntilPlaybackAsync("WinUISample", () => PlayerHost.Player, Close);
        }

        /// <summary>
        /// 將 WinUI 視窗調整為範例標準尺寸。
        /// </summary>
        private void ResizeWindow()
        {
            nint windowHandle = WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);
            appWindow.Resize(new SizeInt32(SampleRuntime.SampleWindowWidth, SampleRuntime.SampleWindowHeight));
        }
    }
}

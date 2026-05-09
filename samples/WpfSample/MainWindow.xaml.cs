using System;
using System.Threading.Tasks;
using System.Windows;
using MediaEmbedKit.Mpv;
using MediaEmbedKit.Mpv.Samples;

namespace MediaEmbedKit.Mpv.Samples.Wpf
{
    /// <summary>
    /// 表示 WPF 範例的主要視窗。
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// 初始化 <see cref="MainWindow"/> 類別的新執行個體。
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            UrlTextBox.Text = SampleRuntime.PlaybackUrl;
            SampleRuntime.CopyTo(SampleRuntime.PlayerOptions, PlayerHost.PlayerOptions);
            Loaded += WindowLoaded;
        }

        /// <summary>
        /// 在視窗載入後載入預設媒體並執行冒煙測試。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private async void WindowLoaded(object sender, RoutedEventArgs e)
        {
            LoadCurrentSource();
            if (SampleRuntime.IsSmokeTestEnabled)
            {
                await RunSmokeAsync().ConfigureAwait(true);
            }
        }

        /// <summary>
        /// 執行 WPF 範例播放冒煙測試。
        /// </summary>
        /// <returns>代表冒煙測試流程的工作。</returns>
        private Task RunSmokeAsync()
        {
            return SampleRuntime.RunSmokeUntilPlaybackAsync("WpfSample", () => PlayerHost.Player, Close);
        }

        /// <summary>
        /// 處理載入按鈕點選事件並載入輸入的媒體來源。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void LoadClick(object sender, RoutedEventArgs e)
        {
            LoadCurrentSource();
        }

        /// <summary>
        /// 載入目前輸入的媒體來源。
        /// </summary>
        private void LoadCurrentSource()
        {
            try
            {
                PlayerHost.LoadFile(UrlTextBox.Text, MpvLoadFileMode.Replace);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "mpv", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 切換目前播放器的暫停狀態。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void PauseClick(object sender, RoutedEventArgs e)
        {
            if (PlayerHost.Player != null)
            {
                PlayerHost.Player.Pause = !PlayerHost.Player.Pause;
            }
        }

        /// <summary>
        /// 停止目前播放項目。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void StopClick(object sender, RoutedEventArgs e)
        {
            PlayerHost.Player?.Stop();
        }
    }
}

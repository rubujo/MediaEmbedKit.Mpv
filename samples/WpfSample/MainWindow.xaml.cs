using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
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
        /// 範例事件輸出的最大保留列數。
        /// </summary>
        private const int EventLogLimit = 200;

        /// <summary>
        /// 範例進階功能控制器。
        /// </summary>
        private readonly SampleFeatureController _features;
        /// <summary>
        /// 週期性更新狀態列的計時器。
        /// </summary>
        private readonly DispatcherTimer _statusTimer;
        /// <summary>
        /// 將播放器事件轉接到範例事件清單。
        /// </summary>
        private SamplePlayerEventBridge? _eventBridge;

        /// <summary>
        /// 初始化 <see cref="MainWindow"/> 類別的新執行個體。
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            UrlTextBox.Text = SampleRuntime.PlaybackUrl;
            SampleRuntime.CopyTo(SampleRuntime.PlayerOptions, PlayerHost.PlayerOptions);
            _features = new SampleFeatureController(() => PlayerHost.Player, AppendEventLine);
            ConfigureFormatComboBox();
            _statusTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _statusTimer.Tick += StatusTimerTick;
            _statusTimer.Start();
            PlayerHost.PlayerCreated += PlayerCreated;
            Loaded += WindowLoaded;
            AppendEventLine(CreateLifecycleLine("WindowCreated", "WPF 視窗已建立，等待 HwndHost 建立播放器。"));
        }

        /// <summary>
        /// 在視窗關閉時釋放事件橋接器。
        /// </summary>
        /// <param name="e">事件資料。</param>
        protected override void OnClosed(EventArgs e)
        {
            _statusTimer.Stop();
            _statusTimer.Tick -= StatusTimerTick;
            _eventBridge?.WriteLifecycle("WindowClosed", "視窗已關閉，準備取消事件訂閱。");
            _eventBridge?.Dispose();
            PlayerHost.PlayerCreated -= PlayerCreated;
            base.OnClosed(e);
        }

        /// <summary>
        /// 在視窗載入後載入預設媒體並執行冒煙測試。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private async void WindowLoaded(object sender, RoutedEventArgs e)
        {
            AppendEventLine(CreateLifecycleLine("Loaded", "視窗已載入，準備載入預設媒體來源。"));
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
        /// 處理播放器建立事件並開始輸出 libmpv 事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void PlayerCreated(object? sender, EventArgs e)
        {
            _eventBridge?.Dispose();
            if (PlayerHost.Player != null)
            {
                _eventBridge = new SamplePlayerEventBridge(PlayerHost.Player, AppendEventLine);
            }
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
                _eventBridge?.WriteLifecycle("LoadFile", UrlTextBox.Text);
                PlayerHost.LoadFile(UrlTextBox.Text, MpvLoadFileMode.Replace);
            }
            catch (Exception ex)
            {
                AppendEventLine(CreateLifecycleLine("LoadFileError", ex.Message));
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
                _eventBridge?.WriteLifecycle("Pause", "切換播放器暫停狀態。");
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
            _eventBridge?.WriteLifecycle("Stop", "停止目前播放項目。");
            PlayerHost.Player?.Stop();
        }

        /// <summary>
        /// 在格式選項變更時套用 yt-dlp 格式。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void FormatComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SampleYtdlpFormatChoice? choice = FormatComboBox.SelectedItem as SampleYtdlpFormatChoice;
            if (choice == null)
            {
                return;
            }

            try
            {
                SampleFeatureController.ApplyYtdlpFormat(PlayerHost.PlayerOptions, choice);
                if (PlayerHost.Player != null)
                {
                    _features.ApplyYtdlpFormat(choice);
                }
            }
            catch (Exception ex)
            {
                AppendEventLine(CreateLifecycleLine("FormatError", ex.Message));
            }
        }

        /// <summary>
        /// 更新播放狀態列。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void StatusTimerTick(object? sender, EventArgs e)
        {
            StatusTextBlock.Text = _features.GetStatusText();
        }

        /// <summary>
        /// 處理 OSD 按鈕點選事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void OsdClick(object sender, RoutedEventArgs e)
        {
            RunFeature(() => _features.ShowOsd());
        }

        /// <summary>
        /// 處理倒退按鈕點選事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void SeekBackwardClick(object sender, RoutedEventArgs e)
        {
            RunFeature(() => _features.SeekRelative(-10));
        }

        /// <summary>
        /// 處理快轉按鈕點選事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void SeekForwardClick(object sender, RoutedEventArgs e)
        {
            RunFeature(() => _features.SeekRelative(10));
        }

        /// <summary>
        /// 處理降低音量按鈕點選事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void VolumeDownClick(object sender, RoutedEventArgs e)
        {
            RunFeature(() => _features.ChangeVolume(-5));
        }

        /// <summary>
        /// 處理提高音量按鈕點選事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void VolumeUpClick(object sender, RoutedEventArgs e)
        {
            RunFeature(() => _features.ChangeVolume(5));
        }

        /// <summary>
        /// 處理靜音按鈕點選事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void MuteClick(object sender, RoutedEventArgs e)
        {
            RunFeature(() => _features.ToggleMute());
        }

        /// <summary>
        /// 處理播放速度按鈕點選事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void SpeedClick(object sender, RoutedEventArgs e)
        {
            RunFeature(() => _features.CycleSpeed());
        }

        /// <summary>
        /// 處理字幕按鈕點選事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void SubtitleClick(object sender, RoutedEventArgs e)
        {
            RunFeature(() => _features.AddSampleSubtitle());
        }

        /// <summary>
        /// 處理播放軌按鈕點選事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void TracksClick(object sender, RoutedEventArgs e)
        {
            RunFeature(() => _features.DumpTracks());
        }

        /// <summary>
        /// 處理截圖按鈕點選事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void ScreenshotClick(object sender, RoutedEventArgs e)
        {
            RunFeature(() => _features.TakeScreenshot());
        }

        /// <summary>
        /// 處理設定檔按鈕點選事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void ConfigClick(object sender, RoutedEventArgs e)
        {
            RunFeature(() => _features.LoadSampleConfig());
        }

        /// <summary>
        /// 處理 Lua 按鈕點選事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void LuaClick(object sender, RoutedEventArgs e)
        {
            RunFeature(() => _features.LoadSampleLuaScript());
        }

        /// <summary>
        /// 處理 yt-dlp 診斷按鈕點選事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private async void YtdlpClick(object sender, RoutedEventArgs e)
        {
            await RunFeatureAsync(() => _features.RunYtdlpDiagnosticsAsync(UrlTextBox.Text)).ConfigureAwait(true);
        }

        /// <summary>
        /// 處理 Deno 診斷按鈕點選事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private async void DenoClick(object sender, RoutedEventArgs e)
        {
            await RunFeatureAsync(() => _features.RunDenoDiagnosticsAsync()).ConfigureAwait(true);
        }

        /// <summary>
        /// 處理 yt-dlp 更新按鈕點選事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private async void YtdlpUpdateClick(object sender, RoutedEventArgs e)
        {
            await RunFeatureAsync(() => _features.RunYtdlpSelfUpdateAsync()).ConfigureAwait(true);
        }

        /// <summary>
        /// 處理 Deno 更新按鈕點選事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private async void DenoUpdateClick(object sender, RoutedEventArgs e)
        {
            await RunFeatureAsync(() => _features.RunDenoSelfUpgradeAsync()).ConfigureAwait(true);
        }

        /// <summary>
        /// 執行同步範例功能並處理錯誤。
        /// </summary>
        /// <param name="action">要執行的功能。</param>
        private void RunFeature(Action action)
        {
            try
            {
                action();
                StatusTextBlock.Text = _features.GetStatusText();
            }
            catch (Exception ex)
            {
                AppendEventLine(CreateLifecycleLine("FeatureError", ex.Message));
            }
        }

        /// <summary>
        /// 執行非同步範例功能並處理錯誤。
        /// </summary>
        /// <param name="action">要執行的非同步功能。</param>
        /// <returns>代表功能執行流程的工作。</returns>
        private async Task RunFeatureAsync(Func<Task> action)
        {
            try
            {
                await action().ConfigureAwait(true);
                StatusTextBlock.Text = _features.GetStatusText();
            }
            catch (Exception ex)
            {
                AppendEventLine(CreateLifecycleLine("FeatureError", ex.Message));
            }
        }

        /// <summary>
        /// 將事件文字加入 UI 清單。
        /// </summary>
        /// <param name="line">要加入事件清單的文字列。</param>
        private void AppendEventLine(string line)
        {
            if (!Dispatcher.CheckAccess())
            {
                _ = Dispatcher.BeginInvoke(new Action<string>(AppendEventLine), line);
                return;
            }

            EventListBox.Items.Add(line);
            while (EventListBox.Items.Count > EventLogLimit)
            {
                EventListBox.Items.RemoveAt(0);
            }

            EventListBox.ScrollIntoView(line);
        }

        /// <summary>
        /// 建立範例生命週期文字列。
        /// </summary>
        /// <param name="stage">生命週期階段名稱。</param>
        /// <param name="detail">階段補充內容。</param>
        /// <returns>可顯示在事件清單中的生命週期文字列。</returns>
        private static string CreateLifecycleLine(string stage, string detail)
        {
            return DateTimeOffset.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + " [lifecycle] " + stage + " | " + detail;
        }

        /// <summary>
        /// 設定 yt-dlp 格式選單。
        /// </summary>
        private void ConfigureFormatComboBox()
        {
            IReadOnlyList<SampleYtdlpFormatChoice> choices = SampleFeatureController.CreateYtdlpFormatChoices();
            SampleYtdlpFormatChoice defaultChoice = SampleFeatureController.CreateDefaultYtdlpFormatChoice();
            int selectedIndex = 0;
            for (int index = 0; index < choices.Count; index++)
            {
                SampleYtdlpFormatChoice choice = choices[index];
                FormatComboBox.Items.Add(choice);
                if (choice.Preset == defaultChoice.Preset)
                {
                    selectedIndex = index;
                }
            }

            FormatComboBox.SelectedIndex = selectedIndex;
            if (FormatComboBox.SelectedItem is SampleYtdlpFormatChoice selectedChoice)
            {
                SampleFeatureController.ApplyYtdlpFormat(PlayerHost.PlayerOptions, selectedChoice);
            }
        }
    }
}

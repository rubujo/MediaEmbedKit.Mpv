using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using MediaEmbedKit.Mpv;
using MediaEmbedKit.Mpv.Samples;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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
        /// 範例事件輸出的最大保留列數。
        /// </summary>
        private const int EventLogLimit = 120;

        /// <summary>
        /// 顯示在 UI 的事件文字列集合。
        /// </summary>
        private readonly ObservableCollection<string> _eventLines = new ObservableCollection<string>();
        /// <summary>
        /// 範例進階功能控制器。
        /// </summary>
        private readonly SampleFeatureController _features;
        /// <summary>
        /// 背景讀取並批次套用狀態列文字的分派器。
        /// </summary>
        private readonly SampleStatusUpdateDispatcher _statusDispatcher;
        /// <summary>
        /// 將播放器事件轉接到範例事件清單。
        /// </summary>
        private SamplePlayerEventBridge? _eventBridge;
        /// <summary>
        /// 目前已建立的播放器。
        /// </summary>
        private MpvPlayer? _currentPlayer;
        /// <summary>
        /// 批次轉送事件文字到 UI 執行緒的分派器。
        /// </summary>
        private readonly SampleEventLogDispatcher _eventLogDispatcher;
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
            ApplyButtonStyles();
            ResizeWindow();
            EventList.ItemsSource = _eventLines;
            SourceBox.Text = SampleRuntime.PlaybackUrl;
            SampleRuntime.CopyTo(SampleRuntime.PlayerOptions, PlayerHost.PlayerOptions);
            _features = new SampleFeatureController(() => _currentPlayer, AppendEventLine);
            ConfigureFormatComboBox();
            _eventLogDispatcher = new SampleEventLogDispatcher(AppendEventLines, ScheduleEventLogFlush);
            _statusDispatcher = new SampleStatusUpdateDispatcher(() => _features.GetStatusText(), SetStatusText, ScheduleUiUpdate);
            PlayerHost.Attach(this);
            PlayerHost.PlayerCreated += PlayerCreated;
            PlayerHost.Loaded += PlayerHostLoaded;
            Closed += WindowClosed;
            AppendEventLine(CreateLifecycleLine("WindowCreated", "WinUI 視窗已建立，等待 HWND 後端建立播放器。"));
            _ = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, StartPlayback);
        }

        /// <summary>
        /// 處理視窗關閉事件並釋放事件橋接器。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="args">視窗事件資料。</param>
        private void WindowClosed(object sender, WindowEventArgs args)
        {
            _statusDispatcher.Dispose();
            _eventBridge?.WriteLifecycle("WindowClosed", "視窗已關閉，準備取消事件訂閱。");
            _eventBridge?.Dispose();
            _eventLogDispatcher.Dispose();
            PlayerHost.PlayerCreated -= PlayerCreated;
            _currentPlayer = null;
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
                _currentPlayer = PlayerHost.Player;
                _eventBridge = new SamplePlayerEventBridge(PlayerHost.Player, AppendEventLine);
                _statusDispatcher.RequestUpdate();
            }
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
                _eventBridge?.WriteLifecycle("Pause", "切換播放器暫停狀態。");
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
            _eventBridge?.WriteLifecycle("Stop", "停止目前播放項目。");
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
            AppendEventLine(CreateLifecycleLine("Loaded", "播放控制項已載入，準備載入預設媒體來源。"));
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
                _eventBridge?.WriteLifecycle("LoadFile", SourceBox.Text);
                PlayerHost.LoadFile(SourceBox.Text, MpvLoadFileMode.Replace);
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
        /// 在格式選項變更時套用 yt-dlp 格式。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void OnFormatSelectionChanged(object sender, SelectionChangedEventArgs e)
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
        /// 處理 OSD 按鈕點選事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void OnOsdClicked(object sender, RoutedEventArgs e)
        {
            RunFeature(() => _features.ShowOsd());
        }

        /// <summary>
        /// 處理倒退按鈕點選事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void OnSeekBackwardClicked(object sender, RoutedEventArgs e)
        {
            RunFeature(() => _features.SeekRelative(-10));
        }

        /// <summary>
        /// 處理快轉按鈕點選事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void OnSeekForwardClicked(object sender, RoutedEventArgs e)
        {
            RunFeature(() => _features.SeekRelative(10));
        }

        /// <summary>
        /// 處理降低音量按鈕點選事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void OnVolumeDownClicked(object sender, RoutedEventArgs e)
        {
            RunFeature(() => _features.ChangeVolume(-5));
        }

        /// <summary>
        /// 處理提高音量按鈕點選事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void OnVolumeUpClicked(object sender, RoutedEventArgs e)
        {
            RunFeature(() => _features.ChangeVolume(5));
        }

        /// <summary>
        /// 處理靜音按鈕點選事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void OnMuteClicked(object sender, RoutedEventArgs e)
        {
            RunFeature(() => _features.ToggleMute());
        }

        /// <summary>
        /// 處理播放速度按鈕點選事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void OnSpeedClicked(object sender, RoutedEventArgs e)
        {
            RunFeature(() => _features.CycleSpeed());
        }

        /// <summary>
        /// 處理字幕按鈕點選事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void OnSubtitleClicked(object sender, RoutedEventArgs e)
        {
            RunFeature(() => _features.AddSampleSubtitle());
        }

        /// <summary>
        /// 處理播放軌按鈕點選事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void OnTracksClicked(object sender, RoutedEventArgs e)
        {
            RunFeature(() => _features.DumpTracks());
        }

        /// <summary>
        /// 處理截圖按鈕點選事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void OnScreenshotClicked(object sender, RoutedEventArgs e)
        {
            RunFeature(() => _features.TakeScreenshot());
        }

        /// <summary>
        /// 處理設定檔按鈕點選事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void OnConfigClicked(object sender, RoutedEventArgs e)
        {
            RunFeature(() => _features.LoadSampleConfig());
        }

        /// <summary>
        /// 處理 Lua 按鈕點選事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void OnLuaClicked(object sender, RoutedEventArgs e)
        {
            RunFeature(() => _features.LoadSampleLuaScript());
        }

        /// <summary>
        /// 處理 yt-dlp 診斷按鈕點選事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private async void OnYtdlpClicked(object sender, RoutedEventArgs e)
        {
            await RunFeatureAsync(() => _features.RunYtdlpDiagnosticsAsync(SourceBox.Text)).ConfigureAwait(true);
        }

        /// <summary>
        /// 處理 Deno 診斷按鈕點選事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private async void OnDenoClicked(object sender, RoutedEventArgs e)
        {
            await RunFeatureAsync(() => _features.RunDenoDiagnosticsAsync()).ConfigureAwait(true);
        }

        /// <summary>
        /// 處理 yt-dlp 更新按鈕點選事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private async void OnYtdlpUpdateClicked(object sender, RoutedEventArgs e)
        {
            await RunFeatureAsync(() => _features.RunYtdlpSelfUpdateAsync()).ConfigureAwait(true);
        }

        /// <summary>
        /// 處理 Deno 更新按鈕點選事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private async void OnDenoUpdateClicked(object sender, RoutedEventArgs e)
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
                _statusDispatcher.RequestUpdate();
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
                _statusDispatcher.RequestUpdate();
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
            _eventLogDispatcher.Enqueue(line);
        }

        /// <summary>
        /// 批次加入事件文字列到 UI 清單。
        /// </summary>
        /// <param name="lines">要加入事件清單的文字列集合。</param>
        private void AppendEventLines(IReadOnlyList<string> lines)
        {
            foreach (string line in lines)
            {
                _eventLines.Add(line);
            }

            while (_eventLines.Count > EventLogLimit)
            {
                _eventLines.RemoveAt(0);
            }

            if (_eventLines.Count > 0)
            {
                EventList.ScrollIntoView(_eventLines[_eventLines.Count - 1]);
            }
        }

        /// <summary>
        /// 將事件清單更新排入 WinUI UI 執行緒。
        /// </summary>
        /// <param name="action">要在 UI 執行緒執行的更新。</param>
        private void ScheduleEventLogFlush(Action action)
        {
            ScheduleUiUpdate(action);
        }

        /// <summary>
        /// 將指定動作排入 WinUI UI 執行緒。
        /// </summary>
        /// <param name="action">要在 UI 執行緒執行的動作。</param>
        private void ScheduleUiUpdate(Action action)
        {
            _ = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => action());
        }

        /// <summary>
        /// 套用背景輪詢取得的狀態列文字。
        /// </summary>
        /// <param name="text">要顯示的狀態列文字。</param>
        private void SetStatusText(string text)
        {
            StatusTextBlock.Text = text;
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

        /// <summary>
        /// 對視窗內所有按鈕套用固定深色外觀。
        /// </summary>
        private void ApplyButtonStyles()
        {
            DependencyObject? root = Content as DependencyObject;
            if (root != null)
            {
                ApplyButtonStyles(root);
            }
        }

        /// <summary>
        /// 遞迴套用按鈕外觀。
        /// </summary>
        /// <param name="root">要掃描的視覺樹節點。</param>
        private static void ApplyButtonStyles(DependencyObject root)
        {
            Button? button = root as Button;
            if (button != null)
            {
                ApplyButtonStyle(button);
            }

            int childCount = VisualTreeHelper.GetChildrenCount(root);
            for (int index = 0; index < childCount; index++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, index);
                ApplyButtonStyles(child);
            }
        }

        /// <summary>
        /// 套用單一按鈕的固定深色外觀。
        /// </summary>
        /// <param name="button">要套用外觀的按鈕。</param>
        private static void ApplyButtonStyle(Button button)
        {
            button.Background = new SolidColorBrush(ColorHelper.FromArgb(255, 48, 48, 48));
            button.BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 224, 224, 224));
            button.BorderThickness = new Thickness(1);
            button.Foreground = new SolidColorBrush(Colors.White);
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

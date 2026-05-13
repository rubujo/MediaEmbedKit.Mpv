using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MediaEmbedKit.Mpv;
using MediaEmbedKit.Mpv.Samples;
using MediaEmbedKit.Mpv.Wpf;

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
        private const int EventLogLimit = 60;

        /// <summary>
        /// 背景 runtime 初始化完成後建立的 WPF 播放控制項。
        /// </summary>
        private MpvWpfPlayer? _playerHost;
        /// <summary>
        /// 需要在 runtime 就緒後才可使用的控制項清單。
        /// </summary>
        private readonly List<Control> _runtimeControls = new List<Control>();
        /// <summary>
        /// 非同步功能進行中需暫時禁用的功能按鈕清單。
        /// </summary>
        private readonly List<Button> _featureButtons = new List<Button>();
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
        /// 控制非同步範例功能不可重入的閘門。
        /// </summary>
        private readonly SampleAsyncFeatureGate _asyncFeatureGate = new SampleAsyncFeatureGate();
        /// <summary>
        /// 紀錄範例 runtime 是否已完成初始化。
        /// </summary>
        private bool _runtimeReady;
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
            UrlTextBox.Text = SampleRuntime.PlaybackUrl;
            _features = new SampleFeatureController(() => _currentPlayer, AppendEventLine);
            ConfigureFormatComboBox();
            _eventLogDispatcher = new SampleEventLogDispatcher(AppendEventLines, ScheduleEventLogFlush);
            _statusDispatcher = new SampleStatusUpdateDispatcher(() => _features.GetStatusText(), SetStatusText, ScheduleUiUpdate);
            SetRuntimeControlsEnabled(false);
            Loaded += WindowLoaded;
            AppendEventLine(CreateLifecycleLine("WindowCreated", "WPF 視窗已建立，等待 runtime 初始化。"));
        }

        /// <summary>
        /// 在視窗關閉時釋放事件橋接器。
        /// </summary>
        /// <param name="e">事件資料。</param>
        protected override void OnClosed(EventArgs e)
        {
            _statusDispatcher.Dispose();
            _eventBridge?.WriteLifecycle("WindowClosed", "視窗已關閉，準備取消事件訂閱。");
            _eventBridge?.Dispose();
            _eventLogDispatcher.Dispose();
            if (_playerHost != null)
            {
                _playerHost.PlayerCreated -= PlayerCreated;
            }

            _currentPlayer = null;
            base.OnClosed(e);
        }

        /// <summary>
        /// 在視窗載入後載入預設媒體並執行冒煙測試。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private async void WindowLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _runtimeControls.Clear();
                _featureButtons.Clear();
                RegisterRuntimeControls(this);
                RegisterFeatureButtons(FeaturePanelHost);
                SetRuntimeControlsEnabled(false);
                AppendEventLine(CreateLifecycleLine("Loaded", "視窗已載入，準備初始化範例 runtime。"));
                bool initialized = await InitializeRuntimeAsync().ConfigureAwait(true);
                if (!initialized)
                {
                    return;
                }

                AppendEventLine(CreateLifecycleLine("Loaded", "runtime 已完成，準備載入預設媒體來源。"));
                LoadCurrentSource();
                if (SampleRuntime.IsSmokeTestEnabled && !_smokeStarted)
                {
                    _smokeStarted = true;
                    await RunSmokeAsync().ConfigureAwait(true);
                }
            }
            catch (Exception ex)
            {
                AppendEventLine(CreateLifecycleLine("LoadedError", ex.Message));
                MessageBox.Show(this, ex.Message, "mpv sample", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 非同步初始化範例 runtime 與 WPF 播放控制項。
        /// </summary>
        /// <returns>初始化成功時為 <see langword="true"/>。</returns>
        private async Task<bool> InitializeRuntimeAsync()
        {
            SetStatusText("正在準備 runtime...");
            try
            {
                await Task.Run(async () => await SampleRuntime.InstallOrUpdateAsync().ConfigureAwait(false)).ConfigureAwait(true);
                CreatePlayerHost();
                _runtimeReady = true;
                SetRuntimeControlsEnabled(true);
                SetStatusText("播放器已初始化");
                AppendEventLine(CreateLifecycleLine("RuntimeReady", SampleRuntime.RuntimeDirectory));
                return true;
            }
            catch (Exception ex)
            {
                Environment.ExitCode = 1;
                SetStatusText("runtime 初始化失敗");
                AppendEventLine(CreateLifecycleLine("RuntimeError", ex.Message));
                if (SampleRuntime.IsSmokeTestEnabled)
                {
                    SampleRuntime.WriteSmokeLine("WpfSample", "FAILED Runtime: " + ex.Message);
                    Close();
                    return false;
                }

                MessageBox.Show(this, ex.Message, "mpv runtime", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// 建立 runtime 就緒後才可初始化的 WPF 播放控制項。
        /// </summary>
        private void CreatePlayerHost()
        {
            if (_playerHost != null)
            {
                return;
            }

            MpvWpfPlayer playerHost = new MpvWpfPlayer
            {
                OverlayContent = CreateSafeOverlayContent(),
                IsOverlayOpen = true
            };
            SampleRuntime.CopyTo(SampleRuntime.PlayerOptions, playerHost.PlayerOptions);
            ApplySelectedYtdlpFormatToPlayerOptions(playerHost.PlayerOptions);
            playerHost.PlayerCreated += PlayerCreated;
            PlayerHostContainer.Children.Add(playerHost);
            _playerHost = playerHost;
        }

        /// <summary>
        /// 建立由 `MpvWpfPlayer` 管理的 AirSpace 安全覆蓋層。
        /// </summary>
        /// <returns>可交給 WPF 播放控制項管理的覆蓋層元素。</returns>
        private static UIElement CreateSafeOverlayContent()
        {
            Grid root = new Grid
            {
                IsHitTestVisible = false
            };
            Border badge = new Border
            {
                Width = SampleRuntime.SampleOverlayBadgeWidth,
                Height = SampleRuntime.SampleOverlayBadgeHeight,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(16),
                Background = new SolidColorBrush(ThemeColor(SampleTheme.AccentBadgeArgb)),
                CornerRadius = new CornerRadius(4)
            };
            badge.Child = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                Text = "OverlayContent：AirSpace 安全覆蓋層"
            };
            root.Children.Add(badge);
            return root;
        }

        /// <summary>
        /// 將 <see cref="SampleTheme"/> 的 ARGB 整數轉成 WPF 顏色物件。
        /// </summary>
        /// <param name="argb">要轉換的 ARGB 整數。</param>
        /// <returns>對應的 WPF 顏色物件。</returns>
        private static Color ThemeColor(int argb)
        {
            return Color.FromArgb(SampleTheme.AlphaOf(argb), SampleTheme.RedOf(argb), SampleTheme.GreenOf(argb), SampleTheme.BlueOf(argb));
        }

        /// <summary>
        /// 執行 WPF 範例播放冒煙測試。
        /// </summary>
        /// <returns>代表冒煙測試流程的工作。</returns>
        private Task RunSmokeAsync()
        {
            return SampleRuntime.RunSmokeUntilPlaybackAsync("WpfSample", () => _playerHost?.Player, Close);
        }

        /// <summary>
        /// 處理播放器建立事件並開始輸出 libmpv 事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void PlayerCreated(object? sender, EventArgs e)
        {
            _eventBridge?.Dispose();
            if (_playerHost?.Player != null)
            {
                _currentPlayer = _playerHost.Player;
                _eventBridge = new SamplePlayerEventBridge(_playerHost.Player, AppendEventLine);
                _statusDispatcher.RequestUpdate();
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
            if (!EnsureRuntimeReady())
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(UrlTextBox.Text))
            {
                AppendEventLine(CreateLifecycleLine("LoadFileSkipped", "媒體來源不可為空白。"));
                return;
            }

            try
            {
                _eventBridge?.WriteLifecycle("LoadFile", UrlTextBox.Text);
                _playerHost?.LoadFile(UrlTextBox.Text, MpvLoadFileMode.Replace);
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
            if (!EnsureRuntimeReady())
            {
                return;
            }

            if (_playerHost?.Player != null)
            {
                _eventBridge?.WriteLifecycle("Pause", "切換播放器暫停狀態。");
                _playerHost.Player.Pause = !_playerHost.Player.Pause;
            }
        }

        /// <summary>
        /// 停止目前播放項目。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void StopClick(object sender, RoutedEventArgs e)
        {
            if (!EnsureRuntimeReady())
            {
                return;
            }

            _eventBridge?.WriteLifecycle("Stop", "停止目前播放項目。");
            _playerHost?.Player?.Stop();
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
                if (_playerHost != null)
                {
                    SampleFeatureController.ApplyYtdlpFormat(_playerHost.PlayerOptions, choice);
                }

                if (_playerHost?.Player != null)
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
        private async void LuaClick(object sender, RoutedEventArgs e)
        {
            await RunFeatureAsync(() => _features.LoadSampleLuaScriptAsync()).ConfigureAwait(true);
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
        /// 處理 FFmpeg 診斷按鈕點選事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private async void FFmpegClick(object sender, RoutedEventArgs e)
        {
            await RunFeatureAsync(() => _features.RunFFmpegDiagnosticsAsync()).ConfigureAwait(true);
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
            if (!EnsureRuntimeReady())
            {
                return;
            }

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
            if (!EnsureRuntimeReady())
            {
                return;
            }

            if (!_asyncFeatureGate.TryEnter())
            {
                AppendEventLine(CreateLifecycleLine("FeatureBusy", "已有非同步功能正在執行。"));
                return;
            }

            SetFeatureButtonsEnabled(false);
            try
            {
                await action().ConfigureAwait(true);
                _statusDispatcher.RequestUpdate();
            }
            catch (Exception ex)
            {
                AppendEventLine(CreateLifecycleLine("FeatureError", ex.Message));
            }
            finally
            {
                _asyncFeatureGate.Exit();
                if (_runtimeReady)
                {
                    SetFeatureButtonsEnabled(true);
                }
            }
        }

        /// <summary>
        /// 確認 runtime 已完成初始化。
        /// </summary>
        /// <returns>runtime 已就緒時為 <see langword="true"/>。</returns>
        private bool EnsureRuntimeReady()
        {
            if (_runtimeReady)
            {
                return true;
            }

            AppendEventLine(CreateLifecycleLine("RuntimePending", "runtime 尚未初始化完成。"));
            return false;
        }

        /// <summary>
        /// 遞迴登錄需要等 runtime 就緒後才可操作的控制項。
        /// </summary>
        /// <param name="dependencyObject">要掃描的視覺樹節點。</param>
        private void RegisterRuntimeControls(DependencyObject dependencyObject)
        {
            Control? control = dependencyObject as Control;
            if (control is Button || control is ComboBox)
            {
                _runtimeControls.Add(control);
            }

            int childCount = VisualTreeHelper.GetChildrenCount(dependencyObject);
            for (int index = 0; index < childCount; index++)
            {
                RegisterRuntimeControls(VisualTreeHelper.GetChild(dependencyObject, index));
            }
        }

        /// <summary>
        /// 遞迴登錄非同步功能進行中需暫時禁用的按鈕。
        /// </summary>
        /// <param name="dependencyObject">要掃描的視覺樹節點。</param>
        private void RegisterFeatureButtons(DependencyObject dependencyObject)
        {
            Button? button = dependencyObject as Button;
            if (button != null)
            {
                _featureButtons.Add(button);
            }

            int childCount = VisualTreeHelper.GetChildrenCount(dependencyObject);
            for (int index = 0; index < childCount; index++)
            {
                RegisterFeatureButtons(VisualTreeHelper.GetChild(dependencyObject, index));
            }
        }

        /// <summary>
        /// 設定 runtime 相關控制項是否可操作。
        /// </summary>
        /// <param name="enabled">控制項可操作時為 <see langword="true"/>。</param>
        private void SetRuntimeControlsEnabled(bool enabled)
        {
            foreach (Control control in _runtimeControls)
            {
                control.IsEnabled = enabled;
            }
        }

        /// <summary>
        /// 設定非同步功能進行中需暫時禁用的按鈕是否可操作。
        /// </summary>
        /// <param name="enabled">按鈕可操作時為 <see langword="true"/>。</param>
        private void SetFeatureButtonsEnabled(bool enabled)
        {
            foreach (Button button in _featureButtons)
            {
                button.IsEnabled = enabled;
            }
        }

        /// <summary>
        /// 將目前選擇的 yt-dlp 格式套用到指定播放器選項。
        /// </summary>
        /// <param name="options">要套用格式的播放器選項。</param>
        private void ApplySelectedYtdlpFormatToPlayerOptions(MpvPlayerOptions options)
        {
            SampleYtdlpFormatChoice? selectedChoice = FormatComboBox.SelectedItem as SampleYtdlpFormatChoice;
            if (selectedChoice != null)
            {
                SampleFeatureController.ApplyYtdlpFormat(options, selectedChoice);
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
            EventListBox.BeginInit();
            try
            {
                foreach (string line in lines)
                {
                    EventListBox.Items.Add(line);
                }

                while (EventListBox.Items.Count > EventLogLimit)
                {
                    EventListBox.Items.RemoveAt(0);
                }
            }
            finally
            {
                EventListBox.EndInit();
            }

            if (EventListBox.Items.Count > 0)
            {
                object lastItem = EventListBox.Items[EventListBox.Items.Count - 1]!;
                EventListBox.ScrollIntoView(lastItem);
            }
        }

        /// <summary>
        /// 將事件清單更新排入 WPF UI 執行緒。
        /// </summary>
        /// <param name="action">要在 UI 執行緒執行的更新。</param>
        /// <returns>成功排入 UI 執行緒時為 <see langword="true"/>。</returns>
        private bool ScheduleEventLogFlush(Action action)
        {
            return ScheduleUiUpdate(action);
        }

        /// <summary>
        /// 將指定動作排入 WPF UI 執行緒。
        /// </summary>
        /// <param name="action">要在 UI 執行緒執行的動作。</param>
        /// <returns>成功排入 UI 執行緒時為 <see langword="true"/>。</returns>
        private bool ScheduleUiUpdate(Action action)
        {
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
            {
                return false;
            }

            try
            {
                DispatcherOperation operation = Dispatcher.BeginInvoke(action, DispatcherPriority.Background);
                return operation.Status != DispatcherOperationStatus.Aborted;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
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
                if (string.Equals(choice.Selector, defaultChoice.Selector, StringComparison.Ordinal))
                {
                    selectedIndex = index;
                }
            }

            FormatComboBox.SelectedIndex = selectedIndex;
        }
    }
}

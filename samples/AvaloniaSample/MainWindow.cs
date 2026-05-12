using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using MediaEmbedKit.Mpv;
using MediaEmbedKit.Mpv.Avalonia;
using MediaEmbedKit.Mpv.Samples;

namespace MediaEmbedKit.Mpv.Samples.Avalonia
{
    /// <summary>
    /// 表示 Avalonia 範例的主要視窗。
    /// </summary>
    public sealed class MainWindow : Window
    {
        /// <summary>
        /// 範例事件輸出的最大保留列數。
        /// </summary>
        private const int EventLogLimit = 60;

        /// <summary>
        /// 顯示 libmpv 視訊內容的 Avalonia OpenGL 控制項。
        /// </summary>
        private readonly MpvAvaloniaPlayer _player;
        /// <summary>
        /// 讓使用者輸入檔案路徑或媒體網址的文字方塊。
        /// </summary>
        private readonly TextBox _sourceBox;
        /// <summary>
        /// 載入目前媒體來源的按鈕。
        /// </summary>
        private readonly Button _loadButton;
        /// <summary>
        /// 切換目前播放器暫停狀態的按鈕。
        /// </summary>
        private readonly Button _pauseButton;
        /// <summary>
        /// 停止目前播放項目的按鈕。
        /// </summary>
        private readonly Button _stopButton;
        /// <summary>
        /// 選擇 yt-dlp 格式預設值的下拉選單。
        /// </summary>
        private readonly ComboBox _formatComboBox;
        /// <summary>
        /// 顯示目前播放狀態的文字區塊。
        /// </summary>
        private readonly TextBlock _statusTextBlock;
        /// <summary>
        /// 顯示 libmpv 事件與範例生命週期的文字框。
        /// </summary>
        private readonly TextBox _eventTextBox;
        /// <summary>
        /// 顯示在 UI 的事件文字列集合。
        /// </summary>
        private readonly List<string> _eventLines = new List<string>();
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
            Title = "MediaEmbedKit.Mpv Avalonia Sample";
            Width = SampleRuntime.SampleWindowWidth;
            Height = SampleRuntime.SampleWindowHeight;
            MinWidth = SampleRuntime.SampleWindowWidth;
            MinHeight = SampleRuntime.SampleWindowHeight;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Brushes.Black;

            _sourceBox = new TextBox
            {
                Text = SampleRuntime.PlaybackUrl,
                VerticalContentAlignment = VerticalAlignment.Center,
                MinHeight = SampleRuntime.SampleButtonHeight,
                Margin = new Thickness(0, 0, SampleRuntime.SampleControlSpacing, 0)
            };

            _loadButton = CreateCommandButton("Load");
            _loadButton.Click += OnLoadClicked;
            _pauseButton = CreateCommandButton("Pause");
            _pauseButton.Click += OnPauseClicked;
            _stopButton = CreateCommandButton("Stop");
            _stopButton.Margin = new Thickness(0);
            _stopButton.Click += OnStopClicked;

            _player = new MpvAvaloniaPlayer
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            _player.PlayerCreated += PlayerCreated;
            SampleRuntime.CopyTo(SampleRuntime.PlayerOptions, _player.PlayerOptions);

            _features = new SampleFeatureController(() => _currentPlayer, AppendEventLine);
            _formatComboBox = CreateFormatComboBox();
            _statusTextBlock = new TextBlock
            {
                Width = 380,
                Height = SampleRuntime.SampleButtonHeight,
                Margin = new Thickness(0, 0, SampleRuntime.SampleControlSpacing, 4),
                Padding = new Thickness(8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.Parse("#E6E6E6")),
                Background = new SolidColorBrush(Color.Parse("#222222")),
                Text = "播放器尚未初始化",
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            _eventTextBox = new TextBox
            {
                Background = new SolidColorBrush(Color.Parse("#161616")),
                Foreground = new SolidColorBrush(Color.Parse("#E6E6E6")),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.NoWrap
            };

            _eventLogDispatcher = new SampleEventLogDispatcher(AppendEventLines, ScheduleEventLogFlush);
            _statusDispatcher = new SampleStatusUpdateDispatcher(() => _features.GetStatusText(), SetStatusText, ScheduleUiUpdate);

            Content = CreateLayout();
            Opened += WindowOpened;
            AppendEventLine(CreateLifecycleLine("WindowCreated", "Avalonia 視窗已建立，等待 OpenGL render API 建立播放器。"));
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
            _player.PlayerCreated -= PlayerCreated;
            _currentPlayer = null;
            base.OnClosed(e);
        }

        /// <summary>
        /// 在視窗開啟後載入預設媒體並執行冒煙測試。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private async void WindowOpened(object? sender, EventArgs e)
        {
            try
            {
                AppendEventLine(CreateLifecycleLine("Opened", "視窗已開啟，準備載入預設媒體來源。"));
                StartPlayback();
                if (SampleRuntime.IsSmokeTestEnabled && !_smokeStarted)
                {
                    _smokeStarted = true;
                    await RunSmokeAsync().ConfigureAwait(true);
                }
            }
            catch (Exception ex)
            {
                AppendEventLine(CreateLifecycleLine("OpenedError", ex.Message));
            }
        }

        /// <summary>
        /// 處理播放器建立事件並開始輸出 libmpv 事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void PlayerCreated(object? sender, EventArgs e)
        {
            _eventBridge?.Dispose();
            if (_player.Player != null)
            {
                _currentPlayer = _player.Player;
                _eventBridge = new SamplePlayerEventBridge(_player.Player, AppendEventLine);
                _statusDispatcher.RequestUpdate();
            }
        }

        /// <summary>
        /// 處理載入按鈕點選事件並載入輸入的媒體來源。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void OnLoadClicked(object? sender, RoutedEventArgs e)
        {
            LoadCurrentSource();
        }

        /// <summary>
        /// 處理暫停按鈕點選事件並切換播放器暫停狀態。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void OnPauseClicked(object? sender, RoutedEventArgs e)
        {
            if (_player.Player != null)
            {
                _eventBridge?.WriteLifecycle("Pause", "切換播放器暫停狀態。");
                _player.Player.Pause = !_player.Player.Pause;
            }
        }

        /// <summary>
        /// 處理停止按鈕點選事件並停止目前播放項目。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void OnStopClicked(object? sender, RoutedEventArgs e)
        {
            _eventBridge?.WriteLifecycle("Stop", "停止目前播放項目。");
            _player.Player?.Stop();
        }

        /// <summary>
        /// 在格式選項變更時套用 yt-dlp 格式。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void FormatComboBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            ComboBox? comboBox = sender as ComboBox;
            SampleYtdlpFormatChoice? choice = comboBox?.SelectedItem as SampleYtdlpFormatChoice;
            if (choice == null)
            {
                return;
            }

            try
            {
                SampleFeatureController.ApplyYtdlpFormat(_player.PlayerOptions, choice);
                if (_player.Player != null)
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
            if (!string.IsNullOrWhiteSpace(_sourceBox.Text))
            {
                _eventBridge?.WriteLifecycle("LoadFile", _sourceBox.Text);
                _player.LoadFile(_sourceBox.Text, MpvLoadFileMode.Replace);
            }
        }

        /// <summary>
        /// 執行 Avalonia 範例播放冒煙測試。
        /// </summary>
        /// <returns>代表冒煙測試流程的工作。</returns>
        private Task RunSmokeAsync()
        {
            return SampleRuntime.RunSmokeUntilPlaybackAsync("AvaloniaSample", () => _player.Player, Close);
        }

        /// <summary>
        /// 建立範例視窗版面。
        /// </summary>
        /// <returns>包含工具列、功能列、播放區域與事件清單的根版面。</returns>
        private Grid CreateLayout()
        {
            Grid root = new Grid
            {
                Background = Brushes.Black,
                RowDefinitions =
                {
                    new RowDefinition(new GridLength(SampleRuntime.SampleToolbarHeight, GridUnitType.Pixel)),
                    new RowDefinition(new GridLength(SampleRuntime.SampleFeaturePanelHeight, GridUnitType.Pixel)),
                    new RowDefinition(new GridLength(1, GridUnitType.Star)),
                    new RowDefinition(new GridLength(SampleRuntime.SampleEventLogHeight, GridUnitType.Pixel))
                }
            };

            Grid toolbar = CreateToolbar();
            Control featurePanel = CreateFeaturePanel();
            Grid playerSurface = CreatePlayerSurface();

            root.Children.Add(toolbar);
            Grid.SetRow(featurePanel, 1);
            root.Children.Add(featurePanel);
            Grid.SetRow(playerSurface, 2);
            root.Children.Add(playerSurface);
            Grid.SetRow(_eventTextBox, 3);
            root.Children.Add(_eventTextBox);
            return root;
        }

        /// <summary>
        /// 建立範例工具列。
        /// </summary>
        /// <returns>包含來源輸入與播放命令的工具列。</returns>
        private Grid CreateToolbar()
        {
            Grid toolbar = new Grid
            {
                Margin = new Thickness(SampleRuntime.SampleControlPadding),
                ColumnDefinitions =
                {
                    new ColumnDefinition(new GridLength(1, GridUnitType.Star)),
                    new ColumnDefinition(new GridLength(SampleRuntime.SampleButtonWidth, GridUnitType.Pixel)),
                    new ColumnDefinition(new GridLength(SampleRuntime.SampleControlSpacing, GridUnitType.Pixel)),
                    new ColumnDefinition(new GridLength(SampleRuntime.SampleButtonWidth, GridUnitType.Pixel)),
                    new ColumnDefinition(new GridLength(SampleRuntime.SampleControlSpacing, GridUnitType.Pixel)),
                    new ColumnDefinition(new GridLength(SampleRuntime.SampleButtonWidth, GridUnitType.Pixel))
                }
            };

            toolbar.Children.Add(_sourceBox);
            Grid.SetColumn(_loadButton, 1);
            toolbar.Children.Add(_loadButton);
            Grid.SetColumn(_pauseButton, 3);
            toolbar.Children.Add(_pauseButton);
            Grid.SetColumn(_stopButton, 5);
            toolbar.Children.Add(_stopButton);
            return toolbar;
        }

        /// <summary>
        /// 建立進階功能展示列。
        /// </summary>
        /// <returns>包含格式、狀態與 API 按鈕的功能列。</returns>
        private Control CreateFeaturePanel()
        {
            WrapPanel panel = new WrapPanel
            {
                Background = new SolidColorBrush(Color.Parse("#181818")),
                Margin = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Orientation = Orientation.Horizontal
            };

            panel.Children.Add(_formatComboBox);
            panel.Children.Add(_statusTextBlock);
            panel.Children.Add(CreateFeatureButton("OSD", () => _features.ShowOsd()));
            panel.Children.Add(CreateFeatureButton("-10s", () => _features.SeekRelative(-10)));
            panel.Children.Add(CreateFeatureButton("+10s", () => _features.SeekRelative(10)));
            panel.Children.Add(CreateFeatureButton("Vol-", () => _features.ChangeVolume(-5)));
            panel.Children.Add(CreateFeatureButton("Vol+", () => _features.ChangeVolume(5)));
            panel.Children.Add(CreateFeatureButton("Mute", () => _features.ToggleMute()));
            panel.Children.Add(CreateFeatureButton("Speed", () => _features.CycleSpeed()));
            panel.Children.Add(CreateFeatureButton("Sub", () => _features.AddSampleSubtitle()));
            panel.Children.Add(CreateFeatureButton("Tracks", () => _features.DumpTracks()));
            panel.Children.Add(CreateFeatureButton("Shot", () => _features.TakeScreenshot()));
            panel.Children.Add(CreateFeatureButton("Config", () => _features.LoadSampleConfig()));
            panel.Children.Add(CreateAsyncFeatureButton("Lua", () => _features.LoadSampleLuaScriptAsync()));
            panel.Children.Add(CreateAsyncFeatureButton("yt-dlp", () => _features.RunYtdlpDiagnosticsAsync(_sourceBox.Text ?? string.Empty)));
            panel.Children.Add(CreateAsyncFeatureButton("Deno", () => _features.RunDenoDiagnosticsAsync()));
            panel.Children.Add(CreateAsyncFeatureButton("Update yt", () => _features.RunYtdlpSelfUpdateAsync(), SampleRuntime.SampleYtdlpUpdateButtonWidth));
            panel.Children.Add(CreateAsyncFeatureButton("Update Deno", () => _features.RunDenoSelfUpgradeAsync(), SampleRuntime.SampleDenoUpdateButtonWidth));
            return panel;
        }

        /// <summary>
        /// 建立播放區域與 Avalonia 覆蓋層展示。
        /// </summary>
        /// <returns>包含單一播放器、安全覆蓋層與一般覆蓋層對照的播放區域。</returns>
        private Grid CreatePlayerSurface()
        {
            Grid playerSurface = new Grid
            {
                Background = Brushes.Black,
                RowDefinitions =
                {
                    new RowDefinition(new GridLength(SampleRuntime.SampleAirspaceComparisonHeight, GridUnitType.Pixel)),
                    new RowDefinition(new GridLength(1, GridUnitType.Star))
                }
            };

            Grid header = CreateHeaderPanel();
            Grid videoSurface = CreateVideoSurface();
            Grid.SetRow(videoSurface, 1);
            playerSurface.Children.Add(header);
            playerSurface.Children.Add(videoSurface);
            return playerSurface;
        }

        /// <summary>
        /// 建立左右對照標題列。
        /// </summary>
        /// <returns>包含安全覆蓋層與一般覆蓋層標題的面板。</returns>
        private static Grid CreateHeaderPanel()
        {
            Grid header = new Grid
            {
                Background = new SolidColorBrush(Color.Parse("#101010")),
                ColumnDefinitions =
                {
                    new ColumnDefinition(new GridLength(1, GridUnitType.Star)),
                    new ColumnDefinition(new GridLength(1, GridUnitType.Star))
                }
            };

            Border safeHeader = CreateHeaderBadge("Avalonia OpenGL 覆蓋層：同層組合", "#DD0078D4", new Thickness(16, 6, 8, 6));
            Border normalHeader = CreateHeaderBadge("一般 Avalonia Overlay 嘗試覆蓋同一個 OpenGL 控制項", "#DD5C2D91", new Thickness(8, 6, 16, 6));
            header.Children.Add(safeHeader);
            Grid.SetColumn(normalHeader, 1);
            header.Children.Add(normalHeader);
            return header;
        }

        /// <summary>
        /// 建立播放視訊與覆蓋層對照面板。
        /// </summary>
        /// <returns>包含播放器、安全覆蓋層與一般覆蓋層的播放面板。</returns>
        private Grid CreateVideoSurface()
        {
            Grid videoSurface = new Grid
            {
                Background = Brushes.Black
            };

            Border safeOverlay = CreateOverlayBadge("Avalonia OpenGL render API 覆蓋層", "#DD0078D4", HorizontalAlignment.Left);
            Border normalOverlay = CreateOverlayBadge("一般 Avalonia Overlay：同一播放區對照", "#DD5C2D91", HorizontalAlignment.Right);
            videoSurface.Children.Add(_player);
            videoSurface.Children.Add(safeOverlay);
            videoSurface.Children.Add(normalOverlay);
            return videoSurface;
        }

        /// <summary>
        /// 建立播放區中的覆蓋層標籤。
        /// </summary>
        /// <param name="text">要顯示的標籤文字。</param>
        /// <param name="background">標籤背景色彩。</param>
        /// <param name="alignment">標籤水平對齊方式。</param>
        /// <returns>已套用固定尺寸與色彩的覆蓋層標籤。</returns>
        private static Border CreateOverlayBadge(string text, string background, HorizontalAlignment alignment)
        {
            return new Border
            {
                Width = SampleRuntime.SampleOverlayBadgeWidth,
                Height = SampleRuntime.SampleOverlayBadgeHeight,
                Margin = new Thickness(16),
                HorizontalAlignment = alignment,
                VerticalAlignment = VerticalAlignment.Top,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Color.Parse(background)),
                Child = new TextBlock
                {
                    Text = text,
                    Foreground = Brushes.White,
                    FontWeight = FontWeight.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
        }

        /// <summary>
        /// 建立 AirSpace 對照區標題列。
        /// </summary>
        /// <param name="text">要顯示的標題文字。</param>
        /// <param name="background">標題背景色彩。</param>
        /// <param name="margin">標題外距。</param>
        /// <returns>已套用固定色彩與邊界的標題。</returns>
        private static Border CreateHeaderBadge(string text, string background, Thickness margin)
        {
            return new Border
            {
                Margin = margin,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Color.Parse(background)),
                Child = new TextBlock
                {
                    Text = text,
                    Foreground = Brushes.White,
                    FontWeight = FontWeight.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
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
            if (!_asyncFeatureGate.TryEnter())
            {
                AppendEventLine(CreateLifecycleLine("FeatureBusy", "已有非同步功能正在執行。"));
                return;
            }

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
        /// 批次加入事件文字列到 UI 文字框。
        /// </summary>
        /// <param name="lines">要加入事件清單的文字列集合。</param>
        private void AppendEventLines(IReadOnlyList<string> lines)
        {
            _eventLines.AddRange(lines);
            while (_eventLines.Count > EventLogLimit)
            {
                _eventLines.RemoveAt(0);
            }

            string text = string.Join(Environment.NewLine, _eventLines);
            _eventTextBox.Text = text;
            _eventTextBox.CaretIndex = text.Length;
        }

        /// <summary>
        /// 將事件清單更新排入 Avalonia UI 執行緒。
        /// </summary>
        /// <param name="action">要在 UI 執行緒執行的更新。</param>
        /// <returns>成功排入 UI 執行緒時為 <see langword="true"/>。</returns>
        private static bool ScheduleEventLogFlush(Action action)
        {
            return ScheduleUiUpdate(action);
        }

        /// <summary>
        /// 將指定動作排入 Avalonia UI 執行緒。
        /// </summary>
        /// <param name="action">要在 UI 執行緒執行的動作。</param>
        /// <returns>成功排入 UI 執行緒時為 <see langword="true"/>。</returns>
        private static bool ScheduleUiUpdate(Action action)
        {
            try
            {
                DispatcherOperation operation = Dispatcher.UIThread.InvokeAsync(action, DispatcherPriority.Background);
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
            _statusTextBlock.Text = text;
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
        /// 建立 yt-dlp 格式下拉選單。
        /// </summary>
        /// <returns>已填入格式選項的下拉選單。</returns>
        private ComboBox CreateFormatComboBox()
        {
            IReadOnlyList<SampleYtdlpFormatChoice> choices = SampleFeatureController.CreateYtdlpFormatChoices();
            SampleYtdlpFormatChoice defaultChoice = SampleFeatureController.CreateDefaultYtdlpFormatChoice();
            int selectedIndex = 0;
            ComboBox comboBox = new ComboBox
            {
                Width = 132,
                Height = SampleRuntime.SampleButtonHeight,
                Margin = new Thickness(SampleRuntime.SampleControlPadding, 4, SampleRuntime.SampleControlSpacing, 4),
                ItemsSource = choices
            };

            for (int index = 0; index < choices.Count; index++)
            {
                if (string.Equals(choices[index].Selector, defaultChoice.Selector, StringComparison.Ordinal))
                {
                    selectedIndex = index;
                }
            }

            comboBox.SelectedIndex = selectedIndex;
            if (comboBox.SelectedItem is SampleYtdlpFormatChoice selectedChoice)
            {
                SampleFeatureController.ApplyYtdlpFormat(_player.PlayerOptions, selectedChoice);
            }

            comboBox.SelectionChanged += FormatComboBoxSelectionChanged;
            return comboBox;
        }

        /// <summary>
        /// 建立標準尺寸的命令按鈕。
        /// </summary>
        /// <param name="text">要顯示在按鈕上的文字。</param>
        /// <returns>已套用範例標準尺寸的按鈕。</returns>
        private static Button CreateCommandButton(string text)
        {
            return new Button
            {
                Content = text,
                MinWidth = SampleRuntime.SampleButtonWidth,
                MinHeight = SampleRuntime.SampleButtonHeight,
                Background = new SolidColorBrush(Color.Parse("#303030")),
                BorderBrush = new SolidColorBrush(Color.Parse("#E0E0E0")),
                BorderThickness = new Thickness(1),
                Foreground = Brushes.White,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
        }

        /// <summary>
        /// 建立同步功能按鈕。
        /// </summary>
        /// <param name="text">按鈕文字。</param>
        /// <param name="action">點選時要執行的功能。</param>
        /// <returns>已建立的功能按鈕。</returns>
        private Button CreateFeatureButton(string text, Action action)
        {
            Button button = CreateFeatureButtonCore(text, SampleRuntime.SampleFeatureButtonWidth);
            button.Click += (sender, e) => RunFeature(action);
            return button;
        }

        /// <summary>
        /// 建立非同步功能按鈕。
        /// </summary>
        /// <param name="text">按鈕文字。</param>
        /// <param name="action">點選時要執行的非同步功能。</param>
        /// <param name="width">按鈕寬度。</param>
        /// <returns>已建立的功能按鈕。</returns>
        private Button CreateAsyncFeatureButton(string text, Func<Task> action, double width = SampleRuntime.SampleFeatureButtonWidth)
        {
            Button button = CreateFeatureButtonCore(text, width);
            button.Click += async (sender, e) => await RunFeatureAsync(action).ConfigureAwait(true);
            return button;
        }

        /// <summary>
        /// 建立功能按鈕共用外觀。
        /// </summary>
        /// <param name="text">按鈕文字。</param>
        /// <param name="width">按鈕寬度。</param>
        /// <returns>已套用共用外觀的按鈕。</returns>
        private static Button CreateFeatureButtonCore(string text, double width)
        {
            return new Button
            {
                Content = text,
                Width = width,
                Height = SampleRuntime.SampleButtonHeight,
                Background = new SolidColorBrush(Color.Parse("#303030")),
                BorderBrush = new SolidColorBrush(Color.Parse("#E0E0E0")),
                BorderThickness = new Thickness(1),
                Foreground = Brushes.White,
                Margin = new Thickness(0, 4, SampleRuntime.SampleControlSpacing, 4),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using MediaEmbedKit.Mpv;
using MediaEmbedKit.Mpv.Maui;
using MediaEmbedKit.Mpv.Samples;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Layouts;

namespace MediaEmbedKit.Mpv.Samples.Maui
{
    /// <summary>
    /// 表示 .NET MAUI 範例的主要頁面。
    /// </summary>
    public sealed class MainPage : ContentPage
    {
        /// <summary>
        /// 範例事件輸出的最大保留列數。
        /// </summary>
        private const int EventLogLimit = 200;

        /// <summary>
        /// 顯示 libmpv 視訊內容的 MAUI 檢視。
        /// </summary>
        private readonly MpvView _player;
        /// <summary>
        /// 讓使用者輸入檔案路徑或媒體網址的輸入方塊。
        /// </summary>
        private readonly Entry _sourceEntry;
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
        /// 選擇 yt-dlp 格式預設值的選擇器。
        /// </summary>
        private readonly Picker _formatPicker;
        /// <summary>
        /// 顯示目前播放狀態的標籤。
        /// </summary>
        private readonly Label _statusLabel;
        /// <summary>
        /// 顯示 libmpv 事件與範例生命週期的清單。
        /// </summary>
        private readonly CollectionView _eventList;
        /// <summary>
        /// 顯示在 UI 的事件文字列集合。
        /// </summary>
        private readonly ObservableCollection<string> _eventLines = new ObservableCollection<string>();
        /// <summary>
        /// 範例可選擇的 yt-dlp 格式清單。
        /// </summary>
        private readonly IReadOnlyList<SampleYtdlpFormatChoice> _formatChoices;
        /// <summary>
        /// 範例進階功能控制器。
        /// </summary>
        private readonly SampleFeatureController _features;
        /// <summary>
        /// 週期性更新狀態列的計時器。
        /// </summary>
        private readonly IDispatcherTimer _statusTimer;
        /// <summary>
        /// 將播放器事件轉接到範例事件清單。
        /// </summary>
        private SamplePlayerEventBridge? _eventBridge;
        /// <summary>
        /// 表示預設媒體是否已載入。
        /// </summary>
        private bool _initialSourceLoaded;
        /// <summary>
        /// 表示冒煙測試是否已啟動。
        /// </summary>
        private bool _smokeStarted;

        /// <summary>
        /// 初始化 <see cref="MainPage"/> 類別的新執行個體。
        /// </summary>
        public MainPage()
        {
            Title = "MediaEmbedKit.Mpv MAUI Sample";
            BackgroundColor = Colors.Black;

            _sourceEntry = new Entry
            {
                Text = SampleRuntime.PlaybackUrl,
                HeightRequest = SampleRuntime.SampleButtonHeight,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Center
            };

            _player = new MpvView
            {
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill
            };
            _player.PlayerCreated += PlayerCreated;
            SampleRuntime.CopyTo(SampleRuntime.PlayerOptions, _player.PlayerOptions);

            _features = new SampleFeatureController(() => _player.Player, AppendEventLine);
            _formatChoices = SampleFeatureController.CreateYtdlpFormatChoices();
            _formatPicker = CreateFormatPicker();
            _statusLabel = new Label
            {
                Text = "播放器尚未初始化",
                WidthRequest = 380,
                HeightRequest = SampleRuntime.SampleButtonHeight,
                Padding = new Thickness(8, 0),
                BackgroundColor = Color.FromArgb("#222222"),
                TextColor = Color.FromArgb("#E6E6E6"),
                VerticalTextAlignment = TextAlignment.Center,
                LineBreakMode = LineBreakMode.TailTruncation
            };

            _loadButton = CreateCommandButton("Load");
            _loadButton.Clicked += LoadButtonClicked;
            _pauseButton = CreateCommandButton("Pause");
            _pauseButton.Clicked += PauseButtonClicked;
            _stopButton = CreateCommandButton("Stop");
            _stopButton.Clicked += StopButtonClicked;

            _eventList = new CollectionView
            {
                ItemsSource = _eventLines,
                ItemTemplate = CreateEventTemplate(),
                BackgroundColor = Color.FromArgb("#161616")
            };

            _statusTimer = Dispatcher.CreateTimer();
            _statusTimer.Interval = TimeSpan.FromSeconds(1);
            _statusTimer.Tick += StatusTimerTick;

            Content = CreateLayout();
            AppendEventLine(CreateLifecycleLine("PageCreated", "MAUI 頁面已建立，等待 Windows handler 建立播放器。"));
        }

        /// <summary>
        /// 在 MAUI 頁面顯示後載入預設媒體並執行冒煙測試。
        /// </summary>
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            _statusTimer.Start();
            if (_player.Player != null && _eventBridge == null)
            {
                AttachPlayerEvents(_player.Player);
            }

            if (!_initialSourceLoaded)
            {
                _initialSourceLoaded = true;
                AppendEventLine(CreateLifecycleLine("Appearing", "頁面已顯示，準備載入預設媒體來源。"));
                LoadCurrentSource();
            }

            if (SampleRuntime.IsSmokeTestEnabled && !_smokeStarted)
            {
                _smokeStarted = true;
                await RunSmokeAsync().ConfigureAwait(true);
            }
        }

        /// <summary>
        /// 在 MAUI 頁面離開畫面時釋放事件橋接器。
        /// </summary>
        protected override void OnDisappearing()
        {
            _statusTimer.Stop();
            _eventBridge?.WriteLifecycle("Disappearing", "頁面即將離開畫面，準備取消事件訂閱。");
            _eventBridge?.Dispose();
            _eventBridge = null;
            base.OnDisappearing();
        }

        /// <summary>
        /// 執行 MAUI Windows 範例播放冒煙測試。
        /// </summary>
        /// <returns>代表冒煙測試流程的工作。</returns>
        private Task RunSmokeAsync()
        {
            return SampleRuntime.RunSmokeUntilPlaybackAsync("MauiSample", () => _player.Player, CloseApplication);
        }

        /// <summary>
        /// 處理播放器建立事件並開始輸出 libmpv 事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void PlayerCreated(object? sender, EventArgs e)
        {
            if (_player.Player != null)
            {
                AttachPlayerEvents(_player.Player);
            }
        }

        /// <summary>
        /// 處理載入按鈕點選事件並載入輸入的媒體來源。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void LoadButtonClicked(object? sender, EventArgs e)
        {
            LoadCurrentSource();
        }

        /// <summary>
        /// 切換目前播放器的暫停狀態。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void PauseButtonClicked(object? sender, EventArgs e)
        {
            if (_player.Player != null)
            {
                _eventBridge?.WriteLifecycle("Pause", "切換播放器暫停狀態。");
                _player.Player.Pause = !_player.Player.Pause;
            }
        }

        /// <summary>
        /// 停止目前播放項目。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void StopButtonClicked(object? sender, EventArgs e)
        {
            _eventBridge?.WriteLifecycle("Stop", "停止目前播放項目。");
            _player.Player?.Stop();
        }

        /// <summary>
        /// 在格式選項變更時套用 yt-dlp 格式。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void FormatPickerSelectedIndexChanged(object? sender, EventArgs e)
        {
            Picker? picker = sender as Picker;
            if (picker == null)
            {
                return;
            }

            int selectedIndex = picker.SelectedIndex;
            if (selectedIndex < 0 || selectedIndex >= _formatChoices.Count)
            {
                return;
            }

            SampleYtdlpFormatChoice choice = _formatChoices[selectedIndex];
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
        /// 更新播放狀態列。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void StatusTimerTick(object? sender, EventArgs e)
        {
            _statusLabel.Text = _features.GetStatusText();
        }

        /// <summary>
        /// 載入目前輸入的媒體來源。
        /// </summary>
        private void LoadCurrentSource()
        {
            string source = _sourceEntry.Text ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(source))
            {
                _eventBridge?.WriteLifecycle("LoadFile", source);
                _player.LoadFile(source, MpvLoadFileMode.Replace);
            }
        }

        /// <summary>
        /// 建立範例根版面。
        /// </summary>
        /// <returns>包含工具列、功能列、播放區域與事件清單的根版面。</returns>
        private Grid CreateLayout()
        {
            Grid root = new Grid
            {
                RowDefinitions =
                {
                    new RowDefinition(GridLength.Auto),
                    new RowDefinition(new GridLength(92)),
                    new RowDefinition(GridLength.Star),
                    new RowDefinition(new GridLength(152))
                }
            };

            root.Add(CreateToolbar(), 0, 0);
            root.Add(CreateFeaturePanel(), 0, 1);
            root.Add(CreatePlayerSurface(), 0, 2);
            root.Add(_eventList, 0, 3);
            return root;
        }

        /// <summary>
        /// 建立範例工具列。
        /// </summary>
        /// <returns>包含來源輸入與播放命令的工具列。</returns>
        private Grid CreateToolbar()
        {
            Grid controls = new Grid
            {
                HeightRequest = SampleRuntime.SampleToolbarHeight,
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(new GridLength(SampleRuntime.SampleButtonWidth)),
                    new ColumnDefinition(new GridLength(SampleRuntime.SampleButtonWidth)),
                    new ColumnDefinition(new GridLength(SampleRuntime.SampleButtonWidth))
                },
                ColumnSpacing = SampleRuntime.SampleControlSpacing,
                Padding = SampleRuntime.SampleControlPadding,
                BackgroundColor = Color.FromArgb("#181818")
            };
            controls.Add(_sourceEntry, 0, 0);
            controls.Add(_loadButton, 1, 0);
            controls.Add(_pauseButton, 2, 0);
            controls.Add(_stopButton, 3, 0);
            return controls;
        }

        /// <summary>
        /// 建立進階功能展示列。
        /// </summary>
        /// <returns>包含格式、狀態與 API 按鈕的功能列。</returns>
        private FlexLayout CreateFeaturePanel()
        {
            FlexLayout panel = new FlexLayout
            {
                Direction = FlexDirection.Row,
                Wrap = FlexWrap.Wrap,
                AlignItems = FlexAlignItems.Center,
                Padding = new Thickness(SampleRuntime.SampleControlPadding, 4),
                BackgroundColor = Color.FromArgb("#181818")
            };

            panel.Children.Add(_formatPicker);
            panel.Children.Add(_statusLabel);
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
            panel.Children.Add(CreateFeatureButton("Lua", () => _features.LoadSampleLuaScript()));
            panel.Children.Add(CreateAsyncFeatureButton("yt-dlp", () => _features.RunYtdlpDiagnosticsAsync(_sourceEntry.Text ?? string.Empty)));
            panel.Children.Add(CreateAsyncFeatureButton("Deno", () => _features.RunDenoDiagnosticsAsync()));
            panel.Children.Add(CreateAsyncFeatureButton("Update yt", () => _features.RunYtdlpSelfUpdateAsync(), 88));
            panel.Children.Add(CreateAsyncFeatureButton("Update Deno", () => _features.RunDenoSelfUpgradeAsync(), 104));
            return panel;
        }

        /// <summary>
        /// 建立播放區域與 MAUI 覆蓋層展示。
        /// </summary>
        /// <returns>包含播放器與覆蓋層的播放區域。</returns>
        private Grid CreatePlayerSurface()
        {
            Grid playerSurface = new Grid
            {
                BackgroundColor = Colors.Black
            };

            Label overlayLabel = new Label
            {
                Text = "一般 MAUI 覆蓋層：用來觀察 HWND AirSpace",
                BackgroundColor = Color.FromArgb("#DD5C2D91"),
                TextColor = Colors.White,
                FontAttributes = FontAttributes.Bold,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                HeightRequest = 32,
                WidthRequest = 360,
                Margin = new Thickness(16),
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Start,
                InputTransparent = true,
                ZIndex = 10
            };

            playerSurface.Add(_player, 0, 0);
            playerSurface.Add(overlayLabel, 0, 0);
            return playerSurface;
        }

        /// <summary>
        /// 將播放器事件橋接到 UI 事件清單。
        /// </summary>
        /// <param name="player">要觀察的播放器。</param>
        private void AttachPlayerEvents(MpvPlayer player)
        {
            _eventBridge?.Dispose();
            _eventBridge = new SamplePlayerEventBridge(player, AppendEventLine);
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
                _statusLabel.Text = _features.GetStatusText();
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
                _statusLabel.Text = _features.GetStatusText();
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
            if (!MainThread.IsMainThread)
            {
                MainThread.BeginInvokeOnMainThread(() => AppendEventLine(line));
                return;
            }

            _eventLines.Add(line);
            while (_eventLines.Count > EventLogLimit)
            {
                _eventLines.RemoveAt(0);
            }

            _eventList.ScrollTo(line, position: ScrollToPosition.End, animate: false);
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
        /// 建立 yt-dlp 格式選擇器。
        /// </summary>
        /// <returns>已填入格式選項的選擇器。</returns>
        private Picker CreateFormatPicker()
        {
            Picker picker = new Picker
            {
                WidthRequest = 132,
                HeightRequest = SampleRuntime.SampleButtonHeight,
                Margin = new Thickness(0, 0, SampleRuntime.SampleControlSpacing, 4),
                TextColor = Color.FromArgb("#E6E6E6"),
                BackgroundColor = Color.FromArgb("#222222")
            };

            SampleYtdlpFormatChoice defaultChoice = SampleFeatureController.CreateDefaultYtdlpFormatChoice();
            int selectedIndex = 0;
            for (int index = 0; index < _formatChoices.Count; index++)
            {
                SampleYtdlpFormatChoice choice = _formatChoices[index];
                picker.Items.Add(choice.DisplayName);
                if (choice.Preset == defaultChoice.Preset)
                {
                    selectedIndex = index;
                }
            }

            picker.SelectedIndex = selectedIndex;
            if (selectedIndex >= 0 && selectedIndex < _formatChoices.Count)
            {
                SampleFeatureController.ApplyYtdlpFormat(_player.PlayerOptions, _formatChoices[selectedIndex]);
            }

            picker.SelectedIndexChanged += FormatPickerSelectedIndexChanged;
            return picker;
        }

        /// <summary>
        /// 建立事件清單項目範本。
        /// </summary>
        /// <returns>用來顯示單列事件文字的資料範本。</returns>
        private static DataTemplate CreateEventTemplate()
        {
            return new DataTemplate(() =>
            {
                Label label = new Label
                {
                    FontFamily = "Consolas",
                    FontSize = 12,
                    TextColor = Color.FromArgb("#E6E6E6"),
                    Padding = new Thickness(4, 1)
                };
                label.SetBinding(Label.TextProperty, ".");
                return label;
            });
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
                Text = text,
                WidthRequest = SampleRuntime.SampleButtonWidth,
                HeightRequest = SampleRuntime.SampleButtonHeight,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Center
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
            Button button = CreateFeatureButtonCore(text, 76);
            button.Clicked += (sender, e) => RunFeature(action);
            return button;
        }

        /// <summary>
        /// 建立非同步功能按鈕。
        /// </summary>
        /// <param name="text">按鈕文字。</param>
        /// <param name="action">點選時要執行的非同步功能。</param>
        /// <param name="width">按鈕寬度。</param>
        /// <returns>已建立的功能按鈕。</returns>
        private Button CreateAsyncFeatureButton(string text, Func<Task> action, double width = 76)
        {
            Button button = CreateFeatureButtonCore(text, width);
            button.Clicked += async (sender, e) => await RunFeatureAsync(action).ConfigureAwait(true);
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
                Text = text,
                WidthRequest = width,
                HeightRequest = SampleRuntime.SampleButtonHeight,
                Margin = new Thickness(0, 0, SampleRuntime.SampleControlSpacing, 4),
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.Center
            };
        }

        /// <summary>
        /// 關閉 MAUI 範例應用程式。
        /// </summary>
        private static void CloseApplication()
        {
            Application.Current?.Quit();
        }
    }
}

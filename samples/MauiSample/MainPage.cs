using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using MediaEmbedKit.Mpv;
using MediaEmbedKit.Mpv.Maui;
using MediaEmbedKit.Mpv.Samples;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Layouts;

namespace MediaEmbedKit.Mpv.Samples.Maui
{
    /// <summary>
    /// 表示 .NET MAUI 範例的主要頁面。
    /// </summary>
    public sealed class MainPage : ContentPage, IDisposable
    {
        /// <summary>
        /// 範例事件輸出的最大保留列數。
        /// </summary>
        private const int EventLogLimit = 60;

        /// <summary>
        /// 顯示 libmpv 視訊內容的 MAUI 檢視。
        /// </summary>
        private MpvView? _player;
        /// <summary>
        /// runtime 就緒後承載 MAUI 播放檢視的容器。
        /// </summary>
        private readonly Grid _playerHostContainer = new Grid();
        /// <summary>
        /// 需要在 runtime 就緒後才可使用的控制項清單。
        /// </summary>
        private readonly List<VisualElement> _runtimeControls = new List<VisualElement>();
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
        /// 表示預設媒體是否已載入。
        /// </summary>
        private bool _initialSourceLoaded;
        /// <summary>
        /// 表示冒煙測試是否已啟動。
        /// </summary>
        private bool _smokeStarted;
        /// <summary>
        /// 表示頁面是否已完成最終釋放。
        /// </summary>
        private int _disposed;

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

            _features = new SampleFeatureController(() => _currentPlayer, AppendEventLine);
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
            _runtimeControls.Add(_loadButton);
            _runtimeControls.Add(_pauseButton);
            _runtimeControls.Add(_stopButton);

            _eventList = new CollectionView
            {
                ItemsSource = _eventLines,
                ItemTemplate = CreateEventTemplate(),
                BackgroundColor = Color.FromArgb("#161616")
            };

            _eventLogDispatcher = new SampleEventLogDispatcher(AppendEventLines, ScheduleEventLogFlush);
            _statusDispatcher = new SampleStatusUpdateDispatcher(() => _features.GetStatusText(), SetStatusText, ScheduleUiUpdate);

            Content = CreateLayout();
            SetRuntimeControlsEnabled(false);
            AppendEventLine(CreateLifecycleLine("PageCreated", "MAUI 頁面已建立，等待 runtime 初始化。"));
        }

        /// <summary>
        /// 在 MAUI 頁面顯示後載入預設媒體並執行冒煙測試。
        /// </summary>
        protected override async void OnAppearing()
        {
            try
            {
                base.OnAppearing();
                if (Volatile.Read(ref _disposed) != 0)
                {
                    return;
                }

                if (!_runtimeReady)
                {
                    AppendEventLine(CreateLifecycleLine("Appearing", "頁面已顯示，準備初始化範例 runtime。"));
                    bool initialized = await InitializeRuntimeAsync().ConfigureAwait(true);
                    if (!initialized)
                    {
                        return;
                    }
                }

                if (_player?.Player != null && _eventBridge == null)
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
            catch (Exception ex)
            {
                AppendEventLine(CreateLifecycleLine("AppearingError", ex.Message));
            }
        }

        /// <summary>
        /// 在 MAUI 頁面離開畫面時釋放事件橋接器。
        /// </summary>
        protected override void OnDisappearing()
        {
            _eventBridge?.WriteLifecycle("Disappearing", "頁面即將離開畫面，準備取消事件訂閱。");
            _eventBridge?.Dispose();
            _eventBridge = null;
            _currentPlayer = null;
            base.OnDisappearing();
        }

        /// <summary>
        /// 釋放 MAUI 範例頁面持有的背景分派器與播放器事件訂閱。
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _statusDispatcher.Dispose();
            _eventBridge?.WriteLifecycle("Dispose", "頁面即將釋放，準備取消事件訂閱。");
            _eventBridge?.Dispose();
            _eventBridge = null;
            _eventLogDispatcher.Dispose();
            if (_player != null)
            {
                _player.PlayerCreated -= PlayerCreated;
            }

            _currentPlayer = null;
        }

        /// <summary>
        /// 非同步初始化範例 runtime 與 MAUI 播放檢視。
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
                    SampleRuntime.WriteSmokeLine("MauiSample", "FAILED Runtime: " + ex.Message);
                    CloseApplication();
                    return false;
                }

                await DisplayAlertAsync("mpv runtime", ex.Message, "確定").ConfigureAwait(true);
                return false;
            }
        }

        /// <summary>
        /// 建立 runtime 就緒後才可初始化的 MAUI 播放檢視。
        /// </summary>
        private void CreatePlayerHost()
        {
            if (_player != null)
            {
                return;
            }

            MpvView player = new MpvView
            {
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                OverlayView = CreateMauiOverlayView(),
                IsOverlayOpen = true
            };
            SampleRuntime.CopyTo(SampleRuntime.PlayerOptions, player.PlayerOptions);
            ApplySelectedYtdlpFormatToPlayerOptions(player.PlayerOptions);
            player.PlayerCreated += PlayerCreated;
            _playerHostContainer.Add(player, 0, 0);
            _player = player;
        }

        /// <summary>
        /// 執行 MAUI Windows 範例播放冒煙測試。
        /// </summary>
        /// <returns>代表冒煙測試流程的工作。</returns>
        private Task RunSmokeAsync()
        {
            return SampleRuntime.RunSmokeUntilPlaybackAsync("MauiSample", () => _player?.Player, CloseApplication);
        }

        /// <summary>
        /// 處理播放器建立事件並開始輸出 libmpv 事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void PlayerCreated(object? sender, EventArgs e)
        {
            if (_player?.Player != null)
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
            if (!EnsureRuntimeReady())
            {
                return;
            }

            if (_player?.Player != null)
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
            if (!EnsureRuntimeReady())
            {
                return;
            }

            _eventBridge?.WriteLifecycle("Stop", "停止目前播放項目。");
            _player?.Player?.Stop();
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
                if (_player != null)
                {
                    SampleFeatureController.ApplyYtdlpFormat(_player.PlayerOptions, choice);
                }

                if (_player?.Player != null)
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
        /// 載入目前輸入的媒體來源。
        /// </summary>
        private void LoadCurrentSource()
        {
            string source = _sourceEntry.Text ?? string.Empty;
            if (!EnsureRuntimeReady())
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(source))
            {
                _eventBridge?.WriteLifecycle("LoadFile", source);
                _player?.LoadFile(source, MpvLoadFileMode.Replace);
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
                    new RowDefinition(new GridLength(SampleRuntime.SampleFeaturePanelHeight)),
                    new RowDefinition(GridLength.Star),
                    new RowDefinition(new GridLength(SampleRuntime.SampleEventLogHeight))
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
            panel.Children.Add(CreateAsyncFeatureButton("Lua", () => _features.LoadSampleLuaScriptAsync()));
            panel.Children.Add(CreateAsyncFeatureButton("yt-dlp", () => _features.RunYtdlpDiagnosticsAsync(_sourceEntry.Text ?? string.Empty)));
            panel.Children.Add(CreateAsyncFeatureButton("Deno", () => _features.RunDenoDiagnosticsAsync()));
            panel.Children.Add(CreateAsyncFeatureButton("Update yt", () => _features.RunYtdlpSelfUpdateAsync(), SampleRuntime.SampleYtdlpUpdateButtonWidth));
            panel.Children.Add(CreateAsyncFeatureButton("Update Deno", () => _features.RunDenoSelfUpgradeAsync(), SampleRuntime.SampleDenoUpdateButtonWidth));
            return panel;
        }

        /// <summary>
        /// 建立播放區域。
        /// </summary>
        /// <returns>包含單一播放器、安全覆蓋層與一般覆蓋層對照的播放區域。</returns>
        private Grid CreatePlayerSurface()
        {
            Grid playerSurface = new Grid
            {
                BackgroundColor = Colors.Black,
                RowDefinitions =
                {
                    new RowDefinition(new GridLength(SampleRuntime.SampleAirspaceComparisonHeight)),
                    new RowDefinition(GridLength.Star)
                }
            };

            playerSurface.Add(CreateHeaderPanel(), 0, 0);
            playerSurface.Add(CreateVideoSurface(), 0, 1);
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
                BackgroundColor = Color.FromArgb("#101010"),
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Star)
                }
            };

            header.Add(CreateHeaderBadge("控制項 OverlayView：可覆蓋 HWND", Color.FromArgb("#DD0078D4"), new Thickness(16, 6, 8, 6)), 0, 0);
            header.Add(CreateHeaderBadge("一般 MAUI Overlay 嘗試覆蓋同一個 HWND", Color.FromArgb("#DD5C2D91"), new Thickness(8, 6, 16, 6)), 1, 0);
            return header;
        }

        /// <summary>
        /// 建立播放視訊與覆蓋層對照面板。
        /// </summary>
        /// <returns>包含播放器與一般 MAUI 覆蓋層的播放面板。</returns>
        private Grid CreateVideoSurface()
        {
            Grid surface = new Grid
            {
                BackgroundColor = Colors.Black
            };

            Label normalOverlay = CreateOverlayBadge("一般 MAUI Overlay：AirSpace 對照", Color.FromArgb("#DD5C2D91"));
            normalOverlay.ZIndex = 10;
            surface.Add(_playerHostContainer, 0, 0);
            surface.Add(normalOverlay, 0, 0);
            return surface;
        }

        /// <summary>
        /// 建立播放區中的覆蓋層標籤。
        /// </summary>
        /// <param name="text">要顯示的標籤文字。</param>
        /// <param name="backgroundColor">標籤背景色彩。</param>
        /// <returns>已套用固定尺寸與色彩的覆蓋層標籤。</returns>
        private static Label CreateOverlayBadge(string text, Color backgroundColor)
        {
            return new Label
            {
                Text = text,
                WidthRequest = SampleRuntime.SampleOverlayBadgeWidth,
                HeightRequest = SampleRuntime.SampleOverlayBadgeHeight,
                Margin = new Thickness(16),
                BackgroundColor = backgroundColor,
                TextColor = Colors.White,
                FontAttributes = FontAttributes.Bold,
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Start,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                LineBreakMode = LineBreakMode.TailTruncation
            };
        }

        /// <summary>
        /// 建立 AirSpace 對照區標題列。
        /// </summary>
        /// <param name="text">要顯示的標題文字。</param>
        /// <param name="backgroundColor">標題背景色彩。</param>
        /// <param name="margin">標題外距。</param>
        /// <returns>已套用固定色彩與邊界的標題。</returns>
        private static Label CreateHeaderBadge(string text, Color backgroundColor, Thickness margin)
        {
            return new Label
            {
                Text = text,
                Margin = margin,
                BackgroundColor = backgroundColor,
                TextColor = Colors.White,
                FontAttributes = FontAttributes.Bold,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                LineBreakMode = LineBreakMode.TailTruncation
            };
        }

        /// <summary>
        /// 建立 MAUI handler 可轉換到平台控制項的安全覆蓋層檢視。
        /// </summary>
        /// <returns>可交給 `MpvView.OverlayView` 顯示的 MAUI 覆蓋層檢視。</returns>
        private static View CreateMauiOverlayView()
        {
            return new Label
            {
                Text = "OverlayView：AirSpace 安全覆蓋層",
                WidthRequest = SampleRuntime.SampleOverlayBadgeWidth,
                HeightRequest = SampleRuntime.SampleOverlayBadgeHeight,
                Margin = new Thickness(16),
                BackgroundColor = Color.FromArgb("#DD0078D4"),
                TextColor = Colors.White,
                FontAttributes = FontAttributes.Bold,
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.Start,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                LineBreakMode = LineBreakMode.TailTruncation,
                InputTransparent = true
            };
        }

        /// <summary>
        /// 將播放器事件橋接到 UI 事件清單。
        /// </summary>
        /// <param name="player">要觀察的播放器。</param>
        private void AttachPlayerEvents(MpvPlayer player)
        {
            _eventBridge?.Dispose();
            _currentPlayer = player;
            _eventBridge = new SamplePlayerEventBridge(player, AppendEventLine);
            _statusDispatcher.RequestUpdate();
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
        /// 設定 runtime 相關控制項是否可操作。
        /// </summary>
        /// <param name="enabled">控制項可操作時為 <see langword="true"/>。</param>
        private void SetRuntimeControlsEnabled(bool enabled)
        {
            foreach (VisualElement control in _runtimeControls)
            {
                control.IsEnabled = enabled;
            }
        }

        /// <summary>
        /// 將目前選擇的 yt-dlp 格式套用到指定播放器選項。
        /// </summary>
        /// <param name="options">要套用格式的播放器選項。</param>
        private void ApplySelectedYtdlpFormatToPlayerOptions(MpvPlayerOptions options)
        {
            int selectedIndex = _formatPicker.SelectedIndex;
            if (selectedIndex >= 0 && selectedIndex < _formatChoices.Count)
            {
                SampleFeatureController.ApplyYtdlpFormat(options, _formatChoices[selectedIndex]);
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
                _eventList.ScrollTo(_eventLines[_eventLines.Count - 1], position: ScrollToPosition.End, animate: false);
            }
        }

        /// <summary>
        /// 將事件清單更新排入 MAUI UI 執行緒。
        /// </summary>
        /// <param name="action">要在 UI 執行緒執行的更新。</param>
        /// <returns>成功排入 UI 執行緒時為 <see langword="true"/>。</returns>
        private static bool ScheduleEventLogFlush(Action action)
        {
            return ScheduleUiUpdate(action);
        }

        /// <summary>
        /// 將指定動作排入 MAUI UI 執行緒。
        /// </summary>
        /// <param name="action">要在 UI 執行緒執行的動作。</param>
        /// <returns>成功排入 UI 執行緒時為 <see langword="true"/>。</returns>
        private static bool ScheduleUiUpdate(Action action)
        {
            try
            {
                MainThread.BeginInvokeOnMainThread(action);
                return true;
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
            _statusLabel.Text = text;
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
                if (string.Equals(choice.Selector, defaultChoice.Selector, StringComparison.Ordinal))
                {
                    selectedIndex = index;
                }
            }

            picker.SelectedIndex = selectedIndex;
            if (selectedIndex >= 0 && selectedIndex < _formatChoices.Count)
            {
                SampleFeatureController.ApplyYtdlpFormat(SampleRuntime.PlayerOptions, _formatChoices[selectedIndex]);
            }

            picker.SelectedIndexChanged += FormatPickerSelectedIndexChanged;
            _runtimeControls.Add(picker);
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
                BackgroundColor = Color.FromArgb("#303030"),
                BorderColor = Color.FromArgb("#E0E0E0"),
                BorderWidth = 1,
                TextColor = Colors.White,
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
            Button button = CreateFeatureButtonCore(text, SampleRuntime.SampleFeatureButtonWidth);
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
        private Button CreateAsyncFeatureButton(string text, Func<Task> action, double width = SampleRuntime.SampleFeatureButtonWidth)
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
        private Button CreateFeatureButtonCore(string text, double width)
        {
            Button button = new Button
            {
                Text = text,
                WidthRequest = width,
                HeightRequest = SampleRuntime.SampleButtonHeight,
                BackgroundColor = Color.FromArgb("#303030"),
                BorderColor = Color.FromArgb("#E0E0E0"),
                BorderWidth = 1,
                TextColor = Colors.White,
                Margin = new Thickness(0, 0, SampleRuntime.SampleControlSpacing, 4),
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.Center
            };
            _runtimeControls.Add(button);
            return button;
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

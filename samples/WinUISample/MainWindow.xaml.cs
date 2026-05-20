using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using MediaEmbedKit.Mpv;
using MediaEmbedKit.Mpv.Samples;
using MediaEmbedKit.Mpv.WinUI;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using WinRT.Interop;

namespace MediaEmbedKit.Mpv.Samples.WinUI;

/// <summary>
/// 表示 WinUI 3 範例的主要視窗。
/// </summary>
public sealed partial class MainWindow : Window
{
    /// <summary>
    /// 範例事件輸出的最大保留列數。
    /// </summary>
    private const int EventLogLimit = 60;

    /// <summary>
    /// 背景執行階段初始化完成後建立的 WinUI 播放控制項。
    /// </summary>
    private MpvWinUiPlayer? _playerHost;
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
    /// 控制非同步範例功能不可重入的閘門。
    /// </summary>
    private readonly SampleAsyncFeatureGate _asyncFeatureGate = new SampleAsyncFeatureGate();
    /// <summary>
    /// 非同步功能進行中需暫時禁用的功能按鈕清單。
    /// </summary>
    private readonly List<Button> _featureButtons = new List<Button>();
    /// <summary>
    /// 紀錄範例 runtime 是否已完成初始化。
    /// </summary>
    private bool _runtimeReady;
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
        EventList.ItemsSource = _eventLines;
        SourceBox.Text = SampleRuntime.PlaybackUrl;
        _features = new SampleFeatureController(() => _currentPlayer, AppendEventLine);
        ConfigureFormatComboBox();
        _eventLogDispatcher = new SampleEventLogDispatcher(AppendEventLines, ScheduleEventLogFlush);
        _statusDispatcher = new SampleStatusUpdateDispatcher(() => _features.GetStatusText(), SetStatusText, ScheduleUiUpdate);
        SetRuntimeControlsEnabled(false);
        if (Content is FrameworkElement rootElement)
        {
            rootElement.Loaded += RootLoaded;
        }
        Closed += WindowClosed;
        AppendEventLine(CreateLifecycleLine("WindowCreated", "WinUI 視窗已建立，等待執行階段初始化。"));
    }

    /// <summary>
    /// 處理視窗關閉事件並釋放事件橋接器。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="args">
    /// 視窗事件資料。
    /// </param>
    private void WindowClosed(object sender, WindowEventArgs args)
    {
        _statusDispatcher.Dispose();
        _eventBridge?.WriteLifecycle("WindowClosed", "視窗已關閉，準備取消事件訂閱。");
        _eventBridge?.Dispose();
        _eventLogDispatcher.Dispose();
        if (_playerHost != null)
        {
            _playerHost.PlayerCreated -= PlayerCreated;
            _playerHost.Dispose();
        }

        _currentPlayer = null;
    }

    /// <summary>
    /// 非同步初始化範例 runtime 與 WinUI 播放控制項。
    /// </summary>
    /// <returns>
    /// 初始化成功時為 <see langword="true"/>。
    /// </returns>
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
            SetStatusText("執行階段初始化失敗");
            AppendEventLine(CreateLifecycleLine("RuntimeError", ex.Message));
            if (SampleRuntime.IsSmokeTestEnabled)
            {
                SampleRuntime.WriteSmokeLine("WinUISample", "FAILED Runtime: " + ex.Message);
                Close();
                return false;
            }

            ContentDialog dialog = new ContentDialog
            {
                Title = "mpv runtime",
                Content = ex.Message,
                CloseButtonText = "確定",
                XamlRoot = PlayerHostContainer.XamlRoot
            };
            _ = dialog.ShowAsync();
            return false;
        }
    }

    /// <summary>
    /// 建立 runtime 就緒後才可初始化的 WinUI 播放控制項。
    /// </summary>
    private void CreatePlayerHost()
    {
        if (_playerHost != null)
        {
            return;
        }

        MpvWinUiPlayer playerHost = new MpvWinUiPlayer
        {
            OverlayContent = CreateSafeOverlayContent(),
            IsOverlayOpen = true
        };
        SampleRuntime.CopyTo(SampleRuntime.PlayerOptions, playerHost.PlayerOptions);
        ApplySelectedYtdlpFormatToPlayerOptions(playerHost.PlayerOptions);
        playerHost.Attach(this);
        playerHost.PlayerCreated += PlayerCreated;
        PlayerHostContainer.Children.Add(playerHost);
        _playerHost = playerHost;
        MvvmDemoBar.DataContext = playerHost;
    }

    /// <summary>
    /// 建立由 `MpvWinUiPlayer` 管理的 AirSpace 安全覆蓋層。
    /// </summary>
    /// <returns>
    /// 可交給 WinUI 播放控制項管理的覆蓋層元素。
    /// </returns>
    private static UIElement CreateSafeOverlayContent()
    {
        Border border = new Border
        {
            Width = SampleRuntime.SampleOverlayBadgeWidth,
            Height = SampleRuntime.SampleOverlayBadgeHeight,
            Margin = new Thickness(16),
            Background = new SolidColorBrush(ThemeColor(SampleTheme.AccentBadgeArgb)),
            CornerRadius = new CornerRadius(4),
            IsHitTestVisible = false
        };
        border.Child = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(ThemeColor(SampleTheme.BadgeForegroundArgb)),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Text = "OverlayContent：AirSpace 安全覆蓋層"
        };
        return border;
    }

    /// <summary>
    /// 處理播放器建立事件並開始輸出 libmpv 事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
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
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private void OnLoadClicked(object sender, RoutedEventArgs e)
    {
        LoadCurrentSource();
    }

    /// <summary>
    /// 處理暫停按鈕點選事件並切換播放器暫停狀態。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private void OnPauseClicked(object sender, RoutedEventArgs e)
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
    /// 處理停止按鈕點選事件並停止目前播放項目。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private void OnStopClicked(object sender, RoutedEventArgs e)
    {
        if (!EnsureRuntimeReady())
        {
            return;
        }

        _eventBridge?.WriteLifecycle("Stop", "停止目前播放項目。");
        _playerHost?.Player?.Stop();
    }

    /// <summary>
    /// 在 WinUI 視覺樹載入後初始化 runtime、載入預設媒體並執行冒煙測試。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private async void RootLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is FrameworkElement rootElement)
            {
                rootElement.Loaded -= RootLoaded;
            }

            _featureButtons.Clear();
            RegisterFeatureButtons(FeaturePanelHost);
            SetRuntimeControlsEnabled(false);
            AppendEventLine(CreateLifecycleLine("Loaded", "視窗內容已載入，準備初始化範例 runtime。"));
            bool initialized = await InitializeRuntimeAsync().ConfigureAwait(true);
            if (!initialized)
            {
                return;
            }

            AppendEventLine(CreateLifecycleLine("Loaded", "runtime 已完成，準備載入預設媒體來源。"));
            StartPlayback();
            if (SampleRuntime.IsSmokeTestEnabled && !_smokeStarted)
            {
                _smokeStarted = true;
                await RunSmokeAsync().ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            AppendEventLine(CreateLifecycleLine("LoadedError", ex.Message));
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
        if (!EnsureRuntimeReady())
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(SourceBox.Text))
        {
            _eventBridge?.WriteLifecycle("LoadFile", SourceBox.Text);
            _playerHost?.LoadFile(SourceBox.Text, MpvLoadFileMode.Replace);
        }
    }

    /// <summary>
    /// 執行 WinUI 3 範例播放冒煙測試。
    /// </summary>
    /// <returns>
    /// 代表冒煙測試流程的工作。
    /// </returns>
    private Task RunSmokeAsync()
    {
        return SampleRuntime.RunSmokeUntilPlaybackAsync("WinUISample", () => _playerHost?.Player, Close);
    }

    /// <summary>
    /// 在格式選項變更時套用 yt-dlp 格式。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private void OnFormatSelectionChanged(object sender, SelectionChangedEventArgs e)
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
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private void OnOsdClicked(object sender, RoutedEventArgs e)
    {
        RunFeature(() => _features.ShowOsd());
    }

    /// <summary>
    /// 處理倒退按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private void OnSeekBackwardClicked(object sender, RoutedEventArgs e)
    {
        RunFeature(() => _features.SeekRelative(-10));
    }

    /// <summary>
    /// 處理快轉按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private void OnSeekForwardClicked(object sender, RoutedEventArgs e)
    {
        RunFeature(() => _features.SeekRelative(10));
    }

    /// <summary>
    /// 處理降低音量按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private void OnVolumeDownClicked(object sender, RoutedEventArgs e)
    {
        RunFeature(() => _features.ChangeVolume(-5));
    }

    /// <summary>
    /// 處理提高音量按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private void OnVolumeUpClicked(object sender, RoutedEventArgs e)
    {
        RunFeature(() => _features.ChangeVolume(5));
    }

    /// <summary>
    /// 處理靜音按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private void OnMuteClicked(object sender, RoutedEventArgs e)
    {
        RunFeature(() => _features.ToggleMute());
    }

    /// <summary>
    /// 處理播放速度按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private void OnSpeedClicked(object sender, RoutedEventArgs e)
    {
        RunFeature(() => _features.CycleSpeed());
    }

    /// <summary>
    /// 處理字幕按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private void OnSubtitleClicked(object sender, RoutedEventArgs e)
    {
        RunFeature(() => _features.AddSampleSubtitle());
    }

    /// <summary>
    /// 處理播放軌按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private void OnTracksClicked(object sender, RoutedEventArgs e)
    {
        RunFeature(() => _features.DumpTracks());
    }

    /// <summary>
    /// 處理截圖按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private void OnScreenshotClicked(object sender, RoutedEventArgs e)
    {
        RunFeature(() => _features.TakeScreenshot());
    }

    /// <summary>
    /// 處理設定檔按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private void OnConfigClicked(object sender, RoutedEventArgs e)
    {
        RunFeature(() => _features.LoadSampleConfig());
    }

    /// <summary>
    /// 處理 Lua 按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private async void OnLuaClicked(object sender, RoutedEventArgs e)
    {
        await RunFeatureAsync(() => _features.LoadSampleLuaScriptAsync()).ConfigureAwait(true);
    }

    /// <summary>
    /// 處理 yt-dlp 診斷按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private async void OnYtdlpClicked(object sender, RoutedEventArgs e)
    {
        await RunFeatureAsync(() => _features.RunYtdlpDiagnosticsAsync(SourceBox.Text)).ConfigureAwait(true);
    }

    /// <summary>
    /// 處理 Deno 診斷按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private async void OnDenoClicked(object sender, RoutedEventArgs e)
    {
        await RunFeatureAsync(() => _features.RunDenoDiagnosticsAsync()).ConfigureAwait(true);
    }

    /// <summary>
    /// 處理 FFmpeg 診斷按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private async void OnFFmpegClicked(object sender, RoutedEventArgs e)
    {
        await RunFeatureAsync(() => _features.RunFFmpegDiagnosticsAsync()).ConfigureAwait(true);
    }

    /// <summary>
    /// 處理 Save MP4 按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private async void OnSaveMp4Clicked(object sender, RoutedEventArgs e)
    {
        await RunFeatureAsync(() => EncodeCurrentSourceToMp4Async()).ConfigureAwait(true);
    }

    /// <summary>
    /// 以共用 <see cref="SampleEncodingHelper"/> 把當前 URL 來源前 5 秒轉碼成 mp4。
    /// </summary>
    /// <returns>
    /// 代表編碼流程的工作。
    /// </returns>
    private async System.Threading.Tasks.Task EncodeCurrentSourceToMp4Async()
    {
        if (!_runtimeReady)
        {
            AppendEventLine(CreateLifecycleLine("Encode", "runtime 尚未就緒。"));
            return;
        }

        string source = SourceBox.Text;
        if (string.IsNullOrWhiteSpace(source))
        {
            AppendEventLine(CreateLifecycleLine("Encode", "請先在 URL 欄輸入來源。"));
            return;
        }

        MpvPlayerOptions playerOptions = new MpvPlayerOptions();
        SampleRuntime.CopyTo(SampleRuntime.PlayerOptions, playerOptions);
        ApplySelectedYtdlpFormatToPlayerOptions(playerOptions);

        try
        {
            await SampleEncodingHelper.EncodeFirstFiveSecondsToMp4Async(
                source,
                playerOptions,
                line => AppendEventLine(CreateLifecycleLine("Encode", line))).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendEventLine(CreateLifecycleLine("EncodeError", ex.GetType().Name + ": " + ex.Message));
        }
    }

    /// <summary>
    /// 處理 yt-dlp 更新按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private async void OnYtdlpUpdateClicked(object sender, RoutedEventArgs e)
    {
        await RunFeatureAsync(() => _features.RunYtdlpSelfUpdateAsync()).ConfigureAwait(true);
    }

    /// <summary>
    /// 處理 Deno 更新按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private async void OnDenoUpdateClicked(object sender, RoutedEventArgs e)
    {
        await RunFeatureAsync(() => _features.RunDenoSelfUpgradeAsync()).ConfigureAwait(true);
    }

    /// <summary>
    /// 執行同步範例功能並處理錯誤。
    /// </summary>
    /// <param name="action">
    /// 要執行的功能。
    /// </param>
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
    /// <param name="action">
    /// 要執行的非同步功能。
    /// </param>
    /// <returns>
    /// 代表功能執行流程的工作。
    /// </returns>
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
    /// <returns>
    /// runtime 已就緒時為 <see langword="true"/>。
    /// </returns>
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
    /// <param name="enabled">
    /// 控制項可操作時為 <see langword="true"/>。
    /// </param>
    private void SetRuntimeControlsEnabled(bool enabled)
    {
        SetRuntimeControlsEnabled(Content as DependencyObject, enabled);
    }

    /// <summary>
    /// 遞迴設定 runtime 相關控制項是否可操作。
    /// </summary>
    /// <param name="root">
    /// 要掃描的視覺樹節點。
    /// </param>
    /// <param name="enabled">
    /// 控制項可操作時為 <see langword="true"/>。
    /// </param>
    private static void SetRuntimeControlsEnabled(DependencyObject? root, bool enabled)
    {
        if (root == null)
        {
            return;
        }

        Control? control = root as Control;
        if (control is Button || control is ComboBox)
        {
            control.IsEnabled = enabled;
        }

        int childCount = VisualTreeHelper.GetChildrenCount(root);
        for (int index = 0; index < childCount; index++)
        {
            SetRuntimeControlsEnabled(VisualTreeHelper.GetChild(root, index), enabled);
        }
    }

    /// <summary>
    /// 遞迴登錄非同步功能進行中需暫時禁用的按鈕。
    /// </summary>
    /// <param name="root">
    /// 要掃描的視覺樹節點。
    /// </param>
    private void RegisterFeatureButtons(DependencyObject? root)
    {
        if (root == null)
        {
            return;
        }

        if (root is Button button)
        {
            _featureButtons.Add(button);
        }

        int childCount = VisualTreeHelper.GetChildrenCount(root);
        for (int index = 0; index < childCount; index++)
        {
            RegisterFeatureButtons(VisualTreeHelper.GetChild(root, index));
        }
    }

    /// <summary>
    /// 設定非同步功能進行中需暫時禁用的按鈕是否可操作。
    /// </summary>
    /// <param name="enabled">
    /// 按鈕可操作時為 <see langword="true"/>。
    /// </param>
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
    /// <param name="options">
    /// 要套用格式的播放器選項。
    /// </param>
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
    /// <param name="line">
    /// 要加入事件清單的文字列。
    /// </param>
    private void AppendEventLine(string line)
    {
        _eventLogDispatcher.Enqueue(line);
    }

    /// <summary>
    /// 批次加入事件文字列到 UI 清單。
    /// </summary>
    /// <param name="lines">
    /// 要加入事件清單的文字列集合。
    /// </param>
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
    /// <param name="action">
    /// 要在 UI 執行緒執行的更新。
    /// </param>
    /// <returns>
    /// 成功排入 UI 執行緒時為 <see langword="true"/>。
    /// </returns>
    private bool ScheduleEventLogFlush(Action action)
    {
        return ScheduleUiUpdate(action);
    }

    /// <summary>
    /// 將指定動作排入 WinUI UI 執行緒。
    /// </summary>
    /// <param name="action">
    /// 要在 UI 執行緒執行的動作。
    /// </param>
    /// <returns>
    /// 成功排入 UI 執行緒時為 <see langword="true"/>。
    /// </returns>
    private bool ScheduleUiUpdate(Action action)
    {
        return DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => action());
    }

    /// <summary>
    /// 套用背景輪詢取得的狀態列文字。
    /// </summary>
    /// <param name="text">
    /// 要顯示的狀態列文字。
    /// </param>
    private void SetStatusText(string text)
    {
        StatusTextBlock.Text = text;
    }

    /// <summary>
    /// 建立範例生命週期文字列。
    /// </summary>
    /// <param name="stage">
    /// 生命週期階段名稱。
    /// </param>
    /// <param name="detail">
    /// 階段補充內容。
    /// </param>
    /// <returns>
    /// 可顯示在事件清單中的生命週期文字列。
    /// </returns>
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

    /// <summary>
    /// 將 <see cref="SampleTheme"/> 的 ARGB 整數轉成 WinUI 顏色。
    /// </summary>
    /// <param name="argb">
    /// 要轉換的 ARGB 整數。
    /// </param>
    /// <returns>
    /// 對應的 WinUI 顏色。
    /// </returns>
    private static Windows.UI.Color ThemeColor(int argb)
    {
        return ColorHelper.FromArgb(SampleTheme.AlphaOf(argb), SampleTheme.RedOf(argb), SampleTheme.GreenOf(argb), SampleTheme.BlueOf(argb));
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

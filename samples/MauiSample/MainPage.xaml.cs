using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using MediaEmbedKit.Mpv;
using MediaEmbedKit.Mpv.Maui.Windows;
using MediaEmbedKit.Mpv.Samples;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace MediaEmbedKit.Mpv.Samples.Maui;

/// <summary>
/// 表示 .NET MAUI 範例的主要頁面。靜態 layout（toolbar / feature panel / player surface /
/// event log）由 <c>MainPage.xaml</c> 序列化；以下 code-behind 負責動態 feature button、
/// Picker 內容、SampleRuntime 初始化、播放器生命週期與 dispatcher 串接等 markup 無法表達的邏輯。
/// </summary>
public sealed partial class MainPage : ContentPage, IDisposable
{
    /// <summary>
    /// 範例事件輸出的最大保留列數。
    /// </summary>
    private const int EventLogLimit = 60;

    /// <summary>
    /// 顯示 libmpv 視訊內容的 MAUI 檢視；runtime 就緒後由 CreatePlayerHost 插入。
    /// </summary>
    private MpvView? _player;
    /// <summary>
    /// 需要在 runtime 就緒後才可使用的控制項清單。
    /// </summary>
    private readonly List<VisualElement> _runtimeControls = new List<VisualElement>();
    /// <summary>
    /// 非同步功能進行中需暫時禁用的功能按鈕清單。
    /// </summary>
    private readonly List<Button> _featureButtons = new List<Button>();
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
    /// 初始化 <see cref="MainPage"/> 類別的新執行個體。所有控制項（21 個按鈕 + Picker +
    /// 兩個 Label + CollectionView）由 <c>MainPage.xaml</c> 完整宣告；本建構式只處理
    /// markup 無法表達的部分：runtime feature controller 建構、Picker 項目填入、
    /// MVVM binding、event log ItemsSource / DataTemplate 與 dispatcher 串接。
    /// </summary>
    public MainPage()
    {
        InitializeComponent();

        _sourceEntry.Text = SampleRuntime.PlaybackUrl;
        _runtimeControls.Add(_loadButton);
        _runtimeControls.Add(_pauseButton);
        _runtimeControls.Add(_stopButton);

        _features = new SampleFeatureController(() => _currentPlayer, AppendEventLine);
        _formatChoices = SampleFeatureController.CreateYtdlpFormatChoices();
        PopulateFormatPicker();
        _runtimeControls.Add(_formatPicker);

        _featureButtons.Add(_osdButton);
        _featureButtons.Add(_seekBackwardButton);
        _featureButtons.Add(_seekForwardButton);
        _featureButtons.Add(_volumeDownButton);
        _featureButtons.Add(_volumeUpButton);
        _featureButtons.Add(_muteButton);
        _featureButtons.Add(_speedButton);
        _featureButtons.Add(_subtitleButton);
        _featureButtons.Add(_tracksButton);
        _featureButtons.Add(_screenshotButton);
        _featureButtons.Add(_configButton);
        _featureButtons.Add(_luaButton);
        _featureButtons.Add(_ytdlpButton);
        _featureButtons.Add(_denoButton);
        _featureButtons.Add(_ffmpegButton);
        _featureButtons.Add(_saveMp4Button);
        _featureButtons.Add(_ytdlpUpdateButton);
        _featureButtons.Add(_denoUpdateButton);
        foreach (Button button in _featureButtons)
        {
            _runtimeControls.Add(button);
        }

        _mvvmStateLabel.SetBinding(
            Label.TextProperty,
            new Binding(nameof(MpvView.PlaybackState), stringFormat: "MVVM 綁定示範：狀態 = {0}"));

        _eventList.ItemsSource = _eventLines;
        _eventList.ItemTemplate = CreateEventTemplate();

        _eventLogDispatcher = new SampleEventLogDispatcher(AppendEventLines, ScheduleEventLogFlush);
        _statusDispatcher = new SampleStatusUpdateDispatcher(() => _features.GetStatusText(), SetStatusText, ScheduleUiUpdate);

        SetRuntimeControlsEnabled(false);
        AppendEventLine(CreateLifecycleLine("PageCreated", "MAUI 頁面已建立，等待執行階段初始化。"));
    }

    /// <summary>
    /// OSD 按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private void OsdClicked(object? sender, EventArgs e) => RunFeature(() => _features.ShowOsd());

    /// <summary>
    /// 後退 10 秒按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private void SeekBackwardClicked(object? sender, EventArgs e) => RunFeature(() => _features.SeekRelative(-10));

    /// <summary>
    /// 前進 10 秒按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private void SeekForwardClicked(object? sender, EventArgs e) => RunFeature(() => _features.SeekRelative(10));

    /// <summary>
    /// 音量下降按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private void VolumeDownClicked(object? sender, EventArgs e) => RunFeature(() => _features.ChangeVolume(-5));

    /// <summary>
    /// 音量上升按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private void VolumeUpClicked(object? sender, EventArgs e) => RunFeature(() => _features.ChangeVolume(5));

    /// <summary>
    /// 靜音按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private void MuteClicked(object? sender, EventArgs e) => RunFeature(() => _features.ToggleMute());

    /// <summary>
    /// 播放速度切換按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private void SpeedClicked(object? sender, EventArgs e) => RunFeature(() => _features.CycleSpeed());

    /// <summary>
    /// 字幕載入按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private void SubtitleClicked(object? sender, EventArgs e) => RunFeature(() => _features.AddSampleSubtitle());

    /// <summary>
    /// 軌道輸出按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private void TracksClicked(object? sender, EventArgs e) => RunFeature(() => _features.DumpTracks());

    /// <summary>
    /// 截圖按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private void ScreenshotClicked(object? sender, EventArgs e) => RunFeature(() => _features.TakeScreenshot());

    /// <summary>
    /// 設定載入按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private void ConfigClicked(object? sender, EventArgs e) => RunFeature(() => _features.LoadSampleConfig());

    /// <summary>
    /// Lua script 載入按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private async void LuaClicked(object? sender, EventArgs e) => await RunFeatureAsync(() => _features.LoadSampleLuaScriptAsync()).ConfigureAwait(true);

    /// <summary>
    /// yt-dlp 診斷按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private async void YtdlpClicked(object? sender, EventArgs e) => await RunFeatureAsync(() => _features.RunYtdlpDiagnosticsAsync(_sourceEntry.Text ?? string.Empty)).ConfigureAwait(true);

    /// <summary>
    /// Deno 診斷按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private async void DenoClicked(object? sender, EventArgs e) => await RunFeatureAsync(() => _features.RunDenoDiagnosticsAsync()).ConfigureAwait(true);

    /// <summary>
    /// FFmpeg 診斷按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private async void FFmpegClicked(object? sender, EventArgs e) => await RunFeatureAsync(() => _features.RunFFmpegDiagnosticsAsync()).ConfigureAwait(true);

    /// <summary>
    /// Save MP4 按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private async void SaveMp4Clicked(object? sender, EventArgs e) => await RunFeatureAsync(() => EncodeCurrentSourceToMp4Async()).ConfigureAwait(true);

    /// <summary>
    /// yt-dlp 自我更新按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private async void YtdlpUpdateClicked(object? sender, EventArgs e) => await RunFeatureAsync(() => _features.RunYtdlpSelfUpdateAsync()).ConfigureAwait(true);

    /// <summary>
    /// Deno 自我升級按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private async void DenoUpdateClicked(object? sender, EventArgs e) => await RunFeatureAsync(() => _features.RunDenoSelfUpgradeAsync()).ConfigureAwait(true);

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
        _mvvmStateLabel.BindingContext = player;
    }

    /// <summary>
    /// 執行 MAUI Windows 範例播放冒煙測試。
    /// </summary>
    /// <returns>
    /// 代表冒煙測試流程的工作。
    /// </returns>
    private Task RunSmokeAsync()
    {
        return SampleRuntime.RunSmokeUntilPlaybackAsync("MauiSample", () => _player?.Player, CloseApplication);
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
        if (_player?.Player != null)
        {
            AttachPlayerEvents(_player.Player);
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
    private void LoadButtonClicked(object? sender, EventArgs e)
    {
        LoadCurrentSource();
    }

    /// <summary>
    /// 切換目前播放器的暫停狀態。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private void PauseButtonClicked(object? sender, EventArgs e)
    {
        if (!EnsureRuntimeReady() || _player == null)
        {
            return;
        }

        _eventBridge?.WriteLifecycle("Pause", "透過 TogglePauseCommand 切換暫停狀態。");
        _player.TogglePauseCommand.Execute(null);
    }

    /// <summary>
    /// 停止目前播放項目。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private void StopButtonClicked(object? sender, EventArgs e)
    {
        if (!EnsureRuntimeReady() || _player == null)
        {
            return;
        }

        _eventBridge?.WriteLifecycle("Stop", "透過 StopCommand 停止播放。");
        _player.StopCommand.Execute(null);
    }

    /// <summary>
    /// 以共用 <see cref="SampleEncodingHelper"/> 把當前 URL 來源前 5 秒轉碼成 mp4。
    /// </summary>
    /// <returns>
    /// 代表編碼流程的工作。
    /// </returns>
    private async Task EncodeCurrentSourceToMp4Async()
    {
        if (!EnsureRuntimeReady())
        {
            return;
        }

        string source = _sourceEntry.Text ?? string.Empty;
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
    /// 在格式選項變更時套用 yt-dlp 格式。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
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
    /// 建立 MAUI handler 可轉換到平台控制項的安全覆蓋層檢視。
    /// </summary>
    /// <returns>
    /// 可交給 `MpvView.OverlayView` 顯示的 MAUI 覆蓋層檢視。
    /// </returns>
    private static View CreateMauiOverlayView()
    {
        return new Label
        {
            Text = "OverlayView：AirSpace 安全覆蓋層",
            WidthRequest = SampleRuntime.SampleOverlayBadgeWidth,
            HeightRequest = SampleRuntime.SampleOverlayBadgeHeight,
            Margin = new Thickness(16),
            BackgroundColor = ThemeColor(SampleTheme.AccentBadgeArgb),
            TextColor = ThemeColor(SampleTheme.BadgeForegroundArgb),
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
    /// <param name="player">
    /// 要觀察的播放器。
    /// </param>
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
        foreach (VisualElement control in _runtimeControls)
        {
            control.IsEnabled = enabled;
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
        int selectedIndex = _formatPicker.SelectedIndex;
        if (selectedIndex >= 0 && selectedIndex < _formatChoices.Count)
        {
            SampleFeatureController.ApplyYtdlpFormat(options, _formatChoices[selectedIndex]);
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
            _eventList.ScrollTo(_eventLines[_eventLines.Count - 1], position: ScrollToPosition.End, animate: false);
        }
    }

    /// <summary>
    /// 將事件清單更新排入 MAUI UI 執行緒。
    /// </summary>
    /// <param name="action">
    /// 要在 UI 執行緒執行的更新。
    /// </param>
    /// <returns>
    /// 成功排入 UI 執行緒時為 <see langword="true"/>。
    /// </returns>
    private static bool ScheduleEventLogFlush(Action action)
    {
        return ScheduleUiUpdate(action);
    }

    /// <summary>
    /// 將指定動作排入 MAUI UI 執行緒。
    /// </summary>
    /// <param name="action">
    /// 要在 UI 執行緒執行的動作。
    /// </param>
    /// <returns>
    /// 成功排入 UI 執行緒時為 <see langword="true"/>。
    /// </returns>
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
    /// <param name="text">
    /// 要顯示的狀態列文字。
    /// </param>
    private void SetStatusText(string text)
    {
        _statusLabel.Text = text;
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
    /// 填入 XAML 宣告之 Picker 的格式選項並設定預設選擇。
    /// </summary>
    private void PopulateFormatPicker()
    {
        SampleYtdlpFormatChoice defaultChoice = SampleFeatureController.CreateDefaultYtdlpFormatChoice();
        int selectedIndex = 0;
        for (int index = 0; index < _formatChoices.Count; index++)
        {
            SampleYtdlpFormatChoice choice = _formatChoices[index];
            _formatPicker.Items.Add(choice.DisplayName);
            if (string.Equals(choice.Selector, defaultChoice.Selector, StringComparison.Ordinal))
            {
                selectedIndex = index;
            }
        }

        _formatPicker.SelectedIndex = selectedIndex;
    }

    /// <summary>
    /// 建立事件清單項目範本。
    /// </summary>
    /// <returns>
    /// 用來顯示單列事件文字的資料範本。
    /// </returns>
    private static DataTemplate CreateEventTemplate()
    {
        return new DataTemplate(() =>
        {
            Label label = new Label
            {
                FontFamily = "Consolas",
                FontSize = 12,
                Padding = new Thickness(4, 1)
            };
            label.SetBinding(Label.TextProperty, ".");
            return label;
        });
    }

    /// <summary>
    /// 將 <see cref="SampleTheme"/> 的 ARGB 整數轉成 MAUI 顏色。
    /// </summary>
    /// <param name="argb">
    /// 要轉換的 ARGB 整數。
    /// </param>
    /// <returns>
    /// 對應的 MAUI 顏色。
    /// </returns>
    private static Color ThemeColor(int argb)
    {
        return Color.FromRgba(SampleTheme.RedOf(argb), SampleTheme.GreenOf(argb), SampleTheme.BlueOf(argb), SampleTheme.AlphaOf(argb));
    }

    /// <summary>
    /// 關閉 MAUI 範例應用程式。
    /// </summary>
    private static void CloseApplication()
    {
        Application.Current?.Quit();
    }
}

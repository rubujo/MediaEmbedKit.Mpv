using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using MediaEmbedKit.Mpv;
using MediaEmbedKit.Mpv.Avalonia;
using MediaEmbedKit.Mpv.Samples;

namespace MediaEmbedKit.Mpv.Samples.Avalonia;

/// <summary>
/// 表示 Avalonia 範例的主要視窗。靜態 layout（toolbar / feature panel / player surface /
/// event log）由 <c>MainWindow.axaml</c> 序列化；以下 code-behind 負責動態 button factory、
/// SampleRuntime 初始化、播放器生命週期與 dispatcher 串接等 markup 無法表達的邏輯。
/// </summary>
public sealed partial class MainWindow : Window
{
    /// <summary>
    /// 範例事件輸出的最大保留列數。
    /// </summary>
    private const int EventLogLimit = 60;

    /// <summary>
    /// 顯示 libmpv 視訊內容的 Avalonia OpenGL 控制項；runtime 就緒後由 CreatePlayerHost 插入。
    /// </summary>
    private MpvAvaloniaPlayer? _player;
    /// <summary>
    /// 需要在 runtime 就緒後才可使用的控制項清單。
    /// </summary>
    private readonly List<Control> _runtimeControls = new List<Control>();
    /// <summary>
    /// 非同步功能進行中需暫時禁用的功能按鈕清單。
    /// </summary>
    private readonly List<Button> _featureButtons = new List<Button>();
    /// <summary>
    /// 顯示在 UI 的事件文字列集合。
    /// </summary>
    private readonly ObservableCollection<string> _eventLines = new ObservableCollection<string>();
    /// <summary>
    /// 範例進階功能控制器。
    /// </summary>
    private SampleFeatureController _features = null!;
    /// <summary>
    /// 背景讀取並批次套用狀態列文字的分派器。
    /// </summary>
    private SampleStatusUpdateDispatcher _statusDispatcher = null!;
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
    private SampleEventLogDispatcher _eventLogDispatcher = null!;
    /// <summary>
    /// 控制非同步範例功能不可重入的閘門。
    /// </summary>
    private readonly SampleAsyncFeatureGate _asyncFeatureGate = new SampleAsyncFeatureGate();
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
    /// 表示關閉前的 libmpv 收尾流程是否已完成。
    /// </summary>
    private bool _closeShutdownCompleted;
    /// <summary>
    /// 表示關閉前的 libmpv 收尾流程是否已啟動。
    /// </summary>
    private bool _closeShutdownStarted;

    /// <summary>
    /// 初始化 <see cref="MainWindow"/> 類別的新執行個體。XAML 序列化後加上動態按鈕與
    /// dispatcher、binding、事件訂閱等 markup 無法序列化的設定。
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();

        _sourceBox.Text = SampleRuntime.PlaybackUrl;

        _runtimeControls.Add(_loadButton);
        _runtimeControls.Add(_pauseButton);
        _runtimeControls.Add(_stopButton);

        _features = new SampleFeatureController(() => _currentPlayer, AppendEventLine);

        ConfigureFormatComboBox();
        _runtimeControls.Add(_formatComboBox);

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

        _mvvmStateTextBlock.Bind(
            TextBlock.TextProperty,
            new global::Avalonia.Data.Binding(nameof(MpvAvaloniaPlayer.PlaybackState))
            {
                StringFormat = "MVVM 綁定示範：狀態 = {0}"
            });

        _eventList.ItemsSource = _eventLines;

        _eventLogDispatcher = new SampleEventLogDispatcher(AppendEventLines, ScheduleEventLogFlush);
        _statusDispatcher = new SampleStatusUpdateDispatcher(() => _features.GetStatusText(), SetStatusText, ScheduleUiUpdate);

        SetRuntimeControlsEnabled(false);
        Opened += WindowOpened;
        AppendEventLine(CreateLifecycleLine("WindowCreated", "Avalonia 視窗已建立，等待執行階段初始化。"));
    }

    // InitializeComponent 由 Avalonia.Generators 的 NameGenerator source-gen 提供
    // （public 簽章 `void InitializeComponent(bool loadXaml = true)`），同時做 XAML
    // 載入與 x:Name 欄位繫結。不要手動實作私有 InitializeComponent；overload resolution
    // 會選錯版本導致 _sourceBox / _toolbarHost 等欄位永遠為 null。

    /// <summary>
    /// 在視窗關閉前先讓 libmpv 非同步結束，避免 UI 執行緒在 OpenGL 控制項釋放階段等待事件執行緒。
    /// </summary>
    /// <param name="e">
    /// 視窗關閉事件資料。
    /// </param>
    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        if (!_closeShutdownCompleted && !_closeShutdownStarted && _currentPlayer != null)
        {
            e.Cancel = true;
            _closeShutdownStarted = true;
            SetRuntimeControlsEnabled(false);
            await SampleShutdown.PreparePlayerCloseAsync(_currentPlayer, WriteCloseLifecycle).ConfigureAwait(true);
            _closeShutdownCompleted = true;
            Close();
            return;
        }

        base.OnClosing(e);
    }

    /// <summary>
    /// 在視窗關閉時釋放事件橋接器。
    /// </summary>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    protected override void OnClosed(EventArgs e)
    {
        _statusDispatcher.Dispose();
        _eventBridge?.WriteLifecycle("WindowClosed", "視窗已關閉，準備取消事件訂閱。");
        _eventBridge?.Dispose();
        _eventLogDispatcher.Dispose();
        if (_player != null)
        {
            _player.PlayerCreated -= PlayerCreated;
            _player.Dispose();
        }

        _currentPlayer = null;
        base.OnClosed(e);
    }

    /// <summary>
    /// 寫入關閉流程生命週期事件。
    /// </summary>
    /// <param name="name">
    /// 事件名稱。
    /// </param>
    /// <param name="message">
    /// 事件訊息。
    /// </param>
    private void WriteCloseLifecycle(string name, string message)
    {
        AppendEventLine(CreateLifecycleLine(name, message));
    }

    /// <summary>
    /// 在視窗開啟後載入預設媒體並執行冒煙測試。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private async void WindowOpened(object? sender, EventArgs e)
    {
        try
        {
            AppendEventLine(CreateLifecycleLine("Opened", "視窗已開啟，準備初始化範例 runtime。"));
            bool initialized = await InitializeRuntimeAsync().ConfigureAwait(true);
            if (!initialized)
            {
                return;
            }

            AppendEventLine(CreateLifecycleLine("Opened", "runtime 已完成，準備載入預設媒體來源。"));
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
    /// 非同步初始化範例 runtime 與 Avalonia OpenGL 播放控制項。
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
                SampleRuntime.WriteSmokeLine("AvaloniaSample", "FAILED Runtime: " + ex.Message);
                Close();
                return false;
            }

            await ShowRuntimeErrorDialogAsync(ex.Message).ConfigureAwait(true);
            return false;
        }
    }

    /// <summary>
    /// 以 modal 視窗顯示執行階段初始化錯誤。
    /// </summary>
    /// <param name="message">
    /// 要顯示的錯誤訊息。
    /// </param>
    /// <returns>
    /// 代表錯誤視窗顯示流程的工作。
    /// </returns>
    private async Task ShowRuntimeErrorDialogAsync(string message)
    {
        Window dialog = new Window
        {
            Title = "mpv runtime",
            Width = 520,
            Height = 200,
            MinWidth = 520,
            MinHeight = 200,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = CreateRuntimeErrorDialogContent(message)
        };

        await dialog.ShowDialog<object?>(this).ConfigureAwait(true);
    }

    /// <summary>
    /// 建立執行階段初始化錯誤視窗內容。
    /// </summary>
    /// <param name="message">
    /// 要顯示的錯誤訊息。
    /// </param>
    /// <returns>
    /// 錯誤視窗使用的內容控制項。
    /// </returns>
    private static Control CreateRuntimeErrorDialogContent(string message)
    {
        Button closeButton = new Button
        {
            Content = "確定",
            Width = 96,
            Height = 32,
            HorizontalAlignment = HorizontalAlignment.Right,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };

        StackPanel panel = new StackPanel
        {
            Spacing = 18,
            Margin = new global::Avalonia.Thickness(20),
            Children =
            {
                new TextBlock
                {
                    Text = message,
                    TextWrapping = global::Avalonia.Media.TextWrapping.Wrap
                },
                closeButton
            }
        };

        closeButton.Click += (sender, e) =>
        {
            TopLevel? topLevel = TopLevel.GetTopLevel(closeButton);
            if (topLevel is Window window)
            {
                window.Close(null);
            }
        };

        return panel;
    }

    /// <summary>
    /// 建立 runtime 就緒後才可初始化的 Avalonia OpenGL 播放控制項。
    /// </summary>
    private void CreatePlayerHost()
    {
        if (_player != null)
        {
            return;
        }

        MpvAvaloniaPlayer player = new MpvAvaloniaPlayer
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        SampleRuntime.CopyTo(SampleRuntime.PlayerOptions, player.PlayerOptions);
        ApplySelectedYtdlpFormatToPlayerOptions(player.PlayerOptions);
        player.PlayerCreated += PlayerCreated;
        _playerHostContainer.Children.Insert(0, player);
        _player = player;
        _mvvmStateTextBlock.DataContext = player;
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
        if (_player?.Player != null)
        {
            _currentPlayer = _player.Player;
            _eventBridge = new SamplePlayerEventBridge(_player.Player, AppendEventLine);
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
    private void OnLoadClicked(object? sender, RoutedEventArgs e)
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
    private void OnPauseClicked(object? sender, RoutedEventArgs e)
    {
        if (!EnsureRuntimeReady() || _player == null)
        {
            return;
        }

        _eventBridge?.WriteLifecycle("Pause", "透過 TogglePauseCommand 切換暫停狀態。");
        _player.TogglePauseCommand.Execute(null);
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
    private void OnStopClicked(object? sender, RoutedEventArgs e)
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

        string source = _sourceBox.Text ?? string.Empty;
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

        if (!string.IsNullOrWhiteSpace(_sourceBox.Text))
        {
            _eventBridge?.WriteLifecycle("LoadFile", _sourceBox.Text);
            _player?.LoadFile(_sourceBox.Text, MpvLoadFileMode.Replace);
        }
    }

    /// <summary>
    /// 執行 Avalonia 範例播放冒煙測試。
    /// </summary>
    /// <returns>
    /// 代表冒煙測試流程的工作。
    /// </returns>
    private Task RunSmokeAsync()
    {
        return SampleRuntime.RunSmokeUntilPlaybackAsync("AvaloniaSample", () => _player?.Player, Close);
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
        foreach (Control control in _runtimeControls)
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
        SampleYtdlpFormatChoice? selectedChoice = _formatComboBox.SelectedItem as SampleYtdlpFormatChoice;
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
            // Avalonia ListBox 沒有 WPF / WinUI 的 ScrollIntoView 公開 API；改透過
            // virtualizing items host 的 BringIntoView 把最後一個容器捲到可視範圍。
            // 由於 ListBox 用 VirtualizingStackPanel，container 可能還沒實體化，需
            // 排到 background priority 等 layout 完成後再執行。
            string lastLine = _eventLines[_eventLines.Count - 1];
            Dispatcher.UIThread.Post(() =>
            {
                _eventList.ScrollIntoView(lastLine);
            }, DispatcherPriority.Background);
        }
    }

    /// <summary>
    /// 將事件清單更新排入 Avalonia UI 執行緒。
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
    /// 將指定動作排入 Avalonia UI 執行緒。
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
    /// <param name="text">
    /// 要顯示的狀態列文字。
    /// </param>
    private void SetStatusText(string text)
    {
        _statusTextBlock.Text = text;
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
    /// 設定 XAML 宣告的 yt-dlp 格式下拉選單：填入選項並訂閱變更事件。
    /// </summary>
    private void ConfigureFormatComboBox()
    {
        IReadOnlyList<SampleYtdlpFormatChoice> choices = SampleFeatureController.CreateYtdlpFormatChoices();
        SampleYtdlpFormatChoice defaultChoice = SampleFeatureController.CreateDefaultYtdlpFormatChoice();
        int selectedIndex = 0;
        _formatComboBox.ItemsSource = choices;
        for (int index = 0; index < choices.Count; index++)
        {
            if (string.Equals(choices[index].Selector, defaultChoice.Selector, StringComparison.Ordinal))
            {
                selectedIndex = index;
            }
        }

        _formatComboBox.SelectedIndex = selectedIndex;
        _formatComboBox.SelectionChanged += FormatComboBoxSelectionChanged;
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
    private void OsdClick(object? sender, RoutedEventArgs e) => RunFeature(() => _features.ShowOsd());

    /// <summary>
    /// 後退 10 秒按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private void SeekBackwardClick(object? sender, RoutedEventArgs e) => RunFeature(() => _features.SeekRelative(-10));

    /// <summary>
    /// 前進 10 秒按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private void SeekForwardClick(object? sender, RoutedEventArgs e) => RunFeature(() => _features.SeekRelative(10));

    /// <summary>
    /// 音量下降按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private void VolumeDownClick(object? sender, RoutedEventArgs e) => RunFeature(() => _features.ChangeVolume(-5));

    /// <summary>
    /// 音量上升按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private void VolumeUpClick(object? sender, RoutedEventArgs e) => RunFeature(() => _features.ChangeVolume(5));

    /// <summary>
    /// 靜音按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private void MuteClick(object? sender, RoutedEventArgs e) => RunFeature(() => _features.ToggleMute());

    /// <summary>
    /// 播放速度切換按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private void SpeedClick(object? sender, RoutedEventArgs e) => RunFeature(() => _features.CycleSpeed());

    /// <summary>
    /// 字幕載入按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private void SubtitleClick(object? sender, RoutedEventArgs e) => RunFeature(() => _features.AddSampleSubtitle());

    /// <summary>
    /// 軌道輸出按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private void TracksClick(object? sender, RoutedEventArgs e) => RunFeature(() => _features.DumpTracks());

    /// <summary>
    /// 截圖按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private void ScreenshotClick(object? sender, RoutedEventArgs e) => RunFeature(() => _features.TakeScreenshot());

    /// <summary>
    /// 設定載入按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private void ConfigClick(object? sender, RoutedEventArgs e) => RunFeature(() => _features.LoadSampleConfig());

    /// <summary>
    /// Lua script 載入按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private async void LuaClick(object? sender, RoutedEventArgs e) => await RunFeatureAsync(() => _features.LoadSampleLuaScriptAsync()).ConfigureAwait(true);

    /// <summary>
    /// yt-dlp 診斷按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private async void YtdlpClick(object? sender, RoutedEventArgs e) => await RunFeatureAsync(() => _features.RunYtdlpDiagnosticsAsync(_sourceBox.Text ?? string.Empty)).ConfigureAwait(true);

    /// <summary>
    /// Deno 診斷按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private async void DenoClick(object? sender, RoutedEventArgs e) => await RunFeatureAsync(() => _features.RunDenoDiagnosticsAsync()).ConfigureAwait(true);

    /// <summary>
    /// FFmpeg 診斷按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private async void FFmpegClick(object? sender, RoutedEventArgs e) => await RunFeatureAsync(() => _features.RunFFmpegDiagnosticsAsync()).ConfigureAwait(true);

    /// <summary>
    /// Save MP4 按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private async void SaveMp4Click(object? sender, RoutedEventArgs e) => await RunFeatureAsync(() => EncodeCurrentSourceToMp4Async()).ConfigureAwait(true);

    /// <summary>
    /// yt-dlp 自我更新按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private async void YtdlpUpdateClick(object? sender, RoutedEventArgs e) => await RunFeatureAsync(() => _features.RunYtdlpSelfUpdateAsync()).ConfigureAwait(true);

    /// <summary>
    /// Deno 自我升級按鈕點選事件。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    private async void DenoUpdateClick(object? sender, RoutedEventArgs e) => await RunFeatureAsync(() => _features.RunDenoSelfUpgradeAsync()).ConfigureAwait(true);
}

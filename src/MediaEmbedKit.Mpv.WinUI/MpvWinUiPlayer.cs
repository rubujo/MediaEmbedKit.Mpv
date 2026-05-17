using System;
using System.Collections.Generic;
using System.Windows.Input;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;

namespace MediaEmbedKit.Mpv.WinUI;

/// <summary>
/// 提供 WinUI 3 用的 HWND libmpv 播放控制項。
/// </summary>
public sealed class MpvWinUiPlayer : Grid, IDisposable
{
    /// <summary>
    /// 識別 <see cref="OverlayContent"/> 相依性屬性。
    /// </summary>
    public static readonly DependencyProperty OverlayContentProperty = DependencyProperty.Register(
        nameof(OverlayContent),
        typeof(UIElement),
        typeof(MpvWinUiPlayer),
        new PropertyMetadata(null, OverlayContentChanged));

    /// <summary>
    /// 識別 <see cref="IsOverlayOpen"/> 相依性屬性。
    /// </summary>
    public static readonly DependencyProperty IsOverlayOpenProperty = DependencyProperty.Register(
        nameof(IsOverlayOpen),
        typeof(bool),
        typeof(MpvWinUiPlayer),
        new PropertyMetadata(true, OverlayOpenChanged));

    /// <summary>
    /// 識別 <see cref="Source"/> 相依性屬性。
    /// </summary>
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source),
        typeof(string),
        typeof(MpvWinUiPlayer),
        new PropertyMetadata(null, SourceChanged));

    /// <summary>
    /// 識別 <see cref="Position"/> 相依性屬性。
    /// </summary>
    public static readonly DependencyProperty PositionProperty = DependencyProperty.Register(
        nameof(Position),
        typeof(TimeSpan),
        typeof(MpvWinUiPlayer),
        new PropertyMetadata(TimeSpan.Zero, PositionChanged));

    /// <summary>
    /// 識別 <see cref="Duration"/> 相依性屬性。
    /// </summary>
    /// <remarks>
    /// WinUI 3 沒有 WPF 的 <c>RegisterReadOnly</c> 等效 API；本控制項以 callback 在偵測到外部寫入時回退舊值，
    /// 對呼叫端（XAML binding、<c>SetValue</c>）模擬唯讀語意。請只透過 <see cref="MpvPlayer"/> 的播放事件更新此屬性。
    /// </remarks>
    public static readonly DependencyProperty DurationProperty = DependencyProperty.Register(
        nameof(Duration),
        typeof(TimeSpan),
        typeof(MpvWinUiPlayer),
        new PropertyMetadata(TimeSpan.Zero, OnDurationChanged));

    /// <summary>
    /// 識別 <see cref="Volume"/> 相依性屬性。
    /// </summary>
    public static readonly DependencyProperty VolumeProperty = DependencyProperty.Register(
        nameof(Volume),
        typeof(double),
        typeof(MpvWinUiPlayer),
        new PropertyMetadata(100.0, VolumeChanged));

    /// <summary>
    /// 識別 <see cref="IsPaused"/> 相依性屬性。
    /// </summary>
    public static readonly DependencyProperty IsPausedProperty = DependencyProperty.Register(
        nameof(IsPaused),
        typeof(bool),
        typeof(MpvWinUiPlayer),
        new PropertyMetadata(false, IsPausedChanged));

    /// <summary>
    /// 識別 <see cref="IsMuted"/> 相依性屬性。
    /// </summary>
    public static readonly DependencyProperty IsMutedProperty = DependencyProperty.Register(
        nameof(IsMuted),
        typeof(bool),
        typeof(MpvWinUiPlayer),
        new PropertyMetadata(false, IsMutedChanged));

    /// <summary>
    /// 識別 <see cref="PlaybackState"/> 相依性屬性。
    /// </summary>
    /// <remarks>
    /// WinUI 3 沒有 WPF 的 <c>RegisterReadOnly</c> 等效 API；本控制項以 callback 在偵測到外部寫入時回退舊值，
    /// 對呼叫端（XAML binding、<c>SetValue</c>）模擬唯讀語意。請只透過 <see cref="MpvPlayer"/> 的播放事件更新此屬性。
    /// </remarks>
    public static readonly DependencyProperty PlaybackStateProperty = DependencyProperty.Register(
        nameof(PlaybackState),
        typeof(MpvPlaybackState),
        typeof(MpvWinUiPlayer),
        new PropertyMetadata(MpvPlaybackState.Idle, OnPlaybackStateChanged));

    /// <summary>
    /// 識別 <see cref="PlaylistIndex"/> 相依性屬性。
    /// </summary>
    public static readonly DependencyProperty PlaylistIndexProperty = DependencyProperty.Register(
        nameof(PlaylistIndex),
        typeof(int),
        typeof(MpvWinUiPlayer),
        new PropertyMetadata(0, PlaylistIndexChanged));

    /// <summary>
    /// 識別 <see cref="Chapter"/> 相依性屬性。
    /// </summary>
    public static readonly DependencyProperty ChapterProperty = DependencyProperty.Register(
        nameof(Chapter),
        typeof(int?),
        typeof(MpvWinUiPlayer),
        new PropertyMetadata(null, ChapterChanged));

    /// <summary>
    /// HWND 播放後端。
    /// </summary>
    private MpvWinUiHwndPlayer? _hwndPlayer;
    /// <summary>
    /// 控制項記錄的 WinUI 視窗。
    /// </summary>
    private Window? _hostWindow;
    /// <summary>
    /// 可容納原生子視窗的父視窗控制代碼。
    /// </summary>
    private IntPtr _parentHwnd;
    /// <summary>
    /// 等待播放器建立後載入的媒體來源。
    /// </summary>
    private string? _pendingSource;
    /// <summary>
    /// 等待播放器建立後套用的載入模式。
    /// </summary>
    private MpvLoadFileMode _pendingMode = MpvLoadFileMode.Replace;
    /// <summary>
    /// 表示目前控制項是否已釋放。
    /// </summary>
    private bool _disposed;
    /// <summary>
    /// 已附加的播放器屬性訂閱清單；player dispose 時一次釋放。
    /// </summary>
    private readonly List<IDisposable> _propertyWatchers = new List<IDisposable>();
    /// <summary>
    /// 表示目前 DP 變更來源是 player（避免回頭再寫入 player 造成循環）。
    /// </summary>
    private bool _suppressPlayerWrite;
    /// <summary>
    /// 已附加 StateChanged 事件的播放器引用。
    /// </summary>
    private MpvPlayer? _boundPlayer;
    /// <summary>
    /// 控制項用來把 UI 變更回送至 UI thread 的 dispatcher queue。
    /// </summary>
    private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

    /// <summary>
    /// 初始化 <see cref="MpvWinUiPlayer"/> 類別的新執行個體。
    /// </summary>
    public MpvWinUiPlayer()
    {
        PlayerOptions = new MpvPlayerOptions();
        if (MpvWinUiDesignMode.IsEnabled)
        {
            Children.Add(MpvWinUiDesignMode.CreatePlaceholder("MediaEmbedKit.Mpv WinUI"));
        }

        Loaded += OnLoaded;

        _playCommand = new MpvRelayCommand(ExecutePlay, CanExecutePlayerCommand);
        _pauseCommand = new MpvRelayCommand(ExecutePause, CanExecutePlayerCommand);
        _stopCommand = new MpvRelayCommand(ExecuteStop, CanExecutePlayerCommand);
        _togglePauseCommand = new MpvRelayCommand(ExecuteTogglePause, CanExecutePlayerCommand);
        _toggleMuteCommand = new MpvRelayCommand(ExecuteToggleMute, CanExecutePlayerCommand);
    }

    /// <summary>
    /// 設定 <see cref="IsPaused"/> 為 <see langword="false"/> 開始或續播。
    /// </summary>
    private readonly MpvRelayCommand _playCommand;
    /// <summary>
    /// 設定 <see cref="IsPaused"/> 為 <see langword="true"/> 暫停。
    /// </summary>
    private readonly MpvRelayCommand _pauseCommand;
    /// <summary>
    /// 呼叫 <see cref="MpvPlayer.Stop()"/> 停止播放。
    /// </summary>
    private readonly MpvRelayCommand _stopCommand;
    /// <summary>
    /// 切換 <see cref="IsPaused"/>。
    /// </summary>
    private readonly MpvRelayCommand _togglePauseCommand;
    /// <summary>
    /// 切換 <see cref="IsMuted"/>。
    /// </summary>
    private readonly MpvRelayCommand _toggleMuteCommand;

    /// <summary>
    /// 取得讓播放器開始或續播的指令。
    /// </summary>
    /// <value>對應 mpv <c>pause=no</c>。</value>
    [System.ComponentModel.Category("MediaEmbedKit.Mpv")]
    public ICommand PlayCommand
    {
        get { return _playCommand; }
    }

    /// <summary>
    /// 取得暫停播放的指令。
    /// </summary>
    /// <value>對應 mpv <c>pause=yes</c>。</value>
    [System.ComponentModel.Category("MediaEmbedKit.Mpv")]
    public ICommand PauseCommand
    {
        get { return _pauseCommand; }
    }

    /// <summary>
    /// 取得停止播放的指令。
    /// </summary>
    /// <value>對應 mpv <c>stop</c>。</value>
    [System.ComponentModel.Category("MediaEmbedKit.Mpv")]
    public ICommand StopCommand
    {
        get { return _stopCommand; }
    }

    /// <summary>
    /// 取得切換暫停狀態的指令。
    /// </summary>
    /// <value>切換 mpv <c>pause</c>。</value>
    [System.ComponentModel.Category("MediaEmbedKit.Mpv")]
    public ICommand TogglePauseCommand
    {
        get { return _togglePauseCommand; }
    }

    /// <summary>
    /// 取得切換靜音狀態的指令。
    /// </summary>
    /// <value>切換 mpv <c>mute</c>。</value>
    [System.ComponentModel.Category("MediaEmbedKit.Mpv")]
    public ICommand ToggleMuteCommand
    {
        get { return _toggleMuteCommand; }
    }

    /// <summary>
    /// 判斷指令目前是否有可用播放器。
    /// </summary>
    /// <returns>已綁定播放器時為 <see langword="true"/>。</returns>
    private bool CanExecutePlayerCommand()
    {
        return _boundPlayer != null;
    }

    /// <summary>
    /// 執行 <see cref="PlayCommand"/>。
    /// </summary>
    private void ExecutePlay()
    {
        if (_boundPlayer == null)
        {
            return;
        }

        try { _boundPlayer.Pause = false; } catch (MpvException) { }
    }

    /// <summary>
    /// 執行 <see cref="PauseCommand"/>。
    /// </summary>
    private void ExecutePause()
    {
        if (_boundPlayer == null)
        {
            return;
        }

        try { _boundPlayer.Pause = true; } catch (MpvException) { }
    }

    /// <summary>
    /// 執行 <see cref="StopCommand"/>。
    /// </summary>
    private void ExecuteStop()
    {
        if (_boundPlayer == null)
        {
            return;
        }

        try { _boundPlayer.Stop(); } catch (MpvException) { }
    }

    /// <summary>
    /// 執行 <see cref="TogglePauseCommand"/>。
    /// </summary>
    private void ExecuteTogglePause()
    {
        if (_boundPlayer == null)
        {
            return;
        }

        try { _boundPlayer.Pause = !_boundPlayer.Pause; } catch (MpvException) { }
    }

    /// <summary>
    /// 執行 <see cref="ToggleMuteCommand"/>。
    /// </summary>
    private void ExecuteToggleMute()
    {
        if (_boundPlayer == null)
        {
            return;
        }

        try { _boundPlayer.Mute = !_boundPlayer.Mute; } catch (MpvException) { }
    }

    /// <summary>
    /// 通知所有指令重新評估 <see cref="ICommand.CanExecute"/>。
    /// </summary>
    private void RaiseCommandsCanExecuteChanged()
    {
        _playCommand.RaiseCanExecuteChanged();
        _pauseCommand.RaiseCanExecuteChanged();
        _stopCommand.RaiseCanExecuteChanged();
        _togglePauseCommand.RaiseCanExecuteChanged();
        _toggleMuteCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// 在控制項建立 mpv 播放器後發生。
    /// </summary>
    public event EventHandler? PlayerCreated;

    /// <summary>
    /// 取得控制項建立播放器時使用的選項。
    /// </summary>
    /// <value>播放器建立選項。</value>
    [System.ComponentModel.Browsable(false)]
    public MpvPlayerOptions PlayerOptions { get; private set; }

    /// <summary>
    /// 取得控制項目前建立的播放器。
    /// </summary>
    /// <value>目前播放器；尚未建立時為 <see langword="null"/>。</value>
    [System.ComponentModel.Browsable(false)]
    public MpvPlayer? Player
    {
        get { return _hwndPlayer == null ? null : _hwndPlayer.Player; }
    }

    /// <summary>
    /// 取得控制項目前記錄的 WinUI 視窗。
    /// </summary>
    /// <value>最近一次附加的 WinUI 視窗；尚未附加時為 <see langword="null"/>。</value>
    [System.ComponentModel.Browsable(false)]
    public Window? HostWindow
    {
        get { return _hostWindow; }
    }

    /// <summary>
    /// 取得此控制項是否已建立原生子視窗。
    /// </summary>
    /// <value>HWND 後端已建立原生子視窗時為 <see langword="true"/>。</value>
    [System.ComponentModel.Browsable(false)]
    public bool IsNativeHostCreated
    {
        get { return _hwndPlayer != null && _hwndPlayer.VideoWindowHandle != IntPtr.Zero; }
    }

    /// <summary>
    /// 取得控制項是否已取得可用的父視窗控制代碼。
    /// </summary>
    /// <value>父視窗控制代碼已設定時為 <see langword="true"/>。</value>
    [System.ComponentModel.Browsable(false)]
    public bool IsAttached
    {
        get { return _parentHwnd != IntPtr.Zero; }
    }

    /// <summary>
    /// 取得最近一次建立 HWND 後端時發生的錯誤。
    /// </summary>
    /// <value>最近一次錯誤；未發生錯誤時為 <see langword="null"/>。</value>
    [System.ComponentModel.Browsable(false)]
    public Exception? LastBackendError { get; private set; }

    /// <summary>
    /// 取得或設定要由控制項自行放入 AirSpace 覆蓋層的 WinUI 內容。
    /// </summary>
    /// <value>顯示在影片上方的 WinUI 元素；未設定時為 <see langword="null"/>。</value>
    [System.ComponentModel.Category("MediaEmbedKit.Mpv")]
    public UIElement? OverlayContent
    {
        get { return (UIElement?)GetValue(OverlayContentProperty); }
        set { SetValue(OverlayContentProperty, value); }
    }

    /// <summary>
    /// 取得或設定控制項管理的 AirSpace 覆蓋層是否開啟。
    /// </summary>
    /// <value>覆蓋層應保持開啟時為 <see langword="true"/>。</value>
    [System.ComponentModel.Category("MediaEmbedKit.Mpv")]
    public bool IsOverlayOpen
    {
        get { return (bool)GetValue(IsOverlayOpenProperty); }
        set { SetValue(IsOverlayOpenProperty, value); }
    }

    /// <summary>
    /// 取得或設定要載入並播放的媒體來源。
    /// </summary>
    /// <value>檔案路徑或媒體網址；變更會自動載入新媒體。</value>
    [System.ComponentModel.Category("MediaEmbedKit.Mpv")]
    public string? Source
    {
        get { return (string?)GetValue(SourceProperty); }
        set { SetValue(SourceProperty, value); }
    }

    /// <summary>
    /// 取得或設定目前播放位置。
    /// </summary>
    /// <value>對應 mpv <c>time-pos</c>；雙向繫結時設值會觸發 seek。</value>
    [System.ComponentModel.Category("MediaEmbedKit.Mpv")]
    public TimeSpan Position
    {
        get { return (TimeSpan)GetValue(PositionProperty); }
        set { SetValue(PositionProperty, value); }
    }

    /// <summary>
    /// 取得目前媒體總時長。
    /// </summary>
    /// <value>對應 mpv <c>duration</c>。</value>
    /// <remarks>
    /// 對外語意為唯讀；WinUI 3 沒有 <c>RegisterReadOnly</c>，因此使用一般 <see cref="DependencyProperty"/>，
    /// 但 <see cref="DurationProperty"/> 的 callback 會在偵測到非播放器來源的寫入時自動回退。請勿透過
    /// XAML binding（<c>Mode=TwoWay</c>）或 <c>SetValue</c> 寫入此屬性。
    /// </remarks>
    [System.ComponentModel.Category("MediaEmbedKit.Mpv")]
    public TimeSpan Duration
    {
        get { return (TimeSpan)GetValue(DurationProperty); }
    }

    /// <summary>
    /// 取得或設定音量。
    /// </summary>
    /// <value>對應 mpv <c>volume</c>，範圍 0–130；預設 100。</value>
    [System.ComponentModel.Category("MediaEmbedKit.Mpv")]
    public double Volume
    {
        get { return (double)GetValue(VolumeProperty); }
        set { SetValue(VolumeProperty, value); }
    }

    /// <summary>
    /// 取得或設定是否暫停。
    /// </summary>
    /// <value>對應 mpv <c>pause</c>。</value>
    [System.ComponentModel.Category("MediaEmbedKit.Mpv")]
    public bool IsPaused
    {
        get { return (bool)GetValue(IsPausedProperty); }
        set { SetValue(IsPausedProperty, value); }
    }

    /// <summary>
    /// 取得或設定是否靜音。
    /// </summary>
    /// <value>對應 mpv <c>mute</c>。</value>
    [System.ComponentModel.Category("MediaEmbedKit.Mpv")]
    public bool IsMuted
    {
        get { return (bool)GetValue(IsMutedProperty); }
        set { SetValue(IsMutedProperty, value); }
    }

    /// <summary>
    /// 取得目前由 libmpv 事件聚合而成的播放狀態。
    /// </summary>
    /// <value>對應 <see cref="MpvPlayer.State"/>。</value>
    /// <remarks>
    /// 對外語意為唯讀；WinUI 3 沒有 <c>RegisterReadOnly</c>，因此使用一般 <see cref="DependencyProperty"/>，
    /// 但 <see cref="PlaybackStateProperty"/> 的 callback 會在偵測到非播放器來源的寫入時自動回退。請勿透過
    /// XAML binding（<c>Mode=TwoWay</c>）或 <c>SetValue</c> 寫入此屬性。
    /// </remarks>
    [System.ComponentModel.Category("MediaEmbedKit.Mpv")]
    public MpvPlaybackState PlaybackState
    {
        get { return (MpvPlaybackState)GetValue(PlaybackStateProperty); }
    }

    /// <summary>
    /// 取得或設定目前播放清單索引。
    /// </summary>
    /// <value>對應 mpv <c>playlist-pos</c>；以 0 起始。</value>
    [System.ComponentModel.Category("MediaEmbedKit.Mpv")]
    public int PlaylistIndex
    {
        get { return (int)GetValue(PlaylistIndexProperty); }
        set { SetValue(PlaylistIndexProperty, value); }
    }

    /// <summary>
    /// 取得或設定目前章節索引。
    /// </summary>
    /// <value>對應 mpv <c>chapter</c>；以 0 起始，<see langword="null"/> 代表無章節或尚未載入。</value>
    [System.ComponentModel.Category("MediaEmbedKit.Mpv")]
    public int? Chapter
    {
        get { return (int?)GetValue(ChapterProperty); }
        set { SetValue(ChapterProperty, value); }
    }

    /// <summary>
    /// 附加 WinUI 視窗，讓控制項可以建立 HWND 後端。
    /// </summary>
    /// <param name="hostWindow">控制項所在的 WinUI 視窗。</param>
    public void Attach(Window hostWindow)
    {
        if (hostWindow == null)
        {
            throw new ArgumentNullException(nameof(hostWindow));
        }

        DetachHostWindowClosedHandler();
        _hostWindow = hostWindow;
        _hostWindow.Closed += HostWindowClosed;
        Attach(WindowNative.GetWindowHandle(hostWindow));
    }

    /// <summary>
    /// 附加父視窗控制代碼，讓控制項可以建立 HWND 後端。
    /// </summary>
    /// <param name="parentHwnd">可容納原生子視窗的父視窗控制代碼。</param>
    public void Attach(IntPtr parentHwnd)
    {
        EnsureNotDisposed();
        if (parentHwnd == IntPtr.Zero)
        {
            throw new ArgumentException("父視窗控制代碼不可為零。", nameof(parentHwnd));
        }

        if (_parentHwnd == parentHwnd)
        {
            return;
        }

        _parentHwnd = parentHwnd;
        if (IsLoaded)
        {
            EnsureHwndBackend();
        }
    }

    /// <summary>
    /// 載入檔案或網址作為播放項目。
    /// </summary>
    /// <param name="pathOrUrl">要載入的檔案路徑或媒體網址。</param>
    /// <param name="mode">播放項目加入播放清單的方式。</param>
    public void LoadFile(string pathOrUrl, MpvLoadFileMode mode = MpvLoadFileMode.Replace)
    {
        if (string.IsNullOrWhiteSpace(pathOrUrl))
        {
            throw new ArgumentException("媒體來源不可為空白。", nameof(pathOrUrl));
        }

        _pendingSource = pathOrUrl;
        _pendingMode = mode;
        if (MpvWinUiDesignMode.IsEnabled)
        {
            return;
        }

        EnsureHwndBackend();
    }

    /// <summary>
    /// 釋放控制項持有的播放後端。
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Loaded -= OnLoaded;
        DetachHostWindowClosedHandler();
        DisposeHwndBackend();
        Children.Clear();
    }

    /// <summary>
    /// 在控制項載入後建立 HWND 播放後端。
    /// </summary>
    /// <param name="sender">引發事件的物件。</param>
    /// <param name="e">事件資料。</param>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (MpvWinUiDesignMode.IsEnabled)
        {
            return;
        }

        EnsureHwndBackend();
    }

    /// <summary>
    /// 在覆蓋層內容變更時同步目前後端。
    /// </summary>
    /// <param name="dependencyObject">相依性屬性所屬物件。</param>
    /// <param name="e">相依性屬性變更資料。</param>
    private static void OverlayContentChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        MpvWinUiPlayer player = (MpvWinUiPlayer)dependencyObject;
        player.UpdateOverlayContent();
    }

    /// <summary>
    /// 在覆蓋層開啟狀態變更時同步目前後端。
    /// </summary>
    /// <param name="dependencyObject">相依性屬性所屬物件。</param>
    /// <param name="e">相依性屬性變更資料。</param>
    private static void OverlayOpenChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        MpvWinUiPlayer player = (MpvWinUiPlayer)dependencyObject;
        player.UpdateOverlayContent();
    }

    /// <summary>
    /// 確保 HWND 後端已建立。
    /// </summary>
    /// <returns>成功建立或已存在 HWND 後端時為 <see langword="true"/>。</returns>
    private bool EnsureHwndBackend()
    {
        if (_disposed || MpvWinUiDesignMode.IsEnabled)
        {
            return false;
        }

        if (!TryResolveParentHwnd())
        {
            return false;
        }

        try
        {
            if (_hwndPlayer == null)
            {
                _hwndPlayer = new MpvWinUiHwndPlayer();
                _hwndPlayer.PlayerCreated += BackendPlayerCreated;
            }

            PlayerOptions.CopyTo(_hwndPlayer.PlayerOptions);
            _hwndPlayer.OverlayContent = OverlayContent;
            _hwndPlayer.IsOverlayOpen = IsOverlayOpen;
            if (!Children.Contains(_hwndPlayer))
            {
                Children.Clear();
                Children.Add(_hwndPlayer);
            }

            _hwndPlayer.Attach(_parentHwnd);
            LastBackendError = null;
            if (!string.IsNullOrWhiteSpace(_pendingSource))
            {
                _hwndPlayer.LoadFile(_pendingSource!, _pendingMode);
            }

            return true;
        }
        catch (Exception exception)
        {
            LastBackendError = exception;
            DisposeHwndBackend();
            throw;
        }
    }

    /// <summary>
    /// 同步覆蓋層內容到目前使用中的後端。
    /// </summary>
    private void UpdateOverlayContent()
    {
        if (_hwndPlayer != null)
        {
            _hwndPlayer.OverlayContent = OverlayContent;
            _hwndPlayer.IsOverlayOpen = IsOverlayOpen;
        }
    }

    /// <summary>
    /// 嘗試取得可建立 HWND 後端的父視窗控制代碼。
    /// </summary>
    /// <returns>已取得父視窗控制代碼時為 <see langword="true"/>。</returns>
    private bool TryResolveParentHwnd()
    {
        if (_parentHwnd != IntPtr.Zero)
        {
            return true;
        }

        if (_hostWindow != null)
        {
            _parentHwnd = WindowNative.GetWindowHandle(_hostWindow);
            return _parentHwnd != IntPtr.Zero;
        }

        if (XamlRoot == null || XamlRoot.ContentIslandEnvironment == null)
        {
            return false;
        }

        WindowId windowId = XamlRoot.ContentIslandEnvironment.AppWindowId;
        _parentHwnd = Win32Interop.GetWindowFromWindowId(windowId);
        return _parentHwnd != IntPtr.Zero;
    }

    /// <summary>
    /// 處理內部後端建立播放器的事件。
    /// </summary>
    /// <param name="sender">引發事件的物件。</param>
    /// <param name="e">事件資料。</param>
    private void BackendPlayerCreated(object? sender, EventArgs e)
    {
        MpvPlayer? player = _hwndPlayer == null ? null : _hwndPlayer.Player;
        if (player != null)
        {
            AttachPlayerBindings(player);
        }

        RaiseCommandsCanExecuteChanged();
        PlayerCreated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 將控制項的 DP 與目前播放器雙向綁定。
    /// </summary>
    /// <param name="player">已初始化的播放器。</param>
    private void AttachPlayerBindings(MpvPlayer player)
    {
        DetachPlayerBindings();

        _boundPlayer = player;
        _propertyWatchers.Add(player.WatchProperty<bool>("pause").Subscribe(new MpvDpObserver<bool>(value => UpdateFromPlayer(IsPausedProperty, value))));
        _propertyWatchers.Add(player.WatchProperty<bool>("mute").Subscribe(new MpvDpObserver<bool>(value => UpdateFromPlayer(IsMutedProperty, value))));
        _propertyWatchers.Add(player.WatchProperty<double>("volume").Subscribe(new MpvDpObserver<double>(value => UpdateFromPlayer(VolumeProperty, value))));
        _propertyWatchers.Add(player.WatchProperty<double>("time-pos").Subscribe(new MpvDpObserver<double>(value => UpdateFromPlayer(PositionProperty, TimeSpan.FromSeconds(value)))));
        _propertyWatchers.Add(player.WatchProperty<double>("duration").Subscribe(new MpvDpObserver<double>(value => UpdateFromPlayer(DurationProperty, TimeSpan.FromSeconds(value)))));
        _propertyWatchers.Add(player.WatchProperty<long>("playlist-pos").Subscribe(new MpvDpObserver<long>(value => UpdateFromPlayer(PlaylistIndexProperty, checked((int)value)))));
        _propertyWatchers.Add(player.WatchProperty<long>("chapter").Subscribe(new MpvDpObserver<long>(value => UpdateFromPlayer(ChapterProperty, value < 0 ? (int?)null : checked((int)value)))));
        player.StateChanged += OnPlayerStateChanged;

        if (IsPaused != player.Pause)
        {
            try { player.Pause = IsPaused; } catch (MpvException) { }
        }

        if (IsMuted != player.Mute)
        {
            try { player.Mute = IsMuted; } catch (MpvException) { }
        }

        if (Math.Abs(Volume - player.Volume) > 0.01)
        {
            try { player.Volume = Volume; } catch (MpvException) { }
        }
    }

    /// <summary>
    /// 在 UI 執行緒以「來自 player」的標記更新 DP，避免回頭再寫 player 觸發迴圈。
    /// </summary>
    /// <typeparam name="T">屬性值型別。</typeparam>
    /// <param name="property">要更新的 DP。</param>
    /// <param name="value">新值。</param>
    private void UpdateFromPlayer<T>(DependencyProperty property, T value)
    {
        if (_dispatcherQueue == null)
        {
            return;
        }

        _dispatcherQueue.TryEnqueue(() =>
        {
            if (_disposed)
            {
                return;
            }

            _suppressPlayerWrite = true;
            try
            {
                SetValue(property, value);
            }
            finally
            {
                _suppressPlayerWrite = false;
            }
        });
    }

    /// <summary>
    /// 處理 <see cref="MpvPlayer.StateChanged"/> 並把新狀態寫進 <see cref="PlaybackState"/>。
    /// </summary>
    /// <param name="sender">引發事件的播放器。</param>
    /// <param name="state">新的播放狀態。</param>
    private void OnPlayerStateChanged(object? sender, MpvPlaybackState state)
    {
        UpdateFromPlayer(PlaybackStateProperty, state);
    }

    /// <summary>
    /// 處理 <see cref="SourceProperty"/> 變更：載入新媒體。
    /// </summary>
    /// <param name="dependencyObject">屬性所屬的相依性物件。</param>
    /// <param name="e">屬性變更資料。</param>
    private static void SourceChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        MpvWinUiPlayer control = (MpvWinUiPlayer)dependencyObject;
        if (control._suppressPlayerWrite)
        {
            return;
        }

        string? newSource = e.NewValue as string;
        control._pendingSource = newSource;
        control._pendingMode = MpvLoadFileMode.Replace;

        if (string.IsNullOrWhiteSpace(newSource) || control._hwndPlayer == null)
        {
            return;
        }

        try
        {
            control._hwndPlayer.LoadFile(newSource!, MpvLoadFileMode.Replace);
        }
        catch (MpvException)
        {
        }
    }

    /// <summary>
    /// 處理 <see cref="PositionProperty"/> 變更：seek 到指定位置。
    /// </summary>
    /// <param name="dependencyObject">屬性所屬的相依性物件。</param>
    /// <param name="e">屬性變更資料。</param>
    private static void PositionChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        MpvWinUiPlayer control = (MpvWinUiPlayer)dependencyObject;
        if (control._suppressPlayerWrite || control._boundPlayer == null)
        {
            return;
        }

        try
        {
            control._boundPlayer.Seek(((TimeSpan)e.NewValue).TotalSeconds, "absolute");
        }
        catch (MpvException)
        {
        }
    }

    /// <summary>
    /// 處理 <see cref="VolumeProperty"/> 變更：寫入 player。
    /// </summary>
    /// <param name="dependencyObject">屬性所屬的相依性物件。</param>
    /// <param name="e">屬性變更資料。</param>
    private static void VolumeChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        MpvWinUiPlayer control = (MpvWinUiPlayer)dependencyObject;
        if (control._suppressPlayerWrite || control._boundPlayer == null)
        {
            return;
        }

        try
        {
            control._boundPlayer.Volume = (double)e.NewValue;
        }
        catch (MpvException)
        {
        }
    }

    /// <summary>
    /// 處理 <see cref="IsPausedProperty"/> 變更：寫入 player。
    /// </summary>
    /// <param name="dependencyObject">屬性所屬的相依性物件。</param>
    /// <param name="e">屬性變更資料。</param>
    private static void IsPausedChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        MpvWinUiPlayer control = (MpvWinUiPlayer)dependencyObject;
        if (control._suppressPlayerWrite || control._boundPlayer == null)
        {
            return;
        }

        try
        {
            control._boundPlayer.Pause = (bool)e.NewValue;
        }
        catch (MpvException)
        {
        }
    }

    /// <summary>
    /// 處理 <see cref="IsMutedProperty"/> 變更：寫入 player。
    /// </summary>
    /// <param name="dependencyObject">屬性所屬的相依性物件。</param>
    /// <param name="e">屬性變更資料。</param>
    private static void IsMutedChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        MpvWinUiPlayer control = (MpvWinUiPlayer)dependencyObject;
        if (control._suppressPlayerWrite || control._boundPlayer == null)
        {
            return;
        }

        try
        {
            control._boundPlayer.Mute = (bool)e.NewValue;
        }
        catch (MpvException)
        {
        }
    }

    /// <summary>
    /// 處理 <see cref="PlaylistIndexProperty"/> 變更：寫入 player；負數忽略。
    /// </summary>
    /// <param name="dependencyObject">屬性所屬的相依性物件。</param>
    /// <param name="e">屬性變更資料。</param>
    private static void PlaylistIndexChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        MpvWinUiPlayer control = (MpvWinUiPlayer)dependencyObject;
        if (control._suppressPlayerWrite || control._boundPlayer == null)
        {
            return;
        }

        int newIndex = (int)e.NewValue;
        if (newIndex < 0)
        {
            return;
        }

        try
        {
            control._boundPlayer.PlaylistIndex = newIndex;
        }
        catch (MpvException)
        {
        }
    }

    /// <summary>
    /// 處理 <see cref="ChapterProperty"/> 變更：寫入 player；null 或負數忽略。
    /// </summary>
    /// <param name="dependencyObject">屬性所屬的相依性物件。</param>
    /// <param name="e">屬性變更資料。</param>
    private static void ChapterChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        MpvWinUiPlayer control = (MpvWinUiPlayer)dependencyObject;
        if (control._suppressPlayerWrite || control._boundPlayer == null)
        {
            return;
        }

        int? newChapter = (int?)e.NewValue;
        if (!newChapter.HasValue || newChapter.Value < 0)
        {
            return;
        }

        try
        {
            control._boundPlayer.Chapter = newChapter.Value;
        }
        catch (MpvException)
        {
        }
        catch (ArgumentOutOfRangeException)
        {
        }
    }

    /// <summary>
    /// 處理 <see cref="DurationProperty"/> 變更：偵測到外部寫入時回退舊值，模擬唯讀語意。
    /// </summary>
    /// <param name="dependencyObject">屬性所屬的相依性物件。</param>
    /// <param name="e">屬性變更資料。</param>
    private static void OnDurationChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        MpvWinUiPlayer control = (MpvWinUiPlayer)dependencyObject;
        if (control._suppressPlayerWrite)
        {
            return;
        }

        TimeSpan oldValue = (TimeSpan)e.OldValue;
        if (oldValue == (TimeSpan)e.NewValue)
        {
            return;
        }

        control._suppressPlayerWrite = true;
        try
        {
            control.SetValue(DurationProperty, oldValue);
        }
        finally
        {
            control._suppressPlayerWrite = false;
        }
    }

    /// <summary>
    /// 處理 <see cref="PlaybackStateProperty"/> 變更：偵測到外部寫入時回退舊值，模擬唯讀語意。
    /// </summary>
    /// <param name="dependencyObject">屬性所屬的相依性物件。</param>
    /// <param name="e">屬性變更資料。</param>
    private static void OnPlaybackStateChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        MpvWinUiPlayer control = (MpvWinUiPlayer)dependencyObject;
        if (control._suppressPlayerWrite)
        {
            return;
        }

        MpvPlaybackState oldValue = (MpvPlaybackState)e.OldValue;
        if (oldValue == (MpvPlaybackState)e.NewValue)
        {
            return;
        }

        control._suppressPlayerWrite = true;
        try
        {
            control.SetValue(PlaybackStateProperty, oldValue);
        }
        finally
        {
            control._suppressPlayerWrite = false;
        }
    }

    /// <summary>
    /// 提供將 <see cref="IObservable{T}"/> 訂閱包裝成委派的最小 observer。
    /// </summary>
    /// <typeparam name="T">屬性值型別。</typeparam>
    private sealed class MpvDpObserver<T> : IObserver<T>
    {
        /// <summary>
        /// 收到新值時要執行的委派。
        /// </summary>
        private readonly Action<T> _onNext;

        /// <summary>
        /// 初始化 <see cref="MpvDpObserver{T}"/> 類別的新執行個體。
        /// </summary>
        /// <param name="onNext">收到新值時要執行的委派。</param>
        public MpvDpObserver(Action<T> onNext)
        {
            _onNext = onNext;
        }

        /// <summary>
        /// 在訂閱因 player 釋放結束時通知；目前不做事。
        /// </summary>
        public void OnCompleted()
        {
        }

        /// <summary>
        /// 在訂閱收到例外狀況時通知；目前不做事。
        /// </summary>
        /// <param name="error">例外狀況。</param>
        public void OnError(Exception error)
        {
        }

        /// <summary>
        /// 收到新值並轉發。
        /// </summary>
        /// <param name="value">新值。</param>
        public void OnNext(T value)
        {
            _onNext(value);
        }
    }

    /// <summary>
    /// 釋放 <see cref="AttachPlayerBindings"/> 建立的訂閱與事件。
    /// </summary>
    private void DetachPlayerBindings()
    {
        bool hadPlayer = _boundPlayer != null;
        if (_boundPlayer != null)
        {
            _boundPlayer.StateChanged -= OnPlayerStateChanged;
            _boundPlayer = null;
        }

        for (int index = 0; index < _propertyWatchers.Count; index++)
        {
            try
            {
                _propertyWatchers[index].Dispose();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        _propertyWatchers.Clear();

        if (hadPlayer)
        {
            RaiseCommandsCanExecuteChanged();
        }
    }

    /// <summary>
    /// 在附加的 WinUI 視窗關閉時釋放控制項資源。
    /// </summary>
    /// <param name="sender">引發事件的物件。</param>
    /// <param name="args">視窗關閉事件資料。</param>
    private void HostWindowClosed(object sender, WindowEventArgs args)
    {
        Dispose();
    }

    /// <summary>
    /// 釋放 HWND 播放後端。
    /// </summary>
    private void DisposeHwndBackend()
    {
        DetachPlayerBindings();

        if (_hwndPlayer == null)
        {
            return;
        }

        _hwndPlayer.OverlayContent = null;
        _hwndPlayer.PlayerCreated -= BackendPlayerCreated;
        Children.Remove(_hwndPlayer);
        _hwndPlayer.Dispose();
        _hwndPlayer = null;
    }

    /// <summary>
    /// 移除附加視窗的關閉事件處理常式。
    /// </summary>
    private void DetachHostWindowClosedHandler()
    {
        if (_hostWindow == null)
        {
            return;
        }

        _hostWindow.Closed -= HostWindowClosed;
        _hostWindow = null;
    }

    /// <summary>
    /// 確認目前控制項尚未釋放。
    /// </summary>
    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }
    }

}

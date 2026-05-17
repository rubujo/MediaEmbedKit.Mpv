using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace MediaEmbedKit.Mpv.Wpf;

/// <summary>
/// 提供 WPF 用的 libmpv 播放主控項。
/// </summary>
[ToolboxItem(true)]
[DesignTimeVisible(true)]
[DefaultProperty(nameof(OverlayContent))]
public class MpvWpfPlayer : HwndHost
{
    /// <summary>
    /// WPF 主控項建立的原生子視窗控制代碼。
    /// </summary>
    private IntPtr _hwnd;
    /// <summary>
    /// 目前主控項持有的 mpv 播放器執行個體。
    /// </summary>
    private MpvPlayer? _player;
    /// <summary>
    /// 目前由控制項管理的 AirSpace 覆蓋層 Popup。
    /// </summary>
    private MpvAirspacePopup? _overlayPopup;
    /// <summary>
    /// 已附加的播放器屬性訂閱清單；player dispose 時一次釋放。
    /// </summary>
    private readonly List<IDisposable> _propertyWatchers = new List<IDisposable>();
    /// <summary>
    /// 表示目前 DP 變更來源是 player（避免回頭再寫入 player 造成循環）。
    /// </summary>
    private bool _suppressPlayerWrite;

    /// <summary>
    /// 識別 <see cref="OverlayContent"/> 相依性屬性。
    /// </summary>
    public static readonly DependencyProperty OverlayContentProperty = DependencyProperty.Register(
        nameof(OverlayContent),
        typeof(UIElement),
        typeof(MpvWpfPlayer),
        new PropertyMetadata(null, OverlayContentChanged));

    /// <summary>
    /// 識別 <see cref="IsOverlayOpen"/> 相依性屬性。
    /// </summary>
    public static readonly DependencyProperty IsOverlayOpenProperty = DependencyProperty.Register(
        nameof(IsOverlayOpen),
        typeof(bool),
        typeof(MpvWpfPlayer),
        new PropertyMetadata(true, OverlayOpenChanged));

    /// <summary>
    /// 識別 <see cref="Source"/> 相依性屬性。
    /// </summary>
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source),
        typeof(string),
        typeof(MpvWpfPlayer),
        new PropertyMetadata(null, SourceChanged));

    /// <summary>
    /// 識別 <see cref="Position"/> 相依性屬性。
    /// </summary>
    public static readonly DependencyProperty PositionProperty = DependencyProperty.Register(
        nameof(Position),
        typeof(TimeSpan),
        typeof(MpvWpfPlayer),
        new PropertyMetadata(TimeSpan.Zero, PositionChanged));

    /// <summary>
    /// 唯讀 <see cref="Duration"/> 相依性屬性的金鑰。
    /// </summary>
    private static readonly DependencyPropertyKey DurationPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(Duration),
        typeof(TimeSpan),
        typeof(MpvWpfPlayer),
        new PropertyMetadata(TimeSpan.Zero));

    /// <summary>
    /// 識別 <see cref="Duration"/> 相依性屬性（唯讀）。
    /// </summary>
    public static readonly DependencyProperty DurationProperty = DurationPropertyKey.DependencyProperty;

    /// <summary>
    /// 識別 <see cref="Volume"/> 相依性屬性。
    /// </summary>
    public static readonly DependencyProperty VolumeProperty = DependencyProperty.Register(
        nameof(Volume),
        typeof(double),
        typeof(MpvWpfPlayer),
        new PropertyMetadata(100.0, VolumeChanged));

    /// <summary>
    /// 識別 <see cref="IsPaused"/> 相依性屬性。
    /// </summary>
    public static readonly DependencyProperty IsPausedProperty = DependencyProperty.Register(
        nameof(IsPaused),
        typeof(bool),
        typeof(MpvWpfPlayer),
        new PropertyMetadata(false, IsPausedChanged));

    /// <summary>
    /// 識別 <see cref="IsMuted"/> 相依性屬性。
    /// </summary>
    public static readonly DependencyProperty IsMutedProperty = DependencyProperty.Register(
        nameof(IsMuted),
        typeof(bool),
        typeof(MpvWpfPlayer),
        new PropertyMetadata(false, IsMutedChanged));

    /// <summary>
    /// 唯讀 <see cref="PlaybackState"/> 相依性屬性的金鑰。
    /// </summary>
    private static readonly DependencyPropertyKey PlaybackStatePropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(PlaybackState),
        typeof(MpvPlaybackState),
        typeof(MpvWpfPlayer),
        new PropertyMetadata(MpvPlaybackState.Idle));

    /// <summary>
    /// 識別 <see cref="PlaybackState"/> 相依性屬性（唯讀）。
    /// </summary>
    public static readonly DependencyProperty PlaybackStateProperty = PlaybackStatePropertyKey.DependencyProperty;

    /// <summary>
    /// 識別 <see cref="PlaylistIndex"/> 相依性屬性。
    /// </summary>
    public static readonly DependencyProperty PlaylistIndexProperty = DependencyProperty.Register(
        nameof(PlaylistIndex),
        typeof(int),
        typeof(MpvWpfPlayer),
        new PropertyMetadata(0, PlaylistIndexChanged));

    /// <summary>
    /// 識別 <see cref="Chapter"/> 相依性屬性。
    /// </summary>
    public static readonly DependencyProperty ChapterProperty = DependencyProperty.Register(
        nameof(Chapter),
        typeof(int?),
        typeof(MpvWpfPlayer),
        new PropertyMetadata(null, ChapterChanged));

    /// <summary>
    /// 初始化 <see cref="MpvWpfPlayer"/> 類別的新執行個體。
    /// </summary>
    public MpvWpfPlayer()
    {
        PlayerOptions = new MpvPlayerOptions();
        Loaded += PlayerLoaded;
        Unloaded += PlayerUnloaded;
        IsVisibleChanged += PlayerIsVisibleChanged;

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
        return _player != null;
    }

    /// <summary>
    /// 執行 <see cref="PlayCommand"/>。
    /// </summary>
    private void ExecutePlay()
    {
        if (_player == null)
        {
            return;
        }

        try { _player.Pause = false; } catch (MpvException) { }
    }

    /// <summary>
    /// 執行 <see cref="PauseCommand"/>。
    /// </summary>
    private void ExecutePause()
    {
        if (_player == null)
        {
            return;
        }

        try { _player.Pause = true; } catch (MpvException) { }
    }

    /// <summary>
    /// 執行 <see cref="StopCommand"/>。
    /// </summary>
    private void ExecuteStop()
    {
        if (_player == null)
        {
            return;
        }

        try { _player.Stop(); } catch (MpvException) { }
    }

    /// <summary>
    /// 執行 <see cref="TogglePauseCommand"/>。
    /// </summary>
    private void ExecuteTogglePause()
    {
        if (_player == null)
        {
            return;
        }

        try { _player.Pause = !_player.Pause; } catch (MpvException) { }
    }

    /// <summary>
    /// 執行 <see cref="ToggleMuteCommand"/>。
    /// </summary>
    private void ExecuteToggleMute()
    {
        if (_player == null)
        {
            return;
        }

        try { _player.Mute = !_player.Mute; } catch (MpvException) { }
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
    /// 在主控項建立 mpv 播放器後發生。
    /// </summary>
    public event EventHandler? PlayerCreated;

    /// <summary>
    /// 取得主控項建立播放器時使用的選項。
    /// </summary>
    /// <value>播放器建立選項。</value>
    [System.ComponentModel.Browsable(false)]
    public MpvPlayerOptions PlayerOptions { get; private set; }

    /// <summary>
    /// 取得主控項目前建立的播放器。
    /// </summary>
    /// <value>目前播放器；尚未建立時為 <see langword="null"/>。</value>
    [System.ComponentModel.Browsable(false)]
    public MpvPlayer? Player
    {
        get { return _player; }
    }

    /// <summary>
    /// 取得或設定要由控制項自行放入 AirSpace 覆蓋層的 WPF 內容。
    /// </summary>
    /// <value>顯示在影片上方的 WPF 元素；未設定時為 <see langword="null"/>。</value>
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
    /// <value>對應 mpv <c>duration</c>；尚未取得時為 <see cref="TimeSpan.Zero"/>。</value>
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
    /// 載入檔案或網址作為播放項目。
    /// </summary>
    /// <param name="pathOrUrl">要載入的檔案路徑或媒體網址。</param>
    /// <param name="mode">播放項目加入播放清單的方式。</param>
    public void LoadFile(string pathOrUrl, MpvLoadFileMode mode = MpvLoadFileMode.Replace)
    {
        if (IsInDesignMode())
        {
            return;
        }

        EnsurePlayer();
        MpvPlayer? player = _player;
        if (player == null)
        {
            throw new InvalidOperationException("播放器尚未建立。");
        }

        player.LoadFile(pathOrUrl, mode);
    }

    /// <summary>
    /// 建立可傳給 libmpv 的原生子視窗。
    /// </summary>
    /// <param name="hwndParent">WPF 提供的父視窗控制代碼。</param>
    /// <returns>新建立的原生子視窗控制代碼包裝。</returns>
    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        bool isInDesignMode = IsInDesignMode();
        int windowStyle = NativeMethods.WS_CHILD | NativeMethods.WS_VISIBLE | NativeMethods.WS_CLIPSIBLINGS | NativeMethods.WS_CLIPCHILDREN;
        if (isInDesignMode)
        {
            windowStyle |= NativeMethods.SS_CENTER | NativeMethods.SS_CENTERIMAGE;
        }

        _hwnd = NativeMethods.CreateWindowEx(
            0,
            "STATIC",
            isInDesignMode ? "MediaEmbedKit.Mpv" : string.Empty,
            windowStyle,
            0,
            0,
            Math.Max(1, (int)ActualWidth),
            Math.Max(1, (int)ActualHeight),
            hwndParent.Handle,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero);

        if (_hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException("Unable to create the native child window used by libmpv.");
        }

        if (!isInDesignMode)
        {
            try
            {
                EnsurePlayer();
            }
            catch
            {
                NativeMethods.DestroyWindow(_hwnd);
                _hwnd = IntPtr.Zero;
                throw;
            }
        }

        return new HandleRef(this, _hwnd);
    }

    /// <summary>
    /// 銷毀先前建立的原生子視窗。
    /// </summary>
    /// <param name="hwnd">要銷毀的原生子視窗控制代碼包裝。</param>
    protected override void DestroyWindowCore(HandleRef hwnd)
    {
        DisposePlayer();

        if (hwnd.Handle != IntPtr.Zero)
        {
            NativeMethods.DestroyWindow(hwnd.Handle);
        }

        _hwnd = IntPtr.Zero;
    }

    /// <summary>
    /// 在 WPF 版面配置尺寸變更時同步更新原生子視窗大小。
    /// </summary>
    /// <param name="sizeInfo">WPF 提供的大小變更資訊。</param>
    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        if (_hwnd != IntPtr.Zero)
        {
            NativeMethods.SetWindowPos(
                _hwnd,
                IntPtr.Zero,
                0,
                0,
                Math.Max(1, (int)ActualWidth),
                Math.Max(1, (int)ActualHeight),
                NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
        }

        _overlayPopup?.UpdateBounds();
    }

    /// <summary>
    /// 釋放主控項使用的 mpv 播放器資源。
    /// </summary>
    /// <param name="disposing">由受控程式碼釋放時為 <see langword="true"/>。</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Loaded -= PlayerLoaded;
            Unloaded -= PlayerUnloaded;
            IsVisibleChanged -= PlayerIsVisibleChanged;
            DisposeOverlayPopup();
            DisposePlayer();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// 在覆蓋層內容相依性屬性變更時重建 Popup。
    /// </summary>
    /// <param name="dependencyObject">屬性所屬的相依性物件。</param>
    /// <param name="e">相依性屬性變更資料。</param>
    private static void OverlayContentChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        MpvWpfPlayer player = (MpvWpfPlayer)dependencyObject;
        player.ReplaceOverlayContent(e.NewValue as UIElement);
    }

    /// <summary>
    /// 在覆蓋層開啟狀態變更時同步 Popup。
    /// </summary>
    /// <param name="dependencyObject">屬性所屬的相依性物件。</param>
    /// <param name="e">相依性屬性變更資料。</param>
    private static void OverlayOpenChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        MpvWpfPlayer player = (MpvWpfPlayer)dependencyObject;
        player.ApplyOverlayState();
    }

    /// <summary>
    /// 在控制項載入時開啟需要顯示的 AirSpace 覆蓋層。
    /// </summary>
    /// <param name="sender">引發事件的物件。</param>
    /// <param name="e">事件資料。</param>
    private void PlayerLoaded(object sender, RoutedEventArgs e)
    {
        ApplyOverlayState();
    }

    /// <summary>
    /// 在控制項卸載時關閉 AirSpace 覆蓋層。
    /// </summary>
    /// <param name="sender">引發事件的物件。</param>
    /// <param name="e">事件資料。</param>
    private void PlayerUnloaded(object sender, RoutedEventArgs e)
    {
        if (_overlayPopup != null)
        {
            _overlayPopup.IsOpen = false;
        }
    }

    /// <summary>
    /// 在控制項可見度變更時同步 AirSpace 覆蓋層。
    /// </summary>
    /// <param name="sender">引發事件的物件。</param>
    /// <param name="e">相依性屬性變更資料。</param>
    private void PlayerIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        ApplyOverlayState();
    }

    /// <summary>
    /// 使用新的 WPF 元素重建控制項內建 AirSpace 覆蓋層。
    /// </summary>
    /// <param name="content">要顯示在影片上方的 WPF 元素。</param>
    private void ReplaceOverlayContent(UIElement? content)
    {
        DisposeOverlayPopup();
        if (content != null)
        {
            _overlayPopup = new MpvAirspacePopup(this, content);
        }

        ApplyOverlayState();
    }

    /// <summary>
    /// 依目前控制項狀態套用 AirSpace 覆蓋層開啟狀態。
    /// </summary>
    private void ApplyOverlayState()
    {
        if (_overlayPopup == null)
        {
            return;
        }

        _overlayPopup.IsOpen = IsOverlayOpen && IsLoaded && IsVisible;
    }

    /// <summary>
    /// 釋放控制項內建的 AirSpace 覆蓋層。
    /// </summary>
    private void DisposeOverlayPopup()
    {
        if (_overlayPopup == null)
        {
            return;
        }

        _overlayPopup.Dispose();
        _overlayPopup = null;
    }

    /// <summary>
    /// 確保主控項已建立並初始化 mpv 播放器。
    /// </summary>
    private void EnsurePlayer()
    {
        if (IsInDesignMode())
        {
            return;
        }

        if (_player != null)
        {
            return;
        }

        if (_hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException("The WPF host window has not been created yet.");
        }

        MpvPlayer player = new MpvPlayer(PlayerOptions);
        try
        {
            player.SetVideoWindow(_hwnd);
            player.Initialize();
            _player = player;
        }
        catch
        {
            player.Dispose();
            throw;
        }

        AttachPlayerBindings(_player);
        RaiseCommandsCanExecuteChanged();
        PlayerCreated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 將控制項的 DP 與目前播放器雙向綁定。
    /// </summary>
    /// <param name="player">已初始化的播放器。</param>
    private void AttachPlayerBindings(MpvPlayer player)
    {
        _propertyWatchers.Add(player.WatchProperty<bool>("pause").Subscribe(new MpvDpObserver<bool>(value => UpdateFromPlayer(IsPausedProperty, value))));
        _propertyWatchers.Add(player.WatchProperty<bool>("mute").Subscribe(new MpvDpObserver<bool>(value => UpdateFromPlayer(IsMutedProperty, value))));
        _propertyWatchers.Add(player.WatchProperty<double>("volume").Subscribe(new MpvDpObserver<double>(value => UpdateFromPlayer(VolumeProperty, value))));
        _propertyWatchers.Add(player.WatchProperty<double>("time-pos").Subscribe(new MpvDpObserver<double>(value => UpdateFromPlayer(PositionProperty, TimeSpan.FromSeconds(value)))));
        _propertyWatchers.Add(player.WatchProperty<double>("duration").Subscribe(new MpvDpObserver<double>(value => UpdateReadOnlyFromPlayer(DurationPropertyKey, TimeSpan.FromSeconds(value)))));
        _propertyWatchers.Add(player.WatchProperty<long>("playlist-pos").Subscribe(new MpvDpObserver<long>(value => UpdateFromPlayer(PlaylistIndexProperty, checked((int)value)))));
        _propertyWatchers.Add(player.WatchProperty<long>("chapter").Subscribe(new MpvDpObserver<long>(value => UpdateFromPlayer(ChapterProperty, value < 0 ? (int?)null : checked((int)value)))));
        player.StateChanged += OnPlayerStateChanged;

        if (!string.IsNullOrWhiteSpace(Source))
        {
            try
            {
                player.Load(new MpvMediaItem(Source!));
            }
            catch (MpvException)
            {
            }
        }

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
    /// 在 UI 執行緒以「來自 player」的標記更新可讀寫 DP，避免回頭再寫 player 觸發迴圈。
    /// </summary>
    /// <typeparam name="T">屬性值型別。</typeparam>
    /// <param name="property">要更新的 DP。</param>
    /// <param name="value">新值。</param>
    private void UpdateFromPlayer<T>(DependencyProperty property, T value)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _suppressPlayerWrite = true;
            try
            {
                SetValue(property, value);
            }
            finally
            {
                _suppressPlayerWrite = false;
            }
        }));
    }

    /// <summary>
    /// 在 UI 執行緒更新唯讀 DP。
    /// </summary>
    /// <typeparam name="T">屬性值型別。</typeparam>
    /// <param name="key">DP 金鑰。</param>
    /// <param name="value">新值。</param>
    private void UpdateReadOnlyFromPlayer<T>(DependencyPropertyKey key, T value)
    {
        Dispatcher.BeginInvoke(new Action(() => SetValue(key, value)));
    }

    /// <summary>
    /// 處理 <see cref="MpvPlayer.StateChanged"/> 並把新狀態寫進 <see cref="PlaybackState"/>。
    /// </summary>
    /// <param name="sender">引發事件的播放器。</param>
    /// <param name="state">新的播放狀態。</param>
    private void OnPlayerStateChanged(object? sender, MpvPlaybackState state)
    {
        UpdateReadOnlyFromPlayer<MpvPlaybackState>(PlaybackStatePropertyKey, state);
    }

    /// <summary>
    /// 處理 <see cref="SourceProperty"/> 變更：載入新媒體。
    /// </summary>
    /// <param name="dependencyObject">屬性所屬的相依性物件。</param>
    /// <param name="e">屬性變更資料。</param>
    private static void SourceChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        MpvWpfPlayer control = (MpvWpfPlayer)dependencyObject;
        if (control._suppressPlayerWrite)
        {
            return;
        }

        string? newSource = e.NewValue as string;
        if (string.IsNullOrWhiteSpace(newSource) || control._player == null)
        {
            return;
        }

        try
        {
            control._player.Load(new MpvMediaItem(newSource!));
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
        MpvWpfPlayer control = (MpvWpfPlayer)dependencyObject;
        if (control._suppressPlayerWrite || control._player == null)
        {
            return;
        }

        try
        {
            control._player.Seek(((TimeSpan)e.NewValue).TotalSeconds, "absolute");
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
        MpvWpfPlayer control = (MpvWpfPlayer)dependencyObject;
        if (control._suppressPlayerWrite || control._player == null)
        {
            return;
        }

        try
        {
            control._player.Volume = (double)e.NewValue;
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
        MpvWpfPlayer control = (MpvWpfPlayer)dependencyObject;
        if (control._suppressPlayerWrite || control._player == null)
        {
            return;
        }

        try
        {
            control._player.Pause = (bool)e.NewValue;
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
        MpvWpfPlayer control = (MpvWpfPlayer)dependencyObject;
        if (control._suppressPlayerWrite || control._player == null)
        {
            return;
        }

        try
        {
            control._player.Mute = (bool)e.NewValue;
        }
        catch (MpvException)
        {
        }
    }

    /// <summary>
    /// 處理 <see cref="PlaylistIndexProperty"/> 變更：寫入 player。
    /// </summary>
    /// <param name="dependencyObject">屬性所屬的相依性物件。</param>
    /// <param name="e">屬性變更資料。</param>
    private static void PlaylistIndexChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        MpvWpfPlayer control = (MpvWpfPlayer)dependencyObject;
        if (control._suppressPlayerWrite || control._player == null)
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
            control._player.PlaylistIndex = newIndex;
        }
        catch (MpvException)
        {
        }
    }

    /// <summary>
    /// 處理 <see cref="ChapterProperty"/> 變更：寫入 player；值為 null 或負數時忽略。
    /// </summary>
    /// <param name="dependencyObject">屬性所屬的相依性物件。</param>
    /// <param name="e">屬性變更資料。</param>
    private static void ChapterChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        MpvWpfPlayer control = (MpvWpfPlayer)dependencyObject;
        if (control._suppressPlayerWrite || control._player == null)
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
            control._player.Chapter = newChapter.Value;
        }
        catch (MpvException)
        {
        }
        catch (ArgumentOutOfRangeException)
        {
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
    /// 判斷主控項目前是否在 XAML 設計工具中執行。
    /// </summary>
    /// <returns>主控項位於設計階段時為 <see langword="true"/>。</returns>
    private bool IsInDesignMode()
    {
        return DesignerProperties.GetIsInDesignMode(this);
    }

    /// <summary>
    /// 釋放目前主控項持有的 mpv 播放器。
    /// </summary>
    private void DisposePlayer()
    {
        if (_player == null)
        {
            return;
        }

        _player.StateChanged -= OnPlayerStateChanged;
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

        _player.Dispose();
        _player = null;
        RaiseCommandsCanExecuteChanged();
    }

    /// <summary>
    /// 宣告 WPF HwndHost 控制項使用的 Win32 API。
    /// </summary>
    private static class NativeMethods
    {
        /// <summary>
        /// 建立子視窗的 Win32 樣式。
        /// </summary>
        internal const int WS_CHILD = 0x40000000;
        /// <summary>
        /// 顯示視窗的 Win32 樣式。
        /// </summary>
        internal const int WS_VISIBLE = 0x10000000;
        /// <summary>
        /// 裁切子視窗的 Win32 樣式。
        /// </summary>
        internal const int WS_CLIPCHILDREN = 0x02000000;
        /// <summary>
        /// 裁切同層視窗的 Win32 樣式。
        /// </summary>
        internal const int WS_CLIPSIBLINGS = 0x04000000;
        /// <summary>
        /// 讓設計階段替代文字水平置中的 STATIC 樣式。
        /// </summary>
        internal const int SS_CENTER = 0x00000001;
        /// <summary>
        /// 讓設計階段替代文字垂直置中的 STATIC 樣式。
        /// </summary>
        internal const int SS_CENTERIMAGE = 0x00000200;
        /// <summary>
        /// 保持目前 Z 順序的 SetWindowPos 旗標。
        /// </summary>
        internal const int SWP_NOZORDER = 0x0004;
        /// <summary>
        /// 避免啟用視窗的 SetWindowPos 旗標。
        /// </summary>
        internal const int SWP_NOACTIVATE = 0x0010;

        /// <summary>
        /// 建立 Win32 視窗。
        /// </summary>
        /// <param name="dwExStyle">延伸視窗樣式。</param>
        /// <param name="lpClassName">視窗類別名稱。</param>
        /// <param name="lpWindowName">視窗名稱。</param>
        /// <param name="dwStyle">視窗樣式。</param>
        /// <param name="x">視窗左上角 X 座標。</param>
        /// <param name="y">視窗左上角 Y 座標。</param>
        /// <param name="nWidth">視窗寬度。</param>
        /// <param name="nHeight">視窗高度。</param>
        /// <param name="hWndParent">父視窗控制代碼。</param>
        /// <param name="hMenu">功能表控制代碼。</param>
        /// <param name="hInstance">執行個體控制代碼。</param>
        /// <param name="lpParam">建立參數指標。</param>
        /// <returns>新建立視窗的控制代碼。</returns>
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr CreateWindowEx(
            int dwExStyle,
            string lpClassName,
            string lpWindowName,
            int dwStyle,
            int x,
            int y,
            int nWidth,
            int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam);

        /// <summary>
        /// 銷毀指定的 Win32 視窗。
        /// </summary>
        /// <param name="hwnd">要銷毀的視窗控制代碼。</param>
        /// <returns>作業成功時為 <see langword="true"/>。</returns>
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool DestroyWindow(IntPtr hwnd);

        /// <summary>
        /// 設定指定 Win32 視窗的位置與大小。
        /// </summary>
        /// <param name="hwnd">要調整的視窗控制代碼。</param>
        /// <param name="hwndInsertAfter">Z 順序參考視窗控制代碼。</param>
        /// <param name="x">新的 X 座標。</param>
        /// <param name="y">新的 Y 座標。</param>
        /// <param name="cx">新的寬度。</param>
        /// <param name="cy">新的高度。</param>
        /// <param name="flags">SetWindowPos 旗標。</param>
        /// <returns>作業成功時為 <see langword="true"/>。</returns>
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool SetWindowPos(
            IntPtr hwnd,
            IntPtr hwndInsertAfter,
            int x,
            int y,
            int cx,
            int cy,
            int flags);
    }
}

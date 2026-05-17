using System;
using System.Collections.Generic;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using WinUiElement = Microsoft.UI.Xaml.UIElement;

using MediaEmbedKit.Mpv.WinUI;

namespace MediaEmbedKit.Mpv.Maui;

/// <summary>
/// 提供 .NET MAUI Windows 使用的 libmpv 播放檢視。
/// </summary>
/// <remarks>
/// <para>
/// 本檢視目前僅在 Windows 平台具備實際的播放實作；底層平台控制項是
/// <see cref="MediaEmbedKit.Mpv.WinUI.MpvWinUiPlayer"/>，由
/// <see cref="MpvViewHandler"/> 負責橋接。其他平台（Android、iOS、MacCatalyst、Tizen）
/// 雖可建立 <see cref="MpvView"/> 與其 handler，但呼叫
/// <see cref="MpvViewHandler.LoadFile(string, MpvLoadFileMode)"/> 等需要平台後端的 API
/// 會擲回 <see cref="PlatformNotSupportedException"/>。
/// </para>
/// <para>
/// 整體支援矩陣請參考 <c>docs/SUPPORT_MATRIX.md</c>；如需跨平台 libmpv，請改用核心
/// <see cref="MediaEmbedKit.Mpv.MpvPlayer"/> 搭配自家的 native window 整合。
/// </para>
/// </remarks>
public class MpvView : View
{
    /// <summary>
    /// 識別 <see cref="Source"/> 可繫結屬性。
    /// </summary>
    public static readonly BindableProperty SourceProperty = BindableProperty.Create(
        nameof(Source),
        typeof(string),
        typeof(MpvView),
        default(string),
        propertyChanged: OnSourceChanged);

    /// <summary>
    /// 識別 <see cref="OverlayView"/> 可繫結屬性。
    /// </summary>
    public static readonly BindableProperty OverlayViewProperty = BindableProperty.Create(
        nameof(OverlayView),
        typeof(View),
        typeof(MpvView),
        default(View),
        propertyChanged: OnOverlayViewChanged);

    /// <summary>
    /// 識別 <see cref="OverlayContent"/> 可繫結屬性。
    /// </summary>
    public static readonly BindableProperty OverlayContentProperty = BindableProperty.Create(
        nameof(OverlayContent),
        typeof(WinUiElement),
        typeof(MpvView),
        default(WinUiElement),
        propertyChanged: OnOverlayContentChanged);

    /// <summary>
    /// 識別 <see cref="IsOverlayOpen"/> 可繫結屬性。
    /// </summary>
    public static readonly BindableProperty IsOverlayOpenProperty = BindableProperty.Create(
        nameof(IsOverlayOpen),
        typeof(bool),
        typeof(MpvView),
        true,
        propertyChanged: OnIsOverlayOpenChanged);

    /// <summary>
    /// 識別 <see cref="Position"/> 可繫結屬性。
    /// </summary>
    public static readonly BindableProperty PositionProperty = BindableProperty.Create(
        nameof(Position),
        typeof(TimeSpan),
        typeof(MpvView),
        TimeSpan.Zero,
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanged: OnPositionChanged);

    /// <summary>
    /// 唯讀 <see cref="Duration"/> 可繫結屬性的金鑰。
    /// </summary>
    private static readonly BindablePropertyKey DurationPropertyKey = BindableProperty.CreateReadOnly(
        nameof(Duration),
        typeof(TimeSpan),
        typeof(MpvView),
        TimeSpan.Zero);

    /// <summary>
    /// 識別 <see cref="Duration"/> 可繫結屬性（唯讀）。
    /// </summary>
    public static readonly BindableProperty DurationProperty = DurationPropertyKey.BindableProperty;

    /// <summary>
    /// 識別 <see cref="Volume"/> 可繫結屬性。
    /// </summary>
    public static readonly BindableProperty VolumeProperty = BindableProperty.Create(
        nameof(Volume),
        typeof(double),
        typeof(MpvView),
        100.0,
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanged: OnVolumeChanged);

    /// <summary>
    /// 識別 <see cref="IsPaused"/> 可繫結屬性。
    /// </summary>
    public static readonly BindableProperty IsPausedProperty = BindableProperty.Create(
        nameof(IsPaused),
        typeof(bool),
        typeof(MpvView),
        false,
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanged: OnIsPausedChanged);

    /// <summary>
    /// 識別 <see cref="IsMuted"/> 可繫結屬性。
    /// </summary>
    public static readonly BindableProperty IsMutedProperty = BindableProperty.Create(
        nameof(IsMuted),
        typeof(bool),
        typeof(MpvView),
        false,
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanged: OnIsMutedChanged);

    /// <summary>
    /// 唯讀 <see cref="PlaybackState"/> 可繫結屬性的金鑰。
    /// </summary>
    private static readonly BindablePropertyKey PlaybackStatePropertyKey = BindableProperty.CreateReadOnly(
        nameof(PlaybackState),
        typeof(MpvPlaybackState),
        typeof(MpvView),
        MpvPlaybackState.Idle);

    /// <summary>
    /// 識別 <see cref="PlaybackState"/> 可繫結屬性（唯讀）。
    /// </summary>
    public static readonly BindableProperty PlaybackStateProperty = PlaybackStatePropertyKey.BindableProperty;

    /// <summary>
    /// 識別 <see cref="PlaylistIndex"/> 可繫結屬性。
    /// </summary>
    public static readonly BindableProperty PlaylistIndexProperty = BindableProperty.Create(
        nameof(PlaylistIndex),
        typeof(int),
        typeof(MpvView),
        0,
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanged: OnPlaylistIndexChanged);

    /// <summary>
    /// 識別 <see cref="Chapter"/> 可繫結屬性。
    /// </summary>
    public static readonly BindableProperty ChapterProperty = BindableProperty.Create(
        nameof(Chapter),
        typeof(int?),
        typeof(MpvView),
        null,
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanged: OnChapterChanged);

    /// <summary>
    /// 保存尚未交給平台 handler 的載入模式。
    /// </summary>
    private MpvLoadFileMode _pendingMode = MpvLoadFileMode.Replace;

    /// <summary>
    /// 已附加的播放器屬性訂閱清單；player dispose 時一次釋放。
    /// </summary>
    private readonly List<IDisposable> _propertyWatchers = new List<IDisposable>();

    /// <summary>
    /// 表示目前 BindableProperty 變更來源是 player（避免回頭再寫入 player 造成循環）。
    /// </summary>
    private bool _suppressPlayerWrite;

    /// <summary>
    /// 在平台 handler 建立 libmpv 播放器後發生。
    /// </summary>
    public event EventHandler? PlayerCreated;

    /// <summary>
    /// 初始化 <see cref="MpvView"/> 類別的新執行個體。
    /// </summary>
    public MpvView()
    {
        PlayerOptions = new MpvPlayerOptions();

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
        return Player != null;
    }

    /// <summary>
    /// 執行 <see cref="PlayCommand"/>。
    /// </summary>
    private void ExecutePlay()
    {
        if (Player == null)
        {
            return;
        }

        try { Player.Pause = false; } catch (MpvException) { }
    }

    /// <summary>
    /// 執行 <see cref="PauseCommand"/>。
    /// </summary>
    private void ExecutePause()
    {
        if (Player == null)
        {
            return;
        }

        try { Player.Pause = true; } catch (MpvException) { }
    }

    /// <summary>
    /// 執行 <see cref="StopCommand"/>。
    /// </summary>
    private void ExecuteStop()
    {
        if (Player == null)
        {
            return;
        }

        try { Player.Stop(); } catch (MpvException) { }
    }

    /// <summary>
    /// 執行 <see cref="TogglePauseCommand"/>。
    /// </summary>
    private void ExecuteTogglePause()
    {
        if (Player == null)
        {
            return;
        }

        try { Player.Pause = !Player.Pause; } catch (MpvException) { }
    }

    /// <summary>
    /// 執行 <see cref="ToggleMuteCommand"/>。
    /// </summary>
    private void ExecuteToggleMute()
    {
        if (Player == null)
        {
            return;
        }

        try { Player.Mute = !Player.Mute; } catch (MpvException) { }
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
    /// 取得建立播放器時使用的選項。
    /// </summary>
    /// <value>播放器建立選項。</value>
    [System.ComponentModel.Browsable(false)]
    public MpvPlayerOptions PlayerOptions { get; private set; }

    /// <summary>
    /// 取得目前平台 handler 建立的播放器。
    /// </summary>
    /// <value>目前播放器；尚未建立時為 <see langword="null"/>。</value>
    [System.ComponentModel.Browsable(false)]
    public MpvPlayer? Player { get; private set; }

    /// <summary>
    /// 取得目前是否在 MAUI 設計工具中執行。
    /// </summary>
    /// <value>檢視位於設計階段時為 <see langword="true"/>。</value>
    public bool IsDesignMode
    {
        get { return DesignMode.IsDesignModeEnabled; }
    }

    /// <summary>
    /// 取得或設定要載入的媒體來源。
    /// </summary>
    /// <value>檔案路徑或媒體網址。</value>
    [System.ComponentModel.Category("MediaEmbedKit.Mpv")]
    public string? Source
    {
        get { return (string?)GetValue(SourceProperty); }
        set { SetValue(SourceProperty, value); }
    }

    /// <summary>
    /// 取得或設定建議使用的 MAUI 覆蓋層檢視。
    /// </summary>
    /// <value>由 handler 轉換並顯示在視訊上方的 MAUI 檢視；未設定時為 <see langword="null"/>。</value>
    [System.ComponentModel.Category("MediaEmbedKit.Mpv")]
    public View? OverlayView
    {
        get { return (View?)GetValue(OverlayViewProperty); }
        set { SetValue(OverlayViewProperty, value); }
    }

    /// <summary>
    /// 取得或設定 Windows 原生 WinUI 覆蓋層內容。
    /// </summary>
    /// <value>直接交給 Windows 平台控制項的 WinUI 元素；未設定時為 <see langword="null"/>。</value>
    [System.ComponentModel.Category("MediaEmbedKit.Mpv")]
    public WinUiElement? OverlayContent
    {
        get { return (WinUiElement?)GetValue(OverlayContentProperty); }
        set { SetValue(OverlayContentProperty, value); }
    }

    /// <summary>
    /// 取得或設定 Windows 平台控制項管理的 AirSpace 覆蓋層是否開啟。
    /// </summary>
    /// <value>覆蓋層應保持開啟時為 <see langword="true"/>。</value>
    [System.ComponentModel.Category("MediaEmbedKit.Mpv")]
    public bool IsOverlayOpen
    {
        get { return (bool)GetValue(IsOverlayOpenProperty); }
        set { SetValue(IsOverlayOpenProperty, value); }
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
    /// 取得等待平台 handler 建立後載入的媒體來源。
    /// </summary>
    /// <value>等待載入的檔案路徑或媒體網址。</value>
    internal string? PendingSource { get; private set; }

    /// <summary>
    /// 取得等待平台 handler 建立後套用的載入模式。
    /// </summary>
    /// <value>等待套用的載入模式。</value>
    internal MpvLoadFileMode PendingMode
    {
        get { return _pendingMode; }
    }

    /// <summary>
    /// 載入檔案或網址作為播放項目。
    /// </summary>
    /// <param name="pathOrUrl">要載入的檔案路徑或媒體網址。</param>
    /// <param name="mode">播放項目加入播放清單的方式。</param>
    public void LoadFile(string pathOrUrl, MpvLoadFileMode mode = MpvLoadFileMode.Replace)
    {
        PendingSource = pathOrUrl;
        _pendingMode = mode;

        if (DesignMode.IsDesignModeEnabled)
        {
            return;
        }

        if (Handler is MpvViewHandler handler)
        {
            handler.LoadFile(pathOrUrl, mode);
        }
    }

    /// <summary>
    /// 從平台 handler 同步目前播放器參考。
    /// </summary>
    /// <param name="player">平台 handler 建立的播放器；中斷連線時為 <see langword="null"/>。</param>
    internal void SetPlayer(MpvPlayer? player)
    {
        if (ReferenceEquals(Player, player))
        {
            return;
        }

        DetachPlayerBindings();

        Player = player;
        if (player != null)
        {
            AttachPlayerBindings(player);
            RaiseCommandsCanExecuteChanged();
            PlayerCreated?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            RaiseCommandsCanExecuteChanged();
        }
    }

    /// <summary>
    /// 將檢視的 BindableProperty 與目前播放器雙向綁定。
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
    /// 釋放 <see cref="AttachPlayerBindings"/> 建立的訂閱與事件。
    /// </summary>
    private void DetachPlayerBindings()
    {
        if (Player != null)
        {
            Player.StateChanged -= OnPlayerStateChanged;
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
    }

    /// <summary>
    /// 在 UI 執行緒以「來自 player」的標記更新可讀寫 BindableProperty，避免回頭再寫 player 觸發迴圈。
    /// </summary>
    /// <typeparam name="T">屬性值型別。</typeparam>
    /// <param name="property">要更新的 BindableProperty。</param>
    /// <param name="value">新值。</param>
    private void UpdateFromPlayer<T>(BindableProperty property, T value)
    {
        IDispatcher? dispatcher = Dispatcher;
        if (dispatcher == null)
        {
            return;
        }

        dispatcher.Dispatch(() =>
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
        });
    }

    /// <summary>
    /// 在 UI 執行緒更新唯讀 BindableProperty。
    /// </summary>
    /// <typeparam name="T">屬性值型別。</typeparam>
    /// <param name="key">屬性金鑰。</param>
    /// <param name="value">新值。</param>
    private void UpdateReadOnlyFromPlayer<T>(BindablePropertyKey key, T value)
    {
        IDispatcher? dispatcher = Dispatcher;
        if (dispatcher == null)
        {
            return;
        }

        dispatcher.Dispatch(() => SetValue(key, value));
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
    /// 在 <see cref="Source"/> 屬性變更時載入新的媒體來源。
    /// </summary>
    /// <param name="bindable">屬性所屬的可繫結物件。</param>
    /// <param name="oldValue">屬性先前的值。</param>
    /// <param name="newValue">屬性新的值。</param>
    private static void OnSourceChanged(BindableObject bindable, object oldValue, object newValue)
    {
        MpvView view = (MpvView)bindable;
        if (view._suppressPlayerWrite)
        {
            return;
        }

        string? source = newValue as string;
        if (!string.IsNullOrWhiteSpace(source))
        {
            view.LoadFile(source!);
        }
    }

    /// <summary>
    /// 在 <see cref="Position"/> 屬性變更時 seek 到指定位置。
    /// </summary>
    /// <param name="bindable">屬性所屬的可繫結物件。</param>
    /// <param name="oldValue">屬性先前的值。</param>
    /// <param name="newValue">屬性新的值。</param>
    private static void OnPositionChanged(BindableObject bindable, object oldValue, object newValue)
    {
        MpvView view = (MpvView)bindable;
        if (view._suppressPlayerWrite || view.Player == null)
        {
            return;
        }

        try
        {
            view.Player.Seek(((TimeSpan)newValue).TotalSeconds, "absolute");
        }
        catch (MpvException)
        {
        }
    }

    /// <summary>
    /// 在 <see cref="Volume"/> 屬性變更時寫入 player。
    /// </summary>
    /// <param name="bindable">屬性所屬的可繫結物件。</param>
    /// <param name="oldValue">屬性先前的值。</param>
    /// <param name="newValue">屬性新的值。</param>
    private static void OnVolumeChanged(BindableObject bindable, object oldValue, object newValue)
    {
        MpvView view = (MpvView)bindable;
        if (view._suppressPlayerWrite || view.Player == null)
        {
            return;
        }

        try
        {
            view.Player.Volume = (double)newValue;
        }
        catch (MpvException)
        {
        }
    }

    /// <summary>
    /// 在 <see cref="IsPaused"/> 屬性變更時寫入 player。
    /// </summary>
    /// <param name="bindable">屬性所屬的可繫結物件。</param>
    /// <param name="oldValue">屬性先前的值。</param>
    /// <param name="newValue">屬性新的值。</param>
    private static void OnIsPausedChanged(BindableObject bindable, object oldValue, object newValue)
    {
        MpvView view = (MpvView)bindable;
        if (view._suppressPlayerWrite || view.Player == null)
        {
            return;
        }

        try
        {
            view.Player.Pause = (bool)newValue;
        }
        catch (MpvException)
        {
        }
    }

    /// <summary>
    /// 在 <see cref="IsMuted"/> 屬性變更時寫入 player。
    /// </summary>
    /// <param name="bindable">屬性所屬的可繫結物件。</param>
    /// <param name="oldValue">屬性先前的值。</param>
    /// <param name="newValue">屬性新的值。</param>
    private static void OnIsMutedChanged(BindableObject bindable, object oldValue, object newValue)
    {
        MpvView view = (MpvView)bindable;
        if (view._suppressPlayerWrite || view.Player == null)
        {
            return;
        }

        try
        {
            view.Player.Mute = (bool)newValue;
        }
        catch (MpvException)
        {
        }
    }

    /// <summary>
    /// 在 <see cref="PlaylistIndex"/> 屬性變更時寫入 player；負數忽略。
    /// </summary>
    /// <param name="bindable">屬性所屬的可繫結物件。</param>
    /// <param name="oldValue">屬性先前的值。</param>
    /// <param name="newValue">屬性新的值。</param>
    private static void OnPlaylistIndexChanged(BindableObject bindable, object oldValue, object newValue)
    {
        MpvView view = (MpvView)bindable;
        if (view._suppressPlayerWrite || view.Player == null)
        {
            return;
        }

        int newIndex = (int)newValue;
        if (newIndex < 0)
        {
            return;
        }

        try
        {
            view.Player.PlaylistIndex = newIndex;
        }
        catch (MpvException)
        {
        }
    }

    /// <summary>
    /// 在 <see cref="Chapter"/> 屬性變更時寫入 player；null 或負數忽略。
    /// </summary>
    /// <param name="bindable">屬性所屬的可繫結物件。</param>
    /// <param name="oldValue">屬性先前的值。</param>
    /// <param name="newValue">屬性新的值。</param>
    private static void OnChapterChanged(BindableObject bindable, object oldValue, object newValue)
    {
        MpvView view = (MpvView)bindable;
        if (view._suppressPlayerWrite || view.Player == null)
        {
            return;
        }

        int? newChapter = (int?)newValue;
        if (!newChapter.HasValue || newChapter.Value < 0)
        {
            return;
        }

        try
        {
            view.Player.Chapter = newChapter.Value;
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
    /// 在 <see cref="OverlayView"/> 屬性變更時同步平台控制項。
    /// </summary>
    /// <param name="bindable">屬性所屬的可繫結物件。</param>
    /// <param name="oldValue">屬性先前的值。</param>
    /// <param name="newValue">屬性新的值。</param>
    private static void OnOverlayViewChanged(BindableObject bindable, object oldValue, object newValue)
    {
        MpvView view = (MpvView)bindable;
        if (view.Handler is MpvViewHandler handler)
        {
            handler.UpdateOverlayContent();
        }
    }

    /// <summary>
    /// 在 <see cref="OverlayContent"/> 屬性變更時同步平台控制項。
    /// </summary>
    /// <param name="bindable">屬性所屬的可繫結物件。</param>
    /// <param name="oldValue">屬性先前的值。</param>
    /// <param name="newValue">屬性新的值。</param>
    private static void OnOverlayContentChanged(BindableObject bindable, object oldValue, object newValue)
    {
        MpvView view = (MpvView)bindable;
        if (view.Handler is MpvViewHandler handler)
        {
            handler.UpdateOverlayContent();
        }
    }

    /// <summary>
    /// 在 <see cref="IsOverlayOpen"/> 屬性變更時同步平台控制項。
    /// </summary>
    /// <param name="bindable">屬性所屬的可繫結物件。</param>
    /// <param name="oldValue">屬性先前的值。</param>
    /// <param name="newValue">屬性新的值。</param>
    private static void OnIsOverlayOpenChanged(BindableObject bindable, object oldValue, object newValue)
    {
        MpvView view = (MpvView)bindable;
        if (view.Handler is MpvViewHandler handler)
        {
            handler.UpdateOverlayContent();
        }
    }
}

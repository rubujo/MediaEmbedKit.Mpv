using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Threading;
using MediaEmbedKit.Mpv.Render;

namespace MediaEmbedKit.Mpv.Avalonia;

/// <summary>
/// 提供以 Avalonia OpenGL render API 組合的 libmpv 播放控制項。
/// </summary>
public sealed class MpvAvaloniaPlayer : OpenGlControlBase, IDisposable
{
    /// <summary>
    /// 目前控制項持有的 mpv 播放器執行個體。
    /// </summary>
    private MpvPlayer? _player;
    /// <summary>
    /// 目前控制項持有的 libmpv OpenGL render API 內容。
    /// </summary>
    private MpvOpenGlRenderContext? _renderContext;
    /// <summary>
    /// 等待 OpenGL render API 內容建立後載入的媒體來源。
    /// </summary>
    private string? _pendingSource;
    /// <summary>
    /// 等待 OpenGL render API 內容建立後套用的載入模式。
    /// </summary>
    private MpvLoadFileMode _pendingMode = MpvLoadFileMode.Replace;
    /// <summary>
    /// 表示目前是否已將下一次轉譯排入 Avalonia UI 佇列。
    /// </summary>
    private bool _renderQueued;
    /// <summary>
    /// 表示目前控制項是否已釋放。
    /// </summary>
    private bool _disposed;
    /// <summary>
    /// 已附加的播放器屬性訂閱清單；player dispose 時一次釋放。
    /// </summary>
    private readonly List<IDisposable> _propertyWatchers = new List<IDisposable>();
    /// <summary>
    /// 表示目前屬性變更來源是 player（避免回頭再寫入 player 造成循環）。
    /// </summary>
    private bool _suppressPlayerWrite;

    /// <summary>
    /// 識別 <see cref="Source"/> 屬性。
    /// </summary>
    public static readonly StyledProperty<string?> SourceProperty =
        AvaloniaProperty.Register<MpvAvaloniaPlayer, string?>(nameof(Source));

    /// <summary>
    /// 識別 <see cref="Position"/> 屬性。
    /// </summary>
    public static readonly StyledProperty<TimeSpan> PositionProperty =
        AvaloniaProperty.Register<MpvAvaloniaPlayer, TimeSpan>(nameof(Position));

    /// <summary>
    /// 識別 <see cref="Duration"/> 唯讀屬性。
    /// </summary>
    public static readonly DirectProperty<MpvAvaloniaPlayer, TimeSpan> DurationProperty =
        AvaloniaProperty.RegisterDirect<MpvAvaloniaPlayer, TimeSpan>(nameof(Duration), control => control._duration);

    /// <summary>
    /// 識別 <see cref="Volume"/> 屬性。
    /// </summary>
    public static readonly StyledProperty<double> VolumeProperty =
        AvaloniaProperty.Register<MpvAvaloniaPlayer, double>(nameof(Volume), 100.0);

    /// <summary>
    /// 識別 <see cref="IsPaused"/> 屬性。
    /// </summary>
    public static readonly StyledProperty<bool> IsPausedProperty =
        AvaloniaProperty.Register<MpvAvaloniaPlayer, bool>(nameof(IsPaused));

    /// <summary>
    /// 識別 <see cref="IsMuted"/> 屬性。
    /// </summary>
    public static readonly StyledProperty<bool> IsMutedProperty =
        AvaloniaProperty.Register<MpvAvaloniaPlayer, bool>(nameof(IsMuted));

    /// <summary>
    /// 識別 <see cref="PlaybackState"/> 唯讀屬性。
    /// </summary>
    public static readonly DirectProperty<MpvAvaloniaPlayer, MpvPlaybackState> PlaybackStateProperty =
        AvaloniaProperty.RegisterDirect<MpvAvaloniaPlayer, MpvPlaybackState>(nameof(PlaybackState), control => control._playbackState);

    /// <summary>
    /// 識別 <see cref="PlaylistIndex"/> 屬性。
    /// </summary>
    public static readonly StyledProperty<int> PlaylistIndexProperty =
        AvaloniaProperty.Register<MpvAvaloniaPlayer, int>(nameof(PlaylistIndex));

    /// <summary>
    /// 識別 <see cref="Chapter"/> 屬性。
    /// </summary>
    public static readonly StyledProperty<int?> ChapterProperty =
        AvaloniaProperty.Register<MpvAvaloniaPlayer, int?>(nameof(Chapter));

    /// <summary>
    /// 由 <see cref="DurationProperty"/> 反射的目前媒體時長。
    /// </summary>
    private TimeSpan _duration;
    /// <summary>
    /// 由 <see cref="PlaybackStateProperty"/> 反射的目前播放狀態。
    /// </summary>
    private MpvPlaybackState _playbackState;

    /// <summary>
    /// 初始化 <see cref="MpvAvaloniaPlayer"/> 類別的新執行個體。
    /// </summary>
    public MpvAvaloniaPlayer()
    {
        PlayerOptions = new MpvPlayerOptions();
        if (Design.IsDesignMode)
        {
            Design.SetPreviewWith(this, CreateDesignPreview());
        }

        SourceProperty.Changed.AddClassHandler<MpvAvaloniaPlayer>((control, args) => control.OnSourceChanged(args));
        PositionProperty.Changed.AddClassHandler<MpvAvaloniaPlayer>((control, args) => control.OnPositionChanged(args));
        VolumeProperty.Changed.AddClassHandler<MpvAvaloniaPlayer>((control, args) => control.OnVolumeChanged(args));
        IsPausedProperty.Changed.AddClassHandler<MpvAvaloniaPlayer>((control, args) => control.OnIsPausedChanged(args));
        IsMutedProperty.Changed.AddClassHandler<MpvAvaloniaPlayer>((control, args) => control.OnIsMutedChanged(args));
        PlaylistIndexProperty.Changed.AddClassHandler<MpvAvaloniaPlayer>((control, args) => control.OnPlaylistIndexChanged(args));
        ChapterProperty.Changed.AddClassHandler<MpvAvaloniaPlayer>((control, args) => control.OnChapterChanged(args));

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
    /// <value>
    /// 對應 mpv <c>pause=no</c>。
    /// </value>
    [System.ComponentModel.Category("MediaEmbedKit.Mpv")]
    public ICommand PlayCommand
    {
        get { return _playCommand; }
    }

    /// <summary>
    /// 取得暫停播放的指令。
    /// </summary>
    /// <value>
    /// 對應 mpv <c>pause=yes</c>。
    /// </value>
    [System.ComponentModel.Category("MediaEmbedKit.Mpv")]
    public ICommand PauseCommand
    {
        get { return _pauseCommand; }
    }

    /// <summary>
    /// 取得停止播放的指令。
    /// </summary>
    /// <value>
    /// 對應 mpv <c>stop</c>。
    /// </value>
    [System.ComponentModel.Category("MediaEmbedKit.Mpv")]
    public ICommand StopCommand
    {
        get { return _stopCommand; }
    }

    /// <summary>
    /// 取得切換暫停狀態的指令。
    /// </summary>
    /// <value>
    /// 切換 mpv <c>pause</c>。
    /// </value>
    [System.ComponentModel.Category("MediaEmbedKit.Mpv")]
    public ICommand TogglePauseCommand
    {
        get { return _togglePauseCommand; }
    }

    /// <summary>
    /// 取得切換靜音狀態的指令。
    /// </summary>
    /// <value>
    /// 切換 mpv <c>mute</c>。
    /// </value>
    [System.ComponentModel.Category("MediaEmbedKit.Mpv")]
    public ICommand ToggleMuteCommand
    {
        get { return _toggleMuteCommand; }
    }

    /// <summary>
    /// 判斷指令目前是否有可用播放器。
    /// </summary>
    /// <returns>
    /// 已綁定播放器時為 <see langword="true"/>。
    /// </returns>
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
    /// 取得或設定要載入並播放的媒體來源。
    /// </summary>
    /// <value>
    /// 檔案路徑或媒體網址；變更會自動載入新媒體。
    /// </value>
    [System.ComponentModel.Category("MediaEmbedKit.Mpv")]
    public string? Source
    {
        get { return GetValue(SourceProperty); }
        set { SetValue(SourceProperty, value); }
    }

    /// <summary>
    /// 取得或設定目前播放位置。
    /// </summary>
    /// <value>
    /// 對應 mpv <c>time-pos</c>；雙向繫結時設值會觸發 seek。
    /// </value>
    [System.ComponentModel.Category("MediaEmbedKit.Mpv")]
    public TimeSpan Position
    {
        get { return GetValue(PositionProperty); }
        set { SetValue(PositionProperty, value); }
    }

    /// <summary>
    /// 取得目前媒體總時長。
    /// </summary>
    /// <value>
    /// 對應 mpv <c>duration</c>。
    /// </value>
    [System.ComponentModel.Category("MediaEmbedKit.Mpv")]
    public TimeSpan Duration
    {
        get { return _duration; }
        private set { SetAndRaise(DurationProperty, ref _duration, value); }
    }

    /// <summary>
    /// 取得或設定音量。
    /// </summary>
    /// <value>
    /// 對應 mpv <c>volume</c>，範圍 0–130；預設 100。
    /// </value>
    [System.ComponentModel.Category("MediaEmbedKit.Mpv")]
    public double Volume
    {
        get { return GetValue(VolumeProperty); }
        set { SetValue(VolumeProperty, value); }
    }

    /// <summary>
    /// 取得或設定是否暫停。
    /// </summary>
    /// <value>
    /// 對應 mpv <c>pause</c>。
    /// </value>
    [System.ComponentModel.Category("MediaEmbedKit.Mpv")]
    public bool IsPaused
    {
        get { return GetValue(IsPausedProperty); }
        set { SetValue(IsPausedProperty, value); }
    }

    /// <summary>
    /// 取得或設定是否靜音。
    /// </summary>
    /// <value>
    /// 對應 mpv <c>mute</c>。
    /// </value>
    [System.ComponentModel.Category("MediaEmbedKit.Mpv")]
    public bool IsMuted
    {
        get { return GetValue(IsMutedProperty); }
        set { SetValue(IsMutedProperty, value); }
    }

    /// <summary>
    /// 取得目前由 libmpv 事件聚合而成的播放狀態。
    /// </summary>
    /// <value>
    /// 對應 <see cref="MpvPlayer.State"/>。
    /// </value>
    [System.ComponentModel.Category("MediaEmbedKit.Mpv")]
    public MpvPlaybackState PlaybackState
    {
        get { return _playbackState; }
        private set { SetAndRaise(PlaybackStateProperty, ref _playbackState, value); }
    }

    /// <summary>
    /// 取得或設定目前播放清單索引。
    /// </summary>
    /// <value>
    /// 對應 mpv <c>playlist-pos</c>；以 0 起始。
    /// </value>
    [System.ComponentModel.Category("MediaEmbedKit.Mpv")]
    public int PlaylistIndex
    {
        get { return GetValue(PlaylistIndexProperty); }
        set { SetValue(PlaylistIndexProperty, value); }
    }

    /// <summary>
    /// 取得或設定目前章節索引。
    /// </summary>
    /// <value>
    /// 對應 mpv <c>chapter</c>；以 0 起始，<see langword="null"/> 代表無章節或尚未載入。
    /// </value>
    [System.ComponentModel.Category("MediaEmbedKit.Mpv")]
    public int? Chapter
    {
        get { return GetValue(ChapterProperty); }
        set { SetValue(ChapterProperty, value); }
    }

    /// <summary>
    /// 在控制項建立 mpv 播放器後發生。
    /// </summary>
    public event EventHandler? PlayerCreated;

    /// <summary>
    /// 取得控制項建立播放器時使用的選項。
    /// </summary>
    /// <value>
    /// 播放器建立選項。
    /// </value>
    [System.ComponentModel.Browsable(false)]
    public MpvPlayerOptions PlayerOptions { get; private set; }

    /// <summary>
    /// 取得控制項目前建立的播放器。
    /// </summary>
    /// <value>
    /// 目前播放器；尚未建立時為 <see langword="null"/>。
    /// </value>
    [System.ComponentModel.Browsable(false)]
    public MpvPlayer? Player
    {
        get { return _player; }
    }

    /// <summary>
    /// 取得目前是否已建立 libmpv OpenGL render API 內容。
    /// </summary>
    /// <value>
    /// OpenGL render API 內容已建立時為 <see langword="true"/>。
    /// </value>
    public bool IsRenderContextCreated
    {
        get { return _renderContext != null; }
    }

    /// <summary>
    /// 取得等待載入的媒體來源。
    /// </summary>
    /// <value>
    /// 等待載入的檔案路徑或媒體網址；沒有待載入項目時為 <see langword="null"/>。
    /// </value>
    [System.ComponentModel.Browsable(false)]
    public string? PendingSource
    {
        get { return _pendingSource; }
    }

    /// <summary>
    /// 載入檔案或網址作為播放項目。
    /// </summary>
    /// <param name="pathOrUrl">
    /// 要載入的檔案路徑或媒體網址。
    /// </param>
    /// <param name="mode">
    /// 播放項目加入播放清單的方式。
    /// </param>
    public void LoadFile(string pathOrUrl, MpvLoadFileMode mode = MpvLoadFileMode.Replace)
    {
        if (string.IsNullOrWhiteSpace(pathOrUrl))
        {
            throw new ArgumentException("媒體來源不可為空白。", nameof(pathOrUrl));
        }

        _pendingSource = pathOrUrl;
        _pendingMode = mode;

        if (Design.IsDesignMode)
        {
            return;
        }

        if (_player != null && _player.IsInitialized)
        {
            _player.LoadFile(pathOrUrl, mode);
        }
    }

    /// <summary>
    /// 釋放控制項持有的播放器與 OpenGL render API 內容。
    /// </summary>
    public void Dispose()
    {
        if (Volatile.Read(ref _disposed))
        {
            return;
        }

        Volatile.Write(ref _disposed, true);
        DisposeRenderContext();
        DisposePlayer();
    }

    /// <summary>
    /// 在 Avalonia 建立 OpenGL 內容時建立 libmpv render API 內容。
    /// </summary>
    /// <param name="gl">
    /// Avalonia 提供的 OpenGL 函式介面。
    /// </param>
    protected override void OnOpenGlInit(GlInterface gl)
    {
        if (Design.IsDesignMode)
        {
            return;
        }

        EnsureNotDisposed();
        EnsurePlayerAndRenderContext(gl);
    }

    /// <summary>
    /// 在 Avalonia 銷毀 OpenGL 內容時釋放 libmpv render API 內容。
    /// </summary>
    /// <param name="gl">
    /// Avalonia 提供的 OpenGL 函式介面。
    /// </param>
    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        DisposeRenderContext();
        DisposePlayer();
    }

    /// <summary>
    /// 在 Avalonia OpenGL 內容遺失時釋放 libmpv render API 內容。
    /// </summary>
    protected override void OnOpenGlLost()
    {
        DisposeRenderContext();
        DisposePlayer();
    }

    /// <summary>
    /// 將 libmpv 目前影格轉譯到 Avalonia 提供的 OpenGL framebuffer。
    /// </summary>
    /// <param name="gl">
    /// Avalonia 提供的 OpenGL 函式介面。
    /// </param>
    /// <param name="fb">
    /// Avalonia 提供的 OpenGL framebuffer 物件識別碼。
    /// </param>
    /// <remarks>
    /// 透過抓取 <see cref="_renderContext"/> 到 local 變數後使用，避免在
    /// libmpv 執行緒觸發 dispose 與 UI 執行緒同時繪製時，
    /// 對 render context 形成 use-after-dispose。
    /// </remarks>
    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (Design.IsDesignMode)
        {
            return;
        }

        if (Volatile.Read(ref _disposed))
        {
            return;
        }

        MpvOpenGlRenderContext? renderContext = _renderContext;
        if (renderContext == null)
        {
            return;
        }

        _renderQueued = false;
        renderContext.Update();

        double renderScaling = GetRenderScaling();
        int width = Math.Max(1, (int)Math.Round(Bounds.Width * renderScaling));
        int height = Math.Max(1, (int)Math.Round(Bounds.Height * renderScaling));
        renderContext.Render(fb, width, height, flipY: true);
        renderContext.ReportSwap();
    }

    /// <summary>
    /// 取得目前視覺根節點使用的實體像素縮放倍率。
    /// </summary>
    /// <returns>
    /// 實體像素相對於邏輯像素的縮放倍率。
    /// </returns>
    private double GetRenderScaling()
    {
        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        return topLevel == null ? 1.0 : topLevel.RenderScaling;
    }

    /// <summary>
    /// 確保播放器與 OpenGL render API 內容已建立。
    /// </summary>
    /// <param name="gl">
    /// Avalonia 提供的 OpenGL 函式介面。
    /// </param>
    private void EnsurePlayerAndRenderContext(GlInterface gl)
    {
        if (Design.IsDesignMode || _renderContext != null)
        {
            return;
        }

        MpvPlayer? player = null;
        MpvOpenGlRenderContext? renderContext = null;
        try
        {
            player = new MpvPlayer(PlayerOptions);
            player.SetOptionString("vo", "libmpv");
            player.Initialize();

            MpvOpenGlRenderContextOptions options = new MpvOpenGlRenderContextOptions(gl.GetProcAddress);
            renderContext = player.CreateOpenGlRenderContext(options);
            renderContext.UpdateAvailable += RenderContextUpdateAvailable;

            _player = player;
            _renderContext = renderContext;
            player = null;
            renderContext = null;
        }
        catch
        {
            if (renderContext != null)
            {
                renderContext.Dispose();
            }

            if (player != null)
            {
                player.Dispose();
            }

            throw;
        }

        AttachPlayerBindings(_player!);
        RaiseCommandsCanExecuteChanged();
        PlayerCreated?.Invoke(this, EventArgs.Empty);

        if (_player != null && !string.IsNullOrWhiteSpace(_pendingSource))
        {
            _player.LoadFile(_pendingSource!, _pendingMode);
        }
    }

    /// <summary>
    /// 將控制項的繫結屬性與目前播放器雙向綁定。
    /// </summary>
    /// <param name="player">
    /// 已初始化的播放器。
    /// </param>
    private void AttachPlayerBindings(MpvPlayer player)
    {
        _propertyWatchers.Add(player.WatchProperty<bool>("pause").Subscribe(new MpvDpObserver<bool>(value => UpdateFromPlayer(IsPausedProperty, value))));
        _propertyWatchers.Add(player.WatchProperty<bool>("mute").Subscribe(new MpvDpObserver<bool>(value => UpdateFromPlayer(IsMutedProperty, value))));
        _propertyWatchers.Add(player.WatchProperty<double>("volume").Subscribe(new MpvDpObserver<double>(value => UpdateFromPlayer(VolumeProperty, value))));
        _propertyWatchers.Add(player.WatchProperty<double>("time-pos").Subscribe(new MpvDpObserver<double>(value => UpdateFromPlayer(PositionProperty, TimeSpan.FromSeconds(value)))));
        _propertyWatchers.Add(player.WatchProperty<double>("duration").Subscribe(new MpvDpObserver<double>(value => UpdateDurationFromPlayer(TimeSpan.FromSeconds(value)))));
        _propertyWatchers.Add(player.WatchProperty<long>("playlist-pos").Subscribe(new MpvDpObserver<long>(value => UpdateFromPlayer(PlaylistIndexProperty, checked((int)value)))));
        _propertyWatchers.Add(player.WatchProperty<long>("chapter").Subscribe(new MpvDpObserver<long>(value => UpdateFromPlayer(ChapterProperty, value < 0 ? (int?)null : checked((int)value)))));
        player.StateChanged += OnPlayerStateChanged;

        string? currentSource = Source;
        if (!string.IsNullOrWhiteSpace(currentSource))
        {
            try
            {
                player.Load(new MpvMediaItem(currentSource!));
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
    /// 在 UI 執行緒以「來自 player」的標記更新可讀寫 StyledProperty，避免回頭再寫 player 觸發迴圈。
    /// </summary>
    /// <typeparam name="T">
    /// 屬性值型別。
    /// </typeparam>
    /// <param name="property">
    /// 要更新的 StyledProperty。
    /// </param>
    /// <param name="value">
    /// 新值。
    /// </param>
    private void UpdateFromPlayer<T>(StyledProperty<T> property, T value)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (Volatile.Read(ref _disposed))
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
    /// 在 UI 執行緒更新 <see cref="DurationProperty"/>。
    /// </summary>
    /// <param name="value">
    /// 新值。
    /// </param>
    private void UpdateDurationFromPlayer(TimeSpan value)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (Volatile.Read(ref _disposed))
            {
                return;
            }

            Duration = value;
        });
    }

    /// <summary>
    /// 在 UI 執行緒更新 <see cref="PlaybackStateProperty"/>。
    /// </summary>
    /// <param name="value">
    /// 新值。
    /// </param>
    private void UpdatePlaybackStateFromPlayer(MpvPlaybackState value)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (Volatile.Read(ref _disposed))
            {
                return;
            }

            PlaybackState = value;
        });
    }

    /// <summary>
    /// 處理 <see cref="MpvPlayer.StateChanged"/> 並把新狀態寫進 <see cref="PlaybackState"/>。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的播放器。
    /// </param>
    /// <param name="state">
    /// 新的播放狀態。
    /// </param>
    private void OnPlayerStateChanged(object? sender, MpvPlaybackState state)
    {
        UpdatePlaybackStateFromPlayer(state);
    }

    /// <summary>
    /// 處理 <see cref="SourceProperty"/> 變更：載入新媒體。
    /// </summary>
    /// <param name="args">
    /// 屬性變更資料。
    /// </param>
    private void OnSourceChanged(AvaloniaPropertyChangedEventArgs args)
    {
        if (_suppressPlayerWrite)
        {
            return;
        }

        string? newSource = args.GetNewValue<string?>();
        _pendingSource = newSource;

        if (string.IsNullOrWhiteSpace(newSource) || _player == null)
        {
            return;
        }

        try
        {
            _player.Load(new MpvMediaItem(newSource!));
        }
        catch (MpvException)
        {
        }
    }

    /// <summary>
    /// 處理 <see cref="PositionProperty"/> 變更：seek 到指定位置。
    /// </summary>
    /// <param name="args">
    /// 屬性變更資料。
    /// </param>
    private void OnPositionChanged(AvaloniaPropertyChangedEventArgs args)
    {
        if (_suppressPlayerWrite || _player == null)
        {
            return;
        }

        try
        {
            _player.Seek(args.GetNewValue<TimeSpan>().TotalSeconds, "absolute");
        }
        catch (MpvException)
        {
        }
    }

    /// <summary>
    /// 處理 <see cref="VolumeProperty"/> 變更：寫入 player。
    /// </summary>
    /// <param name="args">
    /// 屬性變更資料。
    /// </param>
    private void OnVolumeChanged(AvaloniaPropertyChangedEventArgs args)
    {
        if (_suppressPlayerWrite || _player == null)
        {
            return;
        }

        try
        {
            _player.Volume = args.GetNewValue<double>();
        }
        catch (MpvException)
        {
        }
    }

    /// <summary>
    /// 處理 <see cref="IsPausedProperty"/> 變更：寫入 player。
    /// </summary>
    /// <param name="args">
    /// 屬性變更資料。
    /// </param>
    private void OnIsPausedChanged(AvaloniaPropertyChangedEventArgs args)
    {
        if (_suppressPlayerWrite || _player == null)
        {
            return;
        }

        try
        {
            _player.Pause = args.GetNewValue<bool>();
        }
        catch (MpvException)
        {
        }
    }

    /// <summary>
    /// 處理 <see cref="IsMutedProperty"/> 變更：寫入 player。
    /// </summary>
    /// <param name="args">
    /// 屬性變更資料。
    /// </param>
    private void OnIsMutedChanged(AvaloniaPropertyChangedEventArgs args)
    {
        if (_suppressPlayerWrite || _player == null)
        {
            return;
        }

        try
        {
            _player.Mute = args.GetNewValue<bool>();
        }
        catch (MpvException)
        {
        }
    }

    /// <summary>
    /// 處理 <see cref="PlaylistIndexProperty"/> 變更：寫入 player；負數忽略。
    /// </summary>
    /// <param name="args">
    /// 屬性變更資料。
    /// </param>
    private void OnPlaylistIndexChanged(AvaloniaPropertyChangedEventArgs args)
    {
        if (_suppressPlayerWrite || _player == null)
        {
            return;
        }

        int newIndex = args.GetNewValue<int>();
        if (newIndex < 0)
        {
            return;
        }

        try
        {
            _player.PlaylistIndex = newIndex;
        }
        catch (MpvException)
        {
        }
    }

    /// <summary>
    /// 處理 <see cref="ChapterProperty"/> 變更：寫入 player；null 或負數忽略。
    /// </summary>
    /// <param name="args">
    /// 屬性變更資料。
    /// </param>
    private void OnChapterChanged(AvaloniaPropertyChangedEventArgs args)
    {
        if (_suppressPlayerWrite || _player == null)
        {
            return;
        }

        int? newChapter = args.GetNewValue<int?>();
        if (!newChapter.HasValue || newChapter.Value < 0)
        {
            return;
        }

        try
        {
            _player.Chapter = newChapter.Value;
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
    /// <typeparam name="T">
    /// 屬性值型別。
    /// </typeparam>
    private sealed class MpvDpObserver<T> : IObserver<T>
    {
        /// <summary>
        /// 收到新值時要執行的委派。
        /// </summary>
        private readonly Action<T> _onNext;

        /// <summary>
        /// 初始化 <see cref="MpvDpObserver{T}"/> 類別的新執行個體。
        /// </summary>
        /// <param name="onNext">
        /// 收到新值時要執行的委派。
        /// </param>
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
        /// <param name="error">
        /// 例外狀況。
        /// </param>
        public void OnError(Exception error)
        {
        }

        /// <summary>
        /// 收到新值並轉發。
        /// </summary>
        /// <param name="value">
        /// 新值。
        /// </param>
        public void OnNext(T value)
        {
            _onNext(value);
        }
    }

    /// <summary>
    /// 建立 Avalonia 預覽器使用的替代預覽控制項。
    /// </summary>
    /// <returns>
    /// 可由 Avalonia 預覽器顯示的替代控制項。
    /// </returns>
    private static Control CreateDesignPreview()
    {
        TextBlock textBlock = new TextBlock
        {
            Text = "MediaEmbedKit.Mpv Avalonia",
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };

        Border border = new Border
        {
            Background = Brushes.Black,
            Child = textBlock
        };

        return border;
    }

    /// <summary>
    /// 處理 libmpv render API 更新通知並要求 Avalonia 排入下一個影格。
    /// </summary>
    /// <param name="sender">
    /// 引發事件的物件。
    /// </param>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    /// <remarks>
    /// 此回呼由 libmpv 內部執行緒觸發；只讀寫旗標並 post 到 UI 執行緒，
    /// 不直接存取 render context，避免在 dispose 後形成 race。
    /// </remarks>
    private void RenderContextUpdateAvailable(object? sender, EventArgs e)
    {
        if (Volatile.Read(ref _disposed) || _renderQueued)
        {
            return;
        }

        _renderQueued = true;
        Dispatcher.UIThread.Post(RequestRenderIfAlive);
    }

    /// <summary>
    /// 在 UI 執行緒上要求 Avalonia 轉譯下一個 OpenGL 影格。
    /// </summary>
    private void RequestRenderIfAlive()
    {
        if (Volatile.Read(ref _disposed) || _renderContext == null)
        {
            _renderQueued = false;
            return;
        }

        RequestNextFrameRendering();
    }

    /// <summary>
    /// 釋放 libmpv OpenGL render API 內容。
    /// </summary>
    private void DisposeRenderContext()
    {
        if (_renderContext == null)
        {
            return;
        }

        _renderContext.UpdateAvailable -= RenderContextUpdateAvailable;
        _renderContext.Dispose();
        _renderContext = null;
        _renderQueued = false;
    }

    /// <summary>
    /// 釋放目前控制項持有的 mpv 播放器。
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
    /// 確認目前控制項尚未釋放。
    /// </summary>
    private void EnsureNotDisposed()
    {
        if (Volatile.Read(ref _disposed))
        {
            throw new ObjectDisposedException(GetType().FullName);
        }
    }
}

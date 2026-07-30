using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;

namespace MediaEmbedKit.Mpv.WinForms;

/// <summary>
/// 提供 WinForms 用的 libmpv 播放控制項。
/// </summary>
[ToolboxItem(true)]
[DefaultProperty(nameof(Source))]
[DefaultEvent(nameof(PlayerCreated))]
[DefaultBindingProperty(nameof(Source))]
[Description("以 libmpv 播放媒體的 WinForms 控制項。")]
public class MpvPlayerControl : Control, INotifyPropertyChanged
{
    /// <summary>
    /// 目前控制項持有的 mpv 播放器執行個體。
    /// </summary>
    private MpvPlayer? _player;
    /// <summary>
    /// 已附加的播放器屬性訂閱清單； player dispose 時一次釋放。
    /// </summary>
    private readonly List<IDisposable> _propertyWatchers = new List<IDisposable>();
    /// <summary>
    /// 表示目前 setter 變更來源是 player（避免回頭再寫入 player 造成循環）。
    /// </summary>
    private bool _suppressPlayerWrite;
    /// <summary>
    /// 後援 <see cref="Source"/> 的目前值。
    /// </summary>
    private string? _source;
    /// <summary>
    /// 後援 <see cref="Position"/> 的目前值。
    /// </summary>
    private TimeSpan _position;
    /// <summary>
    /// 後援 <see cref="Duration"/> 的目前值。
    /// </summary>
    private TimeSpan _duration;
    /// <summary>
    /// 後援 <see cref="Volume"/> 的目前值。
    /// </summary>
    private double _volume = 100.0;
    /// <summary>
    /// 後援 <see cref="IsPaused"/> 的目前值。
    /// </summary>
    private bool _isPaused;
    /// <summary>
    /// 後援 <see cref="IsMuted"/> 的目前值。
    /// </summary>
    private bool _isMuted;
    /// <summary>
    /// 後援 <see cref="PlaybackState"/> 的目前值。
    /// </summary>
    private MpvPlaybackState _playbackState;
    /// <summary>
    /// 後援 <see cref="PlaylistIndex"/> 的目前值。
    /// </summary>
    private int _playlistIndex;
    /// <summary>
    /// 後援 <see cref="Chapter"/> 的目前值。
    /// </summary>
    private int? _chapter;
    /// <summary>
    /// 最近一次控制項操作失敗的例外。
    /// </summary>
    private Exception? _lastError;

    /// <summary>
    /// 初始化 <see cref="MpvPlayerControl"/> 類別的新執行個體。
    /// </summary>
    public MpvPlayerControl()
    {
        BackColor = Color.Black;
        PlayerOptions = new MpvPlayerOptions();
        SetStyle(ControlStyles.Opaque, true);

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
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
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
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
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
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
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
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
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
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
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

        try { _player.Pause = false; } catch (MpvException exception) { ReportOperationFailure(MpvControlOperation.Command, exception); }
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

        try { _player.Pause = true; } catch (MpvException exception) { ReportOperationFailure(MpvControlOperation.Command, exception); }
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

        try { _player.Stop(); } catch (MpvException exception) { ReportOperationFailure(MpvControlOperation.Command, exception); }
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

        try { _player.Pause = !_player.Pause; } catch (MpvException exception) { ReportOperationFailure(MpvControlOperation.Command, exception); }
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

        try { _player.Mute = !_player.Mute; } catch (MpvException exception) { ReportOperationFailure(MpvControlOperation.Command, exception); }
    }

    /// <summary>
    /// 通知所有指令重新評估 <see cref="ICommand.CanExecute"/>。
    /// </summary>
    private void RaiseCommandCanExecuteChanged()
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
    [Category("MediaEmbedKit.Mpv")]
    [Description("在控制項建立 mpv 播放器後發生。")]
    public event EventHandler? PlayerCreated;

    /// <summary>
    /// 在控制項操作失敗時發生。
    /// </summary>
    [Category("MediaEmbedKit.Mpv")]
    [Description("在播放器初始化、載入、命令或屬性操作失敗時發生。")]
    public event EventHandler<MpvControlOperationFailedEventArgs>? OperationFailed;

    /// <summary>
    /// 在 <see cref="INotifyPropertyChanged"/> 屬性變更時發生。
    /// </summary>
    [Browsable(false)]
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 取得或設定要載入並播放的媒體來源。
    /// </summary>
    /// <value>
    /// 檔案路徑或媒體網址；變更會自動載入新媒體。
    /// </value>
    [Category("MediaEmbedKit.Mpv")]
    [DefaultValue(null)]
    [Description("要載入並播放的媒體檔案路徑或網址。")]
    [Bindable(true)]
    public string? Source
    {
        get { return _source; }
        set
        {
            if (string.Equals(_source, value, StringComparison.Ordinal))
            {
                return;
            }

            _source = value;
            OnPropertyChanged();

            if (_suppressPlayerWrite || _player == null || string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            try
            {
                _player.Load(new MpvMediaItem(value!));
            }
            catch (MpvException exception)
            {
                ReportOperationFailure(MpvControlOperation.Load, exception, value);
            }
        }
    }

    /// <summary>
    /// 取得或設定目前播放位置。
    /// </summary>
    /// <value>
    /// 對應 mpv <c>time-pos</c>；雙向繫結時設值會觸發 seek。
    /// </value>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TimeSpan Position
    {
        get { return _position; }
        set
        {
            if (_position == value)
            {
                return;
            }

            _position = value;
            OnPropertyChanged();

            if (_suppressPlayerWrite || _player == null)
            {
                return;
            }

            try
            {
                _player.Seek(value.TotalSeconds, "absolute");
            }
            catch (MpvException exception)
            {
                ReportOperationFailure(MpvControlOperation.Seek, exception);
            }
        }
    }

    /// <summary>
    /// 取得目前媒體總時長。
    /// </summary>
    /// <value>
    /// 對應 mpv <c>duration</c>。
    /// </value>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TimeSpan Duration
    {
        get { return _duration; }
    }

    /// <summary>
    /// 取得或設定音量。
    /// </summary>
    /// <value>
    /// 對應 mpv <c>volume</c>，範圍 0–130；預設 100。
    /// </value>
    [Category("MediaEmbedKit.Mpv")]
    [DefaultValue(100.0)]
    [Description("音量，範圍 0–130；預設 100。")]
    [Bindable(true)]
    public double Volume
    {
        get { return _volume; }
        set
        {
            if (_volume == value)
            {
                return;
            }

            _volume = value;
            OnPropertyChanged();

            if (_suppressPlayerWrite || _player == null)
            {
                return;
            }

            try
            {
                _player.Volume = value;
            }
            catch (MpvException exception)
            {
                ReportOperationFailure(MpvControlOperation.PropertyWrite, exception);
            }
        }
    }

    /// <summary>
    /// 取得或設定是否暫停。
    /// </summary>
    /// <value>
    /// 對應 mpv <c>pause</c>。
    /// </value>
    [Category("MediaEmbedKit.Mpv")]
    [DefaultValue(false)]
    [Description("是否暫停。")]
    [Bindable(true)]
    public bool IsPaused
    {
        get { return _isPaused; }
        set
        {
            if (_isPaused == value)
            {
                return;
            }

            _isPaused = value;
            OnPropertyChanged();

            if (_suppressPlayerWrite || _player == null)
            {
                return;
            }

            try
            {
                _player.Pause = value;
            }
            catch (MpvException exception)
            {
                ReportOperationFailure(MpvControlOperation.PropertyWrite, exception);
            }
        }
    }

    /// <summary>
    /// 取得或設定是否靜音。
    /// </summary>
    /// <value>
    /// 對應 mpv <c>mute</c>。
    /// </value>
    [Category("MediaEmbedKit.Mpv")]
    [DefaultValue(false)]
    [Description("是否靜音。")]
    [Bindable(true)]
    public bool IsMuted
    {
        get { return _isMuted; }
        set
        {
            if (_isMuted == value)
            {
                return;
            }

            _isMuted = value;
            OnPropertyChanged();

            if (_suppressPlayerWrite || _player == null)
            {
                return;
            }

            try
            {
                _player.Mute = value;
            }
            catch (MpvException exception)
            {
                ReportOperationFailure(MpvControlOperation.PropertyWrite, exception);
            }
        }
    }

    /// <summary>
    /// 取得目前由 libmpv 事件聚合而成的播放狀態。
    /// </summary>
    /// <value>
    /// 對應 <see cref="MpvPlayer.State"/>。
    /// </value>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public MpvPlaybackState PlaybackState
    {
        get { return _playbackState; }
    }

    /// <summary>
    /// 取得或設定目前播放清單索引。
    /// </summary>
    /// <value>
    /// 對應 mpv <c>playlist-pos</c>；以 0 起始，設值會跳到指定播放清單項目。
    /// </value>
    [Browsable(false)]
    [Bindable(true)]
    [DefaultValue(0)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int PlaylistIndex
    {
        get { return _playlistIndex; }
        set
        {
            if (_playlistIndex == value)
            {
                return;
            }

            _playlistIndex = value;
            OnPropertyChanged();

            if (_suppressPlayerWrite || _player == null || value < 0)
            {
                return;
            }

            try
            {
                _player.PlaylistIndex = value;
            }
            catch (MpvException exception)
            {
                ReportOperationFailure(MpvControlOperation.PropertyWrite, exception);
            }
        }
    }

    /// <summary>
    /// 取得或設定目前章節索引。
    /// </summary>
    /// <value>
    /// 對應 mpv <c>chapter</c>；以 0 起始，<see langword="null"/> 代表無章節或尚未載入。
    /// </value>
    [Browsable(false)]
    [Bindable(true)]
    [DefaultValue(null)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int? Chapter
    {
        get { return _chapter; }
        set
        {
            if (_chapter == value)
            {
                return;
            }

            _chapter = value;
            OnPropertyChanged();

            if (_suppressPlayerWrite || _player == null || !value.HasValue || value.Value < 0)
            {
                return;
            }

            try
            {
                _player.Chapter = value.Value;
            }
            catch (MpvException exception)
            {
                ReportOperationFailure(MpvControlOperation.PropertyWrite, exception);
            }
            catch (ArgumentOutOfRangeException exception)
            {
                ReportOperationFailure(MpvControlOperation.PropertyWrite, exception);
            }
        }
    }

    /// <summary>
    /// 取得控制項建立播放器時使用的選項。
    /// </summary>
    /// <value>
    /// 播放器建立選項。
    /// </value>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public MpvPlayerOptions PlayerOptions { get; private set; }

    /// <summary>
    /// 取得或設定控制項是否在 WinForms Handle 建立後自動初始化播放器。
    /// </summary>
    /// <value>
    /// 自動初始化播放器時為 <see langword="true"/>。
    /// </value>
    [Category("MediaEmbedKit.Mpv")]
    [DefaultValue(true)]
    [Description("在 WinForms Handle 建立後是否自動初始化 mpv 播放器。")]
    public bool AutoInitialize { get; set; } = true;

    /// <summary>
    /// 取得控制項目前建立的播放器。
    /// </summary>
    /// <value>
    /// 目前播放器；尚未建立時為 <see langword="null"/>。
    /// </value>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public MpvPlayer? Player
    {
        get { return _player; }
    }

    /// <summary>
    /// 取得控制項是否已建立並初始化播放器。
    /// </summary>
    /// <value>
    /// 播放器可用時為 <see langword="true"/>。
    /// </value>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsPlayerReady
    {
        get { return _player?.IsInitialized == true; }
    }

    /// <summary>
    /// 取得最近一次控制項操作失敗的例外。
    /// </summary>
    /// <value>
    /// 最近一次例外；尚未失敗時為 <see langword="null"/>。
    /// </value>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Exception? LastError
    {
        get { return _lastError; }
    }

    /// <summary>
    /// 依目前的播放器選項明確初始化播放器。
    /// </summary>
    public void InitializePlayer()
    {
        if (IsInDesignMode())
        {
            return;
        }

        EnsurePlayer();
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
        if (IsInDesignMode())
        {
            return;
        }

        InitializePlayer();
        MpvPlayer? player = _player;
        if (player == null)
        {
            throw new InvalidOperationException("播放器尚未建立。");
        }

        try
        {
            player.LoadFile(pathOrUrl, mode);
        }
        catch (Exception exception) when (exception is MpvException || exception is ArgumentException)
        {
            ReportOperationFailure(MpvControlOperation.Load, exception, pathOrUrl);
            throw;
        }
    }

    /// <summary>
    /// 在 WinForms 控制項 Handle 建立後初始化播放器。
    /// </summary>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!IsInDesignMode() && AutoInitialize)
        {
            EnsurePlayer();
        }
    }

    /// <summary>
    /// 在 WinForms 控制項 Handle 銷毀時釋放播放器。
    /// </summary>
    /// <param name="e">
    /// 事件資料。
    /// </param>
    protected override void OnHandleDestroyed(EventArgs e)
    {
        DisposePlayer();

        base.OnHandleDestroyed(e);
    }

    /// <summary>
    /// 在 WinForms 設計工具中繪製替代預覽內容。
    /// </summary>
    /// <param name="e">
    /// 繪製事件資料。
    /// </param>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (!IsInDesignMode())
        {
            return;
        }

        using (SolidBrush brush = new SolidBrush(BackColor))
        {
            e.Graphics.FillRectangle(brush, ClientRectangle);
        }

        TextRenderer.DrawText(
            e.Graphics,
            "MediaEmbedKit.Mpv",
            Font,
            ClientRectangle,
            Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    /// <summary>
    /// 釋放控制項使用的受控資源。
    /// </summary>
    /// <param name="disposing">
    /// 由受控程式碼釋放時為 <see langword="true"/>。
    /// </param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposePlayer();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// 確保控制項已建立並初始化 mpv 播放器。
    /// </summary>
    private void EnsurePlayer()
    {
        if (_player != null)
        {
            return;
        }

        if (!IsHandleCreated)
        {
            CreateControl();
        }

        MpvPlayer player = new MpvPlayer(PlayerOptions);
        try
        {
            player.SetVideoWindow(Handle);
            player.Initialize();
            _player = player;
        }
        catch (Exception exception)
        {
            player.Dispose();
            ReportOperationFailure(MpvControlOperation.Initialize, exception);
            throw;
        }

        AttachPlayerBindings(_player);
        RaiseCommandCanExecuteChanged();
        OnPropertyChanged(nameof(IsPlayerReady));
        PlayerCreated?.Invoke(this, EventArgs.Empty);

        if (!string.IsNullOrWhiteSpace(_source))
        {
            try
            {
                _player.Load(new MpvMediaItem(_source!));
            }
            catch (MpvException exception)
            {
                ReportOperationFailure(MpvControlOperation.Load, exception, _source);
            }
        }
    }

    /// <summary>
    /// 將控制項的屬性與目前播放器雙向繫結。
    /// </summary>
    /// <param name="player">
    /// 已初始化的播放器。
    /// </param>
    private void AttachPlayerBindings(MpvPlayer player)
    {
        Action<Exception> reportError = error => ReportOperationFailure(MpvControlOperation.PropertyWrite, error);
        _propertyWatchers.Add(player.WatchProperty<bool>("pause", value => UpdateFromPlayer(nameof(IsPaused), ref _isPaused, value), reportError));
        _propertyWatchers.Add(player.WatchProperty<bool>("mute", value => UpdateFromPlayer(nameof(IsMuted), ref _isMuted, value), reportError));
        _propertyWatchers.Add(player.WatchProperty<double>("volume", value => UpdateFromPlayer(nameof(Volume), ref _volume, value), reportError));
        _propertyWatchers.Add(player.WatchProperty<double>("time-pos", value => UpdateFromPlayer(nameof(Position), ref _position, TimeSpan.FromSeconds(value)), reportError));
        _propertyWatchers.Add(player.WatchProperty<double>("duration", value => UpdateFromPlayer(nameof(Duration), ref _duration, TimeSpan.FromSeconds(value)), reportError));
        _propertyWatchers.Add(player.WatchProperty<long>("playlist-pos", value => UpdateFromPlayer(nameof(PlaylistIndex), ref _playlistIndex, checked((int)value)), reportError));
        _propertyWatchers.Add(player.WatchProperty<long>("chapter", value => UpdateFromPlayer(nameof(Chapter), ref _chapter, value < 0 ? (int?)null : checked((int)value)), reportError));
        player.StateChanged += OnPlayerStateChanged;

        if (IsPaused != player.Pause)
        {
            try { player.Pause = IsPaused; } catch (MpvException exception) { ReportOperationFailure(MpvControlOperation.PropertyWrite, exception); }
        }

        if (IsMuted != player.Mute)
        {
            try { player.Mute = IsMuted; } catch (MpvException exception) { ReportOperationFailure(MpvControlOperation.PropertyWrite, exception); }
        }

        if (Math.Abs(Volume - player.Volume) > 0.01)
        {
            try { player.Volume = Volume; } catch (MpvException exception) { ReportOperationFailure(MpvControlOperation.PropertyWrite, exception); }
        }
    }

    /// <summary>
    /// 在 UI 執行緒以「來自 player」的標記更新後援欄位並通知 INPC，避免回頭再寫 player 觸發迴圈。
    /// </summary>
    /// <typeparam name="T">
    /// 屬性值型別。
    /// </typeparam>
    /// <param name="propertyName">
    /// 屬性名稱。
    /// </param>
    /// <param name="storage">
    /// 後援欄位。
    /// </param>
    /// <param name="value">
    /// 新值。
    /// </param>
    private void UpdateFromPlayer<T>(string propertyName, ref T storage, T value)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return;
        }

        storage = value;
        Action notify = () =>
        {
            _suppressPlayerWrite = true;
            try
            {
                OnPropertyChanged(propertyName);
            }
            finally
            {
                _suppressPlayerWrite = false;
            }
        };

        if (IsHandleCreated && InvokeRequired)
        {
            BeginInvoke(notify);
        }
        else
        {
            notify();
        }
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
        UpdateFromPlayer(nameof(PlaybackState), ref _playbackState, state);
    }

    /// <summary>
    /// 觸發 <see cref="PropertyChanged"/>。
    /// </summary>
    /// <param name="propertyName">
    /// 變更的屬性名稱，由編譯器自動填入。
    /// </param>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// 載入媒體項目並等待 libmpv 完成載入或回報失敗。
    /// </summary>
    /// <param name="item">
    /// 要載入的媒體項目。
    /// </param>
    /// <param name="mode">
    /// 播放項目加入播放清單的方式。
    /// </param>
    /// <param name="timeout">
    /// 等待載入完成的逾時時間。
    /// </param>
    /// <param name="cancellationToken">
    /// 取消等待的語彙基元。
    /// </param>
    /// <returns>
    /// 代表載入流程的工作。
    /// </returns>
    public async Task LoadAsync(
        MpvMediaItem item,
        MpvLoadFileMode mode = MpvLoadFileMode.Replace,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        InitializePlayer();
        MpvPlayer? player = _player;
        if (player == null)
        {
            throw new InvalidOperationException("播放器尚未建立。");
        }

        try
        {
            await player.LoadAsync(item, mode, timeout, cancellationToken).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            ReportOperationFailure(MpvControlOperation.Load, exception, item?.Source);
            throw;
        }
    }

    /// <summary>
    /// 記錄控制項操作失敗並通知呼叫端。
    /// </summary>
    /// <param name="operation">
    /// 失敗的操作種類。
    /// </param>
    /// <param name="exception">
    /// 操作失敗的例外。
    /// </param>
    /// <param name="source">
    /// 操作涉及的媒體來源。
    /// </param>
    private void ReportOperationFailure(
        MpvControlOperation operation,
        Exception exception,
        string? source = null)
    {
        _lastError = exception;
        OnPropertyChanged(nameof(LastError));
        OperationFailed?.Invoke(
            this,
            new MpvControlOperationFailedEventArgs(operation, exception, source));
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
        RaiseCommandCanExecuteChanged();
        OnPropertyChanged(nameof(IsPlayerReady));
    }

    /// <summary>
    /// 判斷控制項目前是否在設計工具中執行。
    /// </summary>
    /// <returns>
    /// 控制項位於設計階段時為 <see langword="true"/>。
    /// </returns>
    private bool IsInDesignMode()
    {
        return DesignMode || LicenseManager.UsageMode == LicenseUsageMode.Designtime;
    }
}

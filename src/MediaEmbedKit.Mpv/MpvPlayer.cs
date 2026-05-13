using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using MediaEmbedKit.Mpv.Native;
using MediaEmbedKit.Mpv.Render;
using Microsoft.Extensions.Logging;

namespace MediaEmbedKit.Mpv;

/// <summary>
/// 封裝單一 libmpv 用戶端執行個體，並提供命令、屬性與事件 API。
/// </summary>
public sealed class MpvPlayer : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// 保存 libmpv 用戶端的安全控制代碼。
    /// </summary>
    private readonly SafeMpvHandle _handle;
    /// <summary>
    /// 同步播放器釋放狀態與新原生作業建立時機。
    /// </summary>
    private readonly object _lifetimeGate;
    /// <summary>
    /// 同步 render API 內容清單的存取。
    /// </summary>
    private readonly object _renderContextsGate;
    /// <summary>
    /// 保存等待 libmpv 非同步回覆的要求。
    /// </summary>
    private readonly ConcurrentDictionary<ulong, TaskCompletionSource<MpvNode>> _pendingRequests;
    /// <summary>
    /// 保存由此播放器建立且尚未由播放器釋放的 render API 內容。
    /// </summary>
    private readonly List<IDisposable> _renderContexts;
    /// <summary>
    /// 保存已註冊的 libmpv 自訂串流通訊協定。
    /// </summary>
    private readonly List<MpvStreamProtocolRegistration> _streamRegistrations;
    /// <summary>
    /// 保存 libmpv 喚醒通知委派，避免遭到記憶體回收。
    /// </summary>
    private MpvNative.MpvWakeupCallback? _wakeupCallback;
    /// <summary>
    /// 保存使用者提供的喚醒通知動作。
    /// </summary>
    private Action? _wakeupAction;
    /// <summary>
    /// 產生非同步命令與屬性觀察使用的遞增要求識別碼。
    /// </summary>
    private long _nextRequestId;
    /// <summary>
    /// 接收 libmpv 事件的背景執行緒。
    /// </summary>
    private Thread? _eventThread;
    /// <summary>
    /// 表示事件迴圈正在停止。
    /// </summary>
    private volatile bool _eventLoopStopping;
    /// <summary>
    /// 表示 libmpv 用戶端是否已初始化。
    /// </summary>
    private bool _initialized;
    /// <summary>
    /// 表示目前播放器是否已釋放。
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// 使用預設選項初始化 <see cref="MpvPlayer"/> 類別的新執行個體。
    /// </summary>
    public MpvPlayer()
        : this(new MpvPlayerOptions())
    {
    }

    /// <summary>
    /// 使用指定選項初始化 <see cref="MpvPlayer"/> 類別的新執行個體。
    /// </summary>
    /// <param name="options">建立 libmpv 用戶端時套用的播放器選項。</param>
    public MpvPlayer(MpvPlayerOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        MpvLibraryLoader.Load(options.MpvLibraryPath);

        IntPtr handle = MpvNative.mpv_create();
        if (handle == IntPtr.Zero)
        {
            throw new MpvException("mpv_create returned null. Verify libmpv is loadable and LC_NUMERIC is C-compatible.");
        }

        _handle = new SafeMpvHandle(handle);
        _lifetimeGate = new object();
        _renderContextsGate = new object();
        _pendingRequests = new ConcurrentDictionary<ulong, TaskCompletionSource<MpvNode>>();
        _renderContexts = new List<IDisposable>();
        _streamRegistrations = new List<MpvStreamProtocolRegistration>();
        ApplyInitialOptions(options);
        ConfigureLoggerRouting(options);
    }

    /// <summary>
    /// 持有由 <see cref="MpvPlayerOptions.LoggerFactory"/> 衍生的記錄器；未啟用 ILogger 整合時為 <see langword="null"/>。
    /// </summary>
    private ILogger? _logger;

    /// <summary>
    /// 持有訂閱 libmpv 記錄訊息以轉送到 ILogger 的處理常式；用於釋放時取消訂閱。
    /// </summary>
    private EventHandler<MpvLogMessageEventArgs>? _loggerHandler;

    /// <summary>
    /// 依播放器選項建立 ILogger 並訂閱 <see cref="LogMessageReceived"/>。
    /// </summary>
    /// <param name="options">建立播放器時提供的選項。</param>
    private void ConfigureLoggerRouting(MpvPlayerOptions options)
    {
        if (options.LoggerFactory == null)
        {
            return;
        }

        _logger = options.LoggerFactory.CreateLogger("MediaEmbedKit.Mpv");
        _loggerHandler = delegate (object? sender, MpvLogMessageEventArgs e)
        {
            if (_logger == null)
            {
                return;
            }

            LogLevel logLevel = MapMpvLogLevel(e.LogLevel);
            if (!_logger.IsEnabled(logLevel))
            {
                return;
            }

            _logger.Log(logLevel, "[mpv:{Prefix}] {Message}", e.Prefix, e.Text.TrimEnd());
        };
        LogMessageReceived += _loggerHandler;
    }

    /// <summary>
    /// 將 libmpv 記錄等級轉換成 <see cref="Microsoft.Extensions.Logging.LogLevel"/>。
    /// </summary>
    /// <param name="level">libmpv 記錄等級。</param>
    /// <returns>對應的 <see cref="Microsoft.Extensions.Logging.LogLevel"/>。</returns>
    private static LogLevel MapMpvLogLevel(MpvLogLevel level)
    {
        switch (level)
        {
            case MpvLogLevel.Fatal:
                return LogLevel.Critical;
            case MpvLogLevel.Error:
                return LogLevel.Error;
            case MpvLogLevel.Warn:
                return LogLevel.Warning;
            case MpvLogLevel.Info:
                return LogLevel.Information;
            case MpvLogLevel.Verbose:
                return LogLevel.Debug;
            case MpvLogLevel.Debug:
            case MpvLogLevel.Trace:
                return LogLevel.Trace;
            default:
                return LogLevel.None;
        }
    }

    /// <summary>
    /// 在收到任何 libmpv 事件時發生。
    /// </summary>
    public event EventHandler<MpvEventArgs>? EventReceived;

    /// <summary>
    /// 在 libmpv 事件已轉換成節點資料時發生。
    /// </summary>
    public event EventHandler<MpvNodeEventArgs>? EventNodeReceived;

    /// <summary>
    /// 在 libmpv 非同步命令回覆時發生。
    /// </summary>
    public event EventHandler<MpvCommandReplyEventArgs>? CommandReply;

    /// <summary>
    /// 在收到 libmpv 記錄訊息時發生。
    /// </summary>
    public event EventHandler<MpvLogMessageEventArgs>? LogMessageReceived;

    /// <summary>
    /// 在已觀察的 libmpv 屬性變更時發生。
    /// </summary>
    public event EventHandler<MpvPropertyChangedEventArgs>? PropertyChanged;

    /// <summary>
    /// 在 libmpv 結束目前播放項目時發生。
    /// </summary>
    public event EventHandler<MpvEndFileEventArgs>? EndFile;

    /// <summary>
    /// 在 libmpv 即將開始載入播放項目時發生。
    /// </summary>
    public event EventHandler<MpvStartFileEventArgs>? StartFile;

    /// <summary>
    /// 在 libmpv 收到用戶端訊息時發生。
    /// </summary>
    public event EventHandler<MpvClientMessageEventArgs>? ClientMessage;

    /// <summary>
    /// 在 libmpv 掛鉤被觸發時發生。
    /// </summary>
    public event EventHandler<MpvHookEventArgs>? Hook;

    /// <summary>
    /// 在 libmpv 已完成載入播放項目時發生。
    /// </summary>
    public event EventHandler<MpvEventArgs>? FileLoaded;

    /// <summary>
    /// 在 libmpv 進入閒置狀態時發生。
    /// </summary>
    public event EventHandler<MpvEventArgs>? Idle;

    /// <summary>
    /// 在 libmpv 視訊輸出重新設定時發生。
    /// </summary>
    public event EventHandler<MpvEventArgs>? VideoReconfigured;

    /// <summary>
    /// 在 libmpv 音訊輸出重新設定時發生。
    /// </summary>
    public event EventHandler<MpvEventArgs>? AudioReconfigured;

    /// <summary>
    /// 在 libmpv 播放位置搜尋開始時發生。
    /// </summary>
    public event EventHandler<MpvEventArgs>? SeekStarted;

    /// <summary>
    /// 在 libmpv 播放於載入或搜尋後重新開始時發生。
    /// </summary>
    public event EventHandler<MpvEventArgs>? PlaybackRestarted;

    /// <summary>
    /// 在 libmpv 事件佇列溢位時發生。
    /// </summary>
    public event EventHandler<MpvEventArgs>? QueueOverflow;

    /// <summary>
    /// 在 libmpv 用戶端關閉時發生。
    /// </summary>
    public event EventHandler<MpvEventArgs>? Shutdown;

    /// <summary>
    /// 在受控事件處理常式擲回例外狀況時發生。
    /// </summary>
    public event EventHandler<MpvEventDispatchExceptionEventArgs>? EventDispatchException;

    /// <summary>
    /// 在已觀察的播放軌清單變更時發生。
    /// </summary>
    public event EventHandler<MpvTracksChangedEventArgs>? TracksChanged;

    /// <summary>
    /// 取得 libmpv 用戶端是否已初始化。
    /// </summary>
    /// <value>已呼叫 <see cref="Initialize"/> 且成功時為 <see langword="true"/>。</value>
    public bool IsInitialized
    {
        get { return _initialized; }
    }

    /// <summary>
    /// 取得 libmpv 用戶端的原生控制代碼。
    /// </summary>
    /// <value>libmpv 用戶端原生控制代碼。</value>
    public IntPtr DangerousHandle
    {
        get
        {
            EnsureNotDisposed();
            return _handle.DangerousGetHandle();
        }
    }

    /// <summary>
    /// 取得 libmpv 指派給此用戶端的名稱。
    /// </summary>
    /// <value>libmpv 用戶端名稱。</value>
    public string ClientName
    {
        get
        {
            EnsureNotDisposed();
            return InvokeNative(handle => Utf8StringMarshaller.PtrToString(MpvNative.mpv_client_name(handle)) ?? string.Empty);
        }
    }

    /// <summary>
    /// 取得 libmpv 指派給此用戶端的識別碼。
    /// </summary>
    /// <value>libmpv 用戶端識別碼。</value>
    public long ClientId
    {
        get
        {
            EnsureNotDisposed();
            return InvokeNative(handle => MpvNative.mpv_client_id(handle));
        }
    }

    /// <summary>
    /// 取得或設定播放是否暫停。
    /// </summary>
    /// <value>播放暫停時為 <see langword="true"/>。</value>
    public bool Pause
    {
        get { return GetPropertyFlag("pause"); }
        set { SetPropertyFlag("pause", value); }
    }

    /// <summary>
    /// 取得或設定播放器音量。
    /// </summary>
    /// <value>mpv 的 <c>volume</c> 屬性值。</value>
    public double Volume
    {
        get { return GetPropertyDouble("volume"); }
        set { SetPropertyDouble("volume", value); }
    }

    /// <summary>
    /// 取得或設定音訊是否靜音。
    /// </summary>
    /// <value>靜音時為 <see langword="true"/>。</value>
    public bool Mute
    {
        get { return GetPropertyFlag("mute"); }
        set { SetPropertyFlag("mute", value); }
    }

    /// <summary>
    /// 取得或設定播放速度。
    /// </summary>
    /// <value>mpv 的 <c>speed</c> 屬性值。</value>
    public double Speed
    {
        get { return GetPropertyDouble("speed"); }
        set { SetPropertyDouble("speed", value); }
    }

    /// <summary>
    /// 取得目前播放位置。
    /// </summary>
    /// <value>目前播放位置秒數。</value>
    public double TimePosition
    {
        get { return GetPropertyDouble("time-pos"); }
    }

    /// <summary>
    /// 取得目前播放項目的總長度。
    /// </summary>
    /// <value>目前播放項目的秒數長度。</value>
    public double Duration
    {
        get { return GetPropertyDouble("duration"); }
    }

    /// <summary>
    /// 取得目前播放項目的剩餘秒數。
    /// </summary>
    /// <value>目前播放項目的剩餘秒數。</value>
    public double TimeRemaining
    {
        get { return GetPropertyDouble("time-remaining"); }
    }

    /// <summary>
    /// 取得或設定目前播放清單索引。
    /// </summary>
    /// <value>以 0 為起始的播放清單索引。</value>
    public int PlaylistIndex
    {
        get { return checked((int)GetPropertyInt64("playlist-pos")); }
        set { SetPropertyInt64("playlist-pos", value); }
    }

    /// <summary>
    /// 取得目前播放清單項目數量。
    /// </summary>
    /// <value>播放清單項目數量。</value>
    public int PlaylistEntryCount
    {
        get { return checked((int)GetPropertyInt64("playlist-count")); }
    }

    /// <summary>
    /// 取得目前是否處於閒置狀態。
    /// </summary>
    /// <value>播放器處於閒置狀態時為 <see langword="true"/>。</value>
    public bool IsIdle
    {
        get { return GetPropertyFlag("idle-active"); }
    }

    /// <summary>
    /// 取得目前媒體標題。
    /// </summary>
    /// <value>目前媒體標題。</value>
    public string? MediaTitle
    {
        get { return GetPropertyString("media-title"); }
    }

    /// <summary>
    /// 取得或設定目前播放項目是否循環播放。
    /// </summary>
    /// <value>目前播放項目循環播放時為 <see langword="true"/>。</value>
    public bool LoopFile
    {
        get { return !string.Equals(GetPropertyString("loop-file"), "no", StringComparison.OrdinalIgnoreCase); }
        set { SetPropertyString("loop-file", value ? "inf" : "no"); }
    }

    /// <summary>
    /// 取得或設定播放清單是否循環播放。
    /// </summary>
    /// <value>播放清單循環播放時為 <see langword="true"/>。</value>
    public bool LoopPlaylist
    {
        get { return !string.Equals(GetPropertyString("loop-playlist"), "no", StringComparison.OrdinalIgnoreCase); }
        set { SetPropertyString("loop-playlist", value ? "inf" : "no"); }
    }

    /// <summary>
    /// 取得目前 libmpv 用戶端 API 版本。
    /// </summary>
    /// <returns>libmpv 用戶端 API 版本值。</returns>
    public static uint ClientApiVersion()
    {
        return MpvNative.mpv_client_api_version();
    }

    /// <summary>
    /// 取得 libmpv 事件識別碼對應的事件名稱。
    /// </summary>
    /// <param name="eventId">要查詢的 libmpv 事件識別碼。</param>
    /// <returns>libmpv 提供的事件名稱；未知事件會傳回列舉名稱。</returns>
    public static string GetEventName(MpvEventId eventId)
    {
        return MpvNative.GetEventName(eventId);
    }

    /// <summary>
    /// 取得目前 libmpv 用戶端可用功能的一次性快照。
    /// </summary>
    /// <returns>包含協定、解碼器、demuxer 與版本資訊的 <see cref="MpvCapabilities"/>。</returns>
    public MpvCapabilities GetCapabilities()
    {
        EnsureNotDisposed();
        uint rawVersion = ClientApiVersion();
        Version clientApiVersion = new Version((int)((rawVersion >> 16) & 0xFFFF), (int)(rawVersion & 0xFFFF));
        string mpvVersion = TryGetPropertyString("mpv-version");
        string mpvConfiguration = TryGetPropertyString("mpv-configuration");
        IReadOnlyList<string> protocols = GetProtocols();
        IReadOnlyList<MpvDecoderInfo> decoders = GetDecoders();
        IReadOnlyList<string> demuxers = GetDemuxers();
        return new MpvCapabilities(clientApiVersion, mpvVersion, mpvConfiguration, protocols, decoders, demuxers);
    }

    /// <summary>
    /// 嘗試讀取字串屬性；屬性不存在或暫時無法存取時傳回空字串。
    /// </summary>
    /// <param name="name">要讀取的屬性名稱。</param>
    /// <returns>屬性值；失敗時為空字串。</returns>
    private string TryGetPropertyString(string name)
    {
        try
        {
            string? value = GetPropertyString(name);
            return value ?? string.Empty;
        }
        catch (MpvException)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// 取得 libmpv 目前單調時間。
    /// </summary>
    /// <returns>以奈秒表示的 libmpv 時間。</returns>
    public long GetTimeNanoseconds()
    {
        EnsureNotDisposed();
        return InvokeNative(handle => unchecked((long)MpvNative.mpv_get_time_ns(handle)));
    }

    /// <summary>
    /// 取得 libmpv 目前單調時間。
    /// </summary>
    /// <returns>以微秒表示的 libmpv 時間。</returns>
    public long GetTimeMicroseconds()
    {
        EnsureNotDisposed();
        return InvokeNative(handle => MpvNative.mpv_get_time_us(handle));
    }

    /// <summary>
    /// 初始化 libmpv 用戶端並啟動事件迴圈。
    /// </summary>
    public void Initialize()
    {
        bool startEventLoop = false;
        lock (_lifetimeGate)
        {
            EnsureNotDisposed();
            if (_initialized)
            {
                return;
            }

            using (NativeHandleLease handleLease = new NativeHandleLease(_handle))
            {
                MpvError.ThrowIfError(MpvNative.mpv_initialize(handleLease.Handle));
                _initialized = true;
                startEventLoop = true;
            }
        }

        if (startEventLoop)
        {
            StartEventLoop();
        }
    }

    /// <summary>
    /// 非同步初始化 libmpv 用戶端並啟動事件迴圈。
    /// </summary>
    /// <param name="cancellationToken">取消初始化要求的 token。</param>
    /// <returns>代表初始化流程的工作。</returns>
    /// <remarks>
    /// 取消 token 只在開始呼叫 <c>mpv_initialize</c> 之前生效；libmpv 本身為同步介面，
    /// 一旦進入 native 初始化即無法中途中止。
    /// </remarks>
    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                Initialize();
            },
            cancellationToken);
    }

    /// <summary>
    /// 設定 libmpv 視訊輸出的 Windows 子視窗控制代碼。
    /// </summary>
    /// <param name="hwnd">要傳給 mpv <c>wid</c> 選項的視窗控制代碼。</param>
    public void SetVideoWindow(IntPtr hwnd)
    {
        EnsureNotDisposed();
        if (_initialized)
        {
            throw new InvalidOperationException("The wid option must be set before mpv_initialize.");
        }

        SetOptionString("wid", hwnd.ToInt64().ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// 載入指定的 mpv 設定檔。
    /// </summary>
    /// <param name="fileName">要載入的 mpv 設定檔路徑。</param>
    public void LoadConfigFile(string fileName)
    {
        EnsureNotDisposed();
        using (Utf8String name = new Utf8String(fileName))
        {
            InvokeNative(handle => MpvError.ThrowIfError(MpvNative.mpv_load_config_file(handle, name.Pointer)));
        }
    }

    /// <summary>
    /// 以字串格式設定 libmpv 選項。
    /// </summary>
    /// <param name="name">要設定的 mpv 選項名稱。</param>
    /// <param name="value">要套用到選項的字串值。</param>
    public void SetOptionString(string name, string value)
    {
        EnsureNotDisposed();
        using (Utf8String optionName = new Utf8String(name))
        using (Utf8String optionValue = new Utf8String(value))
        {
            InvokeNative(handle => MpvError.ThrowIfError(MpvNative.mpv_set_option_string(handle, optionName.Pointer, optionValue.Pointer)));
        }
    }

    /// <summary>
    /// 使用常用預設值設定 yt-dlp 格式選擇。
    /// </summary>
    /// <param name="preset">要套用的 yt-dlp 格式選擇預設值。</param>
    public void SetYtdlpFormat(MpvYtdlpFormatPreset preset)
    {
        SetYtdlpFormat(MpvYtdlpFormatSelector.FromPreset(preset));
    }

    /// <summary>
    /// 使用自訂字串設定 yt-dlp 格式選擇。
    /// </summary>
    /// <param name="formatSelector">要傳給 mpv <c>ytdl-format</c> 選項的格式選擇字串。</param>
    public void SetYtdlpFormat(string formatSelector)
    {
        if (formatSelector == null)
        {
            throw new ArgumentNullException(nameof(formatSelector));
        }

        string value = string.IsNullOrWhiteSpace(formatSelector) ? MpvYtdlpFormatSelector.Default : formatSelector;
        SetOptionString("ytdl-format", value);
    }

    /// <summary>
    /// 設定 yt-dlp 最高視訊高度。
    /// </summary>
    /// <param name="maximumHeight">允許的最大視訊高度。</param>
    public void SetYtdlpMaximumHeight(int maximumHeight)
    {
        SetYtdlpFormat(MpvYtdlpFormatSelector.MaxHeight(maximumHeight));
    }

    /// <summary>
    /// 重設 yt-dlp 格式選擇為 mpv 與 yt-dlp 的預設行為。
    /// </summary>
    public void ResetYtdlpFormat()
    {
        SetYtdlpFormat(MpvYtdlpFormatPreset.Default);
    }

    /// <summary>
    /// 設定 mpv encoding mode 選項。
    /// </summary>
    /// <param name="encodingOptions">要套用的 encoding mode 選項。</param>
    public void ConfigureEncoding(MpvEncodingOptions encodingOptions)
    {
        if (encodingOptions == null)
        {
            throw new ArgumentNullException(nameof(encodingOptions));
        }

        encodingOptions.ApplyTo(this);
    }

    /// <summary>
    /// 取得 mpv ytdl hook 找到的外部工具路徑。
    /// </summary>
    /// <returns>ytdl hook 找到的外部工具路徑；尚未解析或找不到時為 <see langword="null"/>。</returns>
    public string? GetYtdlHookPath()
    {
        return GetPropertyString("user-data/mpv/ytdl/path");
    }

    /// <summary>
    /// 取得 mpv ytdl hook 執行 yt-dlp JSON 子程序的結果。
    /// </summary>
    /// <returns>ytdl JSON 子程序結果；尚未解析 URL 或結果不可用時為 <see langword="null"/>。</returns>
    public MpvYtdlJsonSubprocessResult? GetYtdlJsonSubprocessResult()
    {
        try
        {
            MpvNode node = GetPropertyNode("user-data/mpv/ytdl/json-subprocess-result");
            return node.IsNone ? null : MpvYtdlJsonSubprocessResult.FromNode(node);
        }
        catch (MpvException ex) when (IsUnavailablePropertyError(ex))
        {
            return null;
        }
    }

    /// <summary>
    /// 嘗試取得 mpv ytdl hook 執行 yt-dlp JSON 子程序的結果。
    /// </summary>
    /// <param name="result">找到時接收 ytdl JSON 子程序結果；找不到時接收 <see langword="null"/>。</param>
    /// <returns>找到 ytdl JSON 子程序結果時為 <see langword="true"/>。</returns>
    public bool TryGetYtdlJsonSubprocessResult(out MpvYtdlJsonSubprocessResult? result)
    {
        result = GetYtdlJsonSubprocessResult();
        return result != null;
    }

    /// <summary>
    /// 以布林旗標格式設定 libmpv 選項。
    /// </summary>
    /// <param name="name">要設定的 mpv 選項名稱。</param>
    /// <param name="value">要套用到選項的布林值。</param>
    public void SetOptionFlag(string name, bool value)
    {
        EnsureNotDisposed();
        int flag = value ? 1 : 0;
        using (Utf8String optionName = new Utf8String(name))
        {
            using (NativeHandleLease handleLease = AcquireNativeHandle())
            {
                MpvError.ThrowIfError(MpvNative.mpv_set_option(handleLease.Handle, optionName.Pointer, MpvFormat.Flag, ref flag));
            }
        }
    }

    /// <summary>
    /// 以 64 位元整數格式設定 libmpv 選項。
    /// </summary>
    /// <param name="name">要設定的 mpv 選項名稱。</param>
    /// <param name="value">要套用到選項的 64 位元整數值。</param>
    public void SetOptionInt64(string name, long value)
    {
        EnsureNotDisposed();
        using (Utf8String optionName = new Utf8String(name))
        {
            using (NativeHandleLease handleLease = AcquireNativeHandle())
            {
                MpvError.ThrowIfError(MpvNative.mpv_set_option(handleLease.Handle, optionName.Pointer, MpvFormat.Int64, ref value));
            }
        }
    }

    /// <summary>
    /// 以雙精確度浮點數格式設定 libmpv 選項。
    /// </summary>
    /// <param name="name">要設定的 mpv 選項名稱。</param>
    /// <param name="value">要套用到選項的雙精確度浮點數值。</param>
    public void SetOptionDouble(string name, double value)
    {
        EnsureNotDisposed();
        using (Utf8String optionName = new Utf8String(name))
        {
            using (NativeHandleLease handleLease = AcquireNativeHandle())
            {
                MpvError.ThrowIfError(MpvNative.mpv_set_option(handleLease.Handle, optionName.Pointer, MpvFormat.Double, ref value));
            }
        }
    }

    /// <summary>
    /// 以節點格式設定 libmpv 選項。
    /// </summary>
    /// <param name="name">要設定的 mpv 選項名稱。</param>
    /// <param name="value">要套用到選項的節點資料。</param>
    public void SetOptionNode(string name, MpvNode value)
    {
        EnsureNotDisposed();
        using (Utf8String optionName = new Utf8String(name))
        using (MpvNodeAllocation node = new MpvNodeAllocation(value))
        {
            NativeMpvNode nativeNode = node.NativeNode;
            using (NativeHandleLease handleLease = AcquireNativeHandle())
            {
                MpvError.ThrowIfError(MpvNative.mpv_set_option(handleLease.Handle, optionName.Pointer, MpvFormat.Node, ref nativeNode));
            }
        }
    }

    /// <summary>
    /// 以字串格式設定 libmpv 屬性。
    /// </summary>
    /// <param name="name">要設定的 mpv 屬性名稱。</param>
    /// <param name="value">要套用到屬性的字串值。</param>
    public void SetPropertyString(string name, string value)
    {
        EnsureNotDisposed();
        using (Utf8String propertyName = new Utf8String(name))
        using (Utf8String propertyValue = new Utf8String(value))
        {
            InvokeNative(handle => MpvError.ThrowIfError(MpvNative.mpv_set_property_string(handle, propertyName.Pointer, propertyValue.Pointer)));
        }
    }

    /// <summary>
    /// 以布林旗標格式設定 libmpv 屬性。
    /// </summary>
    /// <param name="name">要設定的 mpv 屬性名稱。</param>
    /// <param name="value">要套用到屬性的布林值。</param>
    public void SetPropertyFlag(string name, bool value)
    {
        EnsureNotDisposed();
        int flag = value ? 1 : 0;
        using (Utf8String propertyName = new Utf8String(name))
        {
            using (NativeHandleLease handleLease = AcquireNativeHandle())
            {
                MpvError.ThrowIfError(MpvNative.mpv_set_property(handleLease.Handle, propertyName.Pointer, MpvFormat.Flag, ref flag));
            }
        }
    }

    /// <summary>
    /// 以 64 位元整數格式設定 libmpv 屬性。
    /// </summary>
    /// <param name="name">要設定的 mpv 屬性名稱。</param>
    /// <param name="value">要套用到屬性的 64 位元整數值。</param>
    public void SetPropertyInt64(string name, long value)
    {
        EnsureNotDisposed();
        using (Utf8String propertyName = new Utf8String(name))
        {
            using (NativeHandleLease handleLease = AcquireNativeHandle())
            {
                MpvError.ThrowIfError(MpvNative.mpv_set_property(handleLease.Handle, propertyName.Pointer, MpvFormat.Int64, ref value));
            }
        }
    }

    /// <summary>
    /// 以雙精確度浮點數格式設定 libmpv 屬性。
    /// </summary>
    /// <param name="name">要設定的 mpv 屬性名稱。</param>
    /// <param name="value">要套用到屬性的雙精確度浮點數值。</param>
    public void SetPropertyDouble(string name, double value)
    {
        EnsureNotDisposed();
        using (Utf8String propertyName = new Utf8String(name))
        {
            using (NativeHandleLease handleLease = AcquireNativeHandle())
            {
                MpvError.ThrowIfError(MpvNative.mpv_set_property(handleLease.Handle, propertyName.Pointer, MpvFormat.Double, ref value));
            }
        }
    }

    /// <summary>
    /// 以節點格式設定 libmpv 屬性。
    /// </summary>
    /// <param name="name">要設定的 mpv 屬性名稱。</param>
    /// <param name="value">要套用到屬性的節點資料。</param>
    public void SetPropertyNode(string name, MpvNode value)
    {
        EnsureNotDisposed();
        using (Utf8String propertyName = new Utf8String(name))
        using (MpvNodeAllocation node = new MpvNodeAllocation(value))
        {
            NativeMpvNode nativeNode = node.NativeNode;
            using (NativeHandleLease handleLease = AcquireNativeHandle())
            {
                MpvError.ThrowIfError(MpvNative.mpv_set_property(handleLease.Handle, propertyName.Pointer, MpvFormat.Node, ref nativeNode));
            }
        }
    }

    /// <summary>
    /// 刪除指定的 libmpv 屬性。
    /// </summary>
    /// <param name="name">要刪除的 mpv 屬性名稱。</param>
    public void DeleteProperty(string name)
    {
        EnsureNotDisposed();
        using (Utf8String propertyName = new Utf8String(name))
        {
            InvokeNative(handle => MpvError.ThrowIfError(MpvNative.mpv_del_property(handle, propertyName.Pointer)));
        }
    }

    /// <summary>
    /// 以字串格式取得 libmpv 屬性值。
    /// </summary>
    /// <param name="name">要讀取的 mpv 屬性名稱。</param>
    /// <returns>屬性的字串值；沒有值時為 <see langword="null"/>。</returns>
    public string? GetPropertyString(string name)
    {
        EnsureNotDisposed();
        using (Utf8String propertyName = new Utf8String(name))
        {
            IntPtr value = InvokeNative(handle => MpvNative.mpv_get_property_string(handle, propertyName.Pointer));
            try
            {
                return Utf8StringMarshaller.PtrToString(value);
            }
            finally
            {
                if (value != IntPtr.Zero)
                {
                    MpvNative.mpv_free(value);
                }
            }
        }
    }

    /// <summary>
    /// 取得適合螢幕顯示的 libmpv 屬性字串。
    /// </summary>
    /// <param name="name">要讀取的 mpv 屬性名稱。</param>
    /// <returns>屬性的螢幕顯示字串；沒有值時為 <see langword="null"/>。</returns>
    public string? GetPropertyOsdString(string name)
    {
        EnsureNotDisposed();
        using (Utf8String propertyName = new Utf8String(name))
        {
            IntPtr value = InvokeNative(handle => MpvNative.mpv_get_property_osd_string(handle, propertyName.Pointer));
            try
            {
                return Utf8StringMarshaller.PtrToString(value);
            }
            finally
            {
                if (value != IntPtr.Zero)
                {
                    MpvNative.mpv_free(value);
                }
            }
        }
    }

    /// <summary>
    /// 以布林旗標格式取得 libmpv 屬性值。
    /// </summary>
    /// <param name="name">要讀取的 mpv 屬性名稱。</param>
    /// <returns>屬性的布林值。</returns>
    public bool GetPropertyFlag(string name)
    {
        EnsureNotDisposed();
        using (Utf8String propertyName = new Utf8String(name))
        {
            int value;
            using (NativeHandleLease handleLease = AcquireNativeHandle())
            {
                MpvError.ThrowIfError(MpvNative.mpv_get_property(handleLease.Handle, propertyName.Pointer, MpvFormat.Flag, out value));
            }

            return value != 0;
        }
    }

    /// <summary>
    /// 以 64 位元整數格式取得 libmpv 屬性值。
    /// </summary>
    /// <param name="name">要讀取的 mpv 屬性名稱。</param>
    /// <returns>屬性的 64 位元整數值。</returns>
    public long GetPropertyInt64(string name)
    {
        EnsureNotDisposed();
        using (Utf8String propertyName = new Utf8String(name))
        {
            long value;
            using (NativeHandleLease handleLease = AcquireNativeHandle())
            {
                MpvError.ThrowIfError(MpvNative.mpv_get_property(handleLease.Handle, propertyName.Pointer, MpvFormat.Int64, out value));
            }

            return value;
        }
    }

    /// <summary>
    /// 以雙精確度浮點數格式取得 libmpv 屬性值。
    /// </summary>
    /// <param name="name">要讀取的 mpv 屬性名稱。</param>
    /// <returns>屬性的雙精確度浮點數值。</returns>
    public double GetPropertyDouble(string name)
    {
        EnsureNotDisposed();
        using (Utf8String propertyName = new Utf8String(name))
        {
            double value;
            using (NativeHandleLease handleLease = AcquireNativeHandle())
            {
                MpvError.ThrowIfError(MpvNative.mpv_get_property(handleLease.Handle, propertyName.Pointer, MpvFormat.Double, out value));
            }

            return value;
        }
    }

    /// <summary>
    /// 以節點格式取得 libmpv 屬性值。
    /// </summary>
    /// <param name="name">要讀取的 mpv 屬性名稱。</param>
    /// <returns>屬性的節點資料。</returns>
    public MpvNode GetPropertyNode(string name)
    {
        EnsureNotDisposed();
        using (Utf8String propertyName = new Utf8String(name))
        {
            NativeMpvNode value = default(NativeMpvNode);
            using (NativeHandleLease handleLease = AcquireNativeHandle())
            {
                MpvError.ThrowIfError(MpvNative.mpv_get_property(handleLease.Handle, propertyName.Pointer, MpvFormat.Node, out value));
            }

            try
            {
                return MpvNode.FromNative(value);
            }
            finally
            {
                MpvNative.mpv_free_node_contents(ref value);
            }
        }
    }

    /// <summary>
    /// 使用引數陣列同步執行 libmpv 命令。
    /// </summary>
    /// <param name="arguments">命令名稱與其後續引數。</param>
    public void Command(params string[] arguments)
    {
        EnsureNotDisposed();
        using (Utf8StringArray args = new Utf8StringArray(arguments))
        {
            InvokeNative(handle => MpvError.ThrowIfError(MpvNative.mpv_command(handle, args.Pointer)));
        }
    }

    /// <summary>
    /// 使用 mpv 命令列語法同步執行 libmpv 命令。
    /// </summary>
    /// <param name="command">要傳給 libmpv 的命令文字。</param>
    public void CommandString(string command)
    {
        EnsureNotDisposed();
        using (Utf8String args = new Utf8String(command))
        {
            InvokeNative(handle => MpvError.ThrowIfError(MpvNative.mpv_command_string(handle, args.Pointer)));
        }
    }

    /// <summary>
    /// 使用引數陣列同步執行 libmpv 命令並取得節點結果。
    /// </summary>
    /// <param name="arguments">命令名稱與其後續引數。</param>
    /// <returns>命令回傳的節點資料；沒有回傳資料時為空節點。</returns>
    public MpvNode CommandWithResult(params string[] arguments)
    {
        EnsureNotDisposed();
        using (Utf8StringArray args = new Utf8StringArray(arguments))
        {
            NativeMpvNode result = default(NativeMpvNode);
            using (NativeHandleLease handleLease = AcquireNativeHandle())
            {
                MpvError.ThrowIfError(MpvNative.mpv_command_ret(handleLease.Handle, args.Pointer, out result));
            }

            try
            {
                return MpvNode.FromNative(result);
            }
            finally
            {
                MpvNative.mpv_free_node_contents(ref result);
            }
        }
    }

    /// <summary>
    /// 使用節點資料同步執行 libmpv 命令並取得節點結果。
    /// </summary>
    /// <param name="arguments">命令引數節點。</param>
    /// <returns>命令回傳的節點資料；沒有回傳資料時為空節點。</returns>
    public MpvNode CommandNode(MpvNode arguments)
    {
        EnsureNotDisposed();
        using (MpvNodeAllocation args = new MpvNodeAllocation(arguments))
        {
            NativeMpvNode nativeArguments = args.NativeNode;
            NativeMpvNode result = default(NativeMpvNode);
            using (NativeHandleLease handleLease = AcquireNativeHandle())
            {
                MpvError.ThrowIfError(MpvNative.mpv_command_node(handleLease.Handle, ref nativeArguments, out result));
            }

            try
            {
                return MpvNode.FromNative(result);
            }
            finally
            {
                MpvNative.mpv_free_node_contents(ref result);
            }
        }
    }

    /// <summary>
    /// 使用具名引數同步執行 libmpv 命令並取得節點結果，並以最新 libmpv 建議的特殊 `_name` 欄位保存命令名稱。
    /// </summary>
    /// <param name="name">要執行的 mpv 命令名稱。</param>
    /// <param name="arguments">命令具名引數。</param>
    /// <returns>命令回傳的節點資料；沒有回傳資料時為空節點。</returns>
    public MpvNode CommandNamed(string name, IDictionary<string, MpvNode> arguments)
    {
        return CommandNode(CreateNamedCommandNode(name, arguments));
    }

    /// <summary>
    /// 使用引數陣列非同步執行 libmpv 命令。
    /// </summary>
    /// <param name="arguments">命令名稱與其後續引數。</param>
    /// <returns>代表 libmpv 命令回覆的工作。</returns>
    public Task CommandAsync(params string[] arguments)
    {
        EnsureNotDisposed();
        ulong requestId = NextRequestId();
        TaskCompletionSource<MpvNode> completion = new TaskCompletionSource<MpvNode>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[requestId] = completion;

        using (Utf8StringArray args = new Utf8StringArray(arguments))
        {
            int error = InvokeNative(handle => MpvNative.mpv_command_async(handle, requestId, args.Pointer));
            if (error < 0)
            {
                TaskCompletionSource<MpvNode>? removed;
                _pendingRequests.TryRemove(requestId, out removed);
                completion.TrySetException(new MpvException(error));
            }
        }

        return completion.Task;
    }

    /// <summary>
    /// 使用節點資料非同步執行 libmpv 命令。
    /// </summary>
    /// <param name="arguments">命令引數節點。</param>
    /// <returns>代表 libmpv 命令回覆節點的工作。</returns>
    public Task<MpvNode> CommandNodeAsync(MpvNode arguments)
    {
        EnsureNotDisposed();
        ulong requestId = NextRequestId();
        TaskCompletionSource<MpvNode> completion = new TaskCompletionSource<MpvNode>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[requestId] = completion;

        using (MpvNodeAllocation args = new MpvNodeAllocation(arguments))
        {
            NativeMpvNode nativeArguments = args.NativeNode;
            int error;
            using (NativeHandleLease handleLease = AcquireNativeHandle())
            {
                error = MpvNative.mpv_command_node_async(handleLease.Handle, requestId, ref nativeArguments);
            }

            if (error < 0)
            {
                TaskCompletionSource<MpvNode>? removed;
                _pendingRequests.TryRemove(requestId, out removed);
                completion.TrySetException(new MpvException(error));
            }
        }

        return completion.Task;
    }

    /// <summary>
    /// 使用具名引數非同步執行 libmpv 命令，並以最新 libmpv 建議的特殊 `_name` 欄位保存命令名稱。
    /// </summary>
    /// <param name="name">要執行的 mpv 命令名稱。</param>
    /// <param name="arguments">命令具名引數。</param>
    /// <returns>代表 libmpv 命令回覆節點的工作。</returns>
    public Task<MpvNode> CommandNamedAsync(string name, IDictionary<string, MpvNode> arguments)
    {
        return CommandNodeAsync(CreateNamedCommandNode(name, arguments));
    }

    /// <summary>
    /// 中止指定的 libmpv 非同步命令。
    /// </summary>
    /// <param name="requestId">要中止的非同步命令要求識別碼。</param>
    public void AbortAsyncCommand(ulong requestId)
    {
        EnsureNotDisposed();
        InvokeNative(handle => MpvNative.mpv_abort_async_command(handle, requestId));
    }

    /// <summary>
    /// 非同步設定布林旗標格式的 libmpv 屬性。
    /// </summary>
    /// <param name="name">要設定的 mpv 屬性名稱。</param>
    /// <param name="value">要套用到屬性的布林值。</param>
    /// <returns>代表 libmpv 設定屬性回覆的工作。</returns>
    public Task SetPropertyFlagAsync(string name, bool value)
    {
        EnsureNotDisposed();
        int flag = value ? 1 : 0;
        return SetPropertyFlagAsyncCore(name, ref flag);
    }

    /// <summary>
    /// 非同步設定 64 位元整數格式的 libmpv 屬性。
    /// </summary>
    /// <param name="name">要設定的 mpv 屬性名稱。</param>
    /// <param name="value">要套用到屬性的 64 位元整數值。</param>
    /// <returns>代表 libmpv 設定屬性回覆的工作。</returns>
    public Task SetPropertyInt64Async(string name, long value)
    {
        EnsureNotDisposed();
        return SetPropertyInt64AsyncCore(name, ref value);
    }

    /// <summary>
    /// 非同步設定雙精確度浮點數格式的 libmpv 屬性。
    /// </summary>
    /// <param name="name">要設定的 mpv 屬性名稱。</param>
    /// <param name="value">要套用到屬性的雙精確度浮點數值。</param>
    /// <returns>代表 libmpv 設定屬性回覆的工作。</returns>
    public Task SetPropertyDoubleAsync(string name, double value)
    {
        EnsureNotDisposed();
        return SetPropertyDoubleAsyncCore(name, ref value);
    }

    /// <summary>
    /// 非同步設定字串格式的 libmpv 屬性。
    /// </summary>
    /// <param name="name">要設定的 mpv 屬性名稱。</param>
    /// <param name="value">要套用到屬性的字串值。</param>
    /// <returns>代表 libmpv 設定屬性回覆的工作。</returns>
    public Task SetPropertyStringAsync(string name, string value)
    {
        EnsureNotDisposed();
        return SetPropertyNodeAsync(name, MpvNode.FromString(value));
    }

    /// <summary>
    /// 非同步設定節點格式的 libmpv 屬性。
    /// </summary>
    /// <param name="name">要設定的 mpv 屬性名稱。</param>
    /// <param name="value">要套用到屬性的節點資料。</param>
    /// <returns>代表 libmpv 設定屬性回覆的工作。</returns>
    public Task SetPropertyNodeAsync(string name, MpvNode value)
    {
        EnsureNotDisposed();
        ulong requestId = NextRequestId();
        TaskCompletionSource<MpvNode> completion = new TaskCompletionSource<MpvNode>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[requestId] = completion;

        using (Utf8String propertyName = new Utf8String(name))
        using (MpvNodeAllocation node = new MpvNodeAllocation(value))
        {
            NativeMpvNode nativeNode = node.NativeNode;
            int error;
            using (NativeHandleLease handleLease = AcquireNativeHandle())
            {
                error = MpvNative.mpv_set_property_async(handleLease.Handle, requestId, propertyName.Pointer, MpvFormat.Node, ref nativeNode);
            }

            if (error < 0)
            {
                TaskCompletionSource<MpvNode>? removed;
                _pendingRequests.TryRemove(requestId, out removed);
                completion.TrySetException(new MpvException(error));
            }
        }

        return completion.Task;
    }

    /// <summary>
    /// 非同步取得 libmpv 屬性。
    /// </summary>
    /// <param name="name">要讀取的 mpv 屬性名稱。</param>
    /// <param name="format">屬性值要使用的 libmpv 資料格式。</param>
    /// <returns>可用於追蹤回覆事件的要求識別碼。</returns>
    public ulong GetPropertyAsync(string name, MpvFormat format)
    {
        EnsureNotDisposed();
        ulong requestId = NextRequestId();
        using (Utf8String propertyName = new Utf8String(name))
        {
            InvokeNative(handle => MpvError.ThrowIfError(MpvNative.mpv_get_property_async(handle, requestId, propertyName.Pointer, format)));
        }

        return requestId;
    }

    /// <summary>
    /// 非同步取得 libmpv 屬性並以節點工作接收回覆。
    /// </summary>
    /// <param name="name">要讀取的 mpv 屬性名稱。</param>
    /// <returns>代表 libmpv 屬性回覆節點的工作。</returns>
    public Task<MpvNode> GetPropertyNodeAsync(string name)
    {
        return GetPropertyNodeAsync(name, MpvFormat.Node);
    }

    /// <summary>
    /// 非同步取得 libmpv 屬性並以節點工作接收回覆。
    /// </summary>
    /// <param name="name">要讀取的 mpv 屬性名稱。</param>
    /// <param name="format">屬性值要使用的 libmpv 資料格式。</param>
    /// <returns>代表 libmpv 屬性回覆節點的工作。</returns>
    public Task<MpvNode> GetPropertyNodeAsync(string name, MpvFormat format)
    {
        EnsureNotDisposed();
        ulong requestId = NextRequestId();
        TaskCompletionSource<MpvNode> completion = new TaskCompletionSource<MpvNode>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[requestId] = completion;

        using (Utf8String propertyName = new Utf8String(name))
        {
            int error = InvokeNative(handle => MpvNative.mpv_get_property_async(handle, requestId, propertyName.Pointer, format));
            if (error < 0)
            {
                TaskCompletionSource<MpvNode>? removed;
                _pendingRequests.TryRemove(requestId, out removed);
                completion.TrySetException(new MpvException(error));
            }
        }

        return completion.Task;
    }

    /// <summary>
    /// 取得目前 mpv 可用命令清單。
    /// </summary>
    /// <returns>目前 mpv 回報的命令描述集合。</returns>
    public IReadOnlyList<MpvCommandInfo> GetCommandList()
    {
        IReadOnlyList<MpvNode> nodes = GetPropertyNode("command-list").AsArray();
        List<MpvCommandInfo> commands = new List<MpvCommandInfo>(nodes.Count);
        for (int i = 0; i < nodes.Count; i++)
        {
            commands.Add(MpvCommandInfo.FromNode(nodes[i]));
        }

        return new ReadOnlyCollection<MpvCommandInfo>(commands);
    }

    /// <summary>
    /// 取得目前 mpv 可用屬性清單。
    /// </summary>
    /// <returns>目前 mpv 回報的屬性名稱集合。</returns>
    public IReadOnlyList<string> GetPropertyList()
    {
        IReadOnlyList<MpvNode> nodes = GetPropertyNode("property-list").AsArray();
        List<string> properties = new List<string>(nodes.Count);
        for (int i = 0; i < nodes.Count; i++)
        {
            properties.Add(nodes[i].AsString() ?? string.Empty);
        }

        return new ReadOnlyCollection<string>(properties);
    }

    /// <summary>
    /// 取得目前 mpv 可用設定檔清單。
    /// </summary>
    /// <returns>目前 mpv 回報的設定檔資訊集合。</returns>
    public IReadOnlyList<MpvProfileInfo> GetProfiles()
    {
        IReadOnlyList<MpvNode> nodes = GetPropertyNode("profile-list").AsArray();
        List<MpvProfileInfo> profiles = new List<MpvProfileInfo>(nodes.Count);
        for (int i = 0; i < nodes.Count; i++)
        {
            profiles.Add(MpvProfileInfo.FromNode(nodes[i]));
        }

        return new ReadOnlyCollection<MpvProfileInfo>(profiles);
    }

    /// <summary>
    /// 取得目前 mpv 可用解碼器清單。
    /// </summary>
    /// <returns>目前 mpv 回報的解碼器資訊集合。</returns>
    public IReadOnlyList<MpvDecoderInfo> GetDecoders()
    {
        IReadOnlyList<MpvNode> nodes = GetPropertyNode("decoder-list").AsArray();
        List<MpvDecoderInfo> decoders = new List<MpvDecoderInfo>(nodes.Count);
        for (int i = 0; i < nodes.Count; i++)
        {
            decoders.Add(MpvDecoderInfo.FromNode(nodes[i]));
        }

        return new ReadOnlyCollection<MpvDecoderInfo>(decoders);
    }

    /// <summary>
    /// 取得目前 mpv 可用通訊協定清單。
    /// </summary>
    /// <returns>目前 mpv 回報的通訊協定名稱集合。</returns>
    public IReadOnlyList<string> GetProtocols()
    {
        return SplitCommaList(GetPropertyString("protocol-list"));
    }

    /// <summary>
    /// 取得目前 FFmpeg demuxer 清單。
    /// </summary>
    /// <returns>目前 mpv 回報的 demuxer 名稱集合。</returns>
    public IReadOnlyList<string> GetDemuxers()
    {
        return SplitCommaList(GetPropertyString("demuxer-lavf-list"));
    }

    /// <summary>
    /// 取得目前播放清單。
    /// </summary>
    /// <returns>目前播放清單項目集合。</returns>
    public IReadOnlyList<MpvPlaylistEntry> GetPlaylist()
    {
        IReadOnlyList<MpvNode> nodes = GetPropertyNode("playlist").AsArray();
        List<MpvPlaylistEntry> entries = new List<MpvPlaylistEntry>(nodes.Count);
        for (int i = 0; i < nodes.Count; i++)
        {
            entries.Add(MpvPlaylistEntry.FromNode(i, nodes[i]));
        }

        return new ReadOnlyCollection<MpvPlaylistEntry>(entries);
    }

    /// <summary>
    /// 取得目前播放軌清單。
    /// </summary>
    /// <returns>目前播放軌資訊集合。</returns>
    public IReadOnlyList<MpvTrackInfo> GetTracks()
    {
        return ReadTracks(GetPropertyNode("track-list"));
    }

    /// <summary>
    /// 取得目前章節清單。
    /// </summary>
    /// <returns>目前章節資訊集合。</returns>
    public IReadOnlyList<MpvChapterInfo> GetChapters()
    {
        IReadOnlyList<MpvNode> nodes = GetPropertyNode("chapter-list").AsArray();
        List<MpvChapterInfo> chapters = new List<MpvChapterInfo>(nodes.Count);
        for (int i = 0; i < nodes.Count; i++)
        {
            chapters.Add(MpvChapterInfo.FromNode(i, nodes[i]));
        }

        return new ReadOnlyCollection<MpvChapterInfo>(chapters);
    }

    /// <summary>
    /// 取得目前版本清單。
    /// </summary>
    /// <returns>目前版本資訊集合。</returns>
    public IReadOnlyList<MpvEditionInfo> GetEditions()
    {
        IReadOnlyList<MpvNode> nodes = GetPropertyNode("edition-list").AsArray();
        List<MpvEditionInfo> editions = new List<MpvEditionInfo>(nodes.Count);
        for (int i = 0; i < nodes.Count; i++)
        {
            editions.Add(MpvEditionInfo.FromNode(i, nodes[i]));
        }

        return new ReadOnlyCollection<MpvEditionInfo>(editions);
    }

    /// <summary>
    /// 取得目前媒體中繼資料。
    /// </summary>
    /// <returns>目前媒體中繼資料字典。</returns>
    public IReadOnlyDictionary<string, string> GetMetadata()
    {
        Dictionary<string, string> metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, MpvNode> item in GetPropertyNode("metadata").AsMap())
        {
            metadata[item.Key] = item.Value.AsString() ?? string.Empty;
        }

        return new ReadOnlyDictionary<string, string>(metadata);
    }

    /// <summary>
    /// 取得目前可用音訊裝置清單。
    /// </summary>
    /// <returns>目前可用音訊裝置資訊集合。</returns>
    public IReadOnlyList<MpvAudioDeviceInfo> GetAudioDevices()
    {
        IReadOnlyList<MpvNode> nodes = GetPropertyNode("audio-device-list").AsArray();
        List<MpvAudioDeviceInfo> devices = new List<MpvAudioDeviceInfo>(nodes.Count);
        for (int i = 0; i < nodes.Count; i++)
        {
            devices.Add(MpvAudioDeviceInfo.FromNode(nodes[i]));
        }

        return new ReadOnlyCollection<MpvAudioDeviceInfo>(devices);
    }

    /// <summary>
    /// 取得目前視訊參數。
    /// </summary>
    /// <returns>目前視訊參數。</returns>
    public MpvVideoParameters GetVideoParameters()
    {
        return MpvVideoParameters.FromNode(GetPropertyNode("video-params"));
    }

    /// <summary>
    /// 取得目前音訊參數。
    /// </summary>
    /// <returns>目前音訊參數。</returns>
    public MpvAudioParameters GetAudioParameters()
    {
        return MpvAudioParameters.FromNode(GetPropertyNode("audio-params"));
    }

    /// <summary>
    /// 取得目前輸入綁定清單。
    /// </summary>
    /// <returns>目前輸入綁定資訊集合。</returns>
    public IReadOnlyList<MpvInputBindingInfo> GetInputBindings()
    {
        IReadOnlyList<MpvNode> nodes = GetPropertyNode("input-bindings").AsArray();
        List<MpvInputBindingInfo> bindings = new List<MpvInputBindingInfo>(nodes.Count);
        for (int i = 0; i < nodes.Count; i++)
        {
            bindings.Add(MpvInputBindingInfo.FromNode(nodes[i]));
        }

        return new ReadOnlyCollection<MpvInputBindingInfo>(bindings);
    }

    /// <summary>
    /// 取得目前 demuxer 快取狀態。
    /// </summary>
    /// <returns>目前 demuxer 快取狀態。</returns>
    public MpvDemuxerCacheState GetDemuxerCacheState()
    {
        return MpvDemuxerCacheState.FromNode(GetPropertyNode("demuxer-cache-state"));
    }

    /// <summary>
    /// 等待目前用戶端已送出的非同步要求完成。
    /// </summary>
    public void WaitAsyncRequests()
    {
        EnsureNotDisposed();
        InvokeNative(handle => MpvNative.mpv_wait_async_requests(handle));
    }

    /// <summary>
    /// 設定 libmpv 事件喚醒通知。
    /// </summary>
    /// <param name="callback">收到事件喚醒通知時要執行的動作；傳入 <see langword="null"/> 可清除回呼。</param>
    public void SetWakeupCallback(Action? callback)
    {
        EnsureNotDisposed();
        _wakeupAction = callback;
        if (callback == null)
        {
            _wakeupCallback = null;
            InvokeNative(handle => MpvNative.mpv_set_wakeup_callback(handle, null, IntPtr.Zero));
            return;
        }

        _wakeupCallback = OnWakeup;
        InvokeNative(handle => MpvNative.mpv_set_wakeup_callback(handle, _wakeupCallback, IntPtr.Zero));
    }

    /// <summary>
    /// 主動喚醒目前 libmpv 用戶端的事件等待。
    /// </summary>
    public void Wakeup()
    {
        EnsureNotDisposed();
        InvokeNative(handle => MpvNative.mpv_wakeup(handle));
    }

    /// <summary>
    /// 取得 libmpv 事件喚醒 pipe 檔案描述元。
    /// </summary>
    /// <returns>喚醒 pipe 檔案描述元；Windows 平台通常會傳回負值。</returns>
    public int GetWakeupPipe()
    {
        EnsureNotDisposed();
        return InvokeNative(handle => MpvNative.mpv_get_wakeup_pipe(handle));
    }

    /// <summary>
    /// 從目前用戶端建立新的強參考受控用戶端控制代碼。
    /// </summary>
    /// <param name="name">新用戶端名稱。</param>
    /// <returns>新建立的 libmpv 受控用戶端控制代碼。</returns>
    public MpvClientHandle CreateClient(string name)
    {
        return new MpvClientHandle(CreateClientHandle(name), false);
    }

    /// <summary>
    /// 從目前用戶端建立新的弱參考受控用戶端控制代碼。
    /// </summary>
    /// <param name="name">新用戶端名稱。</param>
    /// <returns>新建立的弱參考 libmpv 受控用戶端控制代碼。</returns>
    public MpvClientHandle CreateWeakClient(string name)
    {
        return new MpvClientHandle(CreateWeakClientHandle(name), false);
    }

    /// <summary>
    /// 從目前用戶端建立新的強參考原生用戶端。
    /// </summary>
    /// <param name="name">新用戶端名稱。</param>
    /// <returns>新建立的 libmpv 原生用戶端控制代碼。</returns>
    public IntPtr CreateClientHandle(string name)
    {
        EnsureNotDisposed();
        using (Utf8String clientName = new Utf8String(name))
        {
            IntPtr clientHandle = InvokeNative(handle => MpvNative.mpv_create_client(handle, clientName.Pointer));
            if (clientHandle == IntPtr.Zero)
            {
                throw new MpvException("mpv_create_client returned null.");
            }

            return clientHandle;
        }
    }

    /// <summary>
    /// 從目前用戶端建立新的弱參考原生用戶端。
    /// </summary>
    /// <param name="name">新用戶端名稱。</param>
    /// <returns>新建立的弱參考 libmpv 原生用戶端控制代碼。</returns>
    public IntPtr CreateWeakClientHandle(string name)
    {
        EnsureNotDisposed();
        using (Utf8String clientName = new Utf8String(name))
        {
            IntPtr clientHandle = InvokeNative(handle => MpvNative.mpv_create_weak_client(handle, clientName.Pointer));
            if (clientHandle == IntPtr.Zero)
            {
                throw new MpvException("mpv_create_weak_client returned null.");
            }

            return clientHandle;
        }
    }

    /// <summary>
    /// 銷毀由 libmpv 建立的原生用戶端控制代碼。
    /// </summary>
    /// <param name="clientHandle">要銷毀的原生用戶端控制代碼。</param>
    public static void DestroyClientHandle(IntPtr clientHandle)
    {
        if (clientHandle != IntPtr.Zero)
        {
            MpvNative.mpv_destroy(clientHandle);
        }
    }

    /// <summary>
    /// 終止並銷毀由 libmpv 建立的原生用戶端控制代碼。
    /// </summary>
    /// <param name="clientHandle">要終止並銷毀的原生用戶端控制代碼。</param>
    public static void TerminateDestroyClientHandle(IntPtr clientHandle)
    {
        if (clientHandle != IntPtr.Zero)
        {
            MpvNative.mpv_terminate_destroy(clientHandle);
        }
    }

    /// <summary>
    /// 載入檔案或網址作為播放項目。
    /// </summary>
    /// <param name="pathOrUrl">要載入的檔案路徑或媒體網址。</param>
    /// <param name="mode">播放項目加入播放清單的方式。</param>
    public void LoadFile(string pathOrUrl, MpvLoadFileMode mode = MpvLoadFileMode.Replace)
    {
        LoadFile(pathOrUrl, mode, null, null);
    }

    /// <summary>
    /// 載入檔案或網址作為播放項目，並可指定插入索引與單檔選項。
    /// </summary>
    /// <param name="pathOrUrl">要載入的檔案路徑或媒體網址。</param>
    /// <param name="mode">播放項目加入播放清單的方式。</param>
    /// <param name="insertIndex">使用插入模式時要插入的索引；未指定時由 mpv 決定。</param>
    /// <param name="fileOptions">只在此播放項目播放期間套用的 mpv 選項。</param>
    public void LoadFile(string pathOrUrl, MpvLoadFileMode mode, int? insertIndex, IDictionary<string, string>? fileOptions)
    {
        if (fileOptions != null)
        {
            Dictionary<string, MpvNode> optionNodes = new Dictionary<string, MpvNode>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> option in fileOptions)
            {
                optionNodes[option.Key] = MpvNode.FromString(option.Value);
            }

            CommandNode(MpvNode.FromArray(new[]
            {
                MpvNode.FromString("loadfile"),
                MpvNode.FromString(pathOrUrl),
                MpvNode.FromString(ToLoadFileModeText(mode)),
                MpvNode.FromInt64(insertIndex ?? -1),
                MpvNode.FromMap(optionNodes)
            }));
            return;
        }

        List<string> arguments = new List<string>
        {
            "loadfile",
            pathOrUrl,
            ToLoadFileModeText(mode)
        };

        if (insertIndex.HasValue)
        {
            arguments.Add(insertIndex.Value.ToString(CultureInfo.InvariantCulture));
        }

        Command(arguments.ToArray());
    }

    /// <summary>
    /// 使用 <see cref="MpvMediaItem"/> 載入播放項目。
    /// </summary>
    /// <param name="item">要載入的媒體項目；可帶 HTTP 標頭、起訖時間與 per-file 選項。</param>
    /// <param name="mode">播放項目加入播放清單的方式。</param>
    public void Load(MpvMediaItem item, MpvLoadFileMode mode = MpvLoadFileMode.Replace)
    {
        if (item == null)
        {
            throw new ArgumentNullException(nameof(item));
        }

        IDictionary<string, string> fileOptions = item.BuildFileOptions();
        LoadFile(item.Source, mode, null, fileOptions.Count == 0 ? null : fileOptions);
    }

    /// <summary>
    /// 使用 <see cref="MpvMediaItem"/> 載入播放項目，並等到 libmpv 完成載入或回報失敗。
    /// </summary>
    /// <param name="item">要載入的媒體項目。</param>
    /// <param name="mode">播放項目加入播放清單的方式。</param>
    /// <param name="timeout">等待 <c>FileLoaded</c> 事件的逾時時間；未指定時使用 30 秒。</param>
    /// <param name="cancellationToken">取消等待的 token。</param>
    /// <returns>代表載入流程的工作；libmpv 回報 <c>EndFile</c> 為錯誤時擲出 <see cref="MpvException"/>。</returns>
    public Task LoadAsync(
        MpvMediaItem item,
        MpvLoadFileMode mode = MpvLoadFileMode.Replace,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        if (item == null)
        {
            throw new ArgumentNullException(nameof(item));
        }

        return LoadAsyncCore(item, mode, timeout ?? TimeSpan.FromSeconds(30), cancellationToken);
    }

    /// <summary>
    /// 執行 <see cref="LoadAsync(MpvMediaItem, MpvLoadFileMode, TimeSpan?, CancellationToken)"/> 的內部流程。
    /// </summary>
    /// <param name="item">要載入的媒體項目。</param>
    /// <param name="mode">播放項目加入播放清單的方式。</param>
    /// <param name="timeout">等待 <c>FileLoaded</c> 事件的逾時時間。</param>
    /// <param name="cancellationToken">取消等待的 token。</param>
    /// <returns>代表載入流程的工作。</returns>
    private async Task LoadAsyncCore(
        MpvMediaItem item,
        MpvLoadFileMode mode,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        TaskCompletionSource<MpvException?> completion = new TaskCompletionSource<MpvException?>(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<MpvEventArgs> fileLoadedHandler = delegate (object? sender, MpvEventArgs args)
        {
            completion.TrySetResult(null);
        };
        EventHandler<MpvEndFileEventArgs> endFileHandler = delegate (object? sender, MpvEndFileEventArgs args)
        {
            if (args.Reason == MpvEndFileReason.Error)
            {
                completion.TrySetResult(new MpvException("libmpv 回報播放項目載入失敗：" + args.Reason));
            }
        };

        FileLoaded += fileLoadedHandler;
        EndFile += endFileHandler;
        try
        {
            Load(item, mode);
            using (CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                linked.CancelAfter(timeout);
                using (linked.Token.Register(static state => ((TaskCompletionSource<MpvException?>)state!).TrySetCanceled(), completion))
                {
                    MpvException? failure = await completion.Task.ConfigureAwait(false);
                    if (failure != null)
                    {
                        throw failure;
                    }
                }
            }
        }
        finally
        {
            FileLoaded -= fileLoadedHandler;
            EndFile -= endFileHandler;
        }
    }

    /// <summary>
    /// 停止目前播放。
    /// </summary>
    public void Stop()
    {
        Stop(false);
    }

    /// <summary>
    /// 停止目前播放。
    /// </summary>
    /// <param name="keepPlaylist">停止後是否保留目前播放清單。</param>
    public void Stop(bool keepPlaylist)
    {
        if (keepPlaylist)
        {
            Command("stop", "keep-playlist");
            return;
        }

        Command("stop");
    }

    /// <summary>
    /// 搜尋目前播放項目的播放位置。
    /// </summary>
    /// <param name="seconds">要搜尋的秒數。</param>
    /// <param name="mode">mpv 搜尋模式，例如 <c>relative</c> 或 <c>absolute</c>。</param>
    public void Seek(double seconds, string mode = "relative")
    {
        Command("seek", seconds.ToString(CultureInfo.InvariantCulture), mode);
    }

    /// <summary>
    /// 非同步搜尋目前播放項目的播放位置。
    /// </summary>
    /// <param name="seconds">要搜尋的秒數。</param>
    /// <param name="mode">mpv 搜尋模式，例如 <c>relative</c> 或 <c>absolute</c>。</param>
    /// <returns>代表 libmpv 命令回覆的工作。</returns>
    public Task SeekAsync(double seconds, string mode = "relative")
    {
        return CommandAsync("seek", seconds.ToString(CultureInfo.InvariantCulture), mode);
    }

    /// <summary>
    /// 暫停播放。
    /// </summary>
    public void PausePlayback()
    {
        Pause = true;
    }

    /// <summary>
    /// 繼續播放。
    /// </summary>
    public void ResumePlayback()
    {
        Pause = false;
    }

    /// <summary>
    /// 還原上一個搜尋位置或標記搜尋位置。
    /// </summary>
    /// <param name="mark">是否只標記目前位置作為之後還原目標。</param>
    public void RevertSeek(bool mark = false)
    {
        if (mark)
        {
            Command("revert-seek", "mark");
            return;
        }

        Command("revert-seek");
    }

    /// <summary>
    /// 前進到下一個視訊影格。
    /// </summary>
    public void FrameStep()
    {
        Command("frame-step");
    }

    /// <summary>
    /// 前進指定數量的視訊影格。
    /// </summary>
    /// <param name="frames">要前進的影格數。</param>
    /// <param name="flags">mpv 影格步進旗標；未指定時使用 mpv 預設值。</param>
    public void FrameStep(int frames, string? flags = null)
    {
        if (string.IsNullOrEmpty(flags))
        {
            Command("frame-step", frames.ToString(CultureInfo.InvariantCulture));
            return;
        }

        Command("frame-step", frames.ToString(CultureInfo.InvariantCulture), flags!);
    }

    /// <summary>
    /// 回到上一個視訊影格。
    /// </summary>
    public void FrameBackStep()
    {
        Command("frame-back-step");
    }

    /// <summary>
    /// 載入播放清單檔案或網址。
    /// </summary>
    /// <param name="pathOrUrl">要載入的播放清單檔案路徑或網址。</param>
    /// <param name="mode">播放清單加入目前播放清單的方式。</param>
    /// <param name="insertIndex">使用插入模式時要插入的索引；未指定時由 mpv 決定。</param>
    public void LoadList(string pathOrUrl, MpvLoadListMode mode = MpvLoadListMode.Replace, int? insertIndex = null)
    {
        List<string> arguments = new List<string>
        {
            "loadlist",
            pathOrUrl,
            ToLoadListModeText(mode)
        };

        if (insertIndex.HasValue)
        {
            arguments.Add(insertIndex.Value.ToString(CultureInfo.InvariantCulture));
        }

        Command(arguments.ToArray());
    }

    /// <summary>
    /// 前往播放清單下一個項目。
    /// </summary>
    /// <param name="force">位於最後一個項目時是否強制結束目前播放。</param>
    public void PlaylistNext(bool force = false)
    {
        Command("playlist-next", force ? "force" : "weak");
    }

    /// <summary>
    /// 前往播放清單上一個項目。
    /// </summary>
    /// <param name="force">位於第一個項目時是否強制結束目前播放。</param>
    public void PlaylistPrevious(bool force = false)
    {
        Command("playlist-prev", force ? "force" : "weak");
    }

    /// <summary>
    /// 前往下一個來自不同播放清單路徑的項目。
    /// </summary>
    public void PlaylistNextPlaylist()
    {
        Command("playlist-next-playlist");
    }

    /// <summary>
    /// 前往上一個來自不同播放清單路徑的項目。
    /// </summary>
    public void PlaylistPreviousPlaylist()
    {
        Command("playlist-prev-playlist");
    }

    /// <summary>
    /// 播放指定索引的播放清單項目。
    /// </summary>
    /// <param name="index">以 0 為起始的播放清單索引。</param>
    public void PlayPlaylistIndex(int index)
    {
        Command("playlist-play-index", index.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// 重新播放目前播放清單項目。
    /// </summary>
    public void ReplayCurrentPlaylistEntry()
    {
        Command("playlist-play-index", "current");
    }

    /// <summary>
    /// 停止播放清單播放。
    /// </summary>
    public void StopPlaylistPlayback()
    {
        Command("playlist-play-index", "none");
    }

    /// <summary>
    /// 清除播放清單中目前播放項目以外的項目。
    /// </summary>
    public void ClearPlaylist()
    {
        Command("playlist-clear");
    }

    /// <summary>
    /// 移除指定索引的播放清單項目。
    /// </summary>
    /// <param name="index">以 0 為起始的播放清單索引。</param>
    public void RemovePlaylistIndex(int index)
    {
        Command("playlist-remove", index.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// 移除目前播放清單項目。
    /// </summary>
    public void RemoveCurrentPlaylistEntry()
    {
        Command("playlist-remove", "current");
    }

    /// <summary>
    /// 移動播放清單項目。
    /// </summary>
    /// <param name="sourceIndex">要移動的播放清單索引。</param>
    /// <param name="destinationIndex">目標播放清單索引。</param>
    public void MovePlaylistIndex(int sourceIndex, int destinationIndex)
    {
        Command(
            "playlist-move",
            sourceIndex.ToString(CultureInfo.InvariantCulture),
            destinationIndex.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// 隨機排序播放清單。
    /// </summary>
    public void ShufflePlaylist()
    {
        Command("playlist-shuffle");
    }

    /// <summary>
    /// 嘗試還原上一次播放清單隨機排序。
    /// </summary>
    public void UnshufflePlaylist()
    {
        Command("playlist-unshuffle");
    }

    /// <summary>
    /// 載入外部字幕播放軌。
    /// </summary>
    /// <param name="url">字幕檔案路徑或網路網址。</param>
    /// <param name="mode">字幕載入後的選取模式。</param>
    /// <param name="flags">字幕播放軌旗標。</param>
    /// <param name="title">字幕播放軌標題。</param>
    /// <param name="language">字幕播放軌語言代碼。</param>
    public void AddSubtitle(string url, MpvTrackLoadMode mode = MpvTrackLoadMode.Select, MpvTrackLoadFlags flags = MpvTrackLoadFlags.None, string? title = null, string? language = null)
    {
        Command(BuildExternalTrackArguments("sub-add", url, mode, flags, title, language, null));
    }

    /// <summary>
    /// 非同步載入外部字幕播放軌。
    /// </summary>
    /// <param name="url">字幕檔案路徑或網路網址。</param>
    /// <param name="mode">字幕載入後的選取模式。</param>
    /// <param name="flags">字幕播放軌旗標。</param>
    /// <param name="title">字幕播放軌標題。</param>
    /// <param name="language">字幕播放軌語言代碼。</param>
    /// <returns>代表 libmpv 命令回覆的工作。</returns>
    public Task AddSubtitleAsync(string url, MpvTrackLoadMode mode = MpvTrackLoadMode.Select, MpvTrackLoadFlags flags = MpvTrackLoadFlags.None, string? title = null, string? language = null)
    {
        return CommandAsync(BuildExternalTrackArguments("sub-add", url, mode, flags, title, language, null));
    }

    /// <summary>
    /// 移除外部字幕播放軌。
    /// </summary>
    /// <param name="trackId">要移除的字幕播放軌識別碼；未指定時移除目前字幕軌。</param>
    public void RemoveSubtitle(long? trackId = null)
    {
        CommandOptionalTrackId("sub-remove", trackId);
    }

    /// <summary>
    /// 重新載入外部字幕播放軌。
    /// </summary>
    /// <param name="trackId">要重新載入的字幕播放軌識別碼；未指定時重新載入目前字幕軌。</param>
    public void ReloadSubtitle(long? trackId = null)
    {
        CommandOptionalTrackId("sub-reload", trackId);
    }

    /// <summary>
    /// 依字幕事件調整字幕顯示時間。
    /// </summary>
    /// <param name="skip">要前進或後退的字幕事件數。</param>
    /// <param name="target">要操作的字幕軌。</param>
    public void StepSubtitle(int skip, MpvSubtitleStepTarget target = MpvSubtitleStepTarget.Primary)
    {
        Command("sub-step", skip.ToString(CultureInfo.InvariantCulture), ToSubtitleStepTargetText(target));
    }

    /// <summary>
    /// 依字幕事件搜尋播放位置。
    /// </summary>
    /// <param name="skip">要前進或後退的字幕事件數。</param>
    /// <param name="target">要操作的字幕軌。</param>
    public void SeekSubtitle(int skip, MpvSubtitleStepTarget target = MpvSubtitleStepTarget.Primary)
    {
        Command("sub-seek", skip.ToString(CultureInfo.InvariantCulture), ToSubtitleStepTargetText(target));
    }

    /// <summary>
    /// 載入外部音訊播放軌。
    /// </summary>
    /// <param name="url">音訊檔案路徑或網路網址。</param>
    /// <param name="mode">音訊載入後的選取模式。</param>
    /// <param name="flags">音訊播放軌旗標。</param>
    /// <param name="title">音訊播放軌標題。</param>
    /// <param name="language">音訊播放軌語言代碼。</param>
    public void AddAudioTrack(string url, MpvTrackLoadMode mode = MpvTrackLoadMode.Select, MpvTrackLoadFlags flags = MpvTrackLoadFlags.None, string? title = null, string? language = null)
    {
        Command(BuildExternalTrackArguments("audio-add", url, mode, flags, title, language, null));
    }

    /// <summary>
    /// 非同步載入外部音訊播放軌。
    /// </summary>
    /// <param name="url">音訊檔案路徑或網路網址。</param>
    /// <param name="mode">音訊載入後的選取模式。</param>
    /// <param name="flags">音訊播放軌旗標。</param>
    /// <param name="title">音訊播放軌標題。</param>
    /// <param name="language">音訊播放軌語言代碼。</param>
    /// <returns>代表 libmpv 命令回覆的工作。</returns>
    public Task AddAudioTrackAsync(string url, MpvTrackLoadMode mode = MpvTrackLoadMode.Select, MpvTrackLoadFlags flags = MpvTrackLoadFlags.None, string? title = null, string? language = null)
    {
        return CommandAsync(BuildExternalTrackArguments("audio-add", url, mode, flags, title, language, null));
    }

    /// <summary>
    /// 移除外部音訊播放軌。
    /// </summary>
    /// <param name="trackId">要移除的音訊播放軌識別碼；未指定時移除目前音訊軌。</param>
    public void RemoveAudioTrack(long? trackId = null)
    {
        CommandOptionalTrackId("audio-remove", trackId);
    }

    /// <summary>
    /// 重新載入外部音訊播放軌。
    /// </summary>
    /// <param name="trackId">要重新載入的音訊播放軌識別碼；未指定時重新載入目前音訊軌。</param>
    public void ReloadAudioTrack(long? trackId = null)
    {
        CommandOptionalTrackId("audio-reload", trackId);
    }

    /// <summary>
    /// 載入外部視訊播放軌。
    /// </summary>
    /// <param name="url">視訊檔案路徑或網路網址。</param>
    /// <param name="mode">視訊載入後的選取模式。</param>
    /// <param name="flags">視訊播放軌旗標。</param>
    /// <param name="title">視訊播放軌標題。</param>
    /// <param name="language">視訊播放軌語言代碼。</param>
    /// <param name="albumArt">是否將視訊當作專輯封面載入。</param>
    public void AddVideoTrack(string url, MpvTrackLoadMode mode = MpvTrackLoadMode.Select, MpvTrackLoadFlags flags = MpvTrackLoadFlags.None, string? title = null, string? language = null, bool albumArt = false)
    {
        Command(BuildExternalTrackArguments("video-add", url, mode, flags, title, language, albumArt));
    }

    /// <summary>
    /// 非同步載入外部視訊播放軌。
    /// </summary>
    /// <param name="url">視訊檔案路徑或網路網址。</param>
    /// <param name="mode">視訊載入後的選取模式。</param>
    /// <param name="flags">視訊播放軌旗標。</param>
    /// <param name="title">視訊播放軌標題。</param>
    /// <param name="language">視訊播放軌語言代碼。</param>
    /// <param name="albumArt">是否將視訊當作專輯封面載入。</param>
    /// <returns>代表 libmpv 命令回覆的工作。</returns>
    public Task AddVideoTrackAsync(string url, MpvTrackLoadMode mode = MpvTrackLoadMode.Select, MpvTrackLoadFlags flags = MpvTrackLoadFlags.None, string? title = null, string? language = null, bool albumArt = false)
    {
        return CommandAsync(BuildExternalTrackArguments("video-add", url, mode, flags, title, language, albumArt));
    }

    /// <summary>
    /// 移除外部視訊播放軌。
    /// </summary>
    /// <param name="trackId">要移除的視訊播放軌識別碼；未指定時移除目前視訊軌。</param>
    public void RemoveVideoTrack(long? trackId = null)
    {
        CommandOptionalTrackId("video-remove", trackId);
    }

    /// <summary>
    /// 重新載入外部視訊播放軌。
    /// </summary>
    /// <param name="trackId">要重新載入的視訊播放軌識別碼；未指定時重新載入目前視訊軌。</param>
    public void ReloadVideoTrack(long? trackId = null)
    {
        CommandOptionalTrackId("video-reload", trackId);
    }

    /// <summary>
    /// 重新掃描外部字幕、音訊與封面檔案。
    /// </summary>
    /// <param name="mode">重新掃描後的選軌行為。</param>
    public void RescanExternalFiles(MpvExternalFilesRescanMode mode = MpvExternalFilesRescanMode.Reselect)
    {
        Command("rescan-external-files", mode == MpvExternalFilesRescanMode.KeepSelection ? "keep-selection" : "reselect");
    }

    /// <summary>
    /// 選取指定視訊播放軌。
    /// </summary>
    /// <param name="trackId">要選取的視訊播放軌識別碼；未指定時停用視訊播放軌。</param>
    public void SelectVideoTrack(long? trackId)
    {
        SetTrackSelection("vid", trackId);
    }

    /// <summary>
    /// 選取指定音訊播放軌。
    /// </summary>
    /// <param name="trackId">要選取的音訊播放軌識別碼；未指定時停用音訊播放軌。</param>
    public void SelectAudioTrack(long? trackId)
    {
        SetTrackSelection("aid", trackId);
    }

    /// <summary>
    /// 選取指定字幕播放軌。
    /// </summary>
    /// <param name="trackId">要選取的字幕播放軌識別碼；未指定時停用字幕播放軌。</param>
    public void SelectSubtitleTrack(long? trackId)
    {
        SetTrackSelection("sid", trackId);
    }

    /// <summary>
    /// 選取指定次要字幕播放軌。
    /// </summary>
    /// <param name="trackId">要選取的次要字幕播放軌識別碼；未指定時停用次要字幕播放軌。</param>
    public void SelectSecondarySubtitleTrack(long? trackId)
    {
        SetTrackSelection("secondary-sid", trackId);
    }

    /// <summary>
    /// 選取指定章節。
    /// </summary>
    /// <param name="chapterIndex">以 0 為起始的章節索引。</param>
    public void SelectChapter(long chapterIndex)
    {
        SetPropertyInt64("chapter", chapterIndex);
    }

    /// <summary>
    /// 選取指定版本。
    /// </summary>
    /// <param name="editionIndex">以 0 為起始的版本索引。</param>
    public void SelectEdition(long editionIndex)
    {
        SetPropertyInt64("edition", editionIndex);
    }

    /// <summary>
    /// 選取指定音訊輸出裝置。
    /// </summary>
    /// <param name="name">音訊裝置名稱；傳入 <see langword="null"/> 或空字串時使用自動選取。</param>
    public void SelectAudioDevice(string? name)
    {
        SetPropertyString("audio-device", string.IsNullOrEmpty(name) ? "auto" : name!);
    }

    /// <summary>
    /// 顯示 mpv 文字 OSD。
    /// </summary>
    /// <param name="text">要顯示的文字。</param>
    /// <param name="durationMilliseconds">顯示毫秒數；未指定時使用 mpv 預設值。</param>
    /// <param name="minimumOsdLevel">最低 OSD 等級；未指定時使用 mpv 預設值。</param>
    public void ShowText(string text, int? durationMilliseconds = null, int? minimumOsdLevel = null)
    {
        List<string> arguments = new List<string> { "show-text", text };
        if (durationMilliseconds.HasValue || minimumOsdLevel.HasValue)
        {
            arguments.Add((durationMilliseconds ?? -1).ToString(CultureInfo.InvariantCulture));
        }

        if (minimumOsdLevel.HasValue)
        {
            arguments.Add(minimumOsdLevel.Value.ToString(CultureInfo.InvariantCulture));
        }

        Command(arguments.ToArray());
    }

    /// <summary>
    /// 顯示 mpv 進度 OSD。
    /// </summary>
    public void ShowProgress()
    {
        Command("show-progress");
    }

    /// <summary>
    /// 將文字輸出到 mpv 標準輸出。
    /// </summary>
    /// <param name="text">要輸出的文字。</param>
    public void PrintText(string text)
    {
        Command("print-text", text);
    }

    /// <summary>
    /// 展開文字中的 mpv 屬性參照。
    /// </summary>
    /// <param name="text">要展開的文字。</param>
    /// <returns>展開後的文字。</returns>
    public string ExpandText(string text)
    {
        return CommandWithResult("expand-text", text).AsString() ?? string.Empty;
    }

    /// <summary>
    /// 展開 mpv 雙波浪符號路徑。
    /// </summary>
    /// <param name="path">要展開的路徑文字。</param>
    /// <returns>展開後的路徑文字。</returns>
    public string ExpandPath(string path)
    {
        return CommandWithResult("expand-path", path).AsString() ?? string.Empty;
    }

    /// <summary>
    /// 正規化檔案路徑或網址。
    /// </summary>
    /// <param name="pathOrUrl">要正規化的檔案路徑或網址。</param>
    /// <returns>正規化後的檔案路徑或網址。</returns>
    public string NormalizePath(string pathOrUrl)
    {
        return CommandWithResult("normalize-path", pathOrUrl).AsString() ?? string.Empty;
    }

    /// <summary>
    /// 將文字轉成可在 ASS OSD 中逐字顯示的內容。
    /// </summary>
    /// <param name="text">要轉換的文字。</param>
    /// <returns>已轉換的 ASS 文字。</returns>
    public string EscapeAss(string text)
    {
        return CommandWithResult("escape-ass", text).AsString() ?? string.Empty;
    }

    /// <summary>
    /// 新增或更新 ASS 文字 OSD 覆疊層。
    /// </summary>
    /// <param name="id">覆疊層識別碼。</param>
    /// <param name="assEvents">ASS 事件文字。</param>
    /// <param name="resolutionX">ASS PlayResX 值。</param>
    /// <param name="resolutionY">ASS PlayResY 值。</param>
    /// <param name="zOrder">覆疊層 Z 順序。</param>
    /// <param name="hidden">是否隱藏覆疊層。</param>
    /// <param name="computeBounds">是否要求 mpv 回傳覆疊層邊界。</param>
    /// <returns>mpv 回傳的覆疊層結果節點。</returns>
    public MpvNode ShowAssOverlay(int id, string assEvents, int resolutionX = 0, int resolutionY = 720, int zOrder = 0, bool hidden = false, bool computeBounds = false)
    {
        Dictionary<string, MpvNode> arguments = new Dictionary<string, MpvNode>(StringComparer.Ordinal)
        {
            { "id", MpvNode.FromInt64(id) },
            { "format", MpvNode.FromString("ass-events") },
            { "data", MpvNode.FromString(assEvents) },
            { "res_x", MpvNode.FromInt64(resolutionX) },
            { "res_y", MpvNode.FromInt64(resolutionY) },
            { "z", MpvNode.FromInt64(zOrder) },
            { "hidden", MpvNode.FromFlag(hidden) },
            { "compute_bounds", MpvNode.FromFlag(computeBounds) }
        };
        return CommandNamed("osd-overlay", arguments);
    }

    /// <summary>
    /// 移除 ASS 文字 OSD 覆疊層。
    /// </summary>
    /// <param name="id">要移除的覆疊層識別碼。</param>
    /// <returns>mpv 回傳的覆疊層結果節點。</returns>
    public MpvNode RemoveAssOverlay(int id)
    {
        Dictionary<string, MpvNode> arguments = new Dictionary<string, MpvNode>(StringComparer.Ordinal)
        {
            { "id", MpvNode.FromInt64(id) },
            { "format", MpvNode.FromString("none") }
        };
        return CommandNamed("osd-overlay", arguments);
    }

    /// <summary>
    /// 新增或更新原始像素 OSD 覆疊層。
    /// </summary>
    /// <param name="id">覆疊層識別碼。</param>
    /// <param name="x">覆疊層 X 座標。</param>
    /// <param name="y">覆疊層 Y 座標。</param>
    /// <param name="fileName">包含原始像素資料的檔案路徑。</param>
    /// <param name="offset">像素資料在檔案中的起始位移。</param>
    /// <param name="format">像素格式文字。</param>
    /// <param name="width">來源寬度。</param>
    /// <param name="height">來源高度。</param>
    /// <param name="stride">來源每列位元組距離。</param>
    /// <param name="displayWidth">顯示寬度。</param>
    /// <param name="displayHeight">顯示高度。</param>
    public void AddOverlay(
        int id,
        int x,
        int y,
        string fileName,
        long offset,
        string format,
        int width,
        int height,
        int stride,
        int displayWidth,
        int displayHeight)
    {
        Command(
            "overlay-add",
            id.ToString(CultureInfo.InvariantCulture),
            x.ToString(CultureInfo.InvariantCulture),
            y.ToString(CultureInfo.InvariantCulture),
            fileName,
            offset.ToString(CultureInfo.InvariantCulture),
            format,
            width.ToString(CultureInfo.InvariantCulture),
            height.ToString(CultureInfo.InvariantCulture),
            stride.ToString(CultureInfo.InvariantCulture),
            displayWidth.ToString(CultureInfo.InvariantCulture),
            displayHeight.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// 移除原始像素 OSD 覆疊層。
    /// </summary>
    /// <param name="id">要移除的覆疊層識別碼。</param>
    public void RemoveOverlay(int id)
    {
        Command("overlay-remove", id.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// 依 mpv 截圖設定擷取截圖檔案。
    /// </summary>
    /// <param name="mode">截圖內容模式。</param>
    public void TakeScreenshot(MpvScreenshotMode mode = MpvScreenshotMode.Subtitles)
    {
        Command("screenshot", ToScreenshotModeText(mode));
    }

    /// <summary>
    /// 依 mpv 截圖設定與進階旗標擷取截圖檔案。
    /// </summary>
    /// <param name="mode">截圖內容模式。</param>
    /// <param name="flags">截圖進階旗標。</param>
    public void TakeScreenshot(MpvScreenshotMode mode, MpvScreenshotFlags flags)
    {
        Command("screenshot", ToScreenshotArgumentText(mode, flags));
    }

    /// <summary>
    /// 將目前畫面截圖儲存到指定檔案。
    /// </summary>
    /// <param name="fileName">要儲存的截圖檔案名稱。</param>
    /// <param name="mode">截圖內容模式。</param>
    /// <returns>mpv 實際回報的檔案名稱；沒有回報時傳回輸入檔名。</returns>
    public string TakeScreenshotToFile(string fileName, MpvScreenshotMode mode = MpvScreenshotMode.Subtitles)
    {
        MpvNode result = CommandWithResult("screenshot-to-file", fileName, ToScreenshotModeText(mode));
        return result.GetValueOrNone("filename").AsString() ?? fileName;
    }

    /// <summary>
    /// 將目前畫面截圖儲存到指定檔案，並套用進階旗標。
    /// </summary>
    /// <param name="fileName">要儲存的截圖檔案名稱。</param>
    /// <param name="mode">截圖內容模式。</param>
    /// <param name="flags">截圖進階旗標。</param>
    /// <returns>mpv 實際回報的檔案名稱；沒有回報時傳回輸入檔名。</returns>
    public string TakeScreenshotToFile(string fileName, MpvScreenshotMode mode, MpvScreenshotFlags flags)
    {
        MpvNode result = CommandWithResult("screenshot-to-file", fileName, ToScreenshotArgumentText(mode, flags));
        return result.GetValueOrNone("filename").AsString() ?? fileName;
    }

    /// <summary>
    /// 將目前畫面截圖回傳為記憶體像素資料。
    /// </summary>
    /// <param name="mode">截圖內容模式。</param>
    /// <param name="format">截圖像素格式。</param>
    /// <returns>記憶體截圖結果。</returns>
    public MpvScreenshot TakeRawScreenshot(MpvScreenshotMode mode = MpvScreenshotMode.Subtitles, MpvScreenshotFormat format = MpvScreenshotFormat.Bgr0)
    {
        return MpvScreenshot.FromNode(CommandWithResult("screenshot-raw", ToScreenshotModeText(mode), ToScreenshotFormatText(format)));
    }

    /// <summary>
    /// 將目前畫面截圖回傳為記憶體像素資料，並套用進階旗標。
    /// </summary>
    /// <param name="mode">截圖內容模式。</param>
    /// <param name="flags">截圖進階旗標。</param>
    /// <param name="format">截圖像素格式。</param>
    /// <returns>記憶體截圖結果。</returns>
    public MpvScreenshot TakeRawScreenshot(MpvScreenshotMode mode, MpvScreenshotFlags flags, MpvScreenshotFormat format = MpvScreenshotFormat.Bgr0)
    {
        return MpvScreenshot.FromNode(CommandWithResult("screenshot-raw", ToScreenshotArgumentText(mode, flags), ToScreenshotFormatText(format)));
    }

    /// <summary>
    /// 將濾鏡加入音訊濾鏡鏈。
    /// </summary>
    /// <param name="filter">要加入的音訊濾鏡描述。</param>
    public void AddAudioFilter(string filter)
    {
        Command("af", "add", filter);
    }

    /// <summary>
    /// 設定整個音訊濾鏡鏈。
    /// </summary>
    /// <param name="filters">完整音訊濾鏡鏈描述。</param>
    public void SetAudioFilters(string filters)
    {
        Command("af", "set", filters);
    }

    /// <summary>
    /// 從音訊濾鏡鏈移除濾鏡。
    /// </summary>
    /// <param name="filter">要移除的音訊濾鏡描述。</param>
    public void RemoveAudioFilter(string filter)
    {
        Command("af", "remove", filter);
    }

    /// <summary>
    /// 切換音訊濾鏡鏈中的濾鏡。
    /// </summary>
    /// <param name="filter">要切換的音訊濾鏡描述。</param>
    public void ToggleAudioFilter(string filter)
    {
        Command("af", "toggle", filter);
    }

    /// <summary>
    /// 清除音訊濾鏡鏈。
    /// </summary>
    public void ClearAudioFilters()
    {
        Command("af", "clr", string.Empty);
    }

    /// <summary>
    /// 將濾鏡加入視訊濾鏡鏈。
    /// </summary>
    /// <param name="filter">要加入的視訊濾鏡描述。</param>
    public void AddVideoFilter(string filter)
    {
        Command("vf", "add", filter);
    }

    /// <summary>
    /// 設定整個視訊濾鏡鏈。
    /// </summary>
    /// <param name="filters">完整視訊濾鏡鏈描述。</param>
    public void SetVideoFilters(string filters)
    {
        Command("vf", "set", filters);
    }

    /// <summary>
    /// 從視訊濾鏡鏈移除濾鏡。
    /// </summary>
    /// <param name="filter">要移除的視訊濾鏡描述。</param>
    public void RemoveVideoFilter(string filter)
    {
        Command("vf", "remove", filter);
    }

    /// <summary>
    /// 切換視訊濾鏡鏈中的濾鏡。
    /// </summary>
    /// <param name="filter">要切換的視訊濾鏡描述。</param>
    public void ToggleVideoFilter(string filter)
    {
        Command("vf", "toggle", filter);
    }

    /// <summary>
    /// 清除視訊濾鏡鏈。
    /// </summary>
    public void ClearVideoFilters()
    {
        Command("vf", "clr", string.Empty);
    }

    /// <summary>
    /// 將命令傳送給指定視訊或音訊濾鏡。
    /// </summary>
    /// <param name="target">濾鏡鏈目標。</param>
    /// <param name="label">濾鏡標籤。</param>
    /// <param name="commandName">濾鏡命令名稱。</param>
    /// <param name="argument">濾鏡命令引數。</param>
    /// <param name="commandTarget">濾鏡命令目標；未指定時使用 mpv 預設值。</param>
    public void SendFilterCommand(MpvFilterCommandTarget target, string label, string commandName, string argument, string? commandTarget = null)
    {
        string mpvCommand = target == MpvFilterCommandTarget.Audio ? "af-command" : "vf-command";
        if (string.IsNullOrEmpty(commandTarget))
        {
            Command(mpvCommand, label, commandName, argument);
            return;
        }

        Command(mpvCommand, label, commandName, argument, commandTarget!);
    }

    /// <summary>
    /// 將指定屬性增加指定數值。
    /// </summary>
    /// <param name="propertyName">要變更的 mpv 屬性名稱。</param>
    /// <param name="value">要增加的數值。</param>
    public void AddProperty(string propertyName, double value)
    {
        Command("add", propertyName, value.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// 將指定屬性乘上指定數值。
    /// </summary>
    /// <param name="propertyName">要變更的 mpv 屬性名稱。</param>
    /// <param name="value">要乘上的數值。</param>
    public void MultiplyProperty(string propertyName, double value)
    {
        Command("multiply", propertyName, value.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// 循環切換指定屬性。
    /// </summary>
    /// <param name="propertyName">要循環切換的 mpv 屬性名稱。</param>
    /// <param name="reverse">是否反向循環。</param>
    public void CycleProperty(string propertyName, bool reverse = false)
    {
        if (reverse)
        {
            Command("cycle", propertyName, "down");
            return;
        }

        Command("cycle", propertyName);
    }

    /// <summary>
    /// 在指定值集合中循環切換屬性。
    /// </summary>
    /// <param name="propertyName">要循環切換的 mpv 屬性名稱。</param>
    /// <param name="reverse">是否反向循環。</param>
    /// <param name="values">可循環切換的屬性值集合。</param>
    public void CyclePropertyValues(string propertyName, bool reverse, params string[] values)
    {
        List<string> arguments = new List<string> { "cycle-values" };
        if (reverse)
        {
            arguments.Add("!reverse");
        }

        arguments.Add(propertyName);
        arguments.AddRange(values);
        Command(arguments.ToArray());
    }

    /// <summary>
    /// 變更 mpv 清單型選項或屬性。
    /// </summary>
    /// <param name="name">清單型選項或屬性名稱。</param>
    /// <param name="operation">清單變更操作。</param>
    /// <param name="value">清單項目值；操作不需要值時可省略。</param>
    public void ChangeList(string name, MpvListChangeOperation operation, string? value = null)
    {
        Command("change-list", name, ToListChangeOperationText(operation), value ?? string.Empty);
    }

    /// <summary>
    /// 將鍵盤按鍵事件送入 mpv 輸入處理器。
    /// </summary>
    /// <param name="keyName">mpv input.conf 使用的按鍵名稱。</param>
    /// <param name="scale">可縮放輸入的縮放值；未指定時使用 mpv 預設值。</param>
    public void KeyPress(string keyName, double? scale = null)
    {
        if (scale.HasValue)
        {
            Command("keypress", keyName, scale.Value.ToString(CultureInfo.InvariantCulture));
            return;
        }

        Command("keypress", keyName);
    }

    /// <summary>
    /// 將鍵盤按下事件送入 mpv 輸入處理器。
    /// </summary>
    /// <param name="keyName">mpv input.conf 使用的按鍵名稱。</param>
    public void KeyDown(string keyName)
    {
        Command("keydown", keyName);
    }

    /// <summary>
    /// 將鍵盤放開事件送入 mpv 輸入處理器。
    /// </summary>
    /// <param name="keyName">mpv input.conf 使用的按鍵名稱；未指定時放開全部按鍵。</param>
    public void KeyUp(string? keyName = null)
    {
        if (string.IsNullOrEmpty(keyName))
        {
            Command("keyup");
            return;
        }

        Command("keyup", keyName!);
    }

    /// <summary>
    /// 將滑鼠事件送入 mpv 輸入處理器。
    /// </summary>
    /// <param name="x">滑鼠 X 座標。</param>
    /// <param name="y">滑鼠 Y 座標。</param>
    /// <param name="button">滑鼠按鈕編號；未指定時只更新滑鼠位置。</param>
    /// <param name="doubleClick">是否送出雙擊事件。</param>
    public void Mouse(int x, int y, int? button = null, bool doubleClick = false)
    {
        if (button.HasValue)
        {
            Command(
                "mouse",
                x.ToString(CultureInfo.InvariantCulture),
                y.ToString(CultureInfo.InvariantCulture),
                button.Value.ToString(CultureInfo.InvariantCulture),
                doubleClick ? "double" : "single");
            return;
        }

        Command("mouse", x.ToString(CultureInfo.InvariantCulture), y.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// 載入 mpv 輸入設定檔。
    /// </summary>
    /// <param name="fileName">input.conf 檔案路徑。</param>
    public void LoadInputConfigFile(string fileName)
    {
        Command("load-input-conf", fileName);
    }

    /// <summary>
    /// 設定單一輸入按鍵綁定。
    /// </summary>
    /// <param name="keyName">mpv input.conf 使用的按鍵名稱。</param>
    /// <param name="commandText">要綁定的完整 mpv 命令文字。</param>
    /// <param name="comment">綁定註解。</param>
    public void BindKey(string keyName, string commandText, string? comment = null)
    {
        if (comment == null)
        {
            Command("keybind", keyName, commandText);
            return;
        }

        Command("keybind", keyName, commandText, comment);
    }

    /// <summary>
    /// 定義 mpv 輸入 section。
    /// </summary>
    /// <param name="name">section 名稱。</param>
    /// <param name="contents">section 內容，格式同 input.conf。</param>
    /// <param name="mode">section 建立模式。</param>
    public void DefineInputSection(string name, string contents, MpvInputSectionMode mode = MpvInputSectionMode.Default)
    {
        Command("define-section", name, contents, mode == MpvInputSectionMode.Force ? "force" : "default");
    }

    /// <summary>
    /// 啟用 mpv 輸入 section。
    /// </summary>
    /// <param name="name">要啟用的 section 名稱。</param>
    /// <param name="flags">啟用 section 時使用的旗標。</param>
    public void EnableInputSection(string name, MpvInputSectionFlags flags = MpvInputSectionFlags.None)
    {
        if (flags == MpvInputSectionFlags.None)
        {
            Command("enable-section", name);
            return;
        }

        Command("enable-section", name, ToInputSectionFlagsText(flags));
    }

    /// <summary>
    /// 停用 mpv 輸入 section。
    /// </summary>
    /// <param name="name">要停用的 section 名稱。</param>
    public void DisableInputSection(string name)
    {
        Command("disable-section", name);
    }

    /// <summary>
    /// 載入 Lua 或 JavaScript 腳本檔案。
    /// </summary>
    /// <param name="fileName">Lua 或 JavaScript 腳本檔案路徑。</param>
    public void LoadScript(string fileName)
    {
        Command("load-script", fileName);
    }

    /// <summary>
    /// 傳送訊息給所有 mpv 指令碼與用戶端。
    /// </summary>
    /// <param name="arguments">訊息引數集合。</param>
    public void SendScriptMessage(params string[] arguments)
    {
        List<string> commandArguments = new List<string> { "script-message" };
        commandArguments.AddRange(arguments);
        Command(commandArguments.ToArray());
    }

    /// <summary>
    /// 傳送訊息給指定 mpv 指令碼或用戶端。
    /// </summary>
    /// <param name="target">目標指令碼或用戶端名稱。</param>
    /// <param name="arguments">訊息引數集合。</param>
    public void SendScriptMessageTo(string target, params string[] arguments)
    {
        List<string> commandArguments = new List<string> { "script-message-to", target };
        commandArguments.AddRange(arguments);
        Command(commandArguments.ToArray());
    }

    /// <summary>
    /// 呼叫指令碼提供的按鍵綁定。
    /// </summary>
    /// <param name="name">指令碼綁定名稱。</param>
    /// <param name="argument">傳給指令碼綁定的自訂引數。</param>
    public void InvokeScriptBinding(string name, string? argument = null)
    {
        if (argument == null)
        {
            Command("script-binding", name);
            return;
        }

        Command("script-binding", name, argument);
    }

    /// <summary>
    /// 執行外部處理序命令。
    /// </summary>
    /// <param name="fileName">要執行的檔案名稱。</param>
    /// <param name="arguments">傳給處理序的引數集合。</param>
    public void RunProcess(string fileName, params string[] arguments)
    {
        List<string> commandArguments = new List<string> { "run", fileName };
        commandArguments.AddRange(arguments);
        Command(commandArguments.ToArray());
    }

    /// <summary>
    /// 使用 mpv <c>subprocess</c> 命令執行外部處理序並取得結果。
    /// </summary>
    /// <param name="arguments">處理序命令與引數集合。</param>
    /// <param name="captureStdout">是否擷取標準輸出。</param>
    /// <param name="captureStderr">是否擷取標準錯誤。</param>
    /// <param name="playbackOnly">是否只允許在播放期間執行。</param>
    /// <param name="captureSize">擷取標準輸出與標準錯誤的最大位元組數；未指定時使用 mpv 預設值。</param>
    /// <param name="detach">是否以分離模式執行處理序。</param>
    /// <param name="environment">要傳給處理序的環境變數集合，格式為 <c>名稱=值</c>；未指定時使用 mpv 預設值。</param>
    /// <param name="stdinData">要寫入處理序標準輸入的文字；未指定時不寫入。</param>
    /// <param name="passthroughStdin">是否將 mpv 標準輸入傳遞給處理序。</param>
    /// <returns>mpv 回傳的處理序結果節點。</returns>
    public MpvNode RunSubprocess(
        IReadOnlyList<string> arguments,
        bool captureStdout = true,
        bool captureStderr = true,
        bool playbackOnly = false,
        long? captureSize = null,
        bool detach = false,
        IReadOnlyList<string>? environment = null,
        string? stdinData = null,
        bool passthroughStdin = false)
    {
        if (arguments == null)
        {
            throw new ArgumentNullException(nameof(arguments));
        }

        List<MpvNode> argumentNodes = new List<MpvNode>(arguments.Count);
        for (int i = 0; i < arguments.Count; i++)
        {
            argumentNodes.Add(MpvNode.FromString(arguments[i]));
        }

        Dictionary<string, MpvNode> commandArguments = new Dictionary<string, MpvNode>(StringComparer.Ordinal)
        {
            { "args", MpvNode.FromArray(argumentNodes) },
            { "capture_stdout", MpvNode.FromFlag(captureStdout) },
            { "capture_stderr", MpvNode.FromFlag(captureStderr) },
            { "playback_only", MpvNode.FromFlag(playbackOnly) },
            { "detach", MpvNode.FromFlag(detach) },
            { "passthrough_stdin", MpvNode.FromFlag(passthroughStdin) }
        };

        if (captureSize.HasValue)
        {
            commandArguments["capture_size"] = MpvNode.FromInt64(captureSize.Value);
        }

        if (environment != null)
        {
            List<MpvNode> environmentNodes = new List<MpvNode>(environment.Count);
            for (int i = 0; i < environment.Count; i++)
            {
                environmentNodes.Add(MpvNode.FromString(environment[i]));
            }

            commandArguments["env"] = MpvNode.FromArray(environmentNodes);
        }

        if (stdinData != null)
        {
            commandArguments["stdin_data"] = MpvNode.FromString(stdinData);
        }

        return CommandNamed("subprocess", commandArguments);
    }

    /// <summary>
    /// 套用或還原 mpv profile。
    /// </summary>
    /// <param name="profileName">profile 名稱。</param>
    /// <param name="mode">profile 套用模式。</param>
    public void ApplyProfile(string profileName, MpvApplyProfileMode mode = MpvApplyProfileMode.Apply)
    {
        Command("apply-profile", profileName, mode == MpvApplyProfileMode.Restore ? "restore" : "apply");
    }

    /// <summary>
    /// 寫入目前播放項目的稍後觀看設定。
    /// </summary>
    public void WriteWatchLaterConfig()
    {
        Command("write-watch-later-config");
    }

    /// <summary>
    /// 刪除稍後觀看設定。
    /// </summary>
    /// <param name="fileName">要刪除設定的檔案名稱；未指定時刪除目前播放項目對應設定。</param>
    public void DeleteWatchLaterConfig(string? fileName = null)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            Command("delete-watch-later-config");
            return;
        }

        Command("delete-watch-later-config", fileName!);
    }

    /// <summary>
    /// 以稍後觀看模式結束播放器。
    /// </summary>
    /// <param name="exitCode">處理序結束碼；未指定時使用 mpv 預設值。</param>
    public void QuitWatchLater(int? exitCode = null)
    {
        if (exitCode.HasValue)
        {
            Command("quit-watch-later", exitCode.Value.ToString(CultureInfo.InvariantCulture));
            return;
        }

        Command("quit-watch-later");
    }

    /// <summary>
    /// 要求 mpv 結束。
    /// </summary>
    /// <param name="exitCode">處理序結束碼；未指定時使用 mpv 預設值。</param>
    public void Quit(int? exitCode = null)
    {
        if (exitCode.HasValue)
        {
            Command("quit", exitCode.Value.ToString(CultureInfo.InvariantCulture));
            return;
        }

        Command("quit");
    }

    /// <summary>
    /// 丟棄音訊、視訊與 demuxer 緩衝區。
    /// </summary>
    public void DropBuffers()
    {
        Command("drop-buffers");
    }

    /// <summary>
    /// 將 demuxer 快取區間傾印到檔案。
    /// </summary>
    /// <param name="start">起始秒數。</param>
    /// <param name="end">結束秒數；傳入 <see langword="null"/> 表示持續傾印。</param>
    /// <param name="fileName">輸出檔案名稱；空字串會停止目前傾印。</param>
    public void DumpCache(double start, double? end, string fileName)
    {
        Command(
            "dump-cache",
            start.ToString(CultureInfo.InvariantCulture),
            end.HasValue ? end.Value.ToString(CultureInfo.InvariantCulture) : "no",
            fileName);
    }

    /// <summary>
    /// 停止目前快取傾印。
    /// </summary>
    public void StopDumpCache()
    {
        Command("dump-cache", "0", "0", string.Empty);
    }

    /// <summary>
    /// 循環設定 A-B 重複播放點。
    /// </summary>
    public void CycleAbLoop()
    {
        Command("ab-loop");
    }

    /// <summary>
    /// 設定 A-B 重複播放區間。
    /// </summary>
    /// <param name="startSeconds">A 點秒數；未指定時清除 A 點。</param>
    /// <param name="endSeconds">B 點秒數；未指定時清除 B 點。</param>
    public void SetAbLoop(double? startSeconds, double? endSeconds)
    {
        SetPropertyString("ab-loop-a", startSeconds.HasValue ? startSeconds.Value.ToString(CultureInfo.InvariantCulture) : "no");
        SetPropertyString("ab-loop-b", endSeconds.HasValue ? endSeconds.Value.ToString(CultureInfo.InvariantCulture) : "no");
    }

    /// <summary>
    /// 清除 A-B 重複播放區間。
    /// </summary>
    public void ClearAbLoop()
    {
        SetAbLoop(null, null);
    }

    /// <summary>
    /// 依目前 A-B 重複播放點傾印快取。
    /// </summary>
    /// <param name="fileName">輸出檔案名稱。</param>
    public void DumpAbLoopCache(string fileName)
    {
        Command("ab-loop-dump-cache", fileName);
    }

    /// <summary>
    /// 將 A-B 重複播放點對齊到快取範圍。
    /// </summary>
    public void AlignAbLoopCache()
    {
        Command("ab-loop-align-cache");
    }

    /// <summary>
    /// 開始視訊輸出視窗拖曳。
    /// </summary>
    public void BeginVideoOutputDragging()
    {
        Command("begin-vo-dragging");
    }

    /// <summary>
    /// 顯示 mpv 視訊視窗內容功能表。
    /// </summary>
    public void ShowContextMenu()
    {
        Command("context-menu");
    }

    /// <summary>
    /// 訂閱播放軌清單變更通知。
    /// </summary>
    /// <returns>可用於取消觀察的要求識別碼。</returns>
    public ulong ObserveTrackList()
    {
        return ObserveProperty("track-list", MpvFormat.Node);
    }

    /// <summary>
    /// 訂閱指定 libmpv 屬性的變更通知。
    /// </summary>
    /// <param name="name">要觀察的 mpv 屬性名稱。</param>
    /// <param name="format">屬性值要使用的 libmpv 資料格式。</param>
    /// <returns>可用於取消觀察的要求識別碼。</returns>
    public ulong ObserveProperty(string name, MpvFormat format)
    {
        EnsureNotDisposed();
        ulong requestId = NextRequestId();
        using (Utf8String propertyName = new Utf8String(name))
        {
            InvokeNative(handle => MpvError.ThrowIfError(MpvNative.mpv_observe_property(handle, requestId, propertyName.Pointer, format)));
        }

        return requestId;
    }

    /// <summary>
    /// 取消指定 libmpv 屬性的變更通知。
    /// </summary>
    /// <param name="observeId">先前由 <see cref="ObserveProperty"/> 傳回的觀察識別碼。</param>
    public void UnobserveProperty(ulong observeId)
    {
        EnsureNotDisposed();
        InvokeNative(handle => MpvError.ThrowIfError(MpvNative.mpv_unobserve_property(handle, observeId)));
    }

    /// <summary>
    /// 同步觀察屬性快取的鎖。
    /// </summary>
    private readonly object _propertyObservablesGate = new object();
    /// <summary>
    /// 已為此播放器建立的屬性觀察快照；以屬性名稱與格式作為索引鍵共用 libmpv 註冊。
    /// </summary>
    private readonly Dictionary<(string Name, MpvFormat Format), object> _propertyObservables =
        new Dictionary<(string Name, MpvFormat Format), object>();

    /// <summary>
    /// 取得指定 libmpv 屬性的 <see cref="IObservable{T}"/>，多個訂閱者共享單一觀察註冊。
    /// </summary>
    /// <typeparam name="T">屬性值型別；支援 <see cref="double"/>、<see cref="long"/>、<see cref="bool"/>、<see cref="string"/> 與 <see cref="MpvNode"/>。</typeparam>
    /// <param name="propertyName">要觀察的屬性名稱。</param>
    /// <returns>會於每次屬性變更時呼叫 <see cref="IObserver{T}.OnNext"/> 的 <see cref="IObservable{T}"/>。</returns>
    public IObservable<T> WatchProperty<T>(string propertyName)
    {
        EnsureNotDisposed();
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            throw new ArgumentException("屬性名稱不可為空白。", nameof(propertyName));
        }

        MpvFormat format = MpvPropertyFormatResolver.Resolve<T>();
        (string Name, MpvFormat Format) key = (propertyName, format);
        lock (_propertyObservablesGate)
        {
            if (_propertyObservables.TryGetValue(key, out object? existing) && existing is MpvPropertyObservable<T> reused)
            {
                return reused;
            }

            MpvPropertyObservable<T> created = new MpvPropertyObservable<T>(this, propertyName, format);
            _propertyObservables[key] = created;
            return created;
        }
    }

    /// <summary>
    /// 啟用或停用指定的 libmpv 事件。
    /// </summary>
    /// <param name="eventId">要設定的 libmpv 事件識別碼。</param>
    /// <param name="enable">啟用事件時為 <see langword="true"/>。</param>
    public void RequestEvent(MpvEventId eventId, bool enable)
    {
        EnsureNotDisposed();
        InvokeNative(handle => MpvError.ThrowIfError(MpvNative.mpv_request_event(handle, eventId, enable ? 1 : 0)));
    }

    /// <summary>
    /// 訂閱 libmpv 記錄訊息。
    /// </summary>
    /// <param name="minLevel">要接收的最低 mpv 記錄等級文字。</param>
    public void RequestLogMessages(string minLevel)
    {
        EnsureNotDisposed();
        using (Utf8String level = new Utf8String(minLevel))
        {
            InvokeNative(handle => MpvError.ThrowIfError(MpvNative.mpv_request_log_messages(handle, level.Pointer)));
        }
    }

    /// <summary>
    /// 新增 libmpv 掛鉤。
    /// </summary>
    /// <param name="name">要新增的 libmpv 掛鉤名稱。</param>
    /// <param name="priority">掛鉤執行優先順序。</param>
    /// <param name="replyUserData">掛鉤回覆事件中使用的使用者資料。</param>
    public void AddHook(string name, int priority = 0, ulong replyUserData = 0)
    {
        EnsureNotDisposed();
        using (Utf8String hookName = new Utf8String(name))
        {
            InvokeNative(handle => MpvError.ThrowIfError(MpvNative.mpv_hook_add(handle, replyUserData, hookName.Pointer, priority)));
        }
    }

    /// <summary>
    /// 繼續指定的 libmpv 掛鉤。
    /// </summary>
    /// <param name="hookId">libmpv 掛鉤識別碼。</param>
    public void ContinueHook(ulong hookId)
    {
        EnsureNotDisposed();
        InvokeNative(handle => MpvError.ThrowIfError(MpvNative.mpv_hook_continue(handle, hookId)));
    }

    /// <summary>
    /// 註冊 libmpv 自訂唯讀串流通訊協定。
    /// </summary>
    /// <param name="protocol">不含 <c>://</c> 的通訊協定前置詞。</param>
    /// <param name="openStream">依 URI 建立可讀取串流的委派；傳回 <see langword="null"/> 表示拒絕開啟。</param>
    public void RegisterStreamProtocol(string protocol, Func<string, Stream?> openStream)
    {
        lock (_lifetimeGate)
        {
            EnsureNotDisposed();
            MpvStreamProtocolRegistration registration = MpvStreamProtocolRegistration.Register(this, protocol, openStream);
            _streamRegistrations.Add(registration);
        }
    }

    /// <summary>
    /// 註冊 libmpv 自訂唯讀串流通訊協定。
    /// </summary>
    /// <param name="protocol">不含 <c>://</c> 的通訊協定前置詞。</param>
    /// <param name="openStream">可透過事件資料提供串流的事件處理常式。</param>
    public void RegisterStreamProtocol(string protocol, EventHandler<MpvStreamOpenEventArgs> openStream)
    {
        if (openStream == null)
        {
            throw new ArgumentNullException(nameof(openStream));
        }

        RegisterStreamProtocol(protocol, uri =>
        {
            MpvStreamOpenEventArgs args = new MpvStreamOpenEventArgs(uri);
            openStream(this, args);
            return args.Stream;
        });
    }

    /// <summary>
    /// 建立與此播放器關聯的 OpenGL render API 內容。
    /// </summary>
    /// <param name="options">建立 OpenGL render API 內容所需的選項。</param>
    /// <returns>可用來以 OpenGL 繪製 mpv 影格的 render API 內容。</returns>
    public MpvOpenGlRenderContext CreateOpenGlRenderContext(MpvOpenGlRenderContextOptions options)
    {
        EnsureNotDisposed();
        MpvOpenGlRenderContext context;
        lock (_lifetimeGate)
        {
            EnsureNotDisposed();
            context = MpvOpenGlRenderContext.Create(this, options);
            RegisterRenderContext(context);
        }

        return context;
    }

    /// <summary>
    /// 建立與此播放器關聯的 software render API 內容。
    /// </summary>
    /// <returns>可用來將 mpv 影格繪製到記憶體像素緩衝區的 render API 內容。</returns>
    public MpvSoftwareRenderContext CreateSoftwareRenderContext()
    {
        EnsureNotDisposed();
        MpvSoftwareRenderContext context;
        lock (_lifetimeGate)
        {
            EnsureNotDisposed();
            context = MpvSoftwareRenderContext.Create(this);
            RegisterRenderContext(context);
        }

        return context;
    }

    /// <summary>
    /// 使用短期租用的 libmpv 控制代碼建立 render API 內容。
    /// </summary>
    /// <param name="parameters">傳給 libmpv render API 的建立參數。</param>
    /// <returns>新建立的 render API 內容指標。</returns>
    internal IntPtr CreateRenderContext(MpvRenderParam[] parameters)
    {
        if (parameters == null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        using (NativeHandleLease handleLease = AcquireNativeHandle())
        {
            IntPtr context;
            MpvError.ThrowIfError(MpvNative.mpv_render_context_create(out context, handleLease.Handle, parameters));
            return context;
        }
    }

    /// <summary>
    /// 使用短期租用的 libmpv 控制代碼註冊唯讀串流通訊協定。
    /// </summary>
    /// <param name="protocolName">通訊協定名稱的 UTF-8 原生指標。</param>
    /// <param name="userData">傳給 libmpv 回呼的使用者資料。</param>
    /// <param name="openCallback">開啟串流時使用的原生回呼委派。</param>
    internal void RegisterStreamProtocolCore(IntPtr protocolName, IntPtr userData, MpvStreamOpenCallback openCallback)
    {
        using (NativeHandleLease handleLease = AcquireNativeHandle())
        {
            MpvError.ThrowIfError(MpvNative.mpv_stream_cb_add_ro(handleLease.Handle, protocolName, userData, openCallback));
        }
    }

    /// <summary>
    /// 非同步釋放 libmpv 用戶端，先嘗試讓 libmpv 完成 quit 並等候 Shutdown 事件後再同步釋放資源。
    /// </summary>
    /// <returns>代表非同步釋放流程的 <see cref="ValueTask"/>。</returns>
    public async ValueTask DisposeAsync()
    {
        if (Volatile.Read(ref _disposed))
        {
            return;
        }

        await ShutdownAsync(TimeSpan.FromSeconds(2), CancellationToken.None).ConfigureAwait(false);
        Dispose();
    }

    /// <summary>
    /// 嘗試讓 libmpv 完成播放並接收 Shutdown 事件，提供同步 <see cref="Dispose"/> 前的 graceful 收尾。
    /// </summary>
    /// <param name="timeout">等待 Shutdown 事件的逾時時間；<see langword="null"/> 時使用 2 秒。</param>
    /// <param name="cancellationToken">取消等候的 token。</param>
    /// <returns>代表 graceful shutdown 等候流程的工作。</returns>
    public async Task ShutdownAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _disposed))
        {
            return;
        }

        if (!Volatile.Read(ref _initialized))
        {
            return;
        }

        TaskCompletionSource<bool> shutdownSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<MpvEventArgs> handler = delegate (object? sender, MpvEventArgs e)
        {
            shutdownSignal.TrySetResult(true);
        };

        Shutdown += handler;
        try
        {
            try
            {
                Command("quit");
            }
            catch (MpvException)
            {
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            TimeSpan waitFor = timeout ?? TimeSpan.FromSeconds(2);
            using (CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                if (waitFor > TimeSpan.Zero)
                {
                    linked.CancelAfter(waitFor);
                }

                using (linked.Token.Register(static state => ((TaskCompletionSource<bool>)state!).TrySetResult(false), shutdownSignal))
                {
                    await shutdownSignal.Task.ConfigureAwait(false);
                }
            }
        }
        finally
        {
            Shutdown -= handler;
        }
    }

    /// <summary>
    /// 釋放 libmpv 用戶端與事件執行緒。
    /// </summary>
    public void Dispose()
    {
        NativeHandleLease? wakeupHandle = null;
        lock (_lifetimeGate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _eventLoopStopping = true;
            if (!_handle.IsInvalid && !_handle.IsClosed)
            {
                wakeupHandle = new NativeHandleLease(_handle);
            }
        }

        DisposeRenderContexts();

        try
        {
            if (wakeupHandle != null)
            {
                MpvNative.mpv_set_wakeup_callback(wakeupHandle.Handle, null, IntPtr.Zero);
                MpvNative.mpv_wakeup(wakeupHandle.Handle);
            }
        }
        finally
        {
            wakeupHandle?.Dispose();
        }

        if (_eventThread != null && _eventThread.IsAlive && Thread.CurrentThread.ManagedThreadId != _eventThread.ManagedThreadId)
        {
            _eventThread.Join(TimeSpan.FromSeconds(2));
        }

        CompletePendingRequestsAsDisposed();
        _handle.Dispose();

        for (int i = 0; i < _streamRegistrations.Count; i++)
        {
            _streamRegistrations[i].Dispose();
        }

        _streamRegistrations.Clear();
        _wakeupAction = null;
        _wakeupCallback = null;
        if (_loggerHandler != null)
        {
            LogMessageReceived -= _loggerHandler;
            _loggerHandler = null;
        }

        _logger = null;
        CompletePropertyObservables();
    }

    /// <summary>
    /// 對所有透過 <see cref="WatchProperty{T}"/> 建立的觀察送出 <see cref="IObserver{T}.OnCompleted"/>。
    /// </summary>
    private void CompletePropertyObservables()
    {
        List<IMpvPropertyObservableCompletion> snapshot;
        lock (_propertyObservablesGate)
        {
            snapshot = new List<IMpvPropertyObservableCompletion>(_propertyObservables.Count);
            foreach (KeyValuePair<(string Name, MpvFormat Format), object> pair in _propertyObservables)
            {
                if (pair.Value is IMpvPropertyObservableCompletion completion)
                {
                    snapshot.Add(completion);
                }
            }

            _propertyObservables.Clear();
        }

        foreach (IMpvPropertyObservableCompletion completion in snapshot)
        {
            try
            {
                completion.Complete();
            }
            catch
            {
                // 單一 observable 失敗不可中斷其他 observable。
            }
        }
    }

    /// <summary>
    /// 登錄由此播放器建立的 render API 內容，讓播放器釋放時可先釋放子資源。
    /// </summary>
    /// <param name="context">要登錄的 render API 內容。</param>
    private void RegisterRenderContext(IDisposable context)
    {
        lock (_renderContextsGate)
        {
            _renderContexts.Add(context);
        }
    }

    /// <summary>
    /// 釋放所有由此播放器建立且仍被播放器追蹤的 render API 內容。
    /// </summary>
    private void DisposeRenderContexts()
    {
        List<IDisposable> contexts;
        lock (_renderContextsGate)
        {
            contexts = new List<IDisposable>(_renderContexts);
            _renderContexts.Clear();
        }

        for (int i = 0; i < contexts.Count; i++)
        {
            contexts[i].Dispose();
        }
    }

    /// <summary>
    /// 將尚未收到 libmpv 回覆的非同步要求標示為播放器已釋放。
    /// </summary>
    private void CompletePendingRequestsAsDisposed()
    {
        ObjectDisposedException exception = new ObjectDisposedException(GetType().FullName);
        foreach (KeyValuePair<ulong, TaskCompletionSource<MpvNode>> request in _pendingRequests)
        {
            TaskCompletionSource<MpvNode>? completion;
            if (_pendingRequests.TryRemove(request.Key, out completion))
            {
                completion.TrySetException(exception);
            }
        }
    }

    /// <summary>
    /// 安全分派受控事件，避免使用者事件處理常式中止 libmpv 事件迴圈。
    /// </summary>
    /// <typeparam name="TEventArgs">事件資料型別。</typeparam>
    /// <param name="eventHandler">要分派的事件處理常式。</param>
    /// <param name="args">要傳給事件處理常式的事件資料。</param>
    /// <param name="eventName">事件名稱。</param>
    private void DispatchManagedEvent<TEventArgs>(EventHandler<TEventArgs>? eventHandler, TEventArgs args, string eventName)
        where TEventArgs : EventArgs
    {
        if (eventHandler == null)
        {
            return;
        }

        Delegate[] handlers = eventHandler.GetInvocationList();
        for (int i = 0; i < handlers.Length; i++)
        {
            EventHandler<TEventArgs> handler = (EventHandler<TEventArgs>)handlers[i];
            try
            {
                handler(this, args);
            }
            catch (Exception exception)
            {
                DispatchEventHandlerException(eventName, exception);
            }
        }
    }

    /// <summary>
    /// 分派受控事件處理常式例外狀況通知。
    /// </summary>
    /// <param name="eventName">發生例外狀況的事件名稱。</param>
    /// <param name="exception">事件處理常式擲回的例外狀況。</param>
    private void DispatchEventHandlerException(string eventName, Exception exception)
    {
        EventHandler<MpvEventDispatchExceptionEventArgs>? eventHandler = EventDispatchException;
        if (eventHandler == null)
        {
            return;
        }

        MpvEventDispatchExceptionEventArgs args = new MpvEventDispatchExceptionEventArgs(eventName, exception);
        Delegate[] handlers = eventHandler.GetInvocationList();
        for (int i = 0; i < handlers.Length; i++)
        {
            EventHandler<MpvEventDispatchExceptionEventArgs> handler = (EventHandler<MpvEventDispatchExceptionEventArgs>)handlers[i];
            try
            {
                handler(this, args);
            }
            catch
            {
            }
        }
    }

    /// <summary>
    /// 在安全租用 libmpv 控制代碼期間執行原生作業。
    /// </summary>
    /// <param name="action">要執行的原生作業。</param>
    private void InvokeNative(Action<IntPtr> action)
    {
        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        using (NativeHandleLease handleLease = AcquireNativeHandle())
        {
            action(handleLease.Handle);
        }
    }

    /// <summary>
    /// 在安全租用 libmpv 控制代碼期間執行原生作業並傳回結果。
    /// </summary>
    /// <typeparam name="TResult">原生作業回傳值型別。</typeparam>
    /// <param name="action">要執行的原生作業。</param>
    /// <returns>原生作業回傳值。</returns>
    private TResult InvokeNative<TResult>(Func<IntPtr, TResult> action)
    {
        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        using (NativeHandleLease handleLease = AcquireNativeHandle())
        {
            return action(handleLease.Handle);
        }
    }

    /// <summary>
    /// 取得目前 libmpv 控制代碼的短期租用。
    /// </summary>
    /// <returns>目前 libmpv 控制代碼的短期租用。</returns>
    private NativeHandleLease AcquireNativeHandle()
    {
        NativeHandleLease handleLease;
        if (!TryAcquireNativeHandle(out handleLease))
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        return handleLease;
    }

    /// <summary>
    /// 嘗試取得目前 libmpv 控制代碼的短期租用。
    /// </summary>
    /// <param name="handleLease">成功時接收控制代碼租用。</param>
    /// <returns>成功取得控制代碼租用時為 <see langword="true"/>。</returns>
    private bool TryAcquireNativeHandle(out NativeHandleLease handleLease)
    {
        lock (_lifetimeGate)
        {
            if (_disposed || _handle.IsInvalid || _handle.IsClosed)
            {
                handleLease = null!;
                return false;
            }

            handleLease = new NativeHandleLease(_handle);
            return true;
        }
    }

    /// <summary>
    /// 在原生呼叫期間保存 <see cref="SafeMpvHandle"/> 的短期參考。
    /// </summary>
    private sealed class NativeHandleLease : IDisposable
    {
        /// <summary>
        /// 保存被租用的安全控制代碼。
        /// </summary>
        private readonly SafeMpvHandle _handle;

        /// <summary>
        /// 表示是否已成功增加安全控制代碼參考計數。
        /// </summary>
        private bool _addedReference;

        /// <summary>
        /// 初始化 <see cref="NativeHandleLease"/> 類別的新執行個體。
        /// </summary>
        /// <param name="handle">要租用的安全控制代碼。</param>
        public NativeHandleLease(SafeMpvHandle handle)
        {
            _handle = handle;
            _handle.DangerousAddRef(ref _addedReference);
            Handle = _handle.DangerousGetHandle();
        }

        /// <summary>
        /// 取得租用期間可使用的原生控制代碼。
        /// </summary>
        /// <value>libmpv 原生控制代碼。</value>
        public IntPtr Handle { get; private set; }

        /// <summary>
        /// 釋放安全控制代碼租用。
        /// </summary>
        public void Dispose()
        {
            if (!_addedReference)
            {
                return;
            }

            _addedReference = false;
            Handle = IntPtr.Zero;
            _handle.DangerousRelease();
        }
    }

    /// <summary>
    /// 建立 libmpv 具名命令節點。
    /// </summary>
    /// <param name="name">要執行的 mpv 命令名稱。</param>
    /// <param name="arguments">命令具名引數。</param>
    /// <returns>可傳給 <see cref="CommandNode"/> 的命令節點。</returns>
    private static MpvNode CreateNamedCommandNode(string name, IDictionary<string, MpvNode> arguments)
    {
        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        if (arguments == null)
        {
            throw new ArgumentNullException(nameof(arguments));
        }

        Dictionary<string, MpvNode> command = new Dictionary<string, MpvNode>(arguments, StringComparer.Ordinal)
        {
            ["_name"] = MpvNode.FromString(name)
        };
        return MpvNode.FromMap(command);
    }

    /// <summary>
    /// 從播放軌節點讀取播放軌清單。
    /// </summary>
    /// <param name="node">mpv 播放軌清單節點。</param>
    /// <returns>播放軌資訊集合。</returns>
    private static IReadOnlyList<MpvTrackInfo> ReadTracks(MpvNode node)
    {
        IReadOnlyList<MpvNode> nodes = node.AsArray();
        List<MpvTrackInfo> tracks = new List<MpvTrackInfo>(nodes.Count);
        for (int i = 0; i < nodes.Count; i++)
        {
            tracks.Add(MpvTrackInfo.FromNode(nodes[i]));
        }

        return new ReadOnlyCollection<MpvTrackInfo>(tracks);
    }

    /// <summary>
    /// 將逗號分隔的 mpv 清單屬性轉為唯讀集合。
    /// </summary>
    /// <param name="value">逗號分隔的清單屬性值。</param>
    /// <returns>清單項目集合。</returns>
    private static IReadOnlyList<string> SplitCommaList(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return Array.Empty<string>();
        }

        string[] parts = value!.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        List<string> items = new List<string>(parts.Length);
        for (int i = 0; i < parts.Length; i++)
        {
            items.Add(parts[i]);
        }

        return new ReadOnlyCollection<string>(items);
    }

    /// <summary>
    /// 建立外部播放軌載入命令引數。
    /// </summary>
    /// <param name="command">mpv 外部播放軌載入命令名稱。</param>
    /// <param name="url">播放軌檔案路徑或網路網址。</param>
    /// <param name="mode">播放軌載入後的選取模式。</param>
    /// <param name="flags">播放軌載入旗標。</param>
    /// <param name="title">播放軌標題。</param>
    /// <param name="language">播放軌語言代碼。</param>
    /// <param name="albumArt">是否將視訊當作專輯封面載入。</param>
    /// <returns>可傳給 <see cref="Command(string[])"/> 的命令引數。</returns>
    private static string[] BuildExternalTrackArguments(string command, string url, MpvTrackLoadMode mode, MpvTrackLoadFlags flags, string? title, string? language, bool? albumArt)
    {
        List<string> arguments = new List<string>
        {
            command,
            url,
            ToTrackLoadFlagsText(mode, flags)
        };

        if (title != null || language != null || albumArt.HasValue)
        {
            arguments.Add(title ?? string.Empty);
        }

        if (language != null || albumArt.HasValue)
        {
            arguments.Add(language ?? string.Empty);
        }

        if (albumArt.HasValue)
        {
            arguments.Add(albumArt.Value ? "yes" : "no");
        }

        return arguments.ToArray();
    }

    /// <summary>
    /// 執行可省略播放軌識別碼的 mpv 命令。
    /// </summary>
    /// <param name="command">mpv 命令名稱。</param>
    /// <param name="trackId">播放軌識別碼；未指定時不傳遞識別碼。</param>
    private void CommandOptionalTrackId(string command, long? trackId)
    {
        if (trackId.HasValue)
        {
            Command(command, trackId.Value.ToString(CultureInfo.InvariantCulture));
            return;
        }

        Command(command);
    }

    /// <summary>
    /// 設定播放軌選取屬性。
    /// </summary>
    /// <param name="propertyName">mpv 選軌屬性名稱。</param>
    /// <param name="trackId">要選取的播放軌識別碼；未指定時設定為 <c>no</c>。</param>
    private void SetTrackSelection(string propertyName, long? trackId)
    {
        SetPropertyString(propertyName, trackId.HasValue ? trackId.Value.ToString(CultureInfo.InvariantCulture) : "no");
    }

    /// <summary>
    /// 將外部播放軌載入模式與旗標轉為 mpv 文字。
    /// </summary>
    /// <param name="mode">播放軌載入後的選取模式。</param>
    /// <param name="flags">播放軌載入旗標。</param>
    /// <returns>mpv 外部播放軌旗標文字。</returns>
    private static string ToTrackLoadFlagsText(MpvTrackLoadMode mode, MpvTrackLoadFlags flags)
    {
        List<string> parts = new List<string>();
        switch (mode)
        {
            case MpvTrackLoadMode.Auto:
                parts.Add("auto");
                break;
            case MpvTrackLoadMode.Cached:
                parts.Add("cached");
                break;
            default:
                parts.Add("select");
                break;
        }

        if ((flags & MpvTrackLoadFlags.HearingImpaired) != 0)
        {
            parts.Add("hearing-impaired");
        }

        if ((flags & MpvTrackLoadFlags.VisualImpaired) != 0)
        {
            parts.Add("visual-impaired");
        }

        if ((flags & MpvTrackLoadFlags.Forced) != 0)
        {
            parts.Add("forced");
        }

        if ((flags & MpvTrackLoadFlags.Default) != 0)
        {
            parts.Add("default");
        }

        if ((flags & MpvTrackLoadFlags.AttachedPicture) != 0)
        {
            parts.Add("attached-picture");
        }

        return string.Join("+", parts);
    }

    /// <summary>
    /// 將字幕步進目標轉為 mpv 文字。
    /// </summary>
    /// <param name="target">字幕步進目標。</param>
    /// <returns>mpv 字幕步進目標文字。</returns>
    private static string ToSubtitleStepTargetText(MpvSubtitleStepTarget target)
    {
        return target == MpvSubtitleStepTarget.Secondary ? "secondary" : "primary";
    }

    /// <summary>
    /// 將檔案載入模式轉為 mpv 文字。
    /// </summary>
    /// <param name="mode">檔案載入模式。</param>
    /// <returns>mpv 檔案載入模式文字。</returns>
    private static string ToLoadFileModeText(MpvLoadFileMode mode)
    {
        switch (mode)
        {
            case MpvLoadFileMode.Append:
                return "append";
            case MpvLoadFileMode.AppendPlay:
                return "append-play";
            case MpvLoadFileMode.InsertNext:
                return "insert-next";
            case MpvLoadFileMode.InsertNextPlay:
                return "insert-next-play";
            case MpvLoadFileMode.InsertAt:
                return "insert-at";
            case MpvLoadFileMode.InsertAtPlay:
                return "insert-at-play";
            default:
                return "replace";
        }
    }

    /// <summary>
    /// 將播放清單載入模式轉為 mpv 文字。
    /// </summary>
    /// <param name="mode">播放清單載入模式。</param>
    /// <returns>mpv 播放清單載入模式文字。</returns>
    private static string ToLoadListModeText(MpvLoadListMode mode)
    {
        switch (mode)
        {
            case MpvLoadListMode.Append:
                return "append";
            case MpvLoadListMode.AppendPlay:
                return "append-play";
            case MpvLoadListMode.InsertNext:
                return "insert-next";
            case MpvLoadListMode.InsertNextPlay:
                return "insert-next-play";
            case MpvLoadListMode.InsertAt:
                return "insert-at";
            case MpvLoadListMode.InsertAtPlay:
                return "insert-at-play";
            default:
                return "replace";
        }
    }

    /// <summary>
    /// 將截圖模式轉為 mpv 文字。
    /// </summary>
    /// <param name="mode">截圖內容模式。</param>
    /// <returns>mpv 截圖模式文字。</returns>
    private static string ToScreenshotModeText(MpvScreenshotMode mode)
    {
        switch (mode)
        {
            case MpvScreenshotMode.Video:
                return "video";
            case MpvScreenshotMode.Window:
                return "window";
            default:
                return "subtitles";
        }
    }

    /// <summary>
    /// 將截圖模式與進階旗標轉為 mpv 命令引數文字。
    /// </summary>
    /// <param name="mode">截圖內容模式。</param>
    /// <param name="flags">截圖進階旗標。</param>
    /// <returns>mpv 截圖命令引數文字。</returns>
    private static string ToScreenshotArgumentText(MpvScreenshotMode mode, MpvScreenshotFlags flags)
    {
        List<string> parts = new List<string>
        {
            ToScreenshotModeText(mode)
        };

        if ((flags & MpvScreenshotFlags.Osd) != 0)
        {
            parts.Add("osd");
        }

        if ((flags & MpvScreenshotFlags.Scaled) != 0)
        {
            parts.Add("scaled");
        }

        if ((flags & MpvScreenshotFlags.EachFrame) != 0)
        {
            parts.Add("each-frame");
        }

        return string.Join("+", parts);
    }

    /// <summary>
    /// 將截圖像素格式轉為 mpv 文字。
    /// </summary>
    /// <param name="format">截圖像素格式。</param>
    /// <returns>mpv 截圖像素格式文字。</returns>
    private static string ToScreenshotFormatText(MpvScreenshotFormat format)
    {
        switch (format)
        {
            case MpvScreenshotFormat.Bgra:
                return "bgra";
            case MpvScreenshotFormat.Rgba:
                return "rgba";
            case MpvScreenshotFormat.Rgba64:
                return "rgba64";
            default:
                return "bgr0";
        }
    }

    /// <summary>
    /// 將清單變更動作轉為 mpv 文字。
    /// </summary>
    /// <param name="operation">清單變更動作。</param>
    /// <returns>mpv 清單變更動作文字。</returns>
    private static string ToListChangeOperationText(MpvListChangeOperation operation)
    {
        switch (operation)
        {
            case MpvListChangeOperation.Add:
                return "add";
            case MpvListChangeOperation.Append:
                return "append";
            case MpvListChangeOperation.Remove:
                return "remove";
            case MpvListChangeOperation.Clear:
                return "clr";
            default:
                return "set";
        }
    }

    /// <summary>
    /// 將輸入 section 旗標轉為 mpv 文字。
    /// </summary>
    /// <param name="flags">輸入 section 旗標。</param>
    /// <returns>mpv 輸入 section 旗標文字。</returns>
    private static string ToInputSectionFlagsText(MpvInputSectionFlags flags)
    {
        List<string> parts = new List<string>();
        if ((flags & MpvInputSectionFlags.Exclusive) != 0)
        {
            parts.Add("exclusive");
        }

        if ((flags & MpvInputSectionFlags.AllowHideCursor) != 0)
        {
            parts.Add("allow-hide-cursor");
        }

        if ((flags & MpvInputSectionFlags.AllowVoDragging) != 0)
        {
            parts.Add("allow-vo-dragging");
        }

        return parts.Count == 0 ? "default" : string.Join("+", parts);
    }

    /// <summary>
    /// 非同步設定整數旗標格式的 libmpv 屬性。
    /// </summary>
    /// <param name="name">要設定的 mpv 屬性名稱。</param>
    /// <param name="value">要套用到屬性的旗標值參考。</param>
    /// <returns>代表 libmpv 設定屬性回覆的工作。</returns>
    private Task SetPropertyFlagAsyncCore(string name, ref int value)
    {
        ulong requestId = NextRequestId();
        TaskCompletionSource<MpvNode> completion = new TaskCompletionSource<MpvNode>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[requestId] = completion;

        using (Utf8String propertyName = new Utf8String(name))
        {
            int error;
            using (NativeHandleLease handleLease = AcquireNativeHandle())
            {
                error = MpvNative.mpv_set_property_async(handleLease.Handle, requestId, propertyName.Pointer, MpvFormat.Flag, ref value);
            }

            if (error < 0)
            {
                TaskCompletionSource<MpvNode>? removed;
                _pendingRequests.TryRemove(requestId, out removed);
                completion.TrySetException(new MpvException(error));
            }
        }

        return completion.Task;
    }

    /// <summary>
    /// 非同步設定 64 位元整數格式的 libmpv 屬性。
    /// </summary>
    /// <param name="name">要設定的 mpv 屬性名稱。</param>
    /// <param name="value">要套用到屬性的 64 位元整數值參考。</param>
    /// <returns>代表 libmpv 設定屬性回覆的工作。</returns>
    private Task SetPropertyInt64AsyncCore(string name, ref long value)
    {
        ulong requestId = NextRequestId();
        TaskCompletionSource<MpvNode> completion = new TaskCompletionSource<MpvNode>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[requestId] = completion;

        using (Utf8String propertyName = new Utf8String(name))
        {
            int error;
            using (NativeHandleLease handleLease = AcquireNativeHandle())
            {
                error = MpvNative.mpv_set_property_async(handleLease.Handle, requestId, propertyName.Pointer, MpvFormat.Int64, ref value);
            }

            if (error < 0)
            {
                TaskCompletionSource<MpvNode>? removed;
                _pendingRequests.TryRemove(requestId, out removed);
                completion.TrySetException(new MpvException(error));
            }
        }

        return completion.Task;
    }

    /// <summary>
    /// 非同步設定雙精確度浮點數格式的 libmpv 屬性。
    /// </summary>
    /// <param name="name">要設定的 mpv 屬性名稱。</param>
    /// <param name="value">要套用到屬性的雙精確度浮點數值參考。</param>
    /// <returns>代表 libmpv 設定屬性回覆的工作。</returns>
    private Task SetPropertyDoubleAsyncCore(string name, ref double value)
    {
        ulong requestId = NextRequestId();
        TaskCompletionSource<MpvNode> completion = new TaskCompletionSource<MpvNode>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[requestId] = completion;

        using (Utf8String propertyName = new Utf8String(name))
        {
            int error;
            using (NativeHandleLease handleLease = AcquireNativeHandle())
            {
                error = MpvNative.mpv_set_property_async(handleLease.Handle, requestId, propertyName.Pointer, MpvFormat.Double, ref value);
            }

            if (error < 0)
            {
                TaskCompletionSource<MpvNode>? removed;
                _pendingRequests.TryRemove(requestId, out removed);
                completion.TrySetException(new MpvException(error));
            }
        }

        return completion.Task;
    }

    /// <summary>
    /// 將建立選項套用到尚未初始化的 libmpv 用戶端。
    /// </summary>
    /// <param name="options">要套用的播放器建立選項。</param>
    private void ApplyInitialOptions(MpvPlayerOptions options)
    {
        if (options.LoadUserConfig || !string.IsNullOrWhiteSpace(options.ConfigDirectory))
        {
            SetOptionString("config", "yes");
            if (!string.IsNullOrWhiteSpace(options.ConfigDirectory))
            {
                SetOptionString("config-dir", options.ConfigDirectory!);
            }
        }

        SetOptionString("input-default-bindings", options.EnableDefaultInputBindings ? "yes" : "no");
        SetOptionalOptionString("input-vo-keyboard", options.EnableKeyboardInput ? "yes" : "no");
        if (!string.IsNullOrWhiteSpace(options.InputConfigFile))
        {
            SetOptionString("input-conf", options.InputConfigFile!);
        }

        if (options.LoadScripts.HasValue)
        {
            SetOptionString("load-scripts", options.LoadScripts.Value ? "yes" : "no");
        }

        if (options.ScriptFiles.Count > 0)
        {
            SetOptionString("scripts", string.Join(Path.PathSeparator.ToString(), options.ScriptFiles));
        }

        SetOptionString("osc", options.EnableOsc ? "yes" : "no");
        SetOptionString("ytdl", options.EnableYtdlp ? "yes" : "no");

        if (!string.IsNullOrWhiteSpace(options.ToolDirectory))
        {
            SetOptionalOptionString("working-directory", options.ToolDirectory!);
        }

        if (options.EnableYtdlp && !string.IsNullOrWhiteSpace(options.YtdlpPath))
        {
            SetOptionString("script-opts", "ytdl_hook-ytdl_path=" + options.YtdlpPath);
        }

        if (options.EnableYtdlp)
        {
            if (!string.IsNullOrWhiteSpace(options.YtdlpFormat))
            {
                SetYtdlpFormat(options.YtdlpFormat!);
            }
            else if (options.YtdlpFormatPreset != MpvYtdlpFormatPreset.Default)
            {
                SetYtdlpFormat(options.YtdlpFormatPreset);
            }
        }

        foreach (string configFile in options.ConfigFiles)
        {
            if (!string.IsNullOrWhiteSpace(configFile))
            {
                LoadConfigFile(configFile);
            }
        }

        foreach (KeyValuePair<string, string> option in options.InitialOptions)
        {
            SetOptionString(option.Key, option.Value);
        }

        if (!string.IsNullOrWhiteSpace(options.LogLevel))
        {
            RequestLogMessages(options.LogLevel);
        }
    }

    /// <summary>
    /// 設定可能不存在於目前 libmpv 建置的選項。
    /// </summary>
    /// <param name="name">要設定的 mpv 選項名稱。</param>
    /// <param name="value">要套用到選項的字串值。</param>
    private void SetOptionalOptionString(string name, string value)
    {
        using (Utf8String optionName = new Utf8String(name))
        using (Utf8String optionValue = new Utf8String(value))
        {
            int error = InvokeNative(handle => MpvNative.mpv_set_option_string(handle, optionName.Pointer, optionValue.Pointer));
            if (error == (int)MpvErrorCode.OptionNotFound)
            {
                return;
            }

            MpvError.ThrowIfError(error);
        }
    }

    /// <summary>
    /// 判斷例外狀況是否代表屬性不存在或目前無法使用。
    /// </summary>
    /// <param name="exception">要檢查的 mpv 例外狀況。</param>
    /// <returns>例外狀況代表屬性不存在或目前無法使用時為 <see langword="true"/>。</returns>
    private static bool IsUnavailablePropertyError(MpvException exception)
    {
        return exception.ErrorCode == (int)MpvErrorCode.PropertyNotFound ||
            exception.ErrorCode == (int)MpvErrorCode.PropertyUnavailable;
    }

    /// <summary>
    /// 啟動接收 libmpv 事件的背景執行緒。
    /// </summary>
    private void StartEventLoop()
    {
        _eventThread = new Thread(EventLoop);
        _eventThread.IsBackground = true;
        _eventThread.Name = "libmpv event loop";
        _eventThread.Start();
    }

    /// <summary>
    /// 持續等待並分派 libmpv 事件。
    /// </summary>
    private void EventLoop()
    {
        while (!_eventLoopStopping)
        {
            NativeHandleLease handleLease;
            if (!TryAcquireNativeHandle(out handleLease))
            {
                break;
            }

            IntPtr eventPointer;
            using (handleLease)
            {
                eventPointer = MpvNative.mpv_wait_event(handleLease.Handle, -1);
            }

            if (eventPointer == IntPtr.Zero)
            {
                continue;
            }

            MpvEvent nativeEvent = (MpvEvent)Marshal.PtrToStructure(eventPointer, typeof(MpvEvent))!;
            DispatchEvent(eventPointer, nativeEvent);

            if (nativeEvent.EventId == MpvEventId.Shutdown)
            {
                break;
            }
        }
    }

    /// <summary>
    /// 將單一 libmpv 原生事件轉換並分派給受控事件。
    /// </summary>
    /// <param name="eventPointer">libmpv 原生事件指標。</param>
    /// <param name="nativeEvent">libmpv 原生事件資料。</param>
    private void DispatchEvent(IntPtr eventPointer, MpvEvent nativeEvent)
    {
        MpvEventArgs args = new MpvEventArgs(nativeEvent.EventId, nativeEvent.Error, nativeEvent.ReplyUserData);
        DispatchManagedEvent(EventReceived, args, nameof(EventReceived));
        if (_disposed)
        {
            return;
        }

        DispatchNodeEvent(eventPointer, nativeEvent);

        switch (nativeEvent.EventId)
        {
            case MpvEventId.SetPropertyReply:
                CompletePendingRequest(nativeEvent, MpvNode.None());
                break;
            case MpvEventId.GetPropertyReply:
                CompletePendingProperty(nativeEvent);
                DispatchPropertyChange(nativeEvent);
                break;
            case MpvEventId.CommandReply:
                CompletePendingCommand(nativeEvent);
                break;
            case MpvEventId.PropertyChange:
                DispatchPropertyChange(nativeEvent);
                break;
            case MpvEventId.LogMessage:
                DispatchLogMessage(nativeEvent);
                break;
            case MpvEventId.StartFile:
                DispatchStartFile(nativeEvent);
                break;
            case MpvEventId.EndFile:
                DispatchEndFile(nativeEvent);
                break;
            case MpvEventId.ClientMessage:
                DispatchClientMessage(nativeEvent);
                break;
            case MpvEventId.Hook:
                DispatchHook(nativeEvent);
                break;
            case MpvEventId.FileLoaded:
                DispatchManagedEvent(FileLoaded, args, nameof(FileLoaded));
                break;
            case MpvEventId.Idle:
                DispatchManagedEvent(Idle, args, nameof(Idle));
                break;
            case MpvEventId.VideoReconfig:
                DispatchManagedEvent(VideoReconfigured, args, nameof(VideoReconfigured));
                break;
            case MpvEventId.AudioReconfig:
                DispatchManagedEvent(AudioReconfigured, args, nameof(AudioReconfigured));
                break;
            case MpvEventId.Seek:
                DispatchManagedEvent(SeekStarted, args, nameof(SeekStarted));
                break;
            case MpvEventId.PlaybackRestart:
                DispatchManagedEvent(PlaybackRestarted, args, nameof(PlaybackRestarted));
                break;
            case MpvEventId.QueueOverflow:
                DispatchManagedEvent(QueueOverflow, args, nameof(QueueOverflow));
                break;
            case MpvEventId.Shutdown:
                DispatchManagedEvent(Shutdown, args, nameof(Shutdown));
                break;
        }
    }

    /// <summary>
    /// 完成指定原生命令回覆對應的非同步工作。
    /// </summary>
    /// <param name="nativeEvent">libmpv 命令回覆事件資料。</param>
    private void CompletePendingCommand(MpvEvent nativeEvent)
    {
        MpvNode result = DecodeCommandResult(nativeEvent);
        DispatchManagedEvent(CommandReply, new MpvCommandReplyEventArgs(nativeEvent.Error, nativeEvent.ReplyUserData, result), nameof(CommandReply));
        CompletePendingRequest(nativeEvent, result);
    }

    /// <summary>
    /// 完成指定原生屬性回覆對應的非同步工作。
    /// </summary>
    /// <param name="nativeEvent">libmpv 屬性回覆事件資料。</param>
    private void CompletePendingProperty(MpvEvent nativeEvent)
    {
        MpvNode result = nativeEvent.Error < 0 ? MpvNode.None() : DecodePropertyNode(nativeEvent);
        CompletePendingRequest(nativeEvent, result);
    }

    /// <summary>
    /// 完成指定原生事件對應的非同步工作。
    /// </summary>
    /// <param name="nativeEvent">libmpv 回覆事件資料。</param>
    /// <param name="result">回覆事件的節點結果。</param>
    private void CompletePendingRequest(MpvEvent nativeEvent, MpvNode result)
    {
        TaskCompletionSource<MpvNode>? completion;
        if (!_pendingRequests.TryRemove(nativeEvent.ReplyUserData, out completion))
        {
            return;
        }

        if (nativeEvent.Error < 0)
        {
            completion.TrySetException(new MpvException(nativeEvent.Error));
        }
        else
        {
            completion.TrySetResult(result);
        }
    }

    /// <summary>
    /// 將命令回覆事件資料轉換為節點。
    /// </summary>
    /// <param name="nativeEvent">libmpv 命令回覆事件資料。</param>
    /// <returns>命令回傳節點；沒有資料時為空節點。</returns>
    private static MpvNode DecodeCommandResult(MpvEvent nativeEvent)
    {
        if (nativeEvent.Data == IntPtr.Zero)
        {
            return MpvNode.None();
        }

        MpvEventCommand command = (MpvEventCommand)Marshal.PtrToStructure(nativeEvent.Data, typeof(MpvEventCommand))!;
        return MpvNode.FromNative(command.Result);
    }

    /// <summary>
    /// 將屬性回覆事件資料轉換為節點。
    /// </summary>
    /// <param name="nativeEvent">libmpv 屬性回覆事件資料。</param>
    /// <returns>屬性回傳節點；沒有資料時為空節點。</returns>
    private static MpvNode DecodePropertyNode(MpvEvent nativeEvent)
    {
        if (nativeEvent.Data == IntPtr.Zero)
        {
            return MpvNode.None();
        }

        MpvEventProperty property = (MpvEventProperty)Marshal.PtrToStructure(nativeEvent.Data, typeof(MpvEventProperty))!;
        object? value = DecodePropertyValue(property);
        switch (property.Format)
        {
            case MpvFormat.String:
            case MpvFormat.OsdString:
                return MpvNode.FromString(value as string ?? string.Empty);
            case MpvFormat.Flag:
                return MpvNode.FromFlag(value is bool flag && flag);
            case MpvFormat.Int64:
                return MpvNode.FromInt64(value is long integer ? integer : 0);
            case MpvFormat.Double:
                return MpvNode.FromDouble(value is double number ? number : 0);
            case MpvFormat.Node:
                return value as MpvNode ?? MpvNode.None();
            default:
                return MpvNode.None();
        }
    }

    /// <summary>
    /// 將 libmpv 原生事件轉換為節點並分派。
    /// </summary>
    /// <param name="eventPointer">libmpv 原生事件指標。</param>
    /// <param name="nativeEvent">libmpv 原生事件資料。</param>
    private void DispatchNodeEvent(IntPtr eventPointer, MpvEvent nativeEvent)
    {
        if (eventPointer == IntPtr.Zero || EventNodeReceived == null)
        {
            return;
        }

        NativeMpvNode eventNode;
        int error = MpvNative.mpv_event_to_node(out eventNode, eventPointer);
        if (error < 0)
        {
            return;
        }

        try
        {
            DispatchManagedEvent(EventNodeReceived, new MpvNodeEventArgs(
                nativeEvent.EventId,
                nativeEvent.Error,
                nativeEvent.ReplyUserData,
                MpvNode.FromNative(eventNode)), nameof(EventNodeReceived));
        }
        finally
        {
            MpvNative.mpv_free_node_contents(ref eventNode);
        }
    }

    /// <summary>
    /// 分派 libmpv 屬性變更事件。
    /// </summary>
    /// <param name="nativeEvent">libmpv 屬性變更事件資料。</param>
    private void DispatchPropertyChange(MpvEvent nativeEvent)
    {
        if (nativeEvent.Data == IntPtr.Zero)
        {
            return;
        }

        MpvEventProperty property = (MpvEventProperty)Marshal.PtrToStructure(nativeEvent.Data, typeof(MpvEventProperty))!;
        string name = Utf8StringMarshaller.PtrToString(property.Name) ?? string.Empty;
        object? value = DecodePropertyValue(property);
        DispatchManagedEvent(PropertyChanged, new MpvPropertyChangedEventArgs(nativeEvent.ReplyUserData, name, property.Format, value), nameof(PropertyChanged));
        if (name == "track-list" && value is MpvNode trackListNode)
        {
            DispatchManagedEvent(TracksChanged, new MpvTracksChangedEventArgs(nativeEvent.ReplyUserData, ReadTracks(trackListNode)), nameof(TracksChanged));
        }
    }

    /// <summary>
    /// 將 libmpv 屬性原生資料轉換成受控值。
    /// </summary>
    /// <param name="property">libmpv 屬性事件資料。</param>
    /// <returns>轉換後的受控屬性值；無法轉換時為 <see langword="null"/>。</returns>
    private static object? DecodePropertyValue(MpvEventProperty property)
    {
        if (property.Data == IntPtr.Zero)
        {
            return null;
        }

        switch (property.Format)
        {
            case MpvFormat.String:
            case MpvFormat.OsdString:
                return Utf8StringMarshaller.PtrToString(Marshal.ReadIntPtr(property.Data));
            case MpvFormat.Flag:
                return Marshal.ReadInt32(property.Data) != 0;
            case MpvFormat.Int64:
                return Marshal.ReadInt64(property.Data);
            case MpvFormat.Double:
                byte[] bytes = new byte[8];
                Marshal.Copy(property.Data, bytes, 0, bytes.Length);
                return BitConverter.ToDouble(bytes, 0);
            case MpvFormat.Node:
                NativeMpvNode node = (NativeMpvNode)Marshal.PtrToStructure(property.Data, typeof(NativeMpvNode))!;
                return MpvNode.FromNative(node);
            default:
                return null;
        }
    }

    /// <summary>
    /// 分派 libmpv 記錄訊息事件。
    /// </summary>
    /// <param name="nativeEvent">libmpv 記錄訊息事件資料。</param>
    private void DispatchLogMessage(MpvEvent nativeEvent)
    {
        if (nativeEvent.Data == IntPtr.Zero)
        {
            return;
        }

        MpvEventLogMessage message = (MpvEventLogMessage)Marshal.PtrToStructure(nativeEvent.Data, typeof(MpvEventLogMessage))!;
        DispatchManagedEvent(LogMessageReceived, new MpvLogMessageEventArgs(
            Utf8StringMarshaller.PtrToString(message.Prefix) ?? string.Empty,
            Utf8StringMarshaller.PtrToString(message.Level) ?? string.Empty,
            Utf8StringMarshaller.PtrToString(message.Text) ?? string.Empty,
            message.LogLevel), nameof(LogMessageReceived));
    }

    /// <summary>
    /// 分派 libmpv 播放項目開始載入事件。
    /// </summary>
    /// <param name="nativeEvent">libmpv 播放項目開始載入事件資料。</param>
    private void DispatchStartFile(MpvEvent nativeEvent)
    {
        if (nativeEvent.Data == IntPtr.Zero)
        {
            return;
        }

        MpvEventStartFile startFile = (MpvEventStartFile)Marshal.PtrToStructure(nativeEvent.Data, typeof(MpvEventStartFile))!;
        DispatchManagedEvent(StartFile, new MpvStartFileEventArgs(startFile.PlaylistEntryId), nameof(StartFile));
    }

    /// <summary>
    /// 分派 libmpv 播放項目結束事件。
    /// </summary>
    /// <param name="nativeEvent">libmpv 播放項目結束事件資料。</param>
    private void DispatchEndFile(MpvEvent nativeEvent)
    {
        if (nativeEvent.Data == IntPtr.Zero)
        {
            return;
        }

        MpvEventEndFile endFile = (MpvEventEndFile)Marshal.PtrToStructure(nativeEvent.Data, typeof(MpvEventEndFile))!;
        DispatchManagedEvent(EndFile, new MpvEndFileEventArgs(
            endFile.Reason,
            endFile.Error,
            endFile.PlaylistEntryId,
            endFile.PlaylistInsertId,
            endFile.PlaylistInsertNumEntries), nameof(EndFile));
    }

    /// <summary>
    /// 分派 libmpv 用戶端訊息事件。
    /// </summary>
    /// <param name="nativeEvent">libmpv 用戶端訊息事件資料。</param>
    private void DispatchClientMessage(MpvEvent nativeEvent)
    {
        if (nativeEvent.Data == IntPtr.Zero)
        {
            return;
        }

        MpvEventClientMessage message = (MpvEventClientMessage)Marshal.PtrToStructure(nativeEvent.Data, typeof(MpvEventClientMessage))!;
        DispatchManagedEvent(ClientMessage, new MpvClientMessageEventArgs(
            nativeEvent.Error,
            nativeEvent.ReplyUserData,
            DecodeClientMessageArguments(message)), nameof(ClientMessage));
    }

    /// <summary>
    /// 分派 libmpv 掛鉤事件。
    /// </summary>
    /// <param name="nativeEvent">libmpv 掛鉤事件資料。</param>
    private void DispatchHook(MpvEvent nativeEvent)
    {
        if (nativeEvent.Data == IntPtr.Zero)
        {
            return;
        }

        MpvEventHook hook = (MpvEventHook)Marshal.PtrToStructure(nativeEvent.Data, typeof(MpvEventHook))!;
        DispatchManagedEvent(Hook, new MpvHookEventArgs(
            nativeEvent.Error,
            nativeEvent.ReplyUserData,
            Utf8StringMarshaller.PtrToString(hook.Name) ?? string.Empty,
            hook.Id), nameof(Hook));
    }

    /// <summary>
    /// 將 libmpv 用戶端訊息引數轉換為受控字串集合。
    /// </summary>
    /// <param name="message">libmpv 用戶端訊息事件資料。</param>
    /// <returns>轉換後的訊息引數集合。</returns>
    private static IReadOnlyList<string> DecodeClientMessageArguments(MpvEventClientMessage message)
    {
        if (message.ArgumentCount <= 0 || message.Arguments == IntPtr.Zero)
        {
            return Array.Empty<string>();
        }

        List<string> arguments = new List<string>(message.ArgumentCount);
        for (int i = 0; i < message.ArgumentCount; i++)
        {
            IntPtr argumentPointer = Marshal.ReadIntPtr(message.Arguments, i * IntPtr.Size);
            arguments.Add(Utf8StringMarshaller.PtrToString(argumentPointer) ?? string.Empty);
        }

        return new ReadOnlyCollection<string>(arguments);
    }

    /// <summary>
    /// 取得下一個 libmpv 非同步要求識別碼。
    /// </summary>
    /// <returns>下一個要求識別碼。</returns>
    private ulong NextRequestId()
    {
        long value = Interlocked.Increment(ref _nextRequestId);
        return unchecked((ulong)value);
    }

    /// <summary>
    /// 處理 libmpv 喚醒通知。
    /// </summary>
    /// <param name="context">libmpv 傳回的回呼內容指標。</param>
    private void OnWakeup(IntPtr context)
    {
        try
        {
            _wakeupAction?.Invoke();
        }
        catch (Exception exception)
        {
            DispatchEventHandlerException("WakeupCallback", exception);
        }
    }

    /// <summary>
    /// 確認目前播放器尚未釋放。
    /// </summary>
    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }
    }
}

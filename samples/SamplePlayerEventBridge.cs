using System;
using System.Collections.Generic;
using System.Globalization;

namespace MediaEmbedKit.Mpv.Samples
{
    /// <summary>
    /// 將範例播放器的 libmpv 事件轉成可顯示在 UI 的文字列。
    /// </summary>
    internal sealed class SamplePlayerEventBridge : IDisposable
    {
        /// <summary>
        /// 範例需要觀察的常用 libmpv 屬性。
        /// </summary>
        private static readonly SampleObservedProperty[] ObservedProperties =
        {
            new SampleObservedProperty("pause", MpvFormat.Flag),
            new SampleObservedProperty("time-pos", MpvFormat.Double),
            new SampleObservedProperty("duration", MpvFormat.Double),
            new SampleObservedProperty("media-title", MpvFormat.String),
            new SampleObservedProperty("path", MpvFormat.String),
            new SampleObservedProperty("idle-active", MpvFormat.Flag)
        };

        /// <summary>
        /// 正在觀察的 mpv 播放器。
        /// </summary>
        private readonly MpvPlayer _player;
        /// <summary>
        /// 將事件文字列送回 UI 的委派。
        /// </summary>
        private readonly Action<string> _appendLine;
        /// <summary>
        /// 目前已註冊的屬性觀察識別碼。
        /// </summary>
        private readonly List<ulong> _observeIds = new List<ulong>();
        /// <summary>
        /// 表示目前事件橋接器是否已釋放。
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 初始化 <see cref="SamplePlayerEventBridge"/> 類別的新執行個體。
        /// </summary>
        /// <param name="player">要觀察的 mpv 播放器。</param>
        /// <param name="appendLine">接收格式化事件文字列的委派。</param>
        public SamplePlayerEventBridge(MpvPlayer player, Action<string> appendLine)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _appendLine = appendLine ?? throw new ArgumentNullException(nameof(appendLine));
            Subscribe();
            WriteLifecycle("PlayerCreated", "播放器已建立並完成 libmpv 初始化。");
            RequestLogMessages();
            ObserveCommonProperties();
        }

        /// <summary>
        /// 將範例生命週期訊息寫入事件輸出。
        /// </summary>
        /// <param name="stage">目前生命週期階段。</param>
        /// <param name="detail">生命週期階段的補充內容。</param>
        public void WriteLifecycle(string stage, string detail)
        {
            Append("lifecycle", stage + " | " + detail);
        }

        /// <summary>
        /// 釋放事件橋接器並取消 libmpv 事件訂閱。
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            WriteLifecycle("Dispose", "取消事件訂閱並釋放範例事件橋接器。");
            UnobserveCommonProperties();
            Unsubscribe();
        }

        /// <summary>
        /// 訂閱播放器事件。
        /// </summary>
        private void Subscribe()
        {
            _player.EventReceived += PlayerEventReceived;
            _player.LogMessageReceived += PlayerLogMessageReceived;
            _player.PropertyChanged += PlayerPropertyChanged;
            _player.StartFile += PlayerStartFile;
            _player.FileLoaded += PlayerFileLoaded;
            _player.EndFile += PlayerEndFile;
            _player.TracksChanged += PlayerTracksChanged;
            _player.VideoReconfigured += PlayerVideoReconfigured;
            _player.AudioReconfigured += PlayerAudioReconfigured;
            _player.PlaybackRestarted += PlayerPlaybackRestarted;
            _player.Shutdown += PlayerShutdown;
            _player.EventDispatchException += PlayerEventDispatchException;
        }

        /// <summary>
        /// 取消訂閱播放器事件。
        /// </summary>
        private void Unsubscribe()
        {
            _player.EventReceived -= PlayerEventReceived;
            _player.LogMessageReceived -= PlayerLogMessageReceived;
            _player.PropertyChanged -= PlayerPropertyChanged;
            _player.StartFile -= PlayerStartFile;
            _player.FileLoaded -= PlayerFileLoaded;
            _player.EndFile -= PlayerEndFile;
            _player.TracksChanged -= PlayerTracksChanged;
            _player.VideoReconfigured -= PlayerVideoReconfigured;
            _player.AudioReconfigured -= PlayerAudioReconfigured;
            _player.PlaybackRestarted -= PlayerPlaybackRestarted;
            _player.Shutdown -= PlayerShutdown;
            _player.EventDispatchException -= PlayerEventDispatchException;
        }

        /// <summary>
        /// 啟用 libmpv 記錄訊息輸出。
        /// </summary>
        private void RequestLogMessages()
        {
            try
            {
                _player.RequestLogMessages("v");
            }
            catch (MpvException ex)
            {
                Append("log-request", "無法啟用 libmpv log 輸出：" + ex.Message);
            }
        }

        /// <summary>
        /// 訂閱常用 libmpv 屬性。
        /// </summary>
        private void ObserveCommonProperties()
        {
            foreach (SampleObservedProperty property in ObservedProperties)
            {
                try
                {
                    _observeIds.Add(_player.ObserveProperty(property.Name, property.Format));
                }
                catch (MpvException ex)
                {
                    Append("observe", property.Name + " 訂閱失敗：" + ex.Message);
                }
            }
        }

        /// <summary>
        /// 取消常用 libmpv 屬性訂閱。
        /// </summary>
        private void UnobserveCommonProperties()
        {
            foreach (ulong observeId in _observeIds)
            {
                try
                {
                    _player.UnobserveProperty(observeId);
                }
                catch (MpvException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
            }

            _observeIds.Clear();
        }

        /// <summary>
        /// 處理 libmpv 原始事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void PlayerEventReceived(object? sender, MpvEventArgs e)
        {
            Append("event", e.EventId + " error=" + e.ErrorCode.ToString(CultureInfo.InvariantCulture) + " reply=" + e.ReplyUserData.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// 處理 libmpv 記錄訊息事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">記錄訊息事件資料。</param>
        private void PlayerLogMessageReceived(object? sender, MpvLogMessageEventArgs e)
        {
            Append("log", e.Level + " " + e.Prefix + " | " + e.Text.Trim());
        }

        /// <summary>
        /// 處理 libmpv 屬性變更事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">屬性變更事件資料。</param>
        private void PlayerPropertyChanged(object? sender, MpvPropertyChangedEventArgs e)
        {
            Append("property", e.Name + " = " + FormatValue(e.Value) + " (" + e.Format + ")");
        }

        /// <summary>
        /// 處理播放項目開始載入事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">開始載入事件資料。</param>
        private void PlayerStartFile(object? sender, MpvStartFileEventArgs e)
        {
            Append("start-file", "playlist-entry-id=" + e.PlaylistEntryId.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// 處理播放項目已載入事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">一般事件資料。</param>
        private void PlayerFileLoaded(object? sender, MpvEventArgs e)
        {
            Append("file-loaded", "reply=" + e.ReplyUserData.ToString(CultureInfo.InvariantCulture));
            WriteYtdlJsonSubprocessResult();
        }

        /// <summary>
        /// 處理播放項目結束事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">播放項目結束事件資料。</param>
        private void PlayerEndFile(object? sender, MpvEndFileEventArgs e)
        {
            Append("end-file", "reason=" + e.Reason + " error=" + e.MpvErrorCode.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// 處理播放軌清單變更事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">播放軌清單變更事件資料。</param>
        private void PlayerTracksChanged(object? sender, MpvTracksChangedEventArgs e)
        {
            Append("tracks", "count=" + e.Tracks.Count.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// 處理視訊重新設定事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">一般事件資料。</param>
        private void PlayerVideoReconfigured(object? sender, MpvEventArgs e)
        {
            Append("video", "video-reconfig");
        }

        /// <summary>
        /// 處理音訊重新設定事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">一般事件資料。</param>
        private void PlayerAudioReconfigured(object? sender, MpvEventArgs e)
        {
            Append("audio", "audio-reconfig");
        }

        /// <summary>
        /// 處理播放重新開始事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">一般事件資料。</param>
        private void PlayerPlaybackRestarted(object? sender, MpvEventArgs e)
        {
            Append("playback", "playback-restart");
        }

        /// <summary>
        /// 處理 libmpv 關閉事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">一般事件資料。</param>
        private void PlayerShutdown(object? sender, MpvEventArgs e)
        {
            Append("shutdown", "libmpv shutdown");
        }

        /// <summary>
        /// 處理事件派送例外狀況。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件派送例外狀況資料。</param>
        private void PlayerEventDispatchException(object? sender, MpvEventDispatchExceptionEventArgs e)
        {
            Append("dispatch-error", e.EventName + " | " + e.Exception.GetType().Name + ": " + e.Exception.Message);
        }

        /// <summary>
        /// 將 mpv ytdl hook 的 yt-dlp JSON 子程序結果寫入事件清單。
        /// </summary>
        private void WriteYtdlJsonSubprocessResult()
        {
            MpvYtdlJsonSubprocessResult? result = _player.GetYtdlJsonSubprocessResult();
            if (result == null)
            {
                return;
            }

            Append("ytdl-result", "status=" + result.Status.ToString(CultureInfo.InvariantCulture) + " error=" + (string.IsNullOrEmpty(result.ErrorString) ? "none" : result.ErrorString));
            AppendYtdlOutput("ytdl:out", result.StandardOutput);
            AppendYtdlOutput("ytdl:err", result.StandardError);
        }

        /// <summary>
        /// 將 ytdl hook 輸出拆列並寫入事件清單。
        /// </summary>
        /// <param name="category">事件分類。</param>
        /// <param name="text">ytdl hook 輸出文字。</param>
        private void AppendYtdlOutput(string category, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            string[] lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            int count = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    continue;
                }

                Append(category, lines[i]);
                count++;
                if (count >= 8)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// 將事件文字送往 UI。
        /// </summary>
        /// <param name="category">事件分類。</param>
        /// <param name="message">事件內容。</param>
        private void Append(string category, string message)
        {
            string line = DateTimeOffset.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + " [" + category + "] " + message;
            _appendLine(line);
        }

        /// <summary>
        /// 將屬性值格式化為範例 UI 文字。
        /// </summary>
        /// <param name="value">要格式化的屬性值。</param>
        /// <returns>可顯示的屬性值文字。</returns>
        private static string FormatValue(object? value)
        {
            if (value == null)
            {
                return "null";
            }

            if (value is double doubleValue)
            {
                return doubleValue.ToString("0.###", CultureInfo.InvariantCulture);
            }

            if (value is IFormattable formattable)
            {
                return formattable.ToString(null, CultureInfo.InvariantCulture);
            }

            return value.ToString() ?? string.Empty;
        }

        /// <summary>
        /// 表示範例要觀察的 libmpv 屬性。
        /// </summary>
        private readonly struct SampleObservedProperty
        {
            /// <summary>
            /// 初始化 <see cref="SampleObservedProperty"/> 結構的新執行個體。
            /// </summary>
            /// <param name="name">libmpv 屬性名稱。</param>
            /// <param name="format">屬性資料格式。</param>
            public SampleObservedProperty(string name, MpvFormat format)
            {
                Name = name;
                Format = format;
            }

            /// <summary>
            /// 取得 libmpv 屬性名稱。
            /// </summary>
            /// <value>libmpv 屬性名稱。</value>
            public string Name { get; }

            /// <summary>
            /// 取得屬性資料格式。
            /// </summary>
            /// <value>屬性資料格式。</value>
            public MpvFormat Format { get; }
        }
    }
}

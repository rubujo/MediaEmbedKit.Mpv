using System;
using System.Threading;

namespace MediaEmbedKit.Mpv.Samples
{
    /// <summary>
    /// 在背景執行緒讀取範例播放狀態，並以低頻率將文字更新送回 UI 執行緒。
    /// </summary>
    internal sealed class SampleStatusUpdateDispatcher : IDisposable
    {
        /// <summary>
        /// 狀態輪詢間隔。
        /// </summary>
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

        /// <summary>
        /// 保護狀態更新流程的同步物件。
        /// </summary>
        private readonly object _syncRoot = new object();
        /// <summary>
        /// 在背景執行緒讀取狀態文字的委派。
        /// </summary>
        private readonly Func<string> _readStatusText;
        /// <summary>
        /// 在 UI 執行緒套用狀態文字的委派。
        /// </summary>
        private readonly Action<string> _applyStatusText;
        /// <summary>
        /// 將 UI 更新排入 UI 執行緒的委派。
        /// </summary>
        private readonly Func<Action, bool> _scheduleOnUiThread;
        /// <summary>
        /// 週期性讀取播放狀態的背景計時器。
        /// </summary>
        private readonly Timer _pollTimer;
        /// <summary>
        /// 最近一次套用到 UI 的狀態文字。
        /// </summary>
        private string? _lastStatusText;
        /// <summary>
        /// 表示目前是否已有狀態更新排入 UI 執行緒。
        /// </summary>
        private bool _updateQueued;
        /// <summary>
        /// 表示目前分派器是否已釋放。
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 初始化 <see cref="SampleStatusUpdateDispatcher"/> 類別的新執行個體。
        /// </summary>
        /// <param name="readStatusText">在背景執行緒讀取狀態文字的委派。</param>
        /// <param name="applyStatusText">在 UI 執行緒套用狀態文字的委派。</param>
        /// <param name="scheduleOnUiThread">將 UI 更新排入 UI 執行緒的委派。</param>
        public SampleStatusUpdateDispatcher(
            Func<string> readStatusText,
            Action<string> applyStatusText,
            Func<Action, bool> scheduleOnUiThread)
        {
            _readStatusText = readStatusText ?? throw new ArgumentNullException(nameof(readStatusText));
            _applyStatusText = applyStatusText ?? throw new ArgumentNullException(nameof(applyStatusText));
            _scheduleOnUiThread = scheduleOnUiThread ?? throw new ArgumentNullException(nameof(scheduleOnUiThread));
            _pollTimer = new Timer(PollStatus, null, TimeSpan.Zero, PollInterval);
        }

        /// <summary>
        /// 要求立即重新整理狀態文字。
        /// </summary>
        public void RequestUpdate()
        {
            ThreadPool.QueueUserWorkItem(PollStatus);
        }

        /// <summary>
        /// 釋放背景計時器。
        /// </summary>
        public void Dispose()
        {
            lock (_syncRoot)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
            }

            _pollTimer.Dispose();
        }

        /// <summary>
        /// 在背景執行緒讀取狀態，必要時排入 UI 更新。
        /// </summary>
        /// <param name="state">計時器狀態；未使用。</param>
        private void PollStatus(object? state)
        {
            lock (_syncRoot)
            {
                if (_disposed || _updateQueued)
                {
                    return;
                }

                _updateQueued = true;
            }

            string statusText = ReadStatusText();
            lock (_syncRoot)
            {
                if (_disposed)
                {
                    _updateQueued = false;
                    return;
                }

                if (string.Equals(_lastStatusText, statusText, StringComparison.Ordinal))
                {
                    _updateQueued = false;
                    return;
                }

            }

            try
            {
                bool scheduled = _scheduleOnUiThread(() => ApplyStatusText(statusText));
                if (!scheduled)
                {
                    ResetQueuedState();
                }
            }
            catch (ObjectDisposedException)
            {
                ResetQueuedState();
            }
            catch (InvalidOperationException)
            {
                ResetQueuedState();
            }
        }

        /// <summary>
        /// 安全讀取狀態文字。
        /// </summary>
        /// <returns>可顯示於範例 UI 的狀態文字。</returns>
        private string ReadStatusText()
        {
            try
            {
                return _readStatusText();
            }
            catch (ObjectDisposedException)
            {
                return "播放器已釋放";
            }
            catch (MpvException ex)
            {
                return "播放器狀態暫時不可用：" + ex.ErrorCode;
            }
        }

        /// <summary>
        /// 在 UI 執行緒套用狀態文字。
        /// </summary>
        /// <param name="statusText">要套用的狀態文字。</param>
        private void ApplyStatusText(string statusText)
        {
            try
            {
                lock (_syncRoot)
                {
                    if (_disposed)
                    {
                        return;
                    }
                }

                _applyStatusText(statusText);
                lock (_syncRoot)
                {
                    if (!_disposed)
                    {
                        _lastStatusText = statusText;
                    }
                }
            }
            finally
            {
                ResetQueuedState();
            }
        }

        /// <summary>
        /// 重設目前已排入 UI 更新的狀態。
        /// </summary>
        private void ResetQueuedState()
        {
            lock (_syncRoot)
            {
                _updateQueued = false;
            }
        }
    }
}

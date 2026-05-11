using System;
using System.Collections.Generic;
using System.Threading;

namespace MediaEmbedKit.Mpv.Samples
{
    /// <summary>
    /// 將高頻範例事件先放入背景佇列，再以固定節奏批次送到 UI 執行緒。
    /// </summary>
    internal sealed class SampleEventLogDispatcher : IDisposable
    {
        /// <summary>
        /// 批次送出事件列的間隔。
        /// </summary>
        private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(3);

        /// <summary>
        /// 單次送往 UI 執行緒的最大事件列數。
        /// </summary>
        private const int MaxLinesPerFlush = 12;

        /// <summary>
        /// 背景佇列允許暫存的最大事件列數。
        /// </summary>
        private const int MaxPendingLines = 80;

        /// <summary>
        /// 保護事件佇列的同步物件。
        /// </summary>
        private readonly object _syncRoot = new object();
        /// <summary>
        /// 等待送往 UI 的事件列佇列。
        /// </summary>
        private readonly Queue<string> _pendingLines = new Queue<string>();
        /// <summary>
        /// 接收批次事件列並更新 UI 的委派。
        /// </summary>
        private readonly Action<IReadOnlyList<string>> _appendLines;
        /// <summary>
        /// 將批次更新排入 UI 執行緒的委派。
        /// </summary>
        private readonly Action<Action> _scheduleOnUiThread;
        /// <summary>
        /// 週期性清空背景事件佇列的計時器。
        /// </summary>
        private readonly Timer _flushTimer;
        /// <summary>
        /// 因佇列滿載而略過的事件列數。
        /// </summary>
        private int _droppedLineCount;
        /// <summary>
        /// 表示目前是否已有 UI 更新排入 UI 執行緒。
        /// </summary>
        private bool _uiFlushQueued;
        /// <summary>
        /// 表示目前分派器是否已釋放。
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 初始化 <see cref="SampleEventLogDispatcher"/> 類別的新執行個體。
        /// </summary>
        /// <param name="appendLines">接收批次事件列並更新 UI 的委派。</param>
        /// <param name="scheduleOnUiThread">將批次更新排入 UI 執行緒的委派。</param>
        public SampleEventLogDispatcher(Action<IReadOnlyList<string>> appendLines, Action<Action> scheduleOnUiThread)
        {
            _appendLines = appendLines ?? throw new ArgumentNullException(nameof(appendLines));
            _scheduleOnUiThread = scheduleOnUiThread ?? throw new ArgumentNullException(nameof(scheduleOnUiThread));
            _flushTimer = new Timer(Flush, null, FlushInterval, FlushInterval);
        }

        /// <summary>
        /// 將事件列放入背景佇列。
        /// </summary>
        /// <param name="line">要顯示在事件清單的文字列。</param>
        public void Enqueue(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return;
            }

            lock (_syncRoot)
            {
                if (_disposed)
                {
                    return;
                }

                _pendingLines.Enqueue(line);
                while (_pendingLines.Count > MaxPendingLines)
                {
                    _pendingLines.Dequeue();
                    _droppedLineCount++;
                }
            }
        }

        /// <summary>
        /// 釋放背景計時器並送出最後尚未顯示的事件列。
        /// </summary>
        public void Dispose()
        {
            bool shouldFlush;
            lock (_syncRoot)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                shouldFlush = _pendingLines.Count > 0;
            }

            _flushTimer.Dispose();
            if (shouldFlush)
            {
                Flush(null);
            }
        }

        /// <summary>
        /// 將目前佇列中的事件列批次送往 UI 執行緒。
        /// </summary>
        /// <param name="state">計時器狀態；未使用。</param>
        private void Flush(object? state)
        {
            List<string> lines = new List<string>();
            lock (_syncRoot)
            {
                if (_disposed && state != null)
                {
                    return;
                }

                if (_uiFlushQueued)
                {
                    return;
                }

                if (_droppedLineCount > 0)
                {
                    lines.Add("事件輸出已略過 " + _droppedLineCount + " 列高頻訊息，以避免範例 UI 壅塞。");
                    _droppedLineCount = 0;
                }

                while (_pendingLines.Count > 0 && lines.Count < MaxLinesPerFlush)
                {
                    lines.Add(_pendingLines.Dequeue());
                }

                if (lines.Count > 0)
                {
                    _uiFlushQueued = true;
                }
            }

            if (lines.Count == 0)
            {
                return;
            }

            _scheduleOnUiThread(() =>
            {
                try
                {
                    _appendLines(lines);
                }
                finally
                {
                    lock (_syncRoot)
                    {
                        _uiFlushQueued = false;
                    }
                }
            });
        }
    }
}

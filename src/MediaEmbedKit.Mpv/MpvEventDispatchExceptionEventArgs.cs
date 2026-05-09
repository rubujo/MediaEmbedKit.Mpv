using System;

namespace MediaEmbedKit.Mpv
{
    /// <summary>
    /// 提供受控事件處理常式發生例外狀況時的事件資料。
    /// </summary>
    public sealed class MpvEventDispatchExceptionEventArgs : EventArgs
    {
        /// <summary>
        /// 初始化 <see cref="MpvEventDispatchExceptionEventArgs"/> 類別的新執行個體。
        /// </summary>
        /// <param name="eventName">發生例外狀況的事件名稱。</param>
        /// <param name="exception">事件處理常式擲回的例外狀況。</param>
        public MpvEventDispatchExceptionEventArgs(string eventName, Exception exception)
        {
            EventName = eventName ?? throw new ArgumentNullException(nameof(eventName));
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        }

        /// <summary>
        /// 取得發生例外狀況的事件名稱。
        /// </summary>
        /// <value>事件名稱。</value>
        public string EventName { get; private set; }

        /// <summary>
        /// 取得事件處理常式擲回的例外狀況。
        /// </summary>
        /// <value>事件處理常式擲回的例外狀況。</value>
        public Exception Exception { get; private set; }
    }
}

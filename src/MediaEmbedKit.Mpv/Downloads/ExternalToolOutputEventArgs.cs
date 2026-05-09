using System;

namespace MediaEmbedKit.Mpv.Downloads
{
    /// <summary>
    /// 提供外部工具單列輸出事件的資料。
    /// </summary>
    public sealed class ExternalToolOutputEventArgs : EventArgs
    {
        /// <summary>
        /// 初始化 <see cref="ExternalToolOutputEventArgs"/> 類別的新執行個體。
        /// </summary>
        /// <param name="stream">產生輸出的資料流。</param>
        /// <param name="line">外部工具輸出的單列文字。</param>
        /// <param name="timestamp">收到輸出的時間戳記。</param>
        public ExternalToolOutputEventArgs(ExternalToolOutputStream stream, string line, DateTimeOffset timestamp)
        {
            Stream = stream;
            Line = line ?? string.Empty;
            Timestamp = timestamp;
        }

        /// <summary>
        /// 取得產生輸出的資料流。
        /// </summary>
        /// <value>產生輸出的資料流。</value>
        public ExternalToolOutputStream Stream { get; private set; }

        /// <summary>
        /// 取得外部工具輸出的單列文字。
        /// </summary>
        /// <value>外部工具輸出的單列文字。</value>
        public string Line { get; private set; }

        /// <summary>
        /// 取得收到輸出的時間戳記。
        /// </summary>
        /// <value>收到輸出的時間戳記。</value>
        public DateTimeOffset Timestamp { get; private set; }
    }
}

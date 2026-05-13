namespace MediaEmbedKit.Mpv;

/// <summary>
/// 表示 libmpv 記錄訊息事件的資料。
/// </summary>
public sealed class MpvLogMessageEventArgs : MpvEventArgs
{
    /// <summary>
    /// 初始化 <see cref="MpvLogMessageEventArgs"/> 類別的新執行個體。
    /// </summary>
    /// <param name="prefix">libmpv 記錄訊息的前置詞。</param>
    /// <param name="level">libmpv 記錄訊息的文字等級。</param>
    /// <param name="text">libmpv 記錄訊息的內容。</param>
    /// <param name="logLevel">libmpv 記錄訊息的列舉等級。</param>
    public MpvLogMessageEventArgs(string prefix, string level, string text, MpvLogLevel logLevel)
        : base(MpvEventId.LogMessage, 0, 0)
    {
        Prefix = prefix;
        Level = level;
        Text = text;
        LogLevel = logLevel;
    }

    /// <summary>
    /// 取得 libmpv 記錄訊息的前置詞。
    /// </summary>
    /// <value>記錄訊息前置詞。</value>
    public string Prefix { get; private set; }

    /// <summary>
    /// 取得 libmpv 記錄訊息的文字等級。
    /// </summary>
    /// <value>記錄訊息等級文字。</value>
    public string Level { get; private set; }

    /// <summary>
    /// 取得 libmpv 記錄訊息的內容。
    /// </summary>
    /// <value>記錄訊息內容。</value>
    public string Text { get; private set; }

    /// <summary>
    /// 取得 libmpv 記錄訊息的列舉等級。
    /// </summary>
    /// <value>記錄訊息列舉等級。</value>
    public MpvLogLevel LogLevel { get; private set; }
}

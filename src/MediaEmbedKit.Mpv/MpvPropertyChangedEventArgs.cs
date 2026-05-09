namespace MediaEmbedKit.Mpv
{
    /// <summary>
    /// 表示 libmpv 屬性變更事件的資料。
    /// </summary>
    public sealed class MpvPropertyChangedEventArgs : MpvEventArgs
    {
        /// <summary>
        /// 初始化 <see cref="MpvPropertyChangedEventArgs"/> 類別的新執行個體。
        /// </summary>
        /// <param name="replyUserData">屬性觀察要求使用的回覆資料。</param>
        /// <param name="name">已變更的 libmpv 屬性名稱。</param>
        /// <param name="format">屬性值的 libmpv 資料格式。</param>
        /// <param name="value">屬性變更後的受控值。</param>
        public MpvPropertyChangedEventArgs(ulong replyUserData, string name, MpvFormat format, object? value)
            : base(MpvEventId.PropertyChange, 0, replyUserData)
        {
            Name = name;
            Format = format;
            Value = value;
        }

        /// <summary>
        /// 取得已變更的 libmpv 屬性名稱。
        /// </summary>
        /// <value>libmpv 屬性名稱。</value>
        public string Name { get; private set; }

        /// <summary>
        /// 取得屬性值的 libmpv 資料格式。
        /// </summary>
        /// <value>屬性值資料格式。</value>
        public MpvFormat Format { get; private set; }

        /// <summary>
        /// 取得屬性變更後的受控值。
        /// </summary>
        /// <value>屬性目前值；無資料時為 <see langword="null"/>。</value>
        public object? Value { get; private set; }
    }
}

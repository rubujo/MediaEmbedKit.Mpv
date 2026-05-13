namespace MediaEmbedKit.Mpv;

/// <summary>
/// 表示 libmpv 掛鉤事件資料。
/// </summary>
public sealed class MpvHookEventArgs : MpvEventArgs
{
    /// <summary>
    /// 初始化 <see cref="MpvHookEventArgs"/> 類別的新執行個體。
    /// </summary>
    /// <param name="errorCode">與事件關聯的 libmpv 錯誤碼。</param>
    /// <param name="replyUserData">註冊掛鉤時提供的回覆資料。</param>
    /// <param name="name">被觸發的 libmpv 掛鉤名稱。</param>
    /// <param name="hookId">必須傳給 <see cref="MpvPlayer.ContinueHook"/> 的掛鉤識別碼。</param>
    public MpvHookEventArgs(int errorCode, ulong replyUserData, string name, ulong hookId)
        : base(MpvEventId.Hook, errorCode, replyUserData)
    {
        Name = name;
        HookId = hookId;
    }

    /// <summary>
    /// 取得被觸發的 libmpv 掛鉤名稱。
    /// </summary>
    /// <value>libmpv 掛鉤名稱。</value>
    public string Name { get; private set; }

    /// <summary>
    /// 取得必須傳給 <see cref="MpvPlayer.ContinueHook"/> 的掛鉤識別碼。
    /// </summary>
    /// <value>libmpv 掛鉤識別碼。</value>
    public ulong HookId { get; private set; }
}

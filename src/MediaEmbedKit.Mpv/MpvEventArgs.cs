using System;

namespace MediaEmbedKit.Mpv;

/// <summary>
/// 表示從 libmpv 事件佇列收到的一般事件資料。
/// </summary>
public class MpvEventArgs : EventArgs
{
    /// <summary>
    /// 初始化 <see cref="MpvEventArgs"/> 類別的新執行個體。
    /// </summary>
    /// <param name="eventId">libmpv 事件識別碼。</param>
    /// <param name="errorCode">與事件關聯的 libmpv 錯誤碼。</param>
    /// <param name="replyUserData">非同步命令或屬性觀察使用的回覆資料。</param>
    public MpvEventArgs(MpvEventId eventId, int errorCode, ulong replyUserData)
    {
        EventId = eventId;
        ErrorCode = errorCode;
        ReplyUserData = replyUserData;
    }

    /// <summary>
    /// 取得 libmpv 事件識別碼。
    /// </summary>
    /// <value>目前事件的 libmpv 事件識別碼。</value>
    public MpvEventId EventId { get; private set; }

    /// <summary>
    /// 取得事件關聯的 libmpv 錯誤碼。
    /// </summary>
    /// <value>事件關聯的 libmpv 錯誤碼。</value>
    public int ErrorCode { get; private set; }

    /// <summary>
    /// 取得 libmpv 回覆事件中的使用者資料。
    /// </summary>
    /// <value>非同步命令或屬性觀察所使用的回覆資料。</value>
    public ulong ReplyUserData { get; private set; }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MediaEmbedKit.Mpv;

/// <summary>
/// 表示 libmpv 用戶端訊息事件資料。
/// </summary>
public sealed class MpvClientMessageEventArgs : MpvEventArgs
{
    /// <summary>
    /// 初始化 <see cref="MpvClientMessageEventArgs"/> 類別的新執行個體。
    /// </summary>
    /// <param name="errorCode">與事件關聯的 libmpv 錯誤碼。</param>
    /// <param name="replyUserData">事件附帶的 libmpv 回覆資料。</param>
    /// <param name="arguments">用戶端訊息的字串引數集合。</param>
    public MpvClientMessageEventArgs(int errorCode, ulong replyUserData, IEnumerable<string> arguments)
        : base(MpvEventId.ClientMessage, errorCode, replyUserData)
    {
        if (arguments == null)
        {
            throw new ArgumentNullException(nameof(arguments));
        }

        List<string> items = new List<string>();
        foreach (string argument in arguments)
        {
            items.Add(argument ?? string.Empty);
        }

        Arguments = new ReadOnlyCollection<string>(items);
    }

    /// <summary>
    /// 取得用戶端訊息的字串引數。
    /// </summary>
    /// <value>用戶端訊息引數集合。</value>
    public IReadOnlyList<string> Arguments { get; private set; }
}

using System;

namespace MediaEmbedKit.Mpv
{
    /// <summary>
    /// 表示 libmpv 非同步命令回覆事件資料。
    /// </summary>
    public sealed class MpvCommandReplyEventArgs : MpvEventArgs
    {
        /// <summary>
        /// 初始化 <see cref="MpvCommandReplyEventArgs"/> 類別的新執行個體。
        /// </summary>
        /// <param name="errorCode">與命令回覆關聯的 libmpv 錯誤碼。</param>
        /// <param name="replyUserData">非同步命令使用的回覆資料。</param>
        /// <param name="result">命令成功時傳回的節點資料。</param>
        public MpvCommandReplyEventArgs(int errorCode, ulong replyUserData, MpvNode result)
            : base(MpvEventId.CommandReply, errorCode, replyUserData)
        {
            Result = result ?? throw new ArgumentNullException(nameof(result));
        }

        /// <summary>
        /// 取得命令回傳的節點資料。
        /// </summary>
        /// <value>命令回傳節點；沒有回傳資料時為空節點。</value>
        public MpvNode Result { get; private set; }
    }
}

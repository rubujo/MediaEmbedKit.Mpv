using System;

namespace MediaEmbedKit.Mpv;

/// <summary>
/// 表示 libmpv 呼叫失敗時擲回的例外狀況。
/// </summary>
public sealed class MpvException : Exception
{
    /// <summary>
    /// 使用 libmpv 錯誤碼初始化 <see cref="MpvException"/> 類別的新執行個體。
    /// </summary>
    /// <param name="errorCode">
    /// libmpv 傳回的錯誤碼。
    /// </param>
    public MpvException(int errorCode)
        : base(MpvError.GetMessage(errorCode))
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// 使用指定訊息初始化 <see cref="MpvException"/> 類別的新執行個體。
    /// </summary>
    /// <param name="message">
    /// 描述例外狀況的訊息。
    /// </param>
    public MpvException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// 取得 libmpv 傳回的錯誤碼。
    /// </summary>
    /// <value>
    /// libmpv 錯誤碼；非 libmpv 錯誤時為預設值。
    /// </value>
    public int ErrorCode { get; }
}

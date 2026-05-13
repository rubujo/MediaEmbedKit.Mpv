namespace MediaEmbedKit.Mpv;

/// <summary>
/// 提供自訂 libmpv 串流在收到取消要求時解除阻塞讀取的能力。
/// </summary>
public interface IMpvStreamCancellationHandler
{
    /// <summary>
    /// 取消目前等候中的串流讀取作業。
    /// </summary>
    void CancelPendingRead();
}

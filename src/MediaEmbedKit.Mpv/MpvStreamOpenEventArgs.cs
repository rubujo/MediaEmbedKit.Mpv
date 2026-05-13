using System;
using System.IO;

namespace MediaEmbedKit.Mpv;

/// <summary>
/// 提供 libmpv 自訂串流開啟要求的事件資料。
/// </summary>
public sealed class MpvStreamOpenEventArgs : EventArgs
{
    /// <summary>
    /// 初始化 <see cref="MpvStreamOpenEventArgs"/> 類別的新執行個體。
    /// </summary>
    /// <param name="uri">libmpv 要開啟的 URI。</param>
    public MpvStreamOpenEventArgs(string uri)
    {
        Uri = uri ?? throw new ArgumentNullException(nameof(uri));
    }

    /// <summary>
    /// 取得 libmpv 要開啟的 URI。
    /// </summary>
    /// <value>包含自訂通訊協定前置詞的 URI。</value>
    public string Uri { get; private set; }

    /// <summary>
    /// 取得或設定要交給 libmpv 讀取的受控串流。
    /// </summary>
    /// <value>可讀取的串流；未提供時表示拒絕開啟。</value>
    public Stream? Stream { get; set; }
}

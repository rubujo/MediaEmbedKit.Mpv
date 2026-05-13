namespace MediaEmbedKit.Mpv.Render;

/// <summary>
/// 表示 libmpv 下一個 render API 影格的資訊。
/// </summary>
public sealed class MpvRenderFrameInformation
{
    /// <summary>
    /// 初始化 <see cref="MpvRenderFrameInformation"/> 類別的新執行個體。
    /// </summary>
    /// <param name="flags">下一個影格的資訊旗標。</param>
    /// <param name="targetTime">下一個影格的目標顯示時間。</param>
    internal MpvRenderFrameInformation(MpvRenderFrameInfoFlags flags, long targetTime)
    {
        Flags = flags;
        TargetTime = targetTime;
    }

    /// <summary>
    /// 取得下一個影格的資訊旗標。
    /// </summary>
    /// <value>下一個影格的資訊旗標。</value>
    public MpvRenderFrameInfoFlags Flags { get; private set; }

    /// <summary>
    /// 取得下一個影格的目標顯示時間。
    /// </summary>
    /// <value>libmpv 回報的目標顯示時間。</value>
    public long TargetTime { get; private set; }

    /// <summary>
    /// 取得下一個 render API 結果是否包含可呈現的影格。
    /// </summary>
    /// <value>存在可呈現影格時為 <see langword="true"/>。</value>
    public bool HasFrame
    {
        get { return (Flags & MpvRenderFrameInfoFlags.Present) != 0; }
    }
}

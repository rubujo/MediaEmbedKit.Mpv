namespace MediaEmbedKit.Mpv;

/// <summary>
/// 表示 <see cref="MpvEncoder.SplitAsync"/> 進行中回報的進度快照。
/// </summary>
public readonly struct MpvSplitProgress
{
    /// <summary>
    /// 初始化 <see cref="MpvSplitProgress"/> 結構的新執行個體。
    /// </summary>
    /// <param name="segmentIndex">目前正在編碼的段索引（從 0 起算）。</param>
    /// <param name="totalSegments">總段數。</param>
    /// <param name="segmentProgress">目前段內進度快照。</param>
    public MpvSplitProgress(int segmentIndex, int totalSegments, MpvEncodingProgress segmentProgress)
    {
        SegmentIndex = segmentIndex;
        TotalSegments = totalSegments;
        SegmentProgress = segmentProgress;
    }

    /// <summary>
    /// 取得目前正在編碼的段索引。
    /// </summary>
    /// <value>段索引（從 0 起算）。</value>
    public int SegmentIndex { get; }

    /// <summary>
    /// 取得總段數。
    /// </summary>
    /// <value>總段數。</value>
    public int TotalSegments { get; }

    /// <summary>
    /// 取得目前段內進度快照。
    /// </summary>
    /// <value>段內進度快照。</value>
    public MpvEncodingProgress SegmentProgress { get; }
}

using System;
using System.Collections.Generic;
using MediaEmbedKit.Mpv.Internal;

namespace MediaEmbedKit.Mpv;

/// <summary>
/// 表示 mpv demuxer 快取中的可搜尋範圍。
/// </summary>
public sealed class MpvDemuxerCacheRange
{
    /// <summary>
    /// 初始化 <see cref="MpvDemuxerCacheRange"/> 類別的新執行個體。
    /// </summary>
    /// <param name="start">
    /// 範圍開始時間。
    /// </param>
    /// <param name="end">
    /// 範圍結束時間。
    /// </param>
    /// <param name="rawNode">
    /// 原始節點資料。
    /// </param>
    internal MpvDemuxerCacheRange(double? start, double? end, MpvNode rawNode)
    {
        Start = start;
        End = end;
        RawNode = rawNode ?? throw new ArgumentNullException(nameof(rawNode));
    }

    /// <summary>
    /// 取得範圍開始時間。
    /// </summary>
    /// <value>
    /// 範圍開始秒數；沒有資料時為 <see langword="null"/>。
    /// </value>
    public double? Start { get; private set; }

    /// <summary>
    /// 取得範圍結束時間。
    /// </summary>
    /// <value>
    /// 範圍結束秒數；沒有資料時為 <see langword="null"/>。
    /// </value>
    public double? End { get; private set; }

    /// <summary>
    /// 取得原始節點資料。
    /// </summary>
    /// <value>
    /// 來自 mpv 的原始快取範圍節點。
    /// </value>
    public MpvNode RawNode { get; private set; }

    /// <summary>
    /// 從 mpv 節點建立快取範圍資訊。
    /// </summary>
    /// <param name="node">
    /// 代表單一快取範圍的節點。
    /// </param>
    /// <returns>
    /// 快取範圍資訊。
    /// </returns>
    internal static MpvDemuxerCacheRange FromNode(MpvNode node)
    {
        IReadOnlyDictionary<string, MpvNode> map = node.AsMap();
        return new MpvDemuxerCacheRange(
            MpvNodeReader.GetDouble(map, "start"),
            MpvNodeReader.GetDouble(map, "end"),
            node);
    }
}

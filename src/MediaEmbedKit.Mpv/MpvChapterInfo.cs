using System;
using System.Collections.Generic;
using MediaEmbedKit.Mpv.Internal;

namespace MediaEmbedKit.Mpv;

/// <summary>
/// 表示 mpv <c>chapter-list</c> 屬性中的單一章節。
/// </summary>
public sealed class MpvChapterInfo
{
    /// <summary>
    /// 初始化 <see cref="MpvChapterInfo"/> 類別的新執行個體。
    /// </summary>
    /// <param name="index">
    /// 章節索引。
    /// </param>
    /// <param name="title">
    /// 章節標題。
    /// </param>
    /// <param name="time">
    /// 章節開始時間。
    /// </param>
    /// <param name="rawNode">
    /// 原始節點資料。
    /// </param>
    internal MpvChapterInfo(int index, string? title, double? time, MpvNode rawNode)
    {
        Index = index;
        Title = title;
        Time = time;
        RawNode = rawNode ?? throw new ArgumentNullException(nameof(rawNode));
    }

    /// <summary>
    /// 取得章節索引。
    /// </summary>
    /// <value>
    /// 以 0 為起始的章節索引。
    /// </value>
    public int Index { get; private set; }

    /// <summary>
    /// 取得章節標題。
    /// </summary>
    /// <value>
    /// 章節標題；沒有資料時為 <see langword="null"/>。
    /// </value>
    public string? Title { get; private set; }

    /// <summary>
    /// 取得章節開始時間。
    /// </summary>
    /// <value>
    /// 章節開始秒數；沒有資料時為 <see langword="null"/>。
    /// </value>
    public double? Time { get; private set; }

    /// <summary>
    /// 取得原始節點資料。
    /// </summary>
    /// <value>
    /// 來自 mpv 的原始章節節點。
    /// </value>
    public MpvNode RawNode { get; private set; }

    /// <summary>
    /// 從 mpv 節點建立章節資訊。
    /// </summary>
    /// <param name="index">
    /// 章節索引。
    /// </param>
    /// <param name="node">
    /// 代表單一章節的節點。
    /// </param>
    /// <returns>
    /// 章節資訊。
    /// </returns>
    internal static MpvChapterInfo FromNode(int index, MpvNode node)
    {
        IReadOnlyDictionary<string, MpvNode> map = node.AsMap();
        return new MpvChapterInfo(
            index,
            MpvNodeReader.GetString(map, "title"),
            MpvNodeReader.GetDouble(map, "time"),
            node);
    }
}

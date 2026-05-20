using System;
using System.Collections.Generic;
using MediaEmbedKit.Mpv.Internal;

namespace MediaEmbedKit.Mpv;

/// <summary>
/// 表示 mpv <c>edition-list</c> 屬性中的單一版本。
/// </summary>
public sealed class MpvEditionInfo
{
    /// <summary>
    /// 初始化 <see cref="MpvEditionInfo"/> 類別的新執行個體。
    /// </summary>
    /// <param name="index">
    /// 版本索引。
    /// </param>
    /// <param name="id">
    /// 版本識別碼。
    /// </param>
    /// <param name="title">
    /// 版本標題。
    /// </param>
    /// <param name="defaultEdition">
    /// 版本是否為預設版本。
    /// </param>
    /// <param name="rawNode">
    /// 原始節點資料。
    /// </param>
    internal MpvEditionInfo(int index, long? id, string? title, bool defaultEdition, MpvNode rawNode)
    {
        Index = index;
        Id = id;
        Title = title;
        Default = defaultEdition;
        RawNode = rawNode ?? throw new ArgumentNullException(nameof(rawNode));
    }

    /// <summary>
    /// 取得版本索引。
    /// </summary>
    /// <value>
    /// 以 0 為起始的版本索引。
    /// </value>
    public int Index { get; private set; }

    /// <summary>
    /// 取得版本識別碼。
    /// </summary>
    /// <value>
    /// 版本識別碼；沒有資料時為 <see langword="null"/>。
    /// </value>
    public long? Id { get; private set; }

    /// <summary>
    /// 取得版本標題。
    /// </summary>
    /// <value>
    /// 版本標題；沒有資料時為 <see langword="null"/>。
    /// </value>
    public string? Title { get; private set; }

    /// <summary>
    /// 取得版本是否為預設版本。
    /// </summary>
    /// <value>
    /// 版本為預設版本時為 <see langword="true"/>。
    /// </value>
    public bool Default { get; private set; }

    /// <summary>
    /// 取得原始節點資料。
    /// </summary>
    /// <value>
    /// 來自 mpv 的原始版本節點。
    /// </value>
    public MpvNode RawNode { get; private set; }

    /// <summary>
    /// 從 mpv 節點建立版本資訊。
    /// </summary>
    /// <param name="index">
    /// 版本索引。
    /// </param>
    /// <param name="node">
    /// 代表單一版本的節點。
    /// </param>
    /// <returns>
    /// 版本資訊。
    /// </returns>
    internal static MpvEditionInfo FromNode(int index, MpvNode node)
    {
        IReadOnlyDictionary<string, MpvNode> map = node.AsMap();
        return new MpvEditionInfo(
            index,
            MpvNodeReader.GetInt64(map, "id"),
            MpvNodeReader.GetString(map, "title"),
            MpvNodeReader.GetBoolean(map, "default"),
            node);
    }
}

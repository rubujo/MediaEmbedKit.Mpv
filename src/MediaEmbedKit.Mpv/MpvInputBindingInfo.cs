using System;
using System.Collections.Generic;
using MediaEmbedKit.Mpv.Internal;

namespace MediaEmbedKit.Mpv;

/// <summary>
/// 表示 mpv <c>input-bindings</c> 屬性中的單一輸入綁定。
/// </summary>
public sealed class MpvInputBindingInfo
{
    /// <summary>
    /// 初始化 <see cref="MpvInputBindingInfo"/> 類別的新執行個體。
    /// </summary>
    /// <param name="key">綁定按鍵名稱。</param>
    /// <param name="command">綁定命令文字。</param>
    /// <param name="comment">綁定註解。</param>
    /// <param name="section">綁定所屬 section。</param>
    /// <param name="owner">綁定擁有者。</param>
    /// <param name="priority">綁定優先順序。</param>
    /// <param name="weak">綁定是否為弱綁定。</param>
    /// <param name="rawNode">原始節點資料。</param>
    internal MpvInputBindingInfo(string? key, string? command, string? comment, string? section, string? owner, long? priority, bool weak, MpvNode rawNode)
    {
        Key = key;
        Command = command;
        Comment = comment;
        Section = section;
        Owner = owner;
        Priority = priority;
        Weak = weak;
        RawNode = rawNode ?? throw new ArgumentNullException(nameof(rawNode));
    }

    /// <summary>
    /// 取得綁定按鍵名稱。
    /// </summary>
    /// <value>mpv 按鍵名稱；沒有資料時為 <see langword="null"/>。</value>
    public string? Key { get; private set; }

    /// <summary>
    /// 取得綁定命令文字。
    /// </summary>
    /// <value>mpv 命令文字；沒有資料時為 <see langword="null"/>。</value>
    public string? Command { get; private set; }

    /// <summary>
    /// 取得綁定註解。
    /// </summary>
    /// <value>綁定註解；沒有資料時為 <see langword="null"/>。</value>
    public string? Comment { get; private set; }

    /// <summary>
    /// 取得綁定所屬 section。
    /// </summary>
    /// <value>section 名稱；沒有資料時為 <see langword="null"/>。</value>
    public string? Section { get; private set; }

    /// <summary>
    /// 取得綁定擁有者。
    /// </summary>
    /// <value>擁有者名稱；沒有資料時為 <see langword="null"/>。</value>
    public string? Owner { get; private set; }

    /// <summary>
    /// 取得綁定優先順序。
    /// </summary>
    /// <value>綁定優先順序；沒有資料時為 <see langword="null"/>。</value>
    public long? Priority { get; private set; }

    /// <summary>
    /// 取得綁定是否為弱綁定。
    /// </summary>
    /// <value>綁定為弱綁定時為 <see langword="true"/>。</value>
    public bool Weak { get; private set; }

    /// <summary>
    /// 取得原始節點資料。
    /// </summary>
    /// <value>來自 mpv 的原始輸入綁定節點。</value>
    public MpvNode RawNode { get; private set; }

    /// <summary>
    /// 從 mpv 節點建立輸入綁定資訊。
    /// </summary>
    /// <param name="node">代表單一輸入綁定的節點。</param>
    /// <returns>輸入綁定資訊。</returns>
    internal static MpvInputBindingInfo FromNode(MpvNode node)
    {
        IReadOnlyDictionary<string, MpvNode> map = node.AsMap();
        return new MpvInputBindingInfo(
            MpvNodeReader.GetString(map, "key"),
            MpvNodeReader.GetString(map, "cmd"),
            MpvNodeReader.GetString(map, "comment"),
            MpvNodeReader.GetString(map, "section"),
            MpvNodeReader.GetString(map, "owner"),
            MpvNodeReader.GetInt64(map, "priority"),
            MpvNodeReader.GetBoolean(map, "is_weak"),
            node);
    }
}

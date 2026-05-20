using MediaEmbedKit.Mpv.Internal;

using System.Collections.Generic;

namespace MediaEmbedKit.Mpv;

/// <summary>
/// 表示 mpv <c>decoder-list</c> 屬性中的單一解碼器。
/// </summary>
public sealed class MpvDecoderInfo
{
    /// <summary>
    /// 初始化 <see cref="MpvDecoderInfo"/> 類別的新執行個體。
    /// </summary>
    /// <param name="codec">
    /// 解碼器支援的編解碼器名稱。
    /// </param>
    /// <param name="description">
    /// 解碼器描述。
    /// </param>
    /// <param name="driver">
    /// 解碼器驅動程式名稱。
    /// </param>
    /// <param name="rawNode">
    /// 來自 mpv 的原始節點。
    /// </param>
    internal MpvDecoderInfo(string? codec, string? description, string? driver, MpvNode rawNode)
    {
        Codec = codec;
        Description = description;
        Driver = driver;
        RawNode = rawNode;
    }

    /// <summary>
    /// 取得解碼器支援的編解碼器名稱。
    /// </summary>
    /// <value>
    /// 編解碼器名稱；沒有資料時為 <see langword="null"/>。
    /// </value>
    public string? Codec { get; private set; }

    /// <summary>
    /// 取得解碼器描述。
    /// </summary>
    /// <value>
    /// 解碼器描述；沒有資料時為 <see langword="null"/>。
    /// </value>
    public string? Description { get; private set; }

    /// <summary>
    /// 取得解碼器驅動程式名稱。
    /// </summary>
    /// <value>
    /// 解碼器驅動程式名稱；沒有資料時為 <see langword="null"/>。
    /// </value>
    public string? Driver { get; private set; }

    /// <summary>
    /// 取得來自 mpv 的原始節點。
    /// </summary>
    /// <value>
    /// 原始解碼器節點。
    /// </value>
    public MpvNode RawNode { get; private set; }

    /// <summary>
    /// 從 mpv 節點建立解碼器資訊。
    /// </summary>
    /// <param name="node">
    /// 代表單一解碼器的節點。
    /// </param>
    /// <returns>
    /// 解碼器資訊。
    /// </returns>
    internal static MpvDecoderInfo FromNode(MpvNode node)
    {
        IReadOnlyDictionary<string, MpvNode> map = node.AsMap();
        return new MpvDecoderInfo(
            MpvNodeReader.GetString(map, "codec"),
            MpvNodeReader.GetString(map, "description"),
            MpvNodeReader.GetString(map, "driver"),
            node);
    }
}

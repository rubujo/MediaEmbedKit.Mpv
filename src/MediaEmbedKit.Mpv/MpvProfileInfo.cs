using System.Collections.Generic;
using System.Collections.ObjectModel;
using MediaEmbedKit.Mpv.Internal;

namespace MediaEmbedKit.Mpv;

/// <summary>
/// 表示 mpv <c>profile-list</c> 屬性中的單一設定檔。
/// </summary>
public sealed class MpvProfileInfo
{
    /// <summary>
    /// 初始化 <see cref="MpvProfileInfo"/> 類別的新執行個體。
    /// </summary>
    /// <param name="name">設定檔名稱。</param>
    /// <param name="options">設定檔中的選項對應。</param>
    /// <param name="rawNode">來自 mpv 的原始節點。</param>
    internal MpvProfileInfo(string? name, IReadOnlyDictionary<string, string> options, MpvNode rawNode)
    {
        Name = name;
        Options = options;
        RawNode = rawNode;
    }

    /// <summary>
    /// 取得設定檔名稱。
    /// </summary>
    /// <value>設定檔名稱；沒有資料時為 <see langword="null"/>。</value>
    public string? Name { get; private set; }

    /// <summary>
    /// 取得設定檔中的選項對應。
    /// </summary>
    /// <value>選項名稱到選項值的唯讀對應。</value>
    public IReadOnlyDictionary<string, string> Options { get; private set; }

    /// <summary>
    /// 取得來自 mpv 的原始節點。
    /// </summary>
    /// <value>原始設定檔節點。</value>
    public MpvNode RawNode { get; private set; }

    /// <summary>
    /// 從 mpv 節點建立設定檔資訊。
    /// </summary>
    /// <param name="node">代表單一設定檔的節點。</param>
    /// <returns>設定檔資訊。</returns>
    internal static MpvProfileInfo FromNode(MpvNode node)
    {
        IReadOnlyDictionary<string, MpvNode> map = node.AsMap();
        Dictionary<string, string> options = new Dictionary<string, string>(System.StringComparer.Ordinal);
        MpvNode? optionList;
        if (map.TryGetValue("options", out optionList) && optionList != null)
        {
            IReadOnlyList<MpvNode> items = optionList.AsArray();
            for (int i = 0; i < items.Count; i++)
            {
                IReadOnlyDictionary<string, MpvNode> itemMap = items[i].AsMap();
                string? key = MpvNodeReader.GetString(itemMap, "key");
                if (key != null)
                {
                    options[key] = MpvNodeReader.GetString(itemMap, "value") ?? string.Empty;
                }
            }
        }

        return new MpvProfileInfo(
            MpvNodeReader.GetString(map, "name"),
            new ReadOnlyDictionary<string, string>(options),
            node);
    }
}

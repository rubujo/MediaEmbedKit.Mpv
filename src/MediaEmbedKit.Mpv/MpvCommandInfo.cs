using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MediaEmbedKit.Mpv;

/// <summary>
/// 表示 mpv <c>command-list</c> 屬性中的單一命令描述。
/// </summary>
public sealed class MpvCommandInfo
{
    /// <summary>
    /// 初始化 <see cref="MpvCommandInfo"/> 類別的新執行個體。
    /// </summary>
    /// <param name="name">
    /// 命令名稱。
    /// </param>
    /// <param name="vararg">
    /// 命令是否接受可變數量引數。
    /// </param>
    /// <param name="arguments">
    /// 命令引數描述集合。
    /// </param>
    /// <param name="rawNode">
    /// 原始節點資料。
    /// </param>
    internal MpvCommandInfo(string name, bool vararg, IReadOnlyList<MpvCommandArgumentInfo> arguments, MpvNode rawNode)
    {
        Name = name;
        Vararg = vararg;
        Arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
        RawNode = rawNode ?? throw new ArgumentNullException(nameof(rawNode));
    }

    /// <summary>
    /// 取得命令名稱。
    /// </summary>
    /// <value>
    /// mpv 命令名稱。
    /// </value>
    public string Name { get; private set; }

    /// <summary>
    /// 取得命令是否接受可變數量引數。
    /// </summary>
    /// <value>
    /// 命令接受可變數量引數時為 <see langword="true"/>。
    /// </value>
    public bool Vararg { get; private set; }

    /// <summary>
    /// 取得命令引數描述集合。
    /// </summary>
    /// <value>
    /// 命令引數描述集合。
    /// </value>
    public IReadOnlyList<MpvCommandArgumentInfo> Arguments { get; private set; }

    /// <summary>
    /// 取得原始節點資料。
    /// </summary>
    /// <value>
    /// 來自 mpv 的原始命令描述節點。
    /// </value>
    public MpvNode RawNode { get; private set; }

    /// <summary>
    /// 從 mpv 節點建立命令描述。
    /// </summary>
    /// <param name="node">
    /// 代表單一命令描述的節點。
    /// </param>
    /// <returns>
    /// 命令描述。
    /// </returns>
    internal static MpvCommandInfo FromNode(MpvNode node)
    {
        IReadOnlyDictionary<string, MpvNode> map = node.AsMap();
        MpvNode? argsNode;
        List<MpvCommandArgumentInfo> arguments = new List<MpvCommandArgumentInfo>();
        if (map.TryGetValue("args", out argsNode) && argsNode != null)
        {
            foreach (MpvNode argumentNode in argsNode.AsArray())
            {
                IReadOnlyDictionary<string, MpvNode> argumentMap = argumentNode.AsMap();
                arguments.Add(new MpvCommandArgumentInfo(
                    GetString(argumentMap, "name") ?? string.Empty,
                    GetString(argumentMap, "type") ?? string.Empty,
                    GetFlag(argumentMap, "optional")));
            }
        }

        return new MpvCommandInfo(
            GetString(map, "name") ?? string.Empty,
            GetFlag(map, "vararg"),
            new ReadOnlyCollection<MpvCommandArgumentInfo>(arguments),
            node);
    }

    /// <summary>
    /// 從節點對應讀取字串欄位。
    /// </summary>
    /// <param name="map">
    /// 節點對應。
    /// </param>
    /// <param name="key">
    /// 欄位索引鍵。
    /// </param>
    /// <returns>
    /// 欄位字串值；沒有值時為 <see langword="null"/>。
    /// </returns>
    private static string? GetString(IReadOnlyDictionary<string, MpvNode> map, string key)
    {
        MpvNode? value;
        return map.TryGetValue(key, out value) && value != null ? value.AsString() : null;
    }

    /// <summary>
    /// 從節點對應讀取布林欄位。
    /// </summary>
    /// <param name="map">
    /// 節點對應。
    /// </param>
    /// <param name="key">
    /// 欄位索引鍵。
    /// </param>
    /// <returns>
    /// 欄位布林值；沒有值時為 <see langword="false"/>。
    /// </returns>
    private static bool GetFlag(IReadOnlyDictionary<string, MpvNode> map, string key)
    {
        MpvNode? value;
        return map.TryGetValue(key, out value) && value != null && value.AsBoolean();
    }
}

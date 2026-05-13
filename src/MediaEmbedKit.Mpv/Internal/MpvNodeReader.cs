using System.Collections.Generic;

namespace MediaEmbedKit.Mpv.Internal;

/// <summary>
/// 提供從 <see cref="MpvNode"/> 對應讀取常用型別的輔助方法。
/// </summary>
internal static class MpvNodeReader
{
    /// <summary>
    /// 從節點對應讀取字串欄位。
    /// </summary>
    /// <param name="map">節點對應。</param>
    /// <param name="key">欄位索引鍵。</param>
    /// <returns>欄位字串值；沒有值時為 <see langword="null"/>。</returns>
    public static string? GetString(IReadOnlyDictionary<string, MpvNode> map, string key)
    {
        MpvNode? value;
        return map.TryGetValue(key, out value) && value != null ? value.AsString() : null;
    }

    /// <summary>
    /// 從節點對應讀取布林欄位。
    /// </summary>
    /// <param name="map">節點對應。</param>
    /// <param name="key">欄位索引鍵。</param>
    /// <returns>欄位布林值；沒有值時為 <see langword="false"/>。</returns>
    public static bool GetBoolean(IReadOnlyDictionary<string, MpvNode> map, string key)
    {
        MpvNode? value;
        return map.TryGetValue(key, out value) && value != null && value.AsBoolean();
    }

    /// <summary>
    /// 從節點對應讀取整數欄位。
    /// </summary>
    /// <param name="map">節點對應。</param>
    /// <param name="key">欄位索引鍵。</param>
    /// <returns>欄位整數值；沒有值時為 <see langword="null"/>。</returns>
    public static long? GetInt64(IReadOnlyDictionary<string, MpvNode> map, string key)
    {
        MpvNode? value;
        if (!map.TryGetValue(key, out value) || value == null || value.Format != MpvFormat.Int64)
        {
            return null;
        }

        return value.AsInt64();
    }

    /// <summary>
    /// 從節點對應讀取雙精確度浮點數欄位。
    /// </summary>
    /// <param name="map">節點對應。</param>
    /// <param name="key">欄位索引鍵。</param>
    /// <returns>欄位浮點數值；沒有值時為 <see langword="null"/>。</returns>
    public static double? GetDouble(IReadOnlyDictionary<string, MpvNode> map, string key)
    {
        MpvNode? value;
        if (!map.TryGetValue(key, out value) || value == null)
        {
            return null;
        }

        if (value.Format == MpvFormat.Double)
        {
            return value.AsDouble();
        }

        if (value.Format == MpvFormat.Int64)
        {
            return value.AsInt64();
        }

        return null;
    }
}

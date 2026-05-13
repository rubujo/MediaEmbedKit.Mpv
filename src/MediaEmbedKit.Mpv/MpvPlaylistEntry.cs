using System;
using System.Collections.Generic;

namespace MediaEmbedKit.Mpv;

/// <summary>
/// 表示 mpv 播放清單中的單一項目。
/// </summary>
public sealed class MpvPlaylistEntry
{
    /// <summary>
    /// 初始化 <see cref="MpvPlaylistEntry"/> 類別的新執行個體。
    /// </summary>
    /// <param name="index">播放清單中的索引位置。</param>
    /// <param name="id">mpv 指派的播放清單項目識別碼。</param>
    /// <param name="filename">播放清單項目檔案名稱或網址。</param>
    /// <param name="title">播放清單項目標題。</param>
    /// <param name="playlistPath">項目來源播放清單路徑。</param>
    /// <param name="playing">項目目前是否正在播放或載入。</param>
    /// <param name="current">項目目前是否為選取項目。</param>
    /// <param name="rawNode">原始節點資料。</param>
    internal MpvPlaylistEntry(
        int index,
        long? id,
        string? filename,
        string? title,
        string? playlistPath,
        bool playing,
        bool current,
        MpvNode rawNode)
    {
        Index = index;
        Id = id;
        Filename = filename;
        Title = title;
        PlaylistPath = playlistPath;
        Playing = playing;
        Current = current;
        RawNode = rawNode ?? throw new ArgumentNullException(nameof(rawNode));
    }

    /// <summary>
    /// 取得播放清單中的索引位置。
    /// </summary>
    /// <value>以 0 為起始的索引位置。</value>
    public int Index { get; private set; }

    /// <summary>
    /// 取得 mpv 指派的播放清單項目識別碼。
    /// </summary>
    /// <value>播放清單項目識別碼；沒有資料時為 <see langword="null"/>。</value>
    public long? Id { get; private set; }

    /// <summary>
    /// 取得播放清單項目檔案名稱或網址。
    /// </summary>
    /// <value>檔案名稱或網址；沒有資料時為 <see langword="null"/>。</value>
    public string? Filename { get; private set; }

    /// <summary>
    /// 取得播放清單項目標題。
    /// </summary>
    /// <value>播放清單項目標題；沒有資料時為 <see langword="null"/>。</value>
    public string? Title { get; private set; }

    /// <summary>
    /// 取得項目來源播放清單路徑。
    /// </summary>
    /// <value>來源播放清單路徑；沒有資料時為 <see langword="null"/>。</value>
    public string? PlaylistPath { get; private set; }

    /// <summary>
    /// 取得項目目前是否正在播放或載入。
    /// </summary>
    /// <value>項目正在播放或載入時為 <see langword="true"/>。</value>
    public bool Playing { get; private set; }

    /// <summary>
    /// 取得項目目前是否為選取項目。
    /// </summary>
    /// <value>項目為目前選取項目時為 <see langword="true"/>。</value>
    public bool Current { get; private set; }

    /// <summary>
    /// 取得原始節點資料。
    /// </summary>
    /// <value>來自 mpv 的原始播放清單節點。</value>
    public MpvNode RawNode { get; private set; }

    /// <summary>
    /// 從 mpv 節點建立播放清單項目。
    /// </summary>
    /// <param name="index">播放清單中的索引位置。</param>
    /// <param name="node">代表單一播放清單項目的節點。</param>
    /// <returns>播放清單項目。</returns>
    internal static MpvPlaylistEntry FromNode(int index, MpvNode node)
    {
        IReadOnlyDictionary<string, MpvNode> map = node.AsMap();
        return new MpvPlaylistEntry(
            index,
            GetInt64(map, "id"),
            GetString(map, "filename"),
            GetString(map, "title"),
            GetString(map, "playlist-path"),
            GetFlag(map, "playing"),
            GetFlag(map, "current"),
            node);
    }

    /// <summary>
    /// 從節點對應讀取字串欄位。
    /// </summary>
    /// <param name="map">節點對應。</param>
    /// <param name="key">欄位索引鍵。</param>
    /// <returns>欄位字串值；沒有值時為 <see langword="null"/>。</returns>
    private static string? GetString(IReadOnlyDictionary<string, MpvNode> map, string key)
    {
        MpvNode? value;
        return map.TryGetValue(key, out value) && value != null ? value.AsString() : null;
    }

    /// <summary>
    /// 從節點對應讀取整數欄位。
    /// </summary>
    /// <param name="map">節點對應。</param>
    /// <param name="key">欄位索引鍵。</param>
    /// <returns>欄位整數值；沒有值時為 <see langword="null"/>。</returns>
    private static long? GetInt64(IReadOnlyDictionary<string, MpvNode> map, string key)
    {
        MpvNode? value;
        return map.TryGetValue(key, out value) && value != null && value.Format == MpvFormat.Int64 ? value.AsInt64() : (long?)null;
    }

    /// <summary>
    /// 從節點對應讀取布林欄位。
    /// </summary>
    /// <param name="map">節點對應。</param>
    /// <param name="key">欄位索引鍵。</param>
    /// <returns>欄位布林值；沒有值時為 <see langword="false"/>。</returns>
    private static bool GetFlag(IReadOnlyDictionary<string, MpvNode> map, string key)
    {
        MpvNode? value;
        return map.TryGetValue(key, out value) && value != null && value.AsBoolean();
    }
}

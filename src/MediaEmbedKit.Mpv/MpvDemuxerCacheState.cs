using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using MediaEmbedKit.Mpv.Internal;

namespace MediaEmbedKit.Mpv;

/// <summary>
/// 表示 mpv <c>demuxer-cache-state</c> 屬性的快取狀態。
/// </summary>
public sealed class MpvDemuxerCacheState
{
    /// <summary>
    /// 初始化 <see cref="MpvDemuxerCacheState"/> 類別的新執行個體。
    /// </summary>
    /// <param name="seekable">
    /// 目前串流是否可搜尋。
    /// </param>
    /// <param name="endOfFile">
    /// demuxer 是否已到達檔案結尾。
    /// </param>
    /// <param name="underrun">
    /// 快取是否不足。
    /// </param>
    /// <param name="idle">
    /// demuxer 是否閒置。
    /// </param>
    /// <param name="totalBytes">
    /// 快取總位元組數。
    /// </param>
    /// <param name="forwardBytes">
    /// 前向快取位元組數。
    /// </param>
    /// <param name="fileCacheBytes">
    /// 檔案快取位元組數。
    /// </param>
    /// <param name="cacheDuration">
    /// 快取秒數。
    /// </param>
    /// <param name="rawInputRate">
    /// 原始輸入速率。
    /// </param>
    /// <param name="readerPosition">
    /// 讀取器目前時間。
    /// </param>
    /// <param name="ranges">
    /// 快取範圍集合。
    /// </param>
    /// <param name="rawNode">
    /// 原始節點資料。
    /// </param>
    internal MpvDemuxerCacheState(
        bool seekable,
        bool endOfFile,
        bool underrun,
        bool idle,
        long? totalBytes,
        long? forwardBytes,
        long? fileCacheBytes,
        double? cacheDuration,
        long? rawInputRate,
        double? readerPosition,
        IReadOnlyList<MpvDemuxerCacheRange> ranges,
        MpvNode rawNode)
    {
        Seekable = seekable;
        EndOfFile = endOfFile;
        Underrun = underrun;
        Idle = idle;
        TotalBytes = totalBytes;
        ForwardBytes = forwardBytes;
        FileCacheBytes = fileCacheBytes;
        CacheDuration = cacheDuration;
        RawInputRate = rawInputRate;
        ReaderPosition = readerPosition;
        Ranges = ranges ?? throw new ArgumentNullException(nameof(ranges));
        RawNode = rawNode ?? throw new ArgumentNullException(nameof(rawNode));
    }

    /// <summary>
    /// 取得目前串流是否可搜尋。
    /// </summary>
    /// <value>
    /// 目前串流可搜尋時為 <see langword="true"/>。
    /// </value>
    public bool Seekable { get; private set; }

    /// <summary>
    /// 取得 demuxer 是否已到達檔案結尾。
    /// </summary>
    /// <value>
    /// demuxer 到達檔案結尾時為 <see langword="true"/>。
    /// </value>
    public bool EndOfFile { get; private set; }

    /// <summary>
    /// 取得快取是否不足。
    /// </summary>
    /// <value>
    /// 快取不足時為 <see langword="true"/>。
    /// </value>
    public bool Underrun { get; private set; }

    /// <summary>
    /// 取得 demuxer 是否閒置。
    /// </summary>
    /// <value>
    /// demuxer 閒置時為 <see langword="true"/>。
    /// </value>
    public bool Idle { get; private set; }

    /// <summary>
    /// 取得快取總位元組數。
    /// </summary>
    /// <value>
    /// 快取總位元組數；沒有資料時為 <see langword="null"/>。
    /// </value>
    public long? TotalBytes { get; private set; }

    /// <summary>
    /// 取得前向快取位元組數。
    /// </summary>
    /// <value>
    /// 前向快取位元組數；沒有資料時為 <see langword="null"/>。
    /// </value>
    public long? ForwardBytes { get; private set; }

    /// <summary>
    /// 取得檔案快取位元組數。
    /// </summary>
    /// <value>
    /// 檔案快取位元組數；沒有資料時為 <see langword="null"/>。
    /// </value>
    public long? FileCacheBytes { get; private set; }

    /// <summary>
    /// 取得快取秒數。
    /// </summary>
    /// <value>
    /// 快取秒數；沒有資料時為 <see langword="null"/>。
    /// </value>
    public double? CacheDuration { get; private set; }

    /// <summary>
    /// 取得原始輸入速率。
    /// </summary>
    /// <value>
    /// 原始輸入速率；沒有資料時為 <see langword="null"/>。
    /// </value>
    public long? RawInputRate { get; private set; }

    /// <summary>
    /// 取得讀取器目前時間。
    /// </summary>
    /// <value>
    /// 讀取器目前秒數；沒有資料時為 <see langword="null"/>。
    /// </value>
    public double? ReaderPosition { get; private set; }

    /// <summary>
    /// 取得快取範圍集合。
    /// </summary>
    /// <value>
    /// 目前 demuxer 快取範圍。
    /// </value>
    public IReadOnlyList<MpvDemuxerCacheRange> Ranges { get; private set; }

    /// <summary>
    /// 取得原始節點資料。
    /// </summary>
    /// <value>
    /// 來自 mpv 的原始快取狀態節點。
    /// </value>
    public MpvNode RawNode { get; private set; }

    /// <summary>
    /// 從 mpv 節點建立快取狀態。
    /// </summary>
    /// <param name="node">
    /// 代表 demuxer 快取狀態的節點。
    /// </param>
    /// <returns>
    /// 快取狀態。
    /// </returns>
    internal static MpvDemuxerCacheState FromNode(MpvNode node)
    {
        IReadOnlyDictionary<string, MpvNode> map = node.AsMap();
        List<MpvDemuxerCacheRange> ranges = new List<MpvDemuxerCacheRange>();
        MpvNode rangesNode = node.GetValueOrNone("ranges");
        foreach (MpvNode rangeNode in rangesNode.AsArray())
        {
            ranges.Add(MpvDemuxerCacheRange.FromNode(rangeNode));
        }

        return new MpvDemuxerCacheState(
            MpvNodeReader.GetBoolean(map, "seekable"),
            MpvNodeReader.GetBoolean(map, "eof"),
            MpvNodeReader.GetBoolean(map, "underrun"),
            MpvNodeReader.GetBoolean(map, "idle"),
            MpvNodeReader.GetInt64(map, "total-bytes"),
            MpvNodeReader.GetInt64(map, "fw-bytes"),
            MpvNodeReader.GetInt64(map, "file-cache-bytes"),
            MpvNodeReader.GetDouble(map, "cache-duration"),
            MpvNodeReader.GetInt64(map, "raw-input-rate"),
            MpvNodeReader.GetDouble(map, "reader-pts"),
            new ReadOnlyCollection<MpvDemuxerCacheRange>(ranges),
            node);
    }
}

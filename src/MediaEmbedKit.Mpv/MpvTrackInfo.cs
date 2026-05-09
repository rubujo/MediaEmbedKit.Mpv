using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MediaEmbedKit.Mpv
{
    /// <summary>
    /// 表示 mpv <c>track-list</c> 屬性中的單一播放軌。
    /// </summary>
    public sealed class MpvTrackInfo
    {
        /// <summary>
        /// 初始化 <see cref="MpvTrackInfo"/> 類別的新執行個體。
        /// </summary>
        /// <param name="id">mpv 指派的播放軌識別碼。</param>
        /// <param name="type">播放軌類型。</param>
        /// <param name="sourceId">解封裝器來源播放軌識別碼。</param>
        /// <param name="title">播放軌標題。</param>
        /// <param name="language">播放軌語言代碼。</param>
        /// <param name="codec">播放軌編解碼器名稱。</param>
        /// <param name="externalFilename">外部播放軌檔案名稱。</param>
        /// <param name="selected">播放軌目前是否被選取。</param>
        /// <param name="external">播放軌是否來自外部檔案。</param>
        /// <param name="defaultTrack">播放軌是否被標示為預設播放軌。</param>
        /// <param name="forced">播放軌是否被標示為強制播放軌。</param>
        /// <param name="image">播放軌是否為影像。</param>
        /// <param name="albumArt">播放軌是否為專輯封面。</param>
        /// <param name="hearingImpaired">播放軌是否標示為聽覺障礙輔助內容。</param>
        /// <param name="visualImpaired">播放軌是否標示為視覺障礙輔助內容。</param>
        /// <param name="dependent">播放軌是否依賴其他播放軌。</param>
        /// <param name="metadata">播放軌中繼資料。</param>
        /// <param name="rawNode">原始節點資料。</param>
        internal MpvTrackInfo(
            long id,
            MpvTrackType type,
            long? sourceId,
            string? title,
            string? language,
            string? codec,
            string? externalFilename,
            bool selected,
            bool external,
            bool defaultTrack,
            bool forced,
            bool image,
            bool albumArt,
            bool hearingImpaired,
            bool visualImpaired,
            bool dependent,
            IReadOnlyDictionary<string, string> metadata,
            MpvNode rawNode)
        {
            Id = id;
            Type = type;
            SourceId = sourceId;
            Title = title;
            Language = language;
            Codec = codec;
            ExternalFilename = externalFilename;
            Selected = selected;
            External = external;
            Default = defaultTrack;
            Forced = forced;
            Image = image;
            AlbumArt = albumArt;
            HearingImpaired = hearingImpaired;
            VisualImpaired = visualImpaired;
            Dependent = dependent;
            Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            RawNode = rawNode ?? throw new ArgumentNullException(nameof(rawNode));
        }

        /// <summary>
        /// 取得 mpv 指派的播放軌識別碼。
        /// </summary>
        /// <value>播放軌識別碼。</value>
        public long Id { get; private set; }

        /// <summary>
        /// 取得播放軌類型。
        /// </summary>
        /// <value>播放軌類型。</value>
        public MpvTrackType Type { get; private set; }

        /// <summary>
        /// 取得解封裝器來源播放軌識別碼。
        /// </summary>
        /// <value>來源播放軌識別碼；沒有資料時為 <see langword="null"/>。</value>
        public long? SourceId { get; private set; }

        /// <summary>
        /// 取得播放軌標題。
        /// </summary>
        /// <value>播放軌標題；沒有資料時為 <see langword="null"/>。</value>
        public string? Title { get; private set; }

        /// <summary>
        /// 取得播放軌語言代碼。
        /// </summary>
        /// <value>播放軌語言代碼；沒有資料時為 <see langword="null"/>。</value>
        public string? Language { get; private set; }

        /// <summary>
        /// 取得播放軌編解碼器名稱。
        /// </summary>
        /// <value>播放軌編解碼器名稱；沒有資料時為 <see langword="null"/>。</value>
        public string? Codec { get; private set; }

        /// <summary>
        /// 取得外部播放軌檔案名稱。
        /// </summary>
        /// <value>外部檔案名稱；不是外部播放軌或沒有資料時為 <see langword="null"/>。</value>
        public string? ExternalFilename { get; private set; }

        /// <summary>
        /// 取得播放軌目前是否被選取。
        /// </summary>
        /// <value>播放軌目前被選取時為 <see langword="true"/>。</value>
        public bool Selected { get; private set; }

        /// <summary>
        /// 取得播放軌是否來自外部檔案。
        /// </summary>
        /// <value>播放軌來自外部檔案時為 <see langword="true"/>。</value>
        public bool External { get; private set; }

        /// <summary>
        /// 取得播放軌是否被標示為預設播放軌。
        /// </summary>
        /// <value>播放軌被標示為預設時為 <see langword="true"/>。</value>
        public bool Default { get; private set; }

        /// <summary>
        /// 取得播放軌是否被標示為強制播放軌。
        /// </summary>
        /// <value>播放軌被標示為強制時為 <see langword="true"/>。</value>
        public bool Forced { get; private set; }

        /// <summary>
        /// 取得播放軌是否為影像。
        /// </summary>
        /// <value>播放軌為影像時為 <see langword="true"/>。</value>
        public bool Image { get; private set; }

        /// <summary>
        /// 取得播放軌是否為專輯封面。
        /// </summary>
        /// <value>播放軌為專輯封面時為 <see langword="true"/>。</value>
        public bool AlbumArt { get; private set; }

        /// <summary>
        /// 取得播放軌是否標示為聽覺障礙輔助內容。
        /// </summary>
        /// <value>播放軌標示為聽覺障礙輔助內容時為 <see langword="true"/>。</value>
        public bool HearingImpaired { get; private set; }

        /// <summary>
        /// 取得播放軌是否標示為視覺障礙輔助內容。
        /// </summary>
        /// <value>播放軌標示為視覺障礙輔助內容時為 <see langword="true"/>。</value>
        public bool VisualImpaired { get; private set; }

        /// <summary>
        /// 取得播放軌是否依賴其他播放軌。
        /// </summary>
        /// <value>播放軌依賴其他播放軌時為 <see langword="true"/>。</value>
        public bool Dependent { get; private set; }

        /// <summary>
        /// 取得播放軌中繼資料。
        /// </summary>
        /// <value>播放軌中繼資料字典。</value>
        public IReadOnlyDictionary<string, string> Metadata { get; private set; }

        /// <summary>
        /// 取得原始節點資料。
        /// </summary>
        /// <value>來自 mpv 的原始播放軌節點。</value>
        public MpvNode RawNode { get; private set; }

        /// <summary>
        /// 從 mpv 節點建立播放軌資訊。
        /// </summary>
        /// <param name="node">代表單一播放軌的節點。</param>
        /// <returns>播放軌資訊。</returns>
        internal static MpvTrackInfo FromNode(MpvNode node)
        {
            IReadOnlyDictionary<string, MpvNode> map = node.AsMap();
            long id = GetInt64(map, "id") ?? 0;
            MpvTrackType type = ParseTrackType(GetString(map, "type"));
            IReadOnlyDictionary<string, string> metadata = ReadMetadata(map);

            return new MpvTrackInfo(
                id,
                type,
                GetInt64(map, "src-id"),
                GetString(map, "title"),
                GetString(map, "lang"),
                GetString(map, "codec"),
                GetString(map, "external-filename"),
                GetFlag(map, "selected"),
                GetFlag(map, "external"),
                GetFlag(map, "default"),
                GetFlag(map, "forced"),
                GetFlag(map, "image"),
                GetFlag(map, "albumart"),
                GetFlag(map, "hearing-impaired"),
                GetFlag(map, "visual-impaired"),
                GetFlag(map, "dependent"),
                metadata,
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
            if (!map.TryGetValue(key, out value) || value == null || value.Format != MpvFormat.Int64)
            {
                return null;
            }

            return value.AsInt64();
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

        /// <summary>
        /// 將 mpv 播放軌類型字串轉換為列舉。
        /// </summary>
        /// <param name="type">mpv 播放軌類型字串。</param>
        /// <returns>播放軌類型列舉值。</returns>
        private static MpvTrackType ParseTrackType(string? type)
        {
            switch (type)
            {
                case "video":
                    return MpvTrackType.Video;
                case "audio":
                    return MpvTrackType.Audio;
                case "sub":
                    return MpvTrackType.Subtitle;
                default:
                    return MpvTrackType.Unknown;
            }
        }

        /// <summary>
        /// 從節點對應讀取播放軌中繼資料。
        /// </summary>
        /// <param name="map">節點對應。</param>
        /// <returns>播放軌中繼資料字典。</returns>
        private static IReadOnlyDictionary<string, string> ReadMetadata(IReadOnlyDictionary<string, MpvNode> map)
        {
            MpvNode? metadataNode;
            if (!map.TryGetValue("metadata", out metadataNode) || metadataNode == null)
            {
                return new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
            }

            Dictionary<string, string> metadata = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, MpvNode> item in metadataNode.AsMap())
            {
                metadata[item.Key] = item.Value.AsString() ?? string.Empty;
            }

            return new ReadOnlyDictionary<string, string>(metadata);
        }
    }
}

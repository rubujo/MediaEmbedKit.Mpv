using System;
using System.Collections.Generic;
using System.Globalization;

namespace MediaEmbedKit.Mpv
{
    /// <summary>
    /// 提供 <see cref="MpvPlayer.Load(MpvMediaItem, MpvLoadFileMode)"/> 使用的播放項目描述。
    /// 透過此型別，呼叫端可以對單一媒體指定 HTTP 標頭、起訖時間與每檔 mpv 選項，不影響其他播放項目。
    /// </summary>
    public sealed class MpvMediaItem
    {
        /// <summary>
        /// 初始化 <see cref="MpvMediaItem"/> 類別的新執行個體。
        /// </summary>
        /// <param name="source">要載入的檔案路徑或媒體網址。</param>
        public MpvMediaItem(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                throw new ArgumentException("媒體來源不可為空白。", nameof(source));
            }

            Source = source;
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Options = new Dictionary<string, string>(StringComparer.Ordinal);
        }

        /// <summary>
        /// 取得要載入的檔案路徑或媒體網址。
        /// </summary>
        /// <value>媒體來源。</value>
        public string Source { get; }

        /// <summary>
        /// 取得或設定播放此媒體時要套用的起始時間。
        /// </summary>
        /// <value>起始時間；未指定時為 <see langword="null"/>。</value>
        public TimeSpan? StartTime { get; set; }

        /// <summary>
        /// 取得或設定播放此媒體時要套用的結束時間。
        /// </summary>
        /// <value>結束時間；未指定時為 <see langword="null"/>。</value>
        public TimeSpan? EndTime { get; set; }

        /// <summary>
        /// 取得只在播放此媒體時要送給來源的 HTTP 標頭集合。
        /// </summary>
        /// <value>HTTP 標頭集合；對應 mpv 的 <c>http-header-fields</c> 選項。</value>
        public IDictionary<string, string> Headers { get; }

        /// <summary>
        /// 取得只在播放此媒體時要套用的任意 mpv 選項集合。
        /// </summary>
        /// <value>mpv 選項集合；對應 mpv <c>loadfile</c> 命令的 per-file options 參數。</value>
        public IDictionary<string, string> Options { get; }

        /// <summary>
        /// 取得或設定只在播放此媒體時要套用的 yt-dlp 格式預設值。
        /// </summary>
        /// <value>yt-dlp 格式預設值；未指定時為 <see langword="null"/>。</value>
        public MpvYtdlpFormatPreset? YtdlpFormatPreset { get; set; }

        /// <summary>
        /// 取得或設定只在播放此媒體時要套用的 yt-dlp 格式 selector。
        /// </summary>
        /// <value>yt-dlp 格式 selector；未指定時為 <see langword="null"/>。</value>
        public string? YtdlpFormat { get; set; }

        /// <summary>
        /// 將此 <see cref="MpvMediaItem"/> 的設定整理成可餵給 mpv <c>loadfile</c> 命令的 per-file 選項字典。
        /// </summary>
        /// <returns>包含起訖時間、HTTP 標頭、yt-dlp 格式與額外 mpv 選項的字典。</returns>
        public IDictionary<string, string> BuildFileOptions()
        {
            Dictionary<string, string> options = new Dictionary<string, string>(StringComparer.Ordinal);
            if (StartTime.HasValue)
            {
                options["start"] = FormatSeconds(StartTime.Value);
            }

            if (EndTime.HasValue)
            {
                options["end"] = FormatSeconds(EndTime.Value);
            }

            if (Headers.Count > 0)
            {
                List<string> headerEntries = new List<string>(Headers.Count);
                foreach (KeyValuePair<string, string> header in Headers)
                {
                    if (string.IsNullOrWhiteSpace(header.Key))
                    {
                        continue;
                    }

                    headerEntries.Add(header.Key + ": " + (header.Value ?? string.Empty));
                }

                options["http-header-fields"] = string.Join("\r\n", headerEntries);
            }

            string? ytdlpSelector = ResolveYtdlpFormatSelector();
            if (ytdlpSelector != null)
            {
                options["ytdl-format"] = ytdlpSelector;
            }

            foreach (KeyValuePair<string, string> option in Options)
            {
                if (string.IsNullOrWhiteSpace(option.Key))
                {
                    continue;
                }

                options[option.Key] = option.Value ?? string.Empty;
            }

            return options;
        }

        /// <summary>
        /// 將 <see cref="TimeSpan"/> 格式化為 mpv 可接受的秒數文字。
        /// </summary>
        /// <param name="value">要格式化的時間。</param>
        /// <returns>以秒為單位、不含千分位的字串。</returns>
        private static string FormatSeconds(TimeSpan value)
        {
            return value.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 解析最終要套用的 yt-dlp selector。
        /// </summary>
        /// <returns>解析後的 selector；未指定時為 <see langword="null"/>。</returns>
        private string? ResolveYtdlpFormatSelector()
        {
            if (!string.IsNullOrWhiteSpace(YtdlpFormat))
            {
                return YtdlpFormat;
            }

            if (YtdlpFormatPreset.HasValue)
            {
                return MpvYtdlpFormatSelector.FromPreset(YtdlpFormatPreset.Value);
            }

            return null;
        }
    }
}

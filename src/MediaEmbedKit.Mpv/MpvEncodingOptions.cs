using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MediaEmbedKit.Mpv
{
    /// <summary>
    /// 提供 mpv encoding mode 使用的高階選項。
    /// </summary>
    public sealed class MpvEncodingOptions
    {
        /// <summary>
        /// 初始化 <see cref="MpvEncodingOptions"/> 類別的新執行個體。
        /// </summary>
        /// <param name="outputPath">輸出檔案路徑。</param>
        public MpvEncodingOptions(string outputPath)
        {
            OutputPath = outputPath;
            AdditionalOptions = new Dictionary<string, string>(StringComparer.Ordinal);
        }

        /// <summary>
        /// 建立輸出到指定檔案的 encoding mode 選項。
        /// </summary>
        /// <param name="outputPath">輸出檔案路徑。</param>
        /// <returns>已設定輸出路徑的 encoding mode 選項。</returns>
        public static MpvEncodingOptions ToFile(string outputPath)
        {
            return new MpvEncodingOptions(outputPath);
        }

        /// <summary>
        /// 指定輸出容器格式。
        /// </summary>
        /// <param name="containerFormat">mpv <c>of</c> 選項使用的容器格式。</param>
        /// <returns>目前的 encoding mode 選項。</returns>
        public MpvEncodingOptions AsContainer(string containerFormat)
        {
            if (string.IsNullOrWhiteSpace(containerFormat))
            {
                throw new ArgumentException("輸出容器格式不可為空白。", nameof(containerFormat));
            }

            ContainerFormat = containerFormat;
            return this;
        }

        /// <summary>
        /// 將輸出容器格式設定為 MP4。
        /// </summary>
        /// <returns>目前的 encoding mode 選項。</returns>
        public MpvEncodingOptions AsMp4()
        {
            return AsContainer("mp4");
        }

        /// <summary>
        /// 指定輸出容器格式參數。
        /// </summary>
        /// <param name="containerOptions">mpv <c>ofopts</c> 選項使用的 libavformat 參數字串。</param>
        /// <returns>目前的 encoding mode 選項。</returns>
        public MpvEncodingOptions WithContainerOptions(string containerOptions)
        {
            if (string.IsNullOrWhiteSpace(containerOptions))
            {
                throw new ArgumentException("輸出容器格式參數不可為空白。", nameof(containerOptions));
            }

            ContainerFormatOptions = containerOptions;
            return this;
        }

        /// <summary>
        /// 指定輸出視訊編碼器。
        /// </summary>
        /// <param name="codecName">mpv <c>ovc</c> 選項使用的視訊編碼器名稱。</param>
        /// <returns>目前的 encoding mode 選項。</returns>
        public MpvEncodingOptions WithVideoCodec(string codecName)
        {
            if (string.IsNullOrWhiteSpace(codecName))
            {
                throw new ArgumentException("視訊編碼器名稱不可為空白。", nameof(codecName));
            }

            VideoCodec = codecName;
            VideoCodecOptions = null;
            return this;
        }

        /// <summary>
        /// 指定輸出視訊編碼器與參數。
        /// </summary>
        /// <param name="codecName">mpv <c>ovc</c> 選項使用的視訊編碼器名稱。</param>
        /// <param name="codecOptions">mpv <c>ovcopts</c> 選項使用的 libavcodec 視訊參數字串。</param>
        /// <returns>目前的 encoding mode 選項。</returns>
        public MpvEncodingOptions WithVideoCodec(string codecName, string codecOptions)
        {
            WithVideoCodec(codecName);
            if (string.IsNullOrWhiteSpace(codecOptions))
            {
                throw new ArgumentException("視訊編碼器參數不可為空白。", nameof(codecOptions));
            }

            VideoCodecOptions = codecOptions;
            return this;
        }

        /// <summary>
        /// 指定輸出音訊編碼器。
        /// </summary>
        /// <param name="codecName">mpv <c>oac</c> 選項使用的音訊編碼器名稱。</param>
        /// <returns>目前的 encoding mode 選項。</returns>
        public MpvEncodingOptions WithAudioCodec(string codecName)
        {
            if (string.IsNullOrWhiteSpace(codecName))
            {
                throw new ArgumentException("音訊編碼器名稱不可為空白。", nameof(codecName));
            }

            AudioCodec = codecName;
            AudioCodecOptions = null;
            return this;
        }

        /// <summary>
        /// 指定輸出音訊編碼器與參數。
        /// </summary>
        /// <param name="codecName">mpv <c>oac</c> 選項使用的音訊編碼器名稱。</param>
        /// <param name="codecOptions">mpv <c>oacopts</c> 選項使用的 libavcodec 音訊參數字串。</param>
        /// <returns>目前的 encoding mode 選項。</returns>
        public MpvEncodingOptions WithAudioCodec(string codecName, string codecOptions)
        {
            WithAudioCodec(codecName);
            if (string.IsNullOrWhiteSpace(codecOptions))
            {
                throw new ArgumentException("音訊編碼器參數不可為空白。", nameof(codecOptions));
            }

            AudioCodecOptions = codecOptions;
            return this;
        }

        /// <summary>
        /// 設定是否保留輸入時間戳記。
        /// </summary>
        /// <param name="enabled">需要保留輸入時間戳記時為 <see langword="true"/>。</param>
        /// <returns>目前的 encoding mode 選項。</returns>
        public MpvEncodingOptions CopyInputTimestamps(bool enabled = true)
        {
            CopyRawTimestamps = enabled;
            return this;
        }

        /// <summary>
        /// 設定是否複製輸入中繼資料。
        /// </summary>
        /// <param name="enabled">需要複製輸入中繼資料時為 <see langword="true"/>。</param>
        /// <returns>目前的 encoding mode 選項。</returns>
        public MpvEncodingOptions CopyInputMetadata(bool enabled = true)
        {
            CopyMetadata = enabled;
            return this;
        }

        /// <summary>
        /// 指定要寫入輸出檔案的中繼資料。
        /// </summary>
        /// <param name="metadata">mpv <c>oset-metadata</c> 選項使用的中繼資料清單字串。</param>
        /// <returns>目前的 encoding mode 選項。</returns>
        public MpvEncodingOptions WithMetadata(string metadata)
        {
            if (string.IsNullOrWhiteSpace(metadata))
            {
                throw new ArgumentException("輸出中繼資料不可為空白。", nameof(metadata));
            }

            Metadata = metadata;
            return this;
        }

        /// <summary>
        /// 指定要從輸出檔案排除的中繼資料。
        /// </summary>
        /// <param name="metadata">mpv <c>oremove-metadata</c> 選項使用的中繼資料名稱清單字串。</param>
        /// <returns>目前的 encoding mode 選項。</returns>
        public MpvEncodingOptions RemoveMetadata(string metadata)
        {
            if (string.IsNullOrWhiteSpace(metadata))
            {
                throw new ArgumentException("要排除的中繼資料名稱不可為空白。", nameof(metadata));
            }

            RemovedMetadata = metadata;
            return this;
        }

        /// <summary>
        /// 加入額外的 mpv encoding mode 選項。
        /// </summary>
        /// <param name="name">mpv 選項名稱。</param>
        /// <param name="value">mpv 選項值。</param>
        /// <returns>目前的 encoding mode 選項。</returns>
        public MpvEncodingOptions WithOption(string name, string value)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("mpv 選項名稱不可為空白。", nameof(name));
            }

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            AdditionalOptions[name] = value;
            return this;
        }

        /// <summary>
        /// 取得或設定輸出檔案路徑。
        /// </summary>
        /// <value>要傳給 mpv <c>o</c> 選項的輸出檔案路徑。</value>
        public string OutputPath { get; set; }

        /// <summary>
        /// 取得或設定輸出容器格式。
        /// </summary>
        /// <value>要傳給 mpv <c>of</c> 選項的容器格式；未指定時由 mpv 自行判斷。</value>
        public string? ContainerFormat { get; set; }

        /// <summary>
        /// 取得或設定輸出容器格式選項。
        /// </summary>
        /// <value>要傳給 mpv <c>ofopts</c> 選項的 libavformat 選項字串。</value>
        public string? ContainerFormatOptions { get; set; }

        /// <summary>
        /// 取得或設定輸出視訊編碼器。
        /// </summary>
        /// <value>要傳給 mpv <c>ovc</c> 選項的視訊編碼器名稱。</value>
        public string? VideoCodec { get; set; }

        /// <summary>
        /// 取得或設定輸出視訊編碼器選項。
        /// </summary>
        /// <value>要傳給 mpv <c>ovcopts</c> 選項的 libavcodec 視訊選項字串。</value>
        public string? VideoCodecOptions { get; set; }

        /// <summary>
        /// 取得或設定輸出音訊編碼器。
        /// </summary>
        /// <value>要傳給 mpv <c>oac</c> 選項的音訊編碼器名稱。</value>
        public string? AudioCodec { get; set; }

        /// <summary>
        /// 取得或設定輸出音訊編碼器選項。
        /// </summary>
        /// <value>要傳給 mpv <c>oacopts</c> 選項的 libavcodec 音訊選項字串。</value>
        public string? AudioCodecOptions { get; set; }

        /// <summary>
        /// 取得或設定是否保留輸入時間戳記。
        /// </summary>
        /// <value>要傳給 mpv <c>orawts</c> 選項的旗標；未指定時使用 mpv 預設值。</value>
        public bool? CopyRawTimestamps { get; set; }

        /// <summary>
        /// 取得或設定是否複製輸入中繼資料。
        /// </summary>
        /// <value>要傳給 mpv <c>ocopy-metadata</c> 選項的旗標；未指定時使用 mpv 預設值。</value>
        public bool? CopyMetadata { get; set; }

        /// <summary>
        /// 取得或設定要寫入輸出檔案的中繼資料。
        /// </summary>
        /// <value>要傳給 mpv <c>oset-metadata</c> 選項的 key/value 清單字串。</value>
        public string? Metadata { get; set; }

        /// <summary>
        /// 取得或設定要從輸出檔案排除的中繼資料。
        /// </summary>
        /// <value>要傳給 mpv <c>oremove-metadata</c> 選項的標籤清單字串。</value>
        public string? RemovedMetadata { get; set; }

        /// <summary>
        /// 取得額外的 mpv encoding mode 選項。
        /// </summary>
        /// <value>以 mpv 選項名稱為索引鍵的額外 encoding 選項集合。</value>
        public IDictionary<string, string> AdditionalOptions { get; private set; }

        /// <summary>
        /// 將 encoding mode 選項套用到播放器選項。
        /// </summary>
        /// <param name="playerOptions">要修改的播放器選項。</param>
        public void ApplyTo(MpvPlayerOptions playerOptions)
        {
            if (playerOptions == null)
            {
                throw new ArgumentNullException(nameof(playerOptions));
            }

            Dictionary<string, string> options = CreateOptionDictionary();
            foreach (KeyValuePair<string, string> option in options)
            {
                playerOptions.InitialOptions[option.Key] = option.Value;
            }
        }

        /// <summary>
        /// 將 encoding mode 選項套用到已建立的播放器。
        /// </summary>
        /// <param name="player">要修改的播放器。</param>
        public void ApplyTo(MpvPlayer player)
        {
            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            Dictionary<string, string> options = CreateOptionDictionary();
            foreach (KeyValuePair<string, string> option in options)
            {
                player.SetOptionString(option.Key, option.Value);
            }
        }

        /// <summary>
        /// 建立目前設定對應的 mpv 選項字典。
        /// </summary>
        /// <returns>唯讀 mpv 選項字典。</returns>
        public IReadOnlyDictionary<string, string> ToOptionDictionary()
        {
            return new ReadOnlyDictionary<string, string>(CreateOptionDictionary());
        }

        /// <summary>
        /// 建立目前設定對應的可變 mpv 選項字典。
        /// </summary>
        /// <returns>可傳給 mpv 的選項字典。</returns>
        private Dictionary<string, string> CreateOptionDictionary()
        {
            if (string.IsNullOrWhiteSpace(OutputPath))
            {
                throw new InvalidOperationException("輸出檔案路徑不可為空白。");
            }

            Dictionary<string, string> options = new Dictionary<string, string>(StringComparer.Ordinal);
            AddOption(options, "o", OutputPath);
            AddOptionalOption(options, "of", ContainerFormat);
            AddOptionalOption(options, "ofopts", ContainerFormatOptions);
            AddOptionalOption(options, "ovc", VideoCodec);
            AddOptionalOption(options, "ovcopts", VideoCodecOptions);
            AddOptionalOption(options, "oac", AudioCodec);
            AddOptionalOption(options, "oacopts", AudioCodecOptions);
            AddOptionalOption(options, "orawts", CopyRawTimestamps);
            AddOptionalOption(options, "ocopy-metadata", CopyMetadata);
            AddOptionalOption(options, "oset-metadata", Metadata);
            AddOptionalOption(options, "oremove-metadata", RemovedMetadata);

            foreach (KeyValuePair<string, string> option in AdditionalOptions)
            {
                AddOption(options, option.Key, option.Value);
            }

            return options;
        }

        /// <summary>
        /// 將可為空的字串選項加入字典。
        /// </summary>
        /// <param name="options">目標選項字典。</param>
        /// <param name="name">mpv 選項名稱。</param>
        /// <param name="value">mpv 選項值。</param>
        private static void AddOptionalOption(IDictionary<string, string> options, string name, string? value)
        {
            if (value != null)
            {
                AddOption(options, name, value);
            }
        }

        /// <summary>
        /// 將可為空的旗標選項加入字典。
        /// </summary>
        /// <param name="options">目標選項字典。</param>
        /// <param name="name">mpv 選項名稱。</param>
        /// <param name="value">mpv 旗標值。</param>
        private static void AddOptionalOption(IDictionary<string, string> options, string name, bool? value)
        {
            if (value.HasValue)
            {
                AddOption(options, name, value.Value ? "yes" : "no");
            }
        }

        /// <summary>
        /// 將指定選項加入字典。
        /// </summary>
        /// <param name="options">目標選項字典。</param>
        /// <param name="name">mpv 選項名稱。</param>
        /// <param name="value">mpv 選項值。</param>
        private static void AddOption(IDictionary<string, string> options, string name, string value)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException("mpv 選項名稱不可為空白。");
            }

            if (value == null)
            {
                throw new InvalidOperationException("mpv 選項值不可為 null。");
            }

            options[name] = value;
        }
    }
}

namespace MediaEmbedKit.Mpv
{
    /// <summary>
    /// 定義常用的 yt-dlp 格式選擇預設值。
    /// </summary>
    public enum MpvYtdlpFormatPreset
    {
        /// <summary>
        /// 使用 mpv 與 yt-dlp 的預設格式選擇行為。
        /// </summary>
        Default = 0,

        /// <summary>
        /// 選擇可用的最佳視訊與音訊格式。
        /// </summary>
        Best = 1,

        /// <summary>
        /// 選擇最高不超過 2160p 的視訊與最佳音訊格式。
        /// </summary>
        UpTo2160p = 2,

        /// <summary>
        /// 選擇最高不超過 1440p 的視訊與最佳音訊格式。
        /// </summary>
        UpTo1440p = 3,

        /// <summary>
        /// 選擇最高不超過 1080p 的視訊與最佳音訊格式。
        /// </summary>
        UpTo1080p = 4,

        /// <summary>
        /// 選擇最高不超過 720p 的視訊與最佳音訊格式。
        /// </summary>
        UpTo720p = 5,

        /// <summary>
        /// 選擇最高不超過 480p 的視訊與最佳音訊格式。
        /// </summary>
        UpTo480p = 6,

        /// <summary>
        /// 選擇最高不超過 360p 的視訊與最佳音訊格式。
        /// </summary>
        UpTo360p = 7,

        /// <summary>
        /// 選擇最佳音訊格式。
        /// </summary>
        AudioOnly = 8
    }
}

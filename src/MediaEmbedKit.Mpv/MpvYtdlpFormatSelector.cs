using System;

namespace MediaEmbedKit.Mpv;

/// <summary>
/// 提供 yt-dlp 格式選擇預設值與 selector 字串的轉換方法。
/// </summary>
public static class MpvYtdlpFormatSelector
{
    /// <summary>
    /// 取得 mpv 與 yt-dlp 的預設格式選擇文字。
    /// </summary>
    /// <value>表示不主動傳入 yt-dlp format 的 selector 文字。</value>
    public const string Default = "ytdl";

    /// <summary>
    /// 取得最佳視訊與音訊格式的 selector 文字。
    /// </summary>
    /// <value>最佳視訊與音訊格式 selector。</value>
    public const string Best = "bestvideo*+bestaudio/best";

    /// <summary>
    /// 取得最佳音訊格式的 selector 文字。
    /// </summary>
    /// <value>最佳音訊格式 selector。</value>
    public const string AudioOnly = "bestaudio/best";

    /// <summary>
    /// 將常用格式預設值轉換為 yt-dlp selector 字串。
    /// </summary>
    /// <param name="preset">要轉換的格式預設值。</param>
    /// <returns>可傳給 mpv <c>ytdl-format</c> 選項的 selector 字串。</returns>
    public static string FromPreset(MpvYtdlpFormatPreset preset)
    {
        switch (preset)
        {
            case MpvYtdlpFormatPreset.Default:
                return Default;
            case MpvYtdlpFormatPreset.Best:
                return Best;
            case MpvYtdlpFormatPreset.UpTo2160p:
                return MaxHeight(2160);
            case MpvYtdlpFormatPreset.UpTo1440p:
                return MaxHeight(1440);
            case MpvYtdlpFormatPreset.UpTo1080p:
                return MaxHeight(1080);
            case MpvYtdlpFormatPreset.UpTo720p:
                return MaxHeight(720);
            case MpvYtdlpFormatPreset.UpTo480p:
                return MaxHeight(480);
            case MpvYtdlpFormatPreset.UpTo360p:
                return MaxHeight(360);
            case MpvYtdlpFormatPreset.AudioOnly:
                return AudioOnly;
            default:
                throw new ArgumentOutOfRangeException(nameof(preset), preset, "不支援的 yt-dlp 格式預設值。");
        }
    }

    /// <summary>
    /// 建立最高不超過指定高度的 yt-dlp selector 字串。
    /// </summary>
    /// <param name="maximumHeight">允許的最大視訊高度。</param>
    /// <returns>最高不超過指定高度的 yt-dlp selector 字串。</returns>
    public static string MaxHeight(int maximumHeight)
    {
        if (maximumHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumHeight), "最大視訊高度必須大於零。");
        }

        return "bestvideo*[height<=" + maximumHeight.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]+bestaudio/best[height<=" + maximumHeight.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]";
    }
}

using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using MediaEmbedKit.Mpv.Downloads;

namespace MediaEmbedKit.Mpv.Samples;

/// <summary>
/// 為 5 個 UI sample 提供共用的「把當前 URL 來源以 <see cref="MpvEncoder.EncodeAsync"/>
/// 轉碼成 5 秒 MP4」流程；各 sample 只需在自己的按鈕點擊處呼叫
/// <see cref="EncodeFirstFiveSecondsToMp4Async"/>，並提供寫日誌的 callback。
/// </summary>
internal static class SampleEncodingHelper
{
    /// <summary>
    /// 在使用者 Videos 資料夾建立並回傳一個帶時間戳記的 MP4 輸出路徑；
    /// Videos 資料夾不可用時退至 <see cref="Path.GetTempPath"/>。
    /// </summary>
    /// <returns>新輸出檔路徑。</returns>
    public static string BuildTimestampedOutputPath()
    {
        string folder = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            folder = Path.GetTempPath();
        }

        return Path.Combine(
            folder,
            "MediaEmbedKit-Encode-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".mp4");
    }

    /// <summary>
    /// 把指定來源前 5 秒轉碼成 H.264 + AAC mp4 寫入使用者 Videos 資料夾，並把進度透過 callback 送回。
    /// </summary>
    /// <param name="source">媒體檔案路徑或網址。</param>
    /// <param name="basePlayerOptions">基準播放器選項（將被複製，不會被修改）。</param>
    /// <param name="appendLine">將文字訊息送回 UI 的委派。</param>
    /// <returns>編碼結果。</returns>
    public static async Task<MpvEncodingResult> EncodeFirstFiveSecondsToMp4Async(
        string source,
        MpvPlayerOptions basePlayerOptions,
        Action<string> appendLine)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentException("來源不可為空白。", nameof(source));
        }

        if (basePlayerOptions == null)
        {
            throw new ArgumentNullException(nameof(basePlayerOptions));
        }

        if (appendLine == null)
        {
            throw new ArgumentNullException(nameof(appendLine));
        }

        string outputPath = BuildTimestampedOutputPath();
        appendLine("[encode] output → " + outputPath);

        MpvEncodingOptions options = new MpvEncodingOptions(outputPath)
            .WithStartTime(TimeSpan.Zero)
            .WithLength(TimeSpan.FromSeconds(5))
            .WithVideoCodec(MpvVideoCodecPreset.H264)
            .WithVideoCodecOption("preset", "veryfast")
            .WithVideoCodecOption("crf", "23")
            .WithAudioCodec(MpvAudioCodecPreset.Aac)
            .WithAudioCodecOption("b", "192k");

        MpvPlayerOptions playerOptions = new MpvPlayerOptions();
        SampleRuntime.CopyTo(basePlayerOptions, playerOptions);
        playerOptions.LogLevel = "warn";

        IProgress<MpvEncodingProgress> progress = new Progress<MpvEncodingProgress>(snapshot =>
        {
            string percent = snapshot.Percent.HasValue
                ? snapshot.Percent.Value.ToString("F1", CultureInfo.InvariantCulture)
                : "--";
            appendLine("[encode] " + percent + "%  pos="
                + snapshot.Position.ToString(@"mm\:ss")
                + "  bytes=" + snapshot.OutputBytes);
        });

        try
        {
            MpvEncodingResult result = await MpvEncoder.EncodeAsync(source, options, playerOptions, progress).ConfigureAwait(true);
            string summary = "Success=" + result.Success
                + " EndReason=" + result.EndReason
                + " ErrorCode=" + result.ErrorCode
                + " OutputBytes=" + result.OutputBytes
                + " Elapsed=" + result.Elapsed.ToString(@"mm\:ss\.fff");
            appendLine("[encode] done: " + summary);
            return result;
        }
        catch (Exception ex)
        {
            appendLine("[encode] error: " + ex.GetType().Name + ": " + ex.Message);
            throw;
        }
    }
}

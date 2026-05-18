using System;

namespace MediaEmbedKit.Mpv;

/// <summary>
/// 表示 <c>MpvEncoder.EncodeAsync</c>（位於 <c>MediaEmbedKit.Mpv.Encoding</c> 套件）進行中回報的進度快照。
/// </summary>
public readonly struct MpvEncodingProgress
{
    /// <summary>
    /// 初始化 <see cref="MpvEncodingProgress"/> 結構的新執行個體。
    /// </summary>
    /// <param name="position">由 mpv <c>time-pos</c> 取得的目前編碼來源時間。</param>
    /// <param name="duration">由 mpv <c>duration</c> 取得的來源總時長；無法取得時為 <see cref="TimeSpan.Zero"/>。</param>
    /// <param name="percent">由 mpv <c>percent-pos</c> 取得的百分比進度；無法取得時為 <see langword="null"/>。</param>
    /// <param name="elapsed">自編碼開始以來實際經過的時間。</param>
    /// <param name="estimatedRemaining">依目前速度推估剩餘時間；資料不足時為 <see langword="null"/>。</param>
    /// <param name="outputBytes">輸出檔案目前的位元組大小；檔案尚未產生時為 0。</param>
    public MpvEncodingProgress(
        TimeSpan position,
        TimeSpan duration,
        double? percent,
        TimeSpan elapsed,
        TimeSpan? estimatedRemaining,
        long outputBytes)
    {
        Position = position;
        Duration = duration;
        Percent = percent;
        Elapsed = elapsed;
        EstimatedRemaining = estimatedRemaining;
        OutputBytes = outputBytes;
    }

    /// <summary>
    /// 取得由 mpv <c>time-pos</c> 取得的目前編碼來源時間。
    /// </summary>
    /// <value>目前已處理到的來源時間。</value>
    public TimeSpan Position { get; }

    /// <summary>
    /// 取得由 mpv <c>duration</c> 取得的來源總時長。
    /// </summary>
    /// <value>來源總時長；無法取得時為 <see cref="TimeSpan.Zero"/>。</value>
    public TimeSpan Duration { get; }

    /// <summary>
    /// 取得由 mpv <c>percent-pos</c> 取得的百分比進度。
    /// </summary>
    /// <value>0 到 100 之間的百分比；無法取得時為 <see langword="null"/>。</value>
    public double? Percent { get; }

    /// <summary>
    /// 取得自編碼開始以來實際經過的時間。
    /// </summary>
    /// <value>實際經過的時間（wall-clock）。</value>
    public TimeSpan Elapsed { get; }

    /// <summary>
    /// 取得依目前速度推估剩餘時間。
    /// </summary>
    /// <value>推估剩餘時間；位置、時長或速度資料不足時為 <see langword="null"/>。</value>
    public TimeSpan? EstimatedRemaining { get; }

    /// <summary>
    /// 取得輸出檔案目前的位元組大小。
    /// </summary>
    /// <value>輸出檔位元組數；尚未產生或無法存取時為 0。</value>
    public long OutputBytes { get; }
}

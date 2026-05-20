using System;

namespace MediaEmbedKit.Mpv;

/// <summary>
/// 表示 <see cref="MpvEncoder.SplitAsync"/> 一個輸出段的時間範圍與目的路徑。
/// </summary>
public readonly struct MpvEncodingSegment
{
    /// <summary>
    /// 初始化 <see cref="MpvEncodingSegment"/> 結構的新執行個體。
    /// </summary>
    /// <param name="start">
    /// 段起點時間。
    /// </param>
    /// <param name="end">
    /// 段終點時間。
    /// </param>
    /// <param name="outputPath">
    /// 本段輸出檔案路徑。
    /// </param>
    public MpvEncodingSegment(TimeSpan start, TimeSpan end, string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("段輸出路徑不可為空白。", nameof(outputPath));
        }

        Start = start;
        End = end;
        OutputPath = outputPath;
    }

    /// <summary>
    /// 取得段起點時間。
    /// </summary>
    /// <value>
    /// 對應 mpv <c>start</c> 選項。
    /// </value>
    public TimeSpan Start { get; }

    /// <summary>
    /// 取得段終點時間。
    /// </summary>
    /// <value>
    /// 對應 mpv <c>end</c> 選項。
    /// </value>
    public TimeSpan End { get; }

    /// <summary>
    /// 取得段輸出檔案路徑。
    /// </summary>
    /// <value>
    /// 輸出檔案完整路徑。
    /// </value>
    public string OutputPath { get; }
}

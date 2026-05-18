using System;

namespace MediaEmbedKit.Mpv;

/// <summary>
/// 表示 <c>MpvEncoder.EncodeTwoPassAsync</c>（位於 <c>MediaEmbedKit.Mpv.Encoding</c> 套件）完成後的整體結果。
/// </summary>
public sealed class MpvTwoPassEncodingResult
{
    /// <summary>
    /// 初始化 <see cref="MpvTwoPassEncodingResult"/> 類別的新執行個體。
    /// </summary>
    /// <param name="firstPass">第一階段結果。</param>
    /// <param name="secondPass">第二階段結果；第一階段失敗未進入第二階段時為 <see langword="null"/>。</param>
    public MpvTwoPassEncodingResult(MpvEncodingResult firstPass, MpvEncodingResult? secondPass)
    {
        FirstPass = firstPass ?? throw new ArgumentNullException(nameof(firstPass));
        SecondPass = secondPass;
    }

    /// <summary>
    /// 取得第一階段（分析）結果。
    /// </summary>
    /// <value>第一階段結果。</value>
    public MpvEncodingResult FirstPass { get; }

    /// <summary>
    /// 取得第二階段（最終輸出）結果。
    /// </summary>
    /// <value>第二階段結果；第一階段失敗未進入第二階段時為 <see langword="null"/>。</value>
    public MpvEncodingResult? SecondPass { get; }

    /// <summary>
    /// 取得兩階段是否皆成功完成。
    /// </summary>
    /// <value>第二階段 <see cref="MpvEncodingResult.Success"/> 為 <see langword="true"/> 時為 <see langword="true"/>。</value>
    public bool Success
    {
        get { return SecondPass != null && SecondPass.Success; }
    }
}

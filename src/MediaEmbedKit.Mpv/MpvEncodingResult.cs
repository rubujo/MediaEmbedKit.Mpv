using System;

namespace MediaEmbedKit.Mpv;

/// <summary>
/// 表示 <see cref="MpvEncoder.EncodeAsync"/> 完成後的結果。
/// </summary>
public sealed class MpvEncodingResult
{
    /// <summary>
    /// 初始化 <see cref="MpvEncodingResult"/> 類別的新執行個體。
    /// </summary>
    /// <param name="success">編碼是否成功完成。</param>
    /// <param name="endReason">libmpv <c>EndFile</c> 事件回報的結束原因。</param>
    /// <param name="errorCode">libmpv 隨 <c>EndFile</c> 附帶的錯誤碼；無錯誤時為 <see cref="MpvErrorCode.Success"/>。</param>
    /// <param name="outputPath">輸出檔案路徑。</param>
    /// <param name="outputBytes">輸出檔案最終位元組大小；無檔案產生時為 0。</param>
    /// <param name="elapsed">自編碼開始至 <c>EndFile</c> 之間實際經過的時間。</param>
    public MpvEncodingResult(
        bool success,
        MpvEndFileReason endReason,
        MpvErrorCode errorCode,
        string outputPath,
        long outputBytes,
        TimeSpan elapsed)
    {
        Success = success;
        EndReason = endReason;
        ErrorCode = errorCode;
        OutputPath = outputPath;
        OutputBytes = outputBytes;
        Elapsed = elapsed;
    }

    /// <summary>
    /// 取得編碼是否成功完成。
    /// </summary>
    /// <value>當 <see cref="EndReason"/> 為 <see cref="MpvEndFileReason.EndOfFile"/> 且 <see cref="ErrorCode"/> 為 <see cref="MpvErrorCode.Success"/> 時為 <see langword="true"/>。</value>
    public bool Success { get; }

    /// <summary>
    /// 取得 libmpv <c>EndFile</c> 事件回報的結束原因。
    /// </summary>
    /// <value>結束原因。</value>
    public MpvEndFileReason EndReason { get; }

    /// <summary>
    /// 取得 libmpv 隨 <c>EndFile</c> 附帶的錯誤碼。
    /// </summary>
    /// <value>libmpv 錯誤碼；無錯誤時為 <see cref="MpvErrorCode.Success"/>。</value>
    public MpvErrorCode ErrorCode { get; }

    /// <summary>
    /// 取得輸出檔案路徑。
    /// </summary>
    /// <value>編碼輸出檔案的完整路徑。</value>
    public string OutputPath { get; }

    /// <summary>
    /// 取得輸出檔案最終位元組大小。
    /// </summary>
    /// <value>輸出檔位元組數；無檔案產生時為 0。</value>
    public long OutputBytes { get; }

    /// <summary>
    /// 取得自編碼開始至 <c>EndFile</c> 之間實際經過的時間。
    /// </summary>
    /// <value>實際經過的時間。</value>
    public TimeSpan Elapsed { get; }
}

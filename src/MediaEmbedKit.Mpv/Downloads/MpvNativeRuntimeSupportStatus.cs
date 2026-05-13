namespace MediaEmbedKit.Mpv.Downloads;

/// <summary>
/// 定義本專案對原生執行階段來源的支援狀態。
/// </summary>
public enum MpvNativeRuntimeSupportStatus
{
    /// <summary>
    /// 專案已實作下載或安裝 helper。
    /// </summary>
    Supported = 0,
    /// <summary>
    /// 目前平台沒有列入專案 catalog。
    /// </summary>
    NotCataloged = 1
}

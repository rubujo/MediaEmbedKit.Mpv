namespace MediaEmbedKit.Mpv.Downloads;

/// <summary>
/// 定義原生 libmpv 來源的取得方式。
/// </summary>
public enum MpvNativeRuntimeSourceKind
{
    /// <summary>
    /// 可直接下載的壓縮封存檔。
    /// </summary>
    DirectArchive = 0,
    /// <summary>
    /// 由作業系統套件管理員提供。
    /// </summary>
    PackageManager = 1,
    /// <summary>
    /// 由建置指令碼產生。
    /// </summary>
    BuildScripts = 2,
    /// <summary>
    /// 由完整應用程式建置提供。
    /// </summary>
    ApplicationBuild = 3,
    /// <summary>
    /// 只有文件或參考資訊。
    /// </summary>
    Documentation = 4
}

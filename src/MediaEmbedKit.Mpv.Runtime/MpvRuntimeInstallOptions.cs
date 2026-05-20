using MediaEmbedKit.Mpv.Platforms;

using MediaEmbedKit.Mpv.Externals;

namespace MediaEmbedKit.Mpv.Runtime;

/// <summary>
/// 提供平台感知執行階段安裝 helper 的選項。
/// </summary>
public sealed class MpvRuntimeInstallOptions
{
    /// <summary>
    /// 初始化 <see cref="MpvRuntimeInstallOptions"/> 類別的新執行個體。
    /// </summary>
    public MpvRuntimeInstallOptions()
    {
        Windows = new MpvWindowsRuntimeDownloadOptions();
    }

    /// <summary>
    /// 取得或設定要覆寫自動偵測結果的平台。
    /// </summary>
    /// <value>
    /// 指定的平台；未指定時由 helper 自動偵測。
    /// </value>
    public MpvNativeRuntimePlatform? Platform { get; set; }

    /// <summary>
    /// 取得 Windows 執行階段下載選項。
    /// </summary>
    /// <value>
    /// Windows 執行階段下載選項。
    /// </value>
    public MpvWindowsRuntimeDownloadOptions Windows { get; private set; }
}

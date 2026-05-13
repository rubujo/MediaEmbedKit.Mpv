using MediaEmbedKit.Mpv.Downloads;

namespace MediaEmbedKit.Mpv.Platforms;

/// <summary>
/// 描述 UI 架構在特定平台上的 mpv 支援狀態。
/// </summary>
public sealed class MpvUiFrameworkSupportInfo
{
    /// <summary>
    /// 初始化 <see cref="MpvUiFrameworkSupportInfo"/> 類別的新執行個體。
    /// </summary>
    /// <param name="framework">UI 架構。</param>
    /// <param name="platform">原生執行階段平台。</param>
    /// <param name="status">UI 架構支援狀態。</param>
    /// <param name="nativeBackend">支援項目的原生後端描述。</param>
    /// <param name="notes">支援項目的補充說明。</param>
    public MpvUiFrameworkSupportInfo(
        MpvUiFramework framework,
        MpvNativeRuntimePlatform platform,
        MpvUiFrameworkSupportStatus status,
        string nativeBackend,
        string notes)
    {
        Framework = framework;
        Platform = platform;
        Status = status;
        NativeBackend = nativeBackend;
        Notes = notes;
    }

    /// <summary>
    /// 取得 UI 架構。
    /// </summary>
    /// <value>UI 架構列舉值。</value>
    public MpvUiFramework Framework { get; private set; }

    /// <summary>
    /// 取得原生執行階段平台。
    /// </summary>
    /// <value>原生執行階段平台。</value>
    public MpvNativeRuntimePlatform Platform { get; private set; }

    /// <summary>
    /// 取得 UI 架構支援狀態。
    /// </summary>
    /// <value>支援狀態列舉值。</value>
    public MpvUiFrameworkSupportStatus Status { get; private set; }

    /// <summary>
    /// 取得支援項目的原生後端描述。
    /// </summary>
    /// <value>原生後端描述文字。</value>
    public string NativeBackend { get; private set; }

    /// <summary>
    /// 取得支援項目的補充說明。
    /// </summary>
    /// <value>支援項目補充說明。</value>
    public string Notes { get; private set; }
}

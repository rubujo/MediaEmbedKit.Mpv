using System.Collections.Generic;
using System.Linq;

namespace MediaEmbedKit.Mpv.Platforms;

/// <summary>
/// 提供 UI 架構與平台支援狀態的靜態 catalog。
/// </summary>
public static class MpvUiFrameworkSupportCatalog
{
    /// <summary>
    /// 保存專案追蹤的 UI 架構支援項目。
    /// </summary>
    private static readonly IReadOnlyList<MpvUiFrameworkSupportInfo> Entries = new[]
    {
        new MpvUiFrameworkSupportInfo(MpvUiFramework.WinForms, MpvNativeRuntimePlatform.Windows, MpvUiFrameworkSupportStatus.Supported, "HWND wid", "已使用子 HWND 實作，並列為 WinForms 目前高效能預設。"),
        new MpvUiFrameworkSupportInfo(MpvUiFramework.Wpf, MpvNativeRuntimePlatform.Windows, MpvUiFrameworkSupportStatus.Supported, "HwndHost wid + managed overlay", "已使用 HwndHost 實作，並由控制項內建 OverlayContent AirSpace 覆蓋層管理。"),
        new MpvUiFrameworkSupportInfo(MpvUiFramework.Avalonia, MpvNativeRuntimePlatform.Windows, MpvUiFrameworkSupportStatus.Supported, "OpenGL render API", "已提供 Avalonia OpenGL render API 控制項。"),
        new MpvUiFrameworkSupportInfo(MpvUiFramework.WinUI3, MpvNativeRuntimePlatform.Windows, MpvUiFrameworkSupportStatus.Supported, "HWND wid + managed overlay", "已提供 WinUI 3 HWND 控制項，並由控制項內建 OverlayContent AirSpace 覆蓋層管理。"),
        new MpvUiFrameworkSupportInfo(MpvUiFramework.Maui, MpvNativeRuntimePlatform.Windows, MpvUiFrameworkSupportStatus.Supported, "WinUI 3 HWND handler", "已透過 WinUI 3 HWND 控制項提供 MAUI Windows handler。")
    };

    /// <summary>
    /// 取得所有 UI 架構支援項目。
    /// </summary>
    /// <returns>所有 UI 架構支援項目。</returns>
    public static IReadOnlyList<MpvUiFrameworkSupportInfo> GetAll()
    {
        return Entries;
    }

    /// <summary>
    /// 取得指定 UI 架構的支援項目。
    /// </summary>
    /// <param name="framework">要查詢的 UI 架構。</param>
    /// <returns>符合指定 UI 架構的支援項目。</returns>
    public static IReadOnlyList<MpvUiFrameworkSupportInfo> Get(MpvUiFramework framework)
    {
        return Entries.Where(entry => entry.Framework == framework).ToArray();
    }

    /// <summary>
    /// 尋找指定 UI 架構與平台的支援項目。
    /// </summary>
    /// <param name="framework">要查詢的 UI 架構。</param>
    /// <param name="platform">要查詢的原生執行階段平台。</param>
    /// <returns>符合條件的支援項目；找不到時為 <see langword="null"/>。</returns>
    public static MpvUiFrameworkSupportInfo? Find(MpvUiFramework framework, MpvNativeRuntimePlatform platform)
    {
        return Entries.FirstOrDefault(entry => entry.Framework == framework && entry.Platform == platform);
    }
}

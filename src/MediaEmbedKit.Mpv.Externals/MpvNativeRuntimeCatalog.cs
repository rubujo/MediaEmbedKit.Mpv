using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using MediaEmbedKit.Mpv.Platforms;

namespace MediaEmbedKit.Mpv.Externals;

/// <summary>
/// 提供原生 libmpv 執行階段來源的靜態 catalog。
/// </summary>
public static class MpvNativeRuntimeCatalog
{
    /// <summary>
    /// 取得 mpv 官方安裝頁面 URI。
    /// </summary>
    /// <value>
    /// mpv 官方安裝頁面 URI。
    /// </value>
    public static Uri OfficialInstallationPage
    {
        get { return new Uri("https://mpv.io/installation/"); }
    }

    /// <summary>
    /// 偵測目前執行環境對應的原生 libmpv 平台。
    /// </summary>
    /// <returns>
    /// 目前執行環境對應的平台列舉值。
    /// </returns>
    public static MpvNativeRuntimePlatform CurrentPlatform()
    {
#if NET5_0_OR_GREATER || NETSTANDARD2_0
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return MpvNativeRuntimePlatform.Windows;
        }

        return MpvNativeRuntimePlatform.Unknown;
#else
        return MpvNativeRuntimePlatform.Windows;
#endif
    }

    /// <summary>
    /// 取得目前平台的原生 libmpv 來源清單。
    /// </summary>
    /// <returns>
    /// 目前平台可參考的原生來源清單。
    /// </returns>
    public static IReadOnlyList<MpvNativeRuntimeSource> GetSourcesForCurrentPlatform()
    {
        return GetSources(CurrentPlatform());
    }

    /// <summary>
    /// 取得指定平台的原生 libmpv 來源清單。
    /// </summary>
    /// <param name="platform">
    /// 要查詢的原生 libmpv 平台。
    /// </param>
    /// <returns>
    /// 指定平台可參考的原生來源清單。
    /// </returns>
    public static IReadOnlyList<MpvNativeRuntimeSource> GetSources(MpvNativeRuntimePlatform platform)
    {
        switch (platform)
        {
            case MpvNativeRuntimePlatform.Windows:
                return WindowsSources;
            default:
                return Array.Empty<MpvNativeRuntimeSource>();
        }
    }

    /// <summary>
    /// 取得此專案對指定平台的執行階段支援狀態。
    /// </summary>
    /// <param name="platform">
    /// 要查詢的原生 libmpv 平台。
    /// </param>
    /// <returns>
    /// 此專案對指定平台的執行階段支援狀態。
    /// </returns>
    public static MpvNativeRuntimeSupportStatus GetProjectSupportStatus(MpvNativeRuntimePlatform platform)
    {
        switch (platform)
        {
            case MpvNativeRuntimePlatform.Windows:
                return MpvNativeRuntimeSupportStatus.Supported;
            default:
                return MpvNativeRuntimeSupportStatus.NotCataloged;
        }
    }

    /// <summary>
    /// Windows 平台可參考的原生 libmpv 來源。
    /// </summary>
    private static readonly IReadOnlyList<MpvNativeRuntimeSource> WindowsSources = new[]
    {
        new MpvNativeRuntimeSource(
            MpvNativeRuntimePlatform.Windows,
            "shinchiro/mpv-winbuild-cmake",
            new Uri("https://github.com/shinchiro/mpv-winbuild-cmake/releases"),
            MpvNativeRuntimeSourceKind.DirectArchive,
            MpvNativeRuntimeSupportStatus.Supported,
            "libmpv-2.dll",
            "mpv.io 列出的 Windows 來源。輔助工具支援 x86_64 與 aarch64 mpv-dev 封存檔，並解出 libmpv-2.dll。"),
        new MpvNativeRuntimeSource(
            MpvNativeRuntimePlatform.Windows,
            "zhongfly/mpv-winbuild",
            new Uri("https://github.com/zhongfly/mpv-winbuild/releases"),
            MpvNativeRuntimeSourceKind.DirectArchive,
            MpvNativeRuntimeSupportStatus.Supported,
            "libmpv-2.dll",
            "mpv.io 列出的 Windows 來源。輔助工具支援 x86_64 與 aarch64 mpv-dev 封存檔，並解出 libmpv-2.dll。")
    };
}

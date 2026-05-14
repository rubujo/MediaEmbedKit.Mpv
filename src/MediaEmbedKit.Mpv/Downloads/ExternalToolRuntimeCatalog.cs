using System;
using System.Collections.Generic;

namespace MediaEmbedKit.Mpv.Downloads;

/// <summary>
/// 提供 yt-dlp、Deno 與 FFmpeg 執行階段來源的靜態 catalog。
/// </summary>
public static class ExternalToolRuntimeCatalog
{
    /// <summary>
    /// 取得 yt-dlp GitHub 發行頁面 URI。
    /// </summary>
    /// <value>yt-dlp GitHub 發行頁面 URI。</value>
    public static Uri YtDlpReleases
    {
        get { return new Uri("https://github.com/yt-dlp/yt-dlp/releases"); }
    }

    /// <summary>
    /// 取得 yt-dlp 讀我檔 URI。
    /// </summary>
    /// <value>yt-dlp 讀我檔 URI。</value>
    public static Uri YtDlpReadme
    {
        get { return new Uri("https://github.com/yt-dlp/yt-dlp/blob/master/README.md"); }
    }

    /// <summary>
    /// 取得 Deno 官方安裝文件 URI。
    /// </summary>
    /// <value>Deno 官方安裝文件 URI。</value>
    public static Uri DenoInstallation
    {
        get { return new Uri("https://docs.deno.com/runtime/getting_started/installation/"); }
    }

    /// <summary>
    /// 取得 Deno GitHub 發行頁面 URI。
    /// </summary>
    /// <value>Deno GitHub 發行頁面 URI。</value>
    public static Uri DenoReleases
    {
        get { return new Uri("https://github.com/denoland/deno/releases"); }
    }

    /// <summary>
    /// 取得 yt-dlp FFmpeg-Builds GitHub 發行頁面 URI。
    /// </summary>
    /// <value>yt-dlp FFmpeg-Builds GitHub 發行頁面 URI。</value>
    public static Uri FFmpegBuildsReleases
    {
        get { return new Uri("https://github.com/yt-dlp/FFmpeg-Builds/releases"); }
    }

    /// <summary>
    /// 取得指定外部工具與平台的執行階段來源清單。
    /// </summary>
    /// <param name="tool">要查詢的外部工具種類。</param>
    /// <param name="platform">要查詢的平台。</param>
    /// <returns>外部工具來源清單。</returns>
    public static IReadOnlyList<ExternalToolRuntimeSource> GetSources(ExternalToolKind tool, MpvNativeRuntimePlatform platform)
    {
        switch (tool)
        {
            case ExternalToolKind.Deno:
                return GetDenoSources(platform);
            case ExternalToolKind.FFmpeg:
                return GetFFmpegSources(platform);
            default:
                return GetYtDlpSources(platform);
        }
    }

    /// <summary>
    /// 取得目前平台上指定外部工具的執行階段來源清單。
    /// </summary>
    /// <param name="tool">要查詢的外部工具種類。</param>
    /// <returns>目前平台的外部工具來源清單。</returns>
    public static IReadOnlyList<ExternalToolRuntimeSource> GetSourcesForCurrentPlatform(ExternalToolKind tool)
    {
        return GetSources(tool, MpvNativeRuntimeCatalog.CurrentPlatform());
    }

    /// <summary>
    /// 取得指定平台的 yt-dlp 執行階段來源清單。
    /// </summary>
    /// <param name="platform">要查詢的平台。</param>
    /// <returns>yt-dlp 執行階段來源清單。</returns>
    private static IReadOnlyList<ExternalToolRuntimeSource> GetYtDlpSources(MpvNativeRuntimePlatform platform)
    {
        switch (platform)
        {
            case MpvNativeRuntimePlatform.Windows:
                return YtDlpWindowsSources;
            default:
                return Array.Empty<ExternalToolRuntimeSource>();
        }
    }

    /// <summary>
    /// 取得指定平台的 Deno 執行階段來源清單。
    /// </summary>
    /// <param name="platform">要查詢的平台。</param>
    /// <returns>Deno 執行階段來源清單。</returns>
    private static IReadOnlyList<ExternalToolRuntimeSource> GetDenoSources(MpvNativeRuntimePlatform platform)
    {
        switch (platform)
        {
            case MpvNativeRuntimePlatform.Windows:
                return DenoWindowsSources;
            default:
                return Array.Empty<ExternalToolRuntimeSource>();
        }
    }

    /// <summary>
    /// 取得指定平台的 FFmpeg 執行階段來源清單。
    /// </summary>
    /// <param name="platform">要查詢的平台。</param>
    /// <returns>FFmpeg 執行階段來源清單。</returns>
    private static IReadOnlyList<ExternalToolRuntimeSource> GetFFmpegSources(MpvNativeRuntimePlatform platform)
    {
        switch (platform)
        {
            case MpvNativeRuntimePlatform.Windows:
                return FFmpegWindowsSources;
            default:
                return Array.Empty<ExternalToolRuntimeSource>();
        }
    }

    /// <summary>
    /// Windows 平台可使用的 yt-dlp 執行階段來源。
    /// </summary>
    private static readonly IReadOnlyList<ExternalToolRuntimeSource> YtDlpWindowsSources = new[]
    {
        new ExternalToolRuntimeSource(
            ExternalToolKind.YtDlp,
            MpvNativeRuntimePlatform.Windows,
            "yt-dlp Windows x64",
            YtDlpReleases,
            "yt-dlp.exe",
            MpvNativeRuntimeSupportStatus.Supported,
            true,
            "yt-dlp.exe --update",
            "Implemented by YtDlpDownloader for Windows x64."),
        new ExternalToolRuntimeSource(
            ExternalToolKind.YtDlp,
            MpvNativeRuntimePlatform.Windows,
            "yt-dlp Windows ARM64",
            YtDlpReleases,
            "yt-dlp_arm64.exe",
            MpvNativeRuntimeSupportStatus.Supported,
            true,
            "yt-dlp_arm64.exe --update",
            "Implemented by YtDlpDownloader for Windows ARM64 (yt-dlp 2026.03.17+).")
    };

    /// <summary>
    /// Windows 平台可使用的 Deno 執行階段來源。
    /// </summary>
    private static readonly IReadOnlyList<ExternalToolRuntimeSource> DenoWindowsSources = new[]
    {
        new ExternalToolRuntimeSource(
            ExternalToolKind.Deno,
            MpvNativeRuntimePlatform.Windows,
            "Deno Windows x64",
            DenoReleases,
            "deno-x86_64-pc-windows-msvc.zip",
            MpvNativeRuntimeSupportStatus.Supported,
            true,
            "deno.exe upgrade",
            "Implemented by DenoDownloader for Windows x64."),
        new ExternalToolRuntimeSource(
            ExternalToolKind.Deno,
            MpvNativeRuntimePlatform.Windows,
            "Deno Windows ARM64",
            DenoReleases,
            "deno-aarch64-pc-windows-msvc.zip",
            MpvNativeRuntimeSupportStatus.Supported,
            true,
            "deno.exe upgrade",
            "Implemented by DenoDownloader for Windows ARM64 (Deno 2.7+).")
    };

    /// <summary>
    /// Windows 平台可使用的 FFmpeg 執行階段來源。
    /// </summary>
    private static readonly IReadOnlyList<ExternalToolRuntimeSource> FFmpegWindowsSources = new[]
    {
        new ExternalToolRuntimeSource(
            ExternalToolKind.FFmpeg,
            MpvNativeRuntimePlatform.Windows,
            "yt-dlp FFmpeg-Builds Windows x64 GPL",
            FFmpegBuildsReleases,
            FFmpegDownloader.WindowsX64AssetName,
            MpvNativeRuntimeSupportStatus.Supported,
            false,
            string.Empty,
            "Implemented by FFmpegDownloader for Windows x64; update by re-running the downloader."),
        new ExternalToolRuntimeSource(
            ExternalToolKind.FFmpeg,
            MpvNativeRuntimePlatform.Windows,
            "yt-dlp FFmpeg-Builds Windows ARM64 GPL",
            FFmpegBuildsReleases,
            FFmpegDownloader.WindowsArm64AssetName,
            MpvNativeRuntimeSupportStatus.Supported,
            false,
            string.Empty,
            "Implemented by FFmpegDownloader for Windows ARM64; update by re-running the downloader.")
    };

}

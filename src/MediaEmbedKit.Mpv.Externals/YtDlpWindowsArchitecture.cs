using System;
using System.Runtime.InteropServices;

namespace MediaEmbedKit.Mpv.Externals;

/// <summary>
/// 定義 yt-dlp Windows 發行檔支援的處理器架構。
/// </summary>
public enum YtDlpWindowsArchitecture
{
    /// <summary>
    /// 64 位元 x86 Windows 發行檔。
    /// </summary>
    X64 = 0,

    /// <summary>
    /// 64 位元 ARM Windows 發行檔（aarch64）。
    /// </summary>
    Arm64 = 1,
}

/// <summary>
/// 提供 <see cref="YtDlpWindowsArchitecture"/> 的輔助方法。
/// </summary>
public static class YtDlpWindowsArchitectureExtensions
{
    /// <summary>
    /// 取得目前處理序適用的 yt-dlp Windows 發行檔架構。
    /// </summary>
    /// <returns>
    /// 目前處理序適用的 yt-dlp Windows 架構。
    /// </returns>
    /// <remarks>
    /// 以 <see cref="RuntimeInformation.ProcessArchitecture"/> 為準。Windows on ARM 處理序在
    /// x64 emulation 下會被視為 x64；原生 ARM64 處理序回傳 <see cref="YtDlpWindowsArchitecture.Arm64"/>。
    /// </remarks>
    public static YtDlpWindowsArchitecture CurrentProcess()
    {
#if NET5_0_OR_GREATER || NETSTANDARD2_0
        switch (RuntimeInformation.ProcessArchitecture)
        {
            case Architecture.X64:
                return YtDlpWindowsArchitecture.X64;
            case Architecture.Arm64:
                return YtDlpWindowsArchitecture.Arm64;
            default:
                throw new PlatformNotSupportedException("目前只支援 Windows x64 與 ARM64 yt-dlp runtime。");
        }
#else
        if (IntPtr.Size != 8)
        {
            throw new PlatformNotSupportedException("目前只支援 Windows x64 與 ARM64 yt-dlp runtime。");
        }

        return YtDlpWindowsArchitecture.X64;
#endif
    }

    /// <summary>
    /// 將 yt-dlp Windows 架構轉換為 GitHub 發行資產名稱。
    /// </summary>
    /// <param name="architecture">
    /// 要轉換的 yt-dlp Windows 架構。
    /// </param>
    /// <returns>
    /// 對應的 yt-dlp 發行資產名稱。
    /// </returns>
    /// <remarks>
    /// yt-dlp 官方 release 自 2026 起對 ARM64 提供 <c>yt-dlp_arm64.exe</c>。
    /// </remarks>
    internal static string ToAssetName(this YtDlpWindowsArchitecture architecture)
    {
        switch (architecture)
        {
            case YtDlpWindowsArchitecture.X64:
                return "yt-dlp.exe";
            case YtDlpWindowsArchitecture.Arm64:
                return "yt-dlp_arm64.exe";
            default:
                throw new ArgumentOutOfRangeException(nameof(architecture), architecture, "未支援的 yt-dlp Windows 架構。");
        }
    }
}

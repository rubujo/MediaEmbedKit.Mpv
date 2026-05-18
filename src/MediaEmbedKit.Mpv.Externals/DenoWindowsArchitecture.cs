using System;
using System.Runtime.InteropServices;

namespace MediaEmbedKit.Mpv.Externals;

/// <summary>
/// 定義 Deno Windows 發行檔支援的處理器架構。
/// </summary>
public enum DenoWindowsArchitecture
{
    /// <summary>
    /// 64 位元 x86 Windows 發行檔。
    /// </summary>
    X64 = 0,

    /// <summary>
    /// 64 位元 ARM Windows 發行檔（aarch64-pc-windows-msvc）。
    /// </summary>
    Arm64 = 1,
}

/// <summary>
/// 提供 <see cref="DenoWindowsArchitecture"/> 的輔助方法。
/// </summary>
public static class DenoWindowsArchitectureExtensions
{
    /// <summary>
    /// 取得目前電腦適用的 Deno Windows 發行檔架構。
    /// </summary>
    /// <returns>目前電腦適用的 Deno Windows 架構。</returns>
    /// <remarks>
    /// 以 <see cref="RuntimeInformation.OSArchitecture"/> 為準（Deno 需與作業系統原生位元對齊；
    /// x64 emulation 下仍建議下載 x64 binary 以避免來回轉換）。
    /// </remarks>
    public static DenoWindowsArchitecture CurrentMachine()
    {
#if NET5_0_OR_GREATER || NETSTANDARD2_0
        switch (RuntimeInformation.OSArchitecture)
        {
            case Architecture.X64:
                return DenoWindowsArchitecture.X64;
            case Architecture.Arm64:
                return DenoWindowsArchitecture.Arm64;
            default:
                throw new PlatformNotSupportedException("目前只支援 Windows x64 與 ARM64 Deno runtime。");
        }
#else
        return DenoWindowsArchitecture.X64;
#endif
    }

    /// <summary>
    /// 將 Deno Windows 架構轉換為 GitHub 發行資產名稱。
    /// </summary>
    /// <param name="architecture">要轉換的 Deno Windows 架構。</param>
    /// <returns>對應的 Deno 發行資產名稱。</returns>
    /// <remarks>
    /// Deno 自 2.7（2026-02）起對 Windows on ARM 提供官方 <c>aarch64-pc-windows-msvc</c> 建置。
    /// </remarks>
    internal static string ToAssetName(this DenoWindowsArchitecture architecture)
    {
        switch (architecture)
        {
            case DenoWindowsArchitecture.X64:
                return "deno-x86_64-pc-windows-msvc.zip";
            case DenoWindowsArchitecture.Arm64:
                return "deno-aarch64-pc-windows-msvc.zip";
            default:
                throw new ArgumentOutOfRangeException(nameof(architecture), architecture, "未支援的 Deno Windows 架構。");
        }
    }
}

using System;
using System.Runtime.InteropServices;

namespace MediaEmbedKit.Mpv.Platforms;

/// <summary>
/// 定義 Windows libmpv 建置可使用的處理器架構。
/// </summary>
public enum MpvWindowsArchitecture
{
    /// <summary>
    /// 64 位元 x86 Windows 建置。
    /// </summary>
    X64 = 0,

    /// <summary>
    /// 64 位元 ARM Windows 建置（aarch64）。
    /// </summary>
    Arm64 = 1,
}

/// <summary>
/// 提供 <see cref="MpvWindowsArchitecture"/> 的輔助方法。
/// </summary>
public static class MpvWindowsArchitectureExtensions
{
    /// <summary>
    /// 取得目前處理序適用的 Windows libmpv 建置架構。
    /// </summary>
    /// <returns>目前處理序適用的 Windows libmpv 架構。</returns>
    /// <remarks>
    /// 以 <see cref="RuntimeInformation.ProcessArchitecture"/> 為準。Windows on ARM 上若處理序為
    /// x64（透過 x64 emulation 執行），會回傳 <see cref="MpvWindowsArchitecture.X64"/>；
    /// 處理序原生為 ARM64 時回傳 <see cref="MpvWindowsArchitecture.Arm64"/>。
    /// </remarks>
    public static MpvWindowsArchitecture CurrentProcess()
    {
#if NET5_0_OR_GREATER || NETSTANDARD2_0
        switch (RuntimeInformation.ProcessArchitecture)
        {
            case Architecture.X64:
                return MpvWindowsArchitecture.X64;
            case Architecture.Arm64:
                return MpvWindowsArchitecture.Arm64;
            default:
                throw new PlatformNotSupportedException("目前只支援 Windows x64 與 ARM64 libmpv runtime。");
        }
#else
        if (IntPtr.Size != 8)
        {
            throw new PlatformNotSupportedException("目前只支援 Windows x64 與 ARM64 libmpv runtime。");
        }

        // .NET Framework 4.7.2 / 4.8 沒有原生 ARM64，預設假設為 x64。
        return MpvWindowsArchitecture.X64;
#endif
    }

    /// <summary>
    /// 將 Windows libmpv 架構轉換為發行資產名稱中的架構片段。
    /// </summary>
    /// <param name="architecture">要轉換的 Windows libmpv 架構。</param>
    /// <returns>發行資產名稱使用的架構片段。</returns>
    /// <remarks>
    /// shinchiro 與 zhongfly 的 libmpv 發行檔均採用相同 token 命名規範：
    /// <c>mpv-dev-{token}-{date}-git-{commit}.7z</c>，其中 x64 為
    /// <c>x86_64</c>、ARM64 為 <c>aarch64</c>。
    /// </remarks>
    internal static string ToAssetToken(this MpvWindowsArchitecture architecture)
    {
        switch (architecture)
        {
            case MpvWindowsArchitecture.X64:
                return "x86_64";
            case MpvWindowsArchitecture.Arm64:
                return "aarch64";
            default:
                throw new ArgumentOutOfRangeException(nameof(architecture), architecture, "未支援的 Windows libmpv 架構。");
        }
    }
}

using System;
using System.Runtime.InteropServices;

namespace MediaEmbedKit.Mpv.Downloads;

/// <summary>
/// 定義 Windows libmpv 建置可使用的處理器架構。
/// </summary>
public enum MpvWindowsArchitecture
{
    /// <summary>
    /// 64 位元 x86 Windows 建置。
    /// </summary>
    X64 = 0
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
    public static MpvWindowsArchitecture CurrentProcess()
    {
#if NET5_0_OR_GREATER || NETSTANDARD2_0
        if (RuntimeInformation.OSArchitecture != Architecture.X64 || RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            throw new PlatformNotSupportedException("目前只支援 Windows x64 libmpv runtime。");
        }

        return MpvWindowsArchitecture.X64;
#else
        if (IntPtr.Size != 8)
        {
            throw new PlatformNotSupportedException("目前只支援 Windows x64 libmpv runtime。");
        }

        return MpvWindowsArchitecture.X64;
#endif
    }

    /// <summary>
    /// 將 Windows libmpv 架構轉換為發行資產名稱中的架構片段。
    /// </summary>
    /// <param name="architecture">要轉換的 Windows libmpv 架構。</param>
    /// <returns>發行資產名稱使用的架構片段。</returns>
    internal static string ToAssetToken(this MpvWindowsArchitecture architecture)
    {
        return "x86_64";
    }
}

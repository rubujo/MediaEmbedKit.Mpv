using System;
using System.Runtime.InteropServices;

namespace MediaEmbedKit.Mpv.Downloads
{
    /// <summary>
    /// 定義 Deno Windows 發行檔支援的處理器架構。
    /// </summary>
    public enum DenoWindowsArchitecture
    {
        /// <summary>
        /// 64 位元 x86 Windows 發行檔。
        /// </summary>
        X64 = 0
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
        public static DenoWindowsArchitecture CurrentMachine()
        {
#if NET5_0_OR_GREATER || NETSTANDARD2_0
            if (RuntimeInformation.OSArchitecture != Architecture.X64)
            {
                throw new PlatformNotSupportedException("目前只支援 Windows x64 Deno runtime。");
            }

            return DenoWindowsArchitecture.X64;
#else
            return DenoWindowsArchitecture.X64;
#endif
        }

        /// <summary>
        /// 將 Deno Windows 架構轉換為 GitHub 發行資產名稱。
        /// </summary>
        /// <param name="architecture">要轉換的 Deno Windows 架構。</param>
        /// <returns>對應的 Deno 發行資產名稱。</returns>
        internal static string ToAssetName(this DenoWindowsArchitecture architecture)
        {
            return "deno-x86_64-pc-windows-msvc.zip";
        }
    }
}

using System;
using System.Runtime.InteropServices;

namespace MediaEmbedKit.Mpv.Downloads
{
    /// <summary>
    /// 定義 yt-dlp Windows 發行檔支援的處理器架構。
    /// </summary>
    public enum YtDlpWindowsArchitecture
    {
        /// <summary>
        /// 64 位元 x86 Windows 發行檔。
        /// </summary>
        X64 = 0
    }

    /// <summary>
    /// 提供 <see cref="YtDlpWindowsArchitecture"/> 的輔助方法。
    /// </summary>
    public static class YtDlpWindowsArchitectureExtensions
    {
        /// <summary>
        /// 取得目前處理序適用的 yt-dlp Windows 發行檔架構。
        /// </summary>
        /// <returns>目前處理序適用的 yt-dlp Windows 架構。</returns>
        public static YtDlpWindowsArchitecture CurrentProcess()
        {
#if NET5_0_OR_GREATER || NETSTANDARD2_0
            if (RuntimeInformation.OSArchitecture != Architecture.X64 || RuntimeInformation.ProcessArchitecture != Architecture.X64)
            {
                throw new PlatformNotSupportedException("目前只支援 Windows x64 yt-dlp runtime。");
            }

            return YtDlpWindowsArchitecture.X64;
#else
            if (IntPtr.Size != 8)
            {
                throw new PlatformNotSupportedException("目前只支援 Windows x64 yt-dlp runtime。");
            }

            return YtDlpWindowsArchitecture.X64;
#endif
        }

        /// <summary>
        /// 將 yt-dlp Windows 架構轉換為 GitHub 發行資產名稱。
        /// </summary>
        /// <param name="architecture">要轉換的 yt-dlp Windows 架構。</param>
        /// <returns>對應的 yt-dlp 發行資產名稱。</returns>
        internal static string ToAssetName(this YtDlpWindowsArchitecture architecture)
        {
            return "yt-dlp.exe";
        }
    }
}

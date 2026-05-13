using System.Collections.Generic;

namespace MediaEmbedKit.Mpv.Downloads
{
    /// <summary>
    /// 表示平台感知執行階段安裝 helper 的結果。
    /// </summary>
    public sealed class MpvRuntimeInstallResult
    {
        /// <summary>
        /// 初始化 <see cref="MpvRuntimeInstallResult"/> 類別的新執行個體。
        /// </summary>
        /// <param name="platform">本次安裝目標平台。</param>
        /// <param name="status">本專案對該平台的支援狀態。</param>
        /// <param name="runtimeDirectory">執行階段資料夾路徑。</param>
        /// <param name="message">安裝結果訊息。</param>
        /// <param name="windows">Windows 執行階段下載結果。</param>
        /// <param name="nativeSources">平台對應的原生來源清單。</param>
        internal MpvRuntimeInstallResult(
            MpvNativeRuntimePlatform platform,
            MpvNativeRuntimeSupportStatus status,
            string runtimeDirectory,
            string message,
            MpvWindowsRuntimeDownloadResult? windows,
            IReadOnlyList<MpvNativeRuntimeSource> nativeSources)
        {
            Platform = platform;
            Status = status;
            RuntimeDirectory = runtimeDirectory;
            Message = message;
            Windows = windows;
            NativeSources = nativeSources;
        }

        /// <summary>
        /// 取得本次安裝目標平台。
        /// </summary>
        /// <value>執行階段平台。</value>
        public MpvNativeRuntimePlatform Platform { get; private set; }

        /// <summary>
        /// 取得本專案對該平台的支援狀態。
        /// </summary>
        /// <value>平台執行階段支援狀態。</value>
        public MpvNativeRuntimeSupportStatus Status { get; private set; }

        /// <summary>
        /// 取得本次結果是否已完成專案支援的安裝流程。
        /// </summary>
        /// <value>支援狀態為 <see cref="MpvNativeRuntimeSupportStatus.Supported"/> 時為 <see langword="true"/>。</value>
        public bool IsSupported
        {
            get { return Status == MpvNativeRuntimeSupportStatus.Supported; }
        }

        /// <summary>
        /// 取得執行階段資料夾路徑。
        /// </summary>
        /// <value>執行階段資料夾路徑。</value>
        public string RuntimeDirectory { get; private set; }

        /// <summary>
        /// 取得安裝結果訊息。
        /// </summary>
        /// <value>安裝結果訊息。</value>
        public string Message { get; private set; }

        /// <summary>
        /// 取得 Windows 執行階段下載結果。
        /// </summary>
        /// <value>Windows 下載結果；未執行 Windows 安裝流程時為 <see langword="null"/>。</value>
        public MpvWindowsRuntimeDownloadResult? Windows { get; private set; }

        /// <summary>
        /// 取得平台對應的原生來源清單。
        /// </summary>
        /// <value>原生來源清單。</value>
        public IReadOnlyList<MpvNativeRuntimeSource> NativeSources { get; private set; }

        /// <summary>
        /// 取得安裝後的 libmpv 檔案路徑。
        /// </summary>
        /// <value>libmpv 檔案路徑；未安裝時為 <see langword="null"/>。</value>
        public string? LibMpvPath
        {
            get { return Windows == null ? null : Windows.LibMpvPath; }
        }

        /// <summary>
        /// 取得安裝後的 yt-dlp 可執行檔路徑。
        /// </summary>
        /// <value>yt-dlp 可執行檔路徑；未安裝時為 <see langword="null"/>。</value>
        public string? YtDlpPath
        {
            get { return Windows == null ? null : Windows.YtDlpPath; }
        }

        /// <summary>
        /// 取得安裝後的 Deno 可執行檔路徑。
        /// </summary>
        /// <value>Deno 可執行檔路徑；未安裝時為 <see langword="null"/>。</value>
        public string? DenoPath
        {
            get { return Windows == null ? null : Windows.DenoPath; }
        }

        /// <summary>
        /// 取得安裝後的 FFmpeg 可執行檔路徑。
        /// </summary>
        /// <value>FFmpeg 可執行檔路徑；未安裝時為 <see langword="null"/>。</value>
        public string? FFmpegPath
        {
            get { return Windows == null ? null : Windows.FFmpegPath; }
        }

        /// <summary>
        /// 取得安裝後的 FFprobe 可執行檔路徑。
        /// </summary>
        /// <value>FFprobe 可執行檔路徑；未安裝時為 <see langword="null"/>。</value>
        public string? FFprobePath
        {
            get { return Windows == null ? null : Windows.FFprobePath; }
        }
    }
}

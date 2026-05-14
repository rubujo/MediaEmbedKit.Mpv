using System;

namespace MediaEmbedKit.Mpv.Downloads;

/// <summary>
/// 提供下載 yt-dlp 專用 FFmpeg Windows x64 建置時使用的選項。
/// </summary>
public sealed class FFmpegDownloadOptions
{
    /// <summary>
    /// 初始化 <see cref="FFmpegDownloadOptions"/> 類別的新執行個體。
    /// </summary>
    public FFmpegDownloadOptions()
    {
        Architecture = MpvWindowsArchitectureExtensions.CurrentProcess();
        UserAgent = BrowserRequestHeaders.ChromeStableUserAgent;
        OverwriteExisting = false;
        VerifyDigest = true;
        VerificationPolicy = MpvNativeAssetVerificationPolicy.RequireGitHubDigest;
    }

    /// <summary>
    /// 取得或設定要下載的 FFmpeg-Builds Windows 架構。
    /// </summary>
    /// <value>FFmpeg-Builds Windows 架構，預設為目前處理序架構。</value>
    public MpvWindowsArchitecture Architecture { get; set; }

    /// <summary>
    /// 取得或設定是否覆寫已存在的 FFmpeg 執行檔與壓縮檔。
    /// </summary>
    /// <value>覆寫已存在檔案時為 <see langword="true"/>。</value>
    public bool OverwriteExisting { get; set; }

    /// <summary>
    /// 取得或設定是否驗證 GitHub 發行資產提供的雜湊值。
    /// </summary>
    /// <value>驗證可用的 SHA-256 摘要時為 <see langword="true"/>。</value>
    public bool VerifyDigest { get; set; }

    /// <summary>
    /// 取得或設定下載資產的完整性驗證策略。
    /// </summary>
    /// <value>下載 FFmpeg 發行資產時採用的驗證策略。</value>
    public MpvNativeAssetVerificationPolicy VerificationPolicy { get; set; }

    /// <summary>
    /// 取得或設定預期的 FFmpeg 壓縮檔 SHA-256 值。
    /// </summary>
    /// <value>呼叫端釘選的 SHA-256 十六進位文字；未指定時不進行釘選驗證。</value>
    public string? ExpectedSha256 { get; set; }

    /// <summary>
    /// 取得或設定是否鎖定為 yt-dlp FFmpeg-Builds GitHub 發行來源。
    /// </summary>
    /// <value>拒絕非預設 GitHub Releases API 或下載 URL 時為 <see langword="true"/>。</value>
    public bool LockReleaseSource { get; set; }

    /// <summary>
    /// 取得或設定下載要求使用的使用者代理字串。
    /// </summary>
    /// <value>HTTP 使用者代理字串；未指定時使用專案預設的 Chrome 穩定版設定。</value>
    public string? UserAgent { get; set; }

    /// <summary>
    /// 取得或設定取代預設 GitHub 最新發行 API 的 URI。
    /// </summary>
    /// <value>自訂 GitHub 發行 API URI；未指定時使用 yt-dlp FFmpeg-Builds 發行 API。</value>
    public Uri? ReleaseApiUriOverride { get; set; }
}

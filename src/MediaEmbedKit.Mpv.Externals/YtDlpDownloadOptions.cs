using System;

namespace MediaEmbedKit.Mpv.Externals;

/// <summary>
/// 提供下載 yt-dlp Windows 可執行檔時使用的選項。
/// </summary>
public sealed class YtDlpDownloadOptions
{
    /// <summary>
    /// 初始化 <see cref="YtDlpDownloadOptions"/> 類別的新執行個體。
    /// </summary>
    public YtDlpDownloadOptions()
    {
        Channel = YtDlpReleaseChannel.Stable;
        Architecture = YtDlpWindowsArchitectureExtensions.CurrentProcess();
        UserAgent = BrowserRequestHeaders.ChromeStableUserAgent;
        OverwriteExisting = false;
        VerifyDigest = true;
        VerificationPolicy = MpvNativeAssetVerificationPolicy.RequireGitHubDigest;
    }

    /// <summary>
    /// 取得或設定 yt-dlp 發行通道。
    /// </summary>
    /// <value>要使用的 yt-dlp 發行通道。</value>
    public YtDlpReleaseChannel Channel { get; set; }

    /// <summary>
    /// 取得或設定要下載的 yt-dlp Windows 架構。
    /// </summary>
    /// <value>yt-dlp Windows 發行檔架構。</value>
    public YtDlpWindowsArchitecture Architecture { get; set; }

    /// <summary>
    /// 取得或設定是否覆寫已存在的下載檔案。
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
    /// <value>下載 yt-dlp 發行資產時採用的驗證策略。</value>
    public MpvNativeAssetVerificationPolicy VerificationPolicy { get; set; }

    /// <summary>
    /// 取得或設定預期的 yt-dlp 可執行檔 SHA-256 值。
    /// </summary>
    /// <value>呼叫端釘選的 SHA-256 十六進位文字；未指定時不進行釘選驗證。</value>
    public string? ExpectedSha256 { get; set; }

    /// <summary>
    /// 取得或設定是否鎖定為專案內建的官方 GitHub 發行來源。
    /// </summary>
    /// <value>拒絕非預設 GitHub Releases API 或下載 URL 時為 <see langword="true"/>。</value>
    /// <remarks>
    /// <para>
    /// <strong>此選項不是供應鏈防線</strong>。它只擋兩件事：呼叫端被誘騙設
    /// <see cref="ReleaseApiUriOverride"/> 指向假 API；GitHub API 回傳的 download URL
    /// 不屬於預設 owner/repo（yt-dlp/yt-dlp 或對應 nightly / master-builds repo）。
    /// </para>
    /// <para>
    /// 它<strong>無法防護</strong>上游 maintainer 帳號被入侵 / GitHub 被入侵 / DNS /
    /// TLS 中間人攻擊等供應鏈攻擊。真正的防線是<strong>呼叫端釘版 SHA-256</strong>：
    /// 設 <see cref="VerificationPolicy"/> = <see cref="MpvNativeAssetVerificationPolicy.RequirePinnedSha256"/>
    /// 並把預期 SHA-256 寫進 <see cref="ExpectedSha256"/>。商用環境必走此路。
    /// </para>
    /// </remarks>
    public bool LockReleaseSource { get; set; }

    /// <summary>
    /// 取得或設定下載要求使用的使用者代理字串。
    /// </summary>
    /// <value>HTTP 使用者代理字串；未指定時使用專案預設的 Chrome 穩定版設定。</value>
    public string? UserAgent { get; set; }

    /// <summary>
    /// 取得或設定取代預設 GitHub 最新發行 API 的 URI。
    /// </summary>
    /// <value>自訂 GitHub 發行 API URI；未指定時依發行通道使用預設 API。</value>
    public Uri? ReleaseApiUriOverride { get; set; }
}

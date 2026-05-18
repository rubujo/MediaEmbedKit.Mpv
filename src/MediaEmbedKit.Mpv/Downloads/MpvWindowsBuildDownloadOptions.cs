using System;
using System.Collections.Generic;

namespace MediaEmbedKit.Mpv.Downloads;

/// <summary>
/// 提供下載 Windows libmpv 建置時使用的選項。
/// </summary>
public sealed class MpvWindowsBuildDownloadOptions
{
    /// <summary>
    /// 初始化 <see cref="MpvWindowsBuildDownloadOptions"/> 類別的新執行個體。
    /// </summary>
    public MpvWindowsBuildDownloadOptions()
    {
        Architecture = MpvWindowsArchitectureExtensions.CurrentProcess();
        Provider = MpvWindowsBuildProvider.Zhongfly;
        UserAgent = BrowserRequestHeaders.ChromeStableUserAgent;
        LicensePreference = MpvWindowsBuildLicensePreference.PreferLgpl;
        OverwriteExisting = false;
        VerificationPolicy = MpvNativeAssetVerificationPolicy.RequireGitHubDigest;
        ProviderFallbackOrder = new List<MpvWindowsBuildProvider>
        {
            MpvWindowsBuildProvider.Shinchiro,
        };
        RetainArchive = false;
    }

    /// <summary>
    /// 取得 <see cref="Provider"/> 失敗時的備援嘗試順序。
    /// </summary>
    /// <value>
    /// 下載失敗時要依序嘗試的備援 provider 清單；**預設為 [<see cref="MpvWindowsBuildProvider.Shinchiro"/>]**。
    /// 搭配新預設 <see cref="Provider"/> = <see cref="MpvWindowsBuildProvider.Zhongfly"/>，
    /// zhongfly 失效（API 掛掉、release 結構改變等）時自動 fallback 到 shinchiro，
    /// 保留歷史 provider 作為兜底。呼叫端可清空或重排此清單以調整 fallback 策略。
    /// </value>
    /// <remarks>
    /// 集合不包含 <see cref="Provider"/> 本身（會先嘗試 <see cref="Provider"/> 再依序嘗試此清單）。
    /// 集合中重複出現的 provider 與 <see cref="Provider"/> 相同的項目會自動跳過。
    /// </remarks>
    public IList<MpvWindowsBuildProvider> ProviderFallbackOrder { get; }

    /// <summary>
    /// 取得或設定 Windows libmpv 建置來源提供者。
    /// </summary>
    /// <value>
    /// Windows libmpv 建置提供者。**預設為 <see cref="MpvWindowsBuildProvider.Zhongfly"/>** ——
    /// 為兩個 provider 中唯一同時提供 GPL 與 LGPL libmpv build 的來源，搭配預設
    /// <see cref="LicensePreference"/> = <see cref="MpvWindowsBuildLicensePreference.PreferLgpl"/>
    /// 能實際拿到 LGPL build，對商用閉源散發較安全。<see cref="MpvWindowsBuildProvider.Shinchiro"/>
    /// 只發 GPL build，切到該 provider 時 <see cref="LicensePreference"/> 偏好 LGPL 會
    /// silently fallback 到 GPL；商用嚴格合規請改用
    /// <see cref="MpvWindowsBuildLicensePreference.RequireLgpl"/> 讓不可用情境 fail-loud。
    /// </value>
    public MpvWindowsBuildProvider Provider { get; set; }

    /// <summary>
    /// 取得或設定要下載的 Windows libmpv 架構。
    /// </summary>
    /// <value>Windows libmpv 建置架構。</value>
    public MpvWindowsArchitecture Architecture { get; set; }

    /// <summary>
    /// 取得或設定 libmpv 建置授權偏好。
    /// </summary>
    /// <value>
    /// 用來篩選或偏好發行資產的授權偏好。**預設為
    /// <see cref="MpvWindowsBuildLicensePreference.PreferLgpl"/>**：上游有 LGPL 變體時優先
    /// 選用，無 LGPL 變體時 fallback 到 GPL；對「不確定散發授權」的多數使用者較安全的
    /// 預設值。商用嚴格合規請設 <see cref="MpvWindowsBuildLicensePreference.RequireLgpl"/>
    /// （沒 LGPL 直接 fail，不靜默 fallback）。沒有授權偏好需求請設
    /// <see cref="MpvWindowsBuildLicensePreference.Any"/>。
    /// </value>
    public MpvWindowsBuildLicensePreference LicensePreference { get; set; }

    /// <summary>
    /// 取得或設定是否覆寫已存在的下載檔案。
    /// </summary>
    /// <value>覆寫已存在檔案時為 <see langword="true"/>。</value>
    public bool OverwriteExisting { get; set; }

    /// <summary>
    /// 取得或設定下載資產的完整性驗證策略。
    /// </summary>
    /// <value>下載 libmpv 壓縮檔時採用的驗證策略。</value>
    public MpvNativeAssetVerificationPolicy VerificationPolicy { get; set; }

    /// <summary>
    /// 取得或設定預期的 libmpv 壓縮檔 SHA-256 值。
    /// </summary>
    /// <value>呼叫端釘選的 SHA-256 十六進位文字；未指定時不進行釘選驗證。</value>
    public string? ExpectedSha256 { get; set; }

    /// <summary>
    /// 取得或設定是否鎖定為所選 provider 的內建 GitHub 發行來源。
    /// </summary>
    /// <value>拒絕非預設 GitHub Releases API 或下載 URL 時為 <see langword="true"/>。</value>
    public bool LockReleaseSource { get; set; }

    /// <summary>
    /// 取得或設定是否驗證 GitHub 發行資產提供的雜湊值。
    /// </summary>
    /// <value>驗證可用的 SHA-256 摘要時為 <see langword="true"/>。</value>
    public bool VerifyDigest { get; set; } = true;

    /// <summary>
    /// 取得或設定下載要求使用的使用者代理字串。
    /// </summary>
    /// <value>HTTP 使用者代理字串；未指定時使用專案預設的 Chrome 穩定版設定。</value>
    public string? UserAgent { get; set; }

    /// <summary>
    /// 取得或設定 7-Zip 可執行檔路徑。
    /// </summary>
    /// <value>7z.exe 路徑；未指定時由 helper 自動搜尋。</value>
    public string? SevenZipPath { get; set; }

    /// <summary>
    /// 取得或設定 libmpv 壓縮檔的解壓縮資料夾。
    /// </summary>
    /// <value>解壓縮目標資料夾；未指定時使用下載資料夾下的預設資料夾。</value>
    public string? ExtractDirectory { get; set; }

    /// <summary>
    /// 取得或設定取代預設 GitHub 最新發行 API 的 URI。
    /// </summary>
    /// <value>自訂 GitHub 發行 API URI；未指定時依提供者使用預設 API。</value>
    public Uri? ReleaseApiUriOverride { get; set; }

    /// <summary>
    /// 取得或設定解壓縮成功後是否保留下載的 libmpv 壓縮檔。
    /// </summary>
    /// <value>
    /// 保留壓縮檔時為 <see langword="true"/>；預設為 <see langword="false"/>，
    /// 解壓縮成功後刪除壓縮檔以避免長期佔用磁碟（libmpv .7z 約 50–100 MB）。
    /// 需在 warm restart 重新驗證 SHA-256 而省下載成本時，應明確設為 <see langword="true"/>。
    /// </value>
    public bool RetainArchive { get; set; }
}

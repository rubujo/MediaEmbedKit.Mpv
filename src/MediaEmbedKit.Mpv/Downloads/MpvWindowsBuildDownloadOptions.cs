using System;

namespace MediaEmbedKit.Mpv.Downloads
{
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
            Provider = MpvWindowsBuildProvider.Shinchiro;
            UserAgent = BrowserRequestHeaders.ChromeStableUserAgent;
            LicensePreference = MpvWindowsBuildLicensePreference.Any;
            OverwriteExisting = false;
            VerificationPolicy = MpvNativeAssetVerificationPolicy.BestEffort;
        }

        /// <summary>
        /// 取得或設定 Windows libmpv 建置來源提供者。
        /// </summary>
        /// <value>Windows libmpv 建置提供者。</value>
        public MpvWindowsBuildProvider Provider { get; set; }

        /// <summary>
        /// 取得或設定要下載的 Windows libmpv 架構。
        /// </summary>
        /// <value>Windows libmpv 建置架構。</value>
        public MpvWindowsArchitecture Architecture { get; set; }

        /// <summary>
        /// 取得或設定 libmpv 建置授權偏好。
        /// </summary>
        /// <value>用來篩選或偏好發行資產的授權偏好。</value>
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
    }
}

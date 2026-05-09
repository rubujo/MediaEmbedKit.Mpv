using System;

namespace MediaEmbedKit.Mpv.Downloads
{
    /// <summary>
    /// 提供下載 Deno Windows 可執行檔時使用的選項。
    /// </summary>
    public sealed class DenoDownloadOptions
    {
        /// <summary>
        /// 初始化 <see cref="DenoDownloadOptions"/> 類別的新執行個體。
        /// </summary>
        public DenoDownloadOptions()
        {
            Architecture = DenoWindowsArchitectureExtensions.CurrentMachine();
            UserAgent = BrowserRequestHeaders.ChromeStableUserAgent;
            OverwriteExisting = false;
            VerifyDigest = true;
        }

        /// <summary>
        /// 取得或設定要下載的 Deno Windows 架構。
        /// </summary>
        /// <value>Deno Windows 發行檔架構。</value>
        public DenoWindowsArchitecture Architecture { get; set; }

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
        /// 取得或設定下載要求使用的使用者代理字串。
        /// </summary>
        /// <value>HTTP 使用者代理字串；未指定時使用專案預設的 Chrome 穩定版設定。</value>
        public string? UserAgent { get; set; }

        /// <summary>
        /// 取得或設定取代預設 GitHub 最新發行 API 的 URI。
        /// </summary>
        /// <value>自訂 GitHub 發行 API URI；未指定時使用 Deno 官方發行 API。</value>
        public Uri? ReleaseApiUriOverride { get; set; }
    }
}

using System.Runtime.Serialization;

namespace MediaEmbedKit.Mpv.Downloads
{
    /// <summary>
    /// 表示 GitHub Releases API 傳回的發行資料。
    /// </summary>
    [DataContract]
    internal sealed class GitHubRelease
    {
        /// <summary>
        /// 取得或設定 GitHub 發行標籤。
        /// </summary>
        /// <value>GitHub 發行標籤文字。</value>
        [DataMember(Name = "tag_name")]
        public string TagName { get; set; } = string.Empty;

        /// <summary>
        /// 取得或設定 GitHub 發行資產集合。
        /// </summary>
        /// <value>GitHub 發行資產陣列。</value>
        [DataMember(Name = "assets")]
        public GitHubReleaseAsset[] Assets { get; set; } = new GitHubReleaseAsset[0];
    }

    /// <summary>
    /// 表示 GitHub Releases API 傳回的單一發行資產。
    /// </summary>
    [DataContract]
    internal sealed class GitHubReleaseAsset
    {
        /// <summary>
        /// 取得或設定 GitHub 發行資產名稱。
        /// </summary>
        /// <value>GitHub 發行資產名稱。</value>
        [DataMember(Name = "name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 取得或設定瀏覽器下載 URL。
        /// </summary>
        /// <value>GitHub 發行資產瀏覽器下載 URL。</value>
        [DataMember(Name = "browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = string.Empty;

        /// <summary>
        /// 取得或設定 GitHub 發行資產摘要值。
        /// </summary>
        /// <value>發行資產摘要值；API 未提供時為 <see langword="null"/>。</value>
        [DataMember(Name = "digest")]
        public string? Digest { get; set; }
    }
}

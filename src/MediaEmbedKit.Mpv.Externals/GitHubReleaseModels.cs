using System.Text.Json.Serialization;

namespace MediaEmbedKit.Mpv.Externals;

/// <summary>
/// 表示 GitHub Releases API 傳回的發行資料。
/// </summary>
internal sealed class GitHubRelease
{
    /// <summary>
    /// 取得或設定 GitHub 發行標籤。
    /// </summary>
    /// <value>
    /// GitHub 發行標籤文字。
    /// </value>
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = string.Empty;

    /// <summary>
    /// 取得或設定 GitHub 發行資產集合。
    /// </summary>
    /// <value>
    /// GitHub 發行資產陣列。
    /// </value>
    [JsonPropertyName("assets")]
    public GitHubReleaseAsset[] Assets { get; set; } = new GitHubReleaseAsset[0];
}

/// <summary>
/// 表示 GitHub Releases API 傳回的單一發行資產。
/// </summary>
internal sealed class GitHubReleaseAsset
{
    /// <summary>
    /// 取得或設定 GitHub 發行資產名稱。
    /// </summary>
    /// <value>
    /// GitHub 發行資產名稱。
    /// </value>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 取得或設定瀏覽器下載 URL。
    /// </summary>
    /// <value>
    /// GitHub 發行資產瀏覽器下載 URL。
    /// </value>
    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = string.Empty;

    /// <summary>
    /// 取得或設定 GitHub 發行資產摘要值。
    /// </summary>
    /// <value>
    /// 發行資產摘要值；API 未提供時為 <see langword="null"/>。
    /// </value>
    [JsonPropertyName("digest")]
    public string? Digest { get; set; }
}

/// <summary>
/// 為 GitHub Releases API DTO 提供 System.Text.Json source-generated 序列化內容，
/// 避免 NativeAOT / trimming 下走 reflection-based serializer。
/// </summary>
[JsonSerializable(typeof(GitHubRelease))]
[JsonSerializable(typeof(GitHubReleaseAsset))]
internal sealed partial class GitHubReleaseJsonContext : JsonSerializerContext
{
}

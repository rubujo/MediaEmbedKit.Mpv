using System;
using System.Net.Http.Headers;

namespace MediaEmbedKit.Mpv.Externals;

/// <summary>
/// 提供下載要求使用的瀏覽器相容 HTTP 標頭。
/// </summary>
internal static class BrowserRequestHeaders
{
    /// <summary>
    /// 專案預設模擬的 Chrome 穩定版完整版本。
    /// </summary>
    public const string ChromeStableVersion = "148.0.7778.168";
    /// <summary>
    /// 專案預設模擬的 Chrome 穩定版主要版本。
    /// </summary>
    public const string ChromeStableMajorVersion = "148";

    /// <summary>
    /// 專案預設的 Chrome 穩定版使用者代理字串。
    /// </summary>
    public const string ChromeStableUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/148.0.7778.168 Safari/537.36";

    /// <summary>
    /// 專案預設的 sec-ch-ua 用戶端提示標頭值。
    /// </summary>
    public const string SecChUa =
        "\"Chromium\";v=\"148\", \"Google Chrome\";v=\"148\", \"Not=A?Brand\";v=\"24\"";

    /// <summary>
    /// 專案預設的 sec-ch-ua-full-version-list 用戶端提示標頭值。
    /// </summary>
    public const string SecChUaFullVersionList =
        "\"Chromium\";v=\"148.0.7778.168\", \"Google Chrome\";v=\"148.0.7778.168\", \"Not=A?Brand\";v=\"24.0.0.0\"";

    /// <summary>
    /// 專案預設的 sec-ch-ua-mobile 用戶端提示標頭值。
    /// </summary>
    public const string SecChUaMobile = "?0";
    /// <summary>
    /// 專案預設的 sec-ch-ua-platform 用戶端提示標頭值。
    /// </summary>
    public const string SecChUaPlatform = "\"Windows\"";

    /// <summary>
    /// 將瀏覽器相容標頭套用到指定的 HTTP 要求標頭集合。
    /// </summary>
    /// <param name="headers">要套用標頭的 HTTP 要求標頭集合。</param>
    /// <param name="userAgent">自訂使用者代理字串；未指定時使用專案預設值。</param>
    /// <remarks>
    /// 套用完整 Chrome 瀏覽器標頭（含 sec-ch-ua 系列 client hints），適用於下載 CDN
    /// （GitHub release asset / shinchiro / zhongfly 等）—— 部分 CDN 對非瀏覽器 UA
    /// 有 rate-limit / anti-bot 行為。對 GitHub <c>api.github.com</c> JSON API 請改用
    /// <see cref="ApplyForGitHubApi"/>（誠實 UA、不發 client hints）。
    /// </remarks>
    public static void Apply(HttpRequestHeaders headers, string? userAgent)
    {
        string effectiveUserAgent = string.IsNullOrWhiteSpace(userAgent) ? ChromeStableUserAgent : userAgent!;
        string chromeVersion = GetChromeVersion(effectiveUserAgent);
        string chromeMajorVersion = GetChromeMajorVersion(chromeVersion);

        headers.UserAgent.ParseAdd(effectiveUserAgent);
        headers.TryAddWithoutValidation("sec-ch-ua", CreateSecChUa(chromeMajorVersion));
        headers.TryAddWithoutValidation("sec-ch-ua-full-version-list", CreateSecChUaFullVersionList(chromeVersion));
        headers.TryAddWithoutValidation("sec-ch-ua-mobile", SecChUaMobile);
        headers.TryAddWithoutValidation("sec-ch-ua-platform", SecChUaPlatform);
    }

    /// <summary>
    /// 將 GitHub API 適用的誠實 UA 套到指定的 HTTP 要求標頭集合（**不**含 sec-ch-ua
    /// 系列 client hints —— JSON API 不需要也不期望這些瀏覽器專屬 headers）。
    /// </summary>
    /// <param name="headers">要套用標頭的 HTTP 要求標頭集合。</param>
    /// <param name="userAgent">
    /// 自訂使用者代理字串；未指定時用 <c>MediaEmbedKit.Mpv/&lt;assemblyVersion&gt; (+repo)</c>
    /// 形式，符合 GitHub ToS 偏好「標識性 UA」的觀感。
    /// </param>
    public static void ApplyForGitHubApi(HttpRequestHeaders headers, string? userAgent)
    {
        string effectiveUserAgent = string.IsNullOrWhiteSpace(userAgent) ? GitHubApiUserAgent : userAgent!;
        headers.UserAgent.ParseAdd(effectiveUserAgent);
    }

    /// <summary>
    /// 對 <c>api.github.com</c> 使用的誠實識別 UA。包含 helper 名稱、版本與 repo URL，
    /// 符合 GitHub ToS 偏好的「標識性 UA」觀感，且不發瀏覽器 client hints（JSON API 不需）。
    /// </summary>
    public static readonly string GitHubApiUserAgent = "MediaEmbedKit.Mpv/" + GetAssemblyVersion() + " (+https://github.com/rubujo/MediaEmbedKit.Mpv)";

    /// <summary>取得當前 assembly 版本字串（用於建構 GitHubApiUserAgent）。</summary>
    private static string GetAssemblyVersion()
    {
        try
        {
            System.Reflection.AssemblyName name = typeof(BrowserRequestHeaders).Assembly.GetName();
            return name.Version?.ToString(3) ?? "0.0.0";
        }
        catch
        {
            return "0.0.0";
        }
    }

    /// <summary>
    /// 從使用者代理字串取得 Chrome 完整版本。
    /// </summary>
    /// <param name="userAgent">要檢查的使用者代理字串。</param>
    /// <returns>Chrome 完整版本；無法解析時傳回專案預設版本。</returns>
    private static string GetChromeVersion(string userAgent)
    {
        const string token = "Chrome/";
        int tokenIndex = userAgent.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        if (tokenIndex < 0)
        {
            return ChromeStableVersion;
        }

        int versionStart = tokenIndex + token.Length;
        int versionEnd = versionStart;
        while (versionEnd < userAgent.Length)
        {
            char character = userAgent[versionEnd];
            if (!char.IsDigit(character) && character != '.')
            {
                break;
            }

            versionEnd++;
        }

        if (versionEnd == versionStart)
        {
            return ChromeStableVersion;
        }

        return userAgent.Substring(versionStart, versionEnd - versionStart);
    }

    /// <summary>
    /// 從 Chrome 完整版本取得主要版本。
    /// </summary>
    /// <param name="version">Chrome 完整版本。</param>
    /// <returns>Chrome 主要版本；無法解析時傳回專案預設主要版本。</returns>
    private static string GetChromeMajorVersion(string version)
    {
        int separatorIndex = version.IndexOf('.');
        if (separatorIndex <= 0)
        {
            return string.IsNullOrWhiteSpace(version) ? ChromeStableMajorVersion : version;
        }

        return version.Substring(0, separatorIndex);
    }

    /// <summary>
    /// 建立 sec-ch-ua 標頭值。
    /// </summary>
    /// <param name="majorVersion">Chrome 主要版本。</param>
    /// <returns>可加入 HTTP 要求的 sec-ch-ua 標頭值。</returns>
    private static string CreateSecChUa(string majorVersion)
    {
        if (string.Equals(majorVersion, ChromeStableMajorVersion, StringComparison.Ordinal))
        {
            return SecChUa;
        }

        return "\"Chromium\";v=\"" + majorVersion + "\", \"Google Chrome\";v=\"" + majorVersion + "\", \"Not=A?Brand\";v=\"24\"";
    }

    /// <summary>
    /// 建立 sec-ch-ua-full-version-list 標頭值。
    /// </summary>
    /// <param name="version">Chrome 完整版本。</param>
    /// <returns>可加入 HTTP 要求的 sec-ch-ua-full-version-list 標頭值。</returns>
    private static string CreateSecChUaFullVersionList(string version)
    {
        if (string.Equals(version, ChromeStableVersion, StringComparison.Ordinal))
        {
            return SecChUaFullVersionList;
        }

        return "\"Chromium\";v=\"" + version + "\", \"Google Chrome\";v=\"" + version + "\", \"Not=A?Brand\";v=\"24.0.0.0\"";
    }
}

namespace MediaEmbedKit.Mpv.Externals;

/// <summary>
/// 指定下載原生執行階段資產時採用的完整性驗證策略。
/// </summary>
public enum MpvNativeAssetVerificationPolicy
{
    /// <summary>
    /// GitHub 提供 SHA-256 摘要時進行驗證，未提供時不阻擋下載流程。
    /// </summary>
    BestEffort = 0,

    /// <summary>
    /// 要求 GitHub 發行資產必須提供 SHA-256 摘要，且下載內容必須相符。
    /// </summary>
    RequireGitHubDigest = 1,

    /// <summary>
    /// 要求 GitHub SHA-256 摘要與 upstream 發行的 checksum 檔案皆必須通過驗證。
    /// </summary>
    RequireProviderChecksum = 2,

    /// <summary>
    /// 要求呼叫端提供預期 SHA-256 值，且下載內容必須與該值相符。
    /// </summary>
    RequirePinnedSha256 = 3
}

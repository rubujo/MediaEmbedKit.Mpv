using System;
using System.IO;

namespace MediaEmbedKit.Mpv.Externals;

/// <summary>
/// 對解壓後的檔案做安全檢查，防護惡意 archive 透過 symlink / reparse point 把
/// 解壓寫入導向 archive 範圍外的系統路徑（對應 CVE-2025-11001 同類攻擊）。
/// </summary>
/// <remarks>
/// <para>
/// 本 helper 是 defense-in-depth 層；上游 archive 通常可信，但 helper 從 GitHub 抓
/// 第三方 binary，威脅模型必須假設 maintainer 帳號被入侵 / archive 被替換的場景
/// （此時 GitHub asset.digest 也是被入侵者改的，本專案的 SHA 驗證無法擋下）。
/// 解壓後加一道「不接受 reparse point / symlink 指到 archive 外的關鍵檔」檢查，
/// 至少能擋住「替換 libmpv-2.dll 成指向系統 DLL 的 symlink」這類具體攻擊。
/// </para>
/// <para>
/// 真正的供應鏈防線仍是 caller 提供 <c>ExpectedSha256</c> +
/// <c>RequirePinnedSha256</c> policy；本 helper 是 defense-in-depth，不是替代。
/// </para>
/// </remarks>
internal static class ArchiveSafety
{
    /// <summary>
    /// 拒絕指定檔案路徑為 symlink / NTFS reparse point。若是 → 立刻刪除並擲
    /// <see cref="InvalidOperationException"/>。
    /// </summary>
    /// <param name="filePath">要檢查的檔案路徑。</param>
    /// <param name="contextDescription">擲例外時要包含的 context 描述（給 caller / log 用）。</param>
    /// <exception cref="InvalidOperationException">檔案是 reparse point。</exception>
    public static void RejectIfReparsePoint(string filePath, string contextDescription)
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        FileAttributes attributes;
        try
        {
            attributes = File.GetAttributes(filePath);
        }
        catch (IOException)
        {
            // 無法讀屬性 → 保守拒絕（這個檔案不該被信任繼續使用）。
            TryDeleteFile(filePath);
            throw new InvalidOperationException(
                "無法讀取檔案屬性以驗證 reparse point；已刪除以防 archive 不安全內容繼續使用：" + filePath +
                "（context: " + contextDescription + "）");
        }
        catch (UnauthorizedAccessException)
        {
            TryDeleteFile(filePath);
            throw new InvalidOperationException(
                "無權限讀取檔案屬性；已嘗試刪除：" + filePath + "（context: " + contextDescription + "）");
        }

        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            TryDeleteFile(filePath);
            throw new InvalidOperationException(
                "解壓出的檔案為 symlink / NTFS reparse point，拒絕信任：" + filePath +
                "（context: " + contextDescription + "）。" +
                "可能是 archive 被替換成含 symlink 指向系統路徑的攻擊（參見 CVE-2025-11001 同類）。" +
                "請使用 ExpectedSha256 + RequirePinnedSha256 驗證 archive 完整性。");
        }
    }

    /// <summary>
    /// 嘗試刪除檔案；失敗吞掉（呼叫端要 throw 給上層，刪不掉只是清理失敗、不影響擲例外）。
    /// </summary>
    private static void TryDeleteFile(string filePath)
    {
        try
        {
            File.Delete(filePath);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

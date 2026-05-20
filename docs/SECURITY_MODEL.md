# 供應鏈風險模型

本文件說明 `MediaEmbedKit.Mpv` runtime helper 的威脅模型、防線與限制。這是使用者指引與風險揭露，不是安全支援政策。本專案不提供 GitHub Issues、私訊、email、bug bounty、SLA 或安全事件處理通道；正式政策見 [`../SECURITY.md`](../SECURITY.md)。

## 威脅模型

helper 自動下載第三方原生二進位（libmpv、yt-dlp、Deno、FFmpeg），並在使用者處理序內 `NativeLibrary.Load` 載入 libmpv。可信邊界是「helper 從 GitHub 抓到的 binary 完整未被竄改，且 GitHub 與上游 release 本身可信」。

| 威脅 | helper 是否防護 |
| --- | --- |
| 上游 maintainer GitHub 帳號被入侵，發出含後門的 `libmpv-2.dll` | 不防。攻擊者可同步替換 binary 與 GitHub asset digest，預設驗證仍會通過。 |
| GitHub.com 本身被入侵 | 不防。 |
| 對 GitHub API / CDN 的 DNS 攻擊 / TLS 中間人 | 不防。`HttpClient` 走 OS TLS，但無 cert pinning。 |
| 惡意 archive 含 symlink / reparse point 指向系統路徑 | 防。`ArchiveSafety.RejectIfReparsePoint` 會拒絕含 reparse point 的解壓內容。 |
| `RequirePinnedSha256` + `ExpectedSha256` 防住的 binary 替換 | 防，前提是 caller 從 GitHub release 以外的可信通道取得 SHA pin。 |
| 多個處理序共用 runtime 目錄造成寫入競爭 | 防。`RuntimeDirectoryLock` 以 cross-process `FileStream` 鎖包覆 install / stage / apply / prune 序列。 |
| 已載入 libmpv 後嘗試 hot-reload 新版 | 防。`MpvLibraryUpdateScheduler` 使用 staged update + restart-required 流程。 |

## helper 防線

### GitHub asset digest 驗證

`MpvNativeAssetVerificationPolicy.RequireGitHubDigest`（預設）要求 GitHub Releases API 提供 `sha256:` digest，並驗證下載內容與 digest 一致。`RequireProviderChecksum` 額外驗證 provider 發行的 checksum 檔（例如 yt-dlp 的 `SHA2-256SUMS`）。

此機制可防下載傳輸錯誤與 accidental corruption；不能防上游帳號或 GitHub release 本身遭入侵。

### NuGet 套件 build provenance

本專案發行的 `.nupkg` 與 `.snupkg` 透過 `actions/attest-build-provenance` 產生 Sigstore-signed build provenance attestation，寫入 Rekor transparency log。consumer 可用 GitHub CLI 或 cosign 驗證套件是否由 `github.com/rubujo/MediaEmbedKit.Mpv` 的特定 commit 與 workflow run 建出。

```powershell
gh attestation verify MediaEmbedKit.Mpv.<version>.nupkg --owner rubujo
```

provenance 只涵蓋本專案 NuGet 套件，不涵蓋 helper 下載的第三方 runtime binary。

### 釘版 SHA-256

商用、受監管或高信任需求環境應使用 `RequirePinnedSha256` + `ExpectedSha256`：

```csharp
MpvWindowsBuildDownloadOptions options = new MpvWindowsBuildDownloadOptions
{
    VerificationPolicy = MpvNativeAssetVerificationPolicy.RequirePinnedSha256,
    ExpectedSha256 = "abc123...64-hex-chars...",
    OverwriteExisting = true,
};
```

建議 consumer 維護自己的 SHA pin 清單，並從 GitHub release 以外的可信流程審查後更新。若 SHA pin 驗證失敗，helper 會 throw，避免載入未驗證 binary。

### idempotency 與快取

`InstallOrUpdateAsync` 預設用 sidecar marker（`libmpv-2.dll.version.json`）避免重複下載。skip 路徑信任磁碟上由前次 helper 安裝完成的檔案。若 consumer 擔心重複呼叫之間檔案被外部改寫，應設定 `OverwriteExisting = true` 強制走完整下載與 SHA 驗證。

### 授權預設值

- libmpv 預設 `Provider = Zhongfly` + `LicensePreference = PreferLgpl`，在上游可用時優先取得 LGPL build。
- FFmpeg 預設 `IncludeFFmpeg = false`，避免預設下載 yt-dlp/FFmpeg-Builds GPL binary。

這些預設降低不確定散發場景的風險，但不取代 consumer 自行審查授權義務。散發前可用 `MpvLicenseAuditor.AnalyzeAsync(runtimeDirectory)` 解析 `mpv-configuration` 與 `ffmpeg -version`。

## 已知殘留風險

1. `HttpClient` 無 cert pinning；DNS / TLS 中間人攻擊不在 helper 防護範圍內。
2. 上游 release 帳號或 GitHub release 本身遭入侵時，預設 GitHub digest 驗證不足以辨識惡意 binary。
3. 第三方 runtime binary 的安全維護由各 upstream 承擔；本專案只提供下載、驗證與載入流程。

## consumer 建議

- CI 可使用 cached runtime 以縮短驗證時間；production runtime 應走 SHA pin。
- 將 SHA pin 清單納入 consumer 自己的變更審查流程。
- 散發前以 `MpvLicenseAuditor.AnalyzeAsync` 驗證實際 runtime 授權狀態。
- 需要嚴格合規時，避免使用未釘版的 latest 下載流程。

## 第三方元件責任邊界

| 元件 | upstream | consumer 應自行查核 |
| --- | --- | --- |
| libmpv | [shinchiro/mpv-winbuild-cmake](https://github.com/shinchiro/mpv-winbuild-cmake) / [zhongfly/mpv-winbuild](https://github.com/zhongfly/mpv-winbuild) | 上游 release、build 設定與授權標籤 |
| yt-dlp | [yt-dlp/yt-dlp](https://github.com/yt-dlp/yt-dlp) | 上游安全政策與 release checksum |
| Deno | [denoland/deno](https://github.com/denoland/deno) | 上游安全政策與 `.sha256sum` |
| FFmpeg | [yt-dlp/FFmpeg-Builds](https://github.com/yt-dlp/FFmpeg-Builds) | FFmpeg build flags、GPL / nonfree 狀態 |
| 7-Zip（fallback） | [ip7z/7zip](https://github.com/ip7z/7zip) | 上游 release asset 與 checksum |

第三方授權義務彙整於 [`../THIRD_PARTY_NOTICES.md`](../THIRD_PARTY_NOTICES.md)。

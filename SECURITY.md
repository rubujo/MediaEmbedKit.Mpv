# Security

本檔案說明 `MediaEmbedKit.Mpv` 的威脅模型（threat model）、helper 提供的防線與其限制、已知殘留風險，以及商用 / 受監管環境的 hardening 建議。

## 1. 威脅模型

helper 自動下載第三方原生二進位（libmpv、yt-dlp、Deno、FFmpeg），並在使用者處理序內 `NativeLibrary.Load` 載入 libmpv。可信邊界即為「helper 從 GitHub 抓到的 binary 完整未被竄改 + GitHub 本身可信」。

落在此邊界**外**的風險：

| 威脅 | helper 是否防護 |
| --- | --- |
| 上游 maintainer GitHub 帳號被入侵，發出含後門的 `libmpv-2.dll` | ❌ 不防（攻擊者同時改 binary 與 GitHub asset digest，本 helper 會「用攻擊者提供的 hash 驗攻擊者提供的 binary」、驗證通過） |
| GitHub.com 本身被入侵 | ❌ 不防 |
| 對 GitHub API / CDN 的 DNS 攻擊 / TLS 中間人 | ❌ 不防（`HttpClient` 走 OS TLS 但無 cert pinning） |
| 惡意 archive 含 symlink / reparse point 指向系統路徑 | ⚠️ 本版本不防（追蹤中，預計下版本修補） |
| `LockReleaseSource = true` 防住的：呼叫端被誘騙設 `ReleaseApiUriOverride` 指向假 API | ✅ 防 |
| `LockReleaseSource = true` 防住的：GitHub API 回傳的 download URL host 不是預設 owner/repo | ✅ 防 |
| `RequirePinnedSha256` + `ExpectedSha256` 防住的：上游被入侵後的 binary 替換 | ✅ 防（**前提是 caller 從可信通道取得 SHA**，例如 vendor 自己維護的 SHA pin 清單） |
| corrupted libmpv-2.dll（兩個應用實例共用同一 runtime dir 並發 install 寫壞） | ⚠️ 本版本不防（追蹤中，預計下版本加 cross-process file lock） |
| 已載入 libmpv 後嘗試 hot-reload 新版 | ✅ 防（`MpvLibraryUpdateScheduler` 走 staged update + restart-required 流程） |

## 2. helper 提供的防線

### 2.1 GitHub asset digest 驗證（預設）

`MpvNativeAssetVerificationPolicy.RequireGitHubDigest`（**預設**）強制驗證 GitHub Releases API 提供的 `sha256:` digest 與下載內容相符。`RequireProviderChecksum` 額外驗證 provider 發行的 checksum 檔（例如 yt-dlp 的 `SHA2-256SUMS`）。

**範圍**：防護「下載傳輸過程被改」/「GitHub 端有 partial 修補但 maintainer 未換 asset」這類**accidental** corruption。**不防** 上游被入侵的場景（攻擊者會同步改 binary 與 digest）。

### 2.2 來源 host 鎖定（`LockReleaseSource`）

設 `LockReleaseSource = true` 後 helper 拒絕：

- 呼叫端傳入的 `ReleaseApiUriOverride` 指向非預設 GitHub repo。
- API 回傳的 download URL 不屬於該 repo。

**範圍**：防護「config 注入」（攻擊者透過設定檔 / 環境變數誘導 caller 切到假 API）。**不防**供應鏈攻擊。

### 2.3 NuGet 套件 build provenance（Sigstore attestation）

本專案發行的 6 個 `.nupkg` + 6 個 `.snupkg` 透過 [`actions/attest-build-provenance`](https://github.com/actions/attest-build-provenance) 產生 Sigstore-signed build provenance attestation（寫進 Rekor [transparency log](https://search.sigstore.dev/)），證明套件來自 `github.com/rubujo/MediaEmbedKit.Mpv` 的特定 commit + workflow run。

**驗證方式**（caller 取得 `.nupkg` 後）：

```powershell
# 方法 1：GitHub CLI（最簡單）
gh attestation verify MediaEmbedKit.Mpv.0.0.1.nupkg --owner rubujo

# 方法 2：cosign（不依賴 gh CLI）
cosign verify-blob-attestation `
  --bundle MediaEmbedKit.Mpv.0.0.1.nupkg.sigstore `
  --new-bundle-format `
  --certificate-identity-regexp 'https://github.com/rubujo/MediaEmbedKit.Mpv/.+' `
  --certificate-oidc-issuer https://token.actions.githubusercontent.com `
  MediaEmbedKit.Mpv.0.0.1.nupkg
```

驗證通過代表：套件確實由本 repo 的 GitHub Actions release workflow 在指定 commit 上 build 出來，未被中途替換。**範圍**：防護「NuGet 套件本身被替換」攻擊。**不防** runtime helper 自動下載的第三方 binary（libmpv / yt-dlp / Deno / FFmpeg）—— 上游 mpv ecosystem 目前尚未採用 Sigstore，那層仍需 caller 走 `ExpectedSha256` 釘版。

### 2.4 釘版 SHA-256（**商用環境必走**）

`MpvNativeAssetVerificationPolicy.RequirePinnedSha256` + `ExpectedSha256` 由 caller 提供預期 SHA-256：

```csharp
MpvWindowsBuildDownloadOptions options = new MpvWindowsBuildDownloadOptions
{
    VerificationPolicy = MpvNativeAssetVerificationPolicy.RequirePinnedSha256,
    ExpectedSha256 = "abc123...64-hex-chars...",  // 從 vendor 維護的 SHA pin 清單取得
};
```

**範圍**：防護**所有**已知供應鏈攻擊向量（前提是 SHA 從可信通道取得，不是從 GitHub 同一個被入侵的 release 取的）。

商用 / 受監管環境**必須走這條路**。具體流程：

1. Vendor 維護自家 SHA pin 清單（內部 Wiki / 安全 repo），定期審查上游 release。
2. App 啟動前讀 vendor 提供的 SHA pin 對應當前要裝的版本。
3. 把 SHA pin 設進 `ExpectedSha256`、`VerificationPolicy = RequirePinnedSha256`、`OverwriteExisting = true`（強制重抓驗證，不走 idempotency skip）。
4. helper 驗證 SHA 失敗即 throw，CI / startup 立即 fail，不會載入未驗證的 binary。

### 2.5 idempotency 與快取

`InstallOrUpdateAsync` 預設使用 sidecar marker（`libmpv-2.dll.version.json`）skip 第二次後的重複下載。**注意：skip 路徑信任 disk 上的檔是我們上次自己裝完寫入的** —— 若 caller 對「重複呼叫之間檔案被改」有疑慮，請設 `OverwriteExisting = true` 強制走完整下載 + SHA 驗證。

### 2.6 預設 LGPL 偏好（降低法律風險）

- libmpv 預設 `Provider = Zhongfly` + `LicensePreference = PreferLgpl` → 實際拿 LGPL build。
- FFmpeg 預設 `IncludeFFmpeg = false` → 不下載 GPL FFmpeg。

對「閉源商用散發」較安全的預設。詳見 [`docs/RUNTIME_ASSETS.md`](docs/RUNTIME_ASSETS.md) 授權真值表。

## 3. 已知殘留風險（本版本未修，追蹤中）

1. **跨 process file lock 缺失**：兩個應用實例 / 並行 CI 共用同一 runtime dir 並發呼叫 `InstallOrUpdateAsync` 會撞 race，寫壞 `libmpv-2.dll`。預定下版本加 `FileStream` lock 包整段 install/stage/apply/prune。
2. **archive symlink / reparse point 無防護**：對應 [CVE-2025-11001 / CVE-2025-11002](https://www.threatlocker.com/blog/analysis-of-7-zip-vulnerabilities-cve-2025-11001-and-cve-2025-11002) 攻擊類別。被替換的 archive 含 symlink 指向系統路徑時可任意寫檔。商用環境請搭配 SHA pin 阻擋 archive 被替換。預定下版本加解壓後 reparse point 拒絕檢查。
3. **`UpdateLibMpvAsync` TOCTOU**：`IsLoaded` 檢查與 `File.Copy` 之間 race。多執行緒 startup 環境可能撞鎖檔 `IOException`。預定下版本修補。
4. **`HttpClient` 無 retry / backoff**：GitHub API 暫掛 / 429 直接 fail，無重試。可能讓首次安裝在網路抖動時掛掉。
5. **`HttpClient` 無 cert pinning**：DNS / TLS 中間人攻擊無防護。
6. **`User-Agent` 偽裝 Chrome 真實版號**：對 `api.github.com` 觀感不佳；對 release CDN 是為了避免 anti-bot 而做的。

## 4. CI / 維運建議

- **CI 與 production runtime 分離**：CI 共用 cached runtime dir（無需 SHA pin）、production runtime 走 SHA pin 路徑。
- **release gate 含 `MpvLicenseAuditor.AnalyzeAsync`**：散發前驗證 `ffmpeg -version` 不含 `--enable-gpl-and-nonfree`、`mpv-configuration` 含 `--enable-lgpl`（若預期 LGPL build）。
- **vendor 自家 SHA pin 清單 git track**：與 release tag 對齊，每次升版手動審查上游 release 後更新。

## 5. Report a vulnerability

若發現 helper 的安全議題，請**不要**開 public issue。改私訊維護者：

- GitHub: [@rubujo](https://github.com/rubujo)

提供：

- 影響版本（git commit SHA）
- 復現步驟
- 影響評估
- 建議修法（若有）

預期回應時間：5 個工作天內初步回覆、30 天內修補或公開揭露時程。

## 6. 第三方元件安全聲明

helper 下載 / 載入的第三方 binary 安全責任由各 upstream 維護者承擔：

| 元件 | upstream | 安全 contact |
| --- | --- | --- |
| libmpv | [shinchiro/mpv-winbuild-cmake](https://github.com/shinchiro/mpv-winbuild-cmake) / [zhongfly/mpv-winbuild](https://github.com/zhongfly/mpv-winbuild) | 各 repo issue tracker |
| yt-dlp | [yt-dlp/yt-dlp](https://github.com/yt-dlp/yt-dlp) | [SECURITY.md](https://github.com/yt-dlp/yt-dlp/security/policy) |
| Deno | [denoland/deno](https://github.com/denoland/deno) | [SECURITY.md](https://github.com/denoland/deno/security/policy) |
| FFmpeg | [yt-dlp/FFmpeg-Builds](https://github.com/yt-dlp/FFmpeg-Builds) | [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds) upstream |
| 7-Zip（fallback） | [ip7z/7zip](https://github.com/ip7z/7zip) | Igor Pavlov |

[`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md) 列各元件的授權義務揭露。

# Changelog

本檔案紀錄 `MediaEmbedKit.Mpv` 各版本的功能變更。格式參考 [Keep a Changelog](https://keepachangelog.com/zh-TW/1.1.0/)，版號遵循 [Semantic Versioning](https://semver.org/lang/zh-TW/)。

> 本專案 1.0 以前的版本（0.x）公開 API 仍可能小幅調整，請參考 [`docs/CONSUMING_PACKAGES.md`](docs/CONSUMING_PACKAGES.md) 鎖定特定版本。

## [Unreleased]

（下個 release 的改動寫在這裡。）

## [0.0.1] — 初始發行

首次公開 release。內容為 Windows desktop 嵌入 libmpv 的核心包裝、5 個 UI 框架控制項、與 runtime helper（自動下載 libmpv / yt-dlp / Deno / FFmpeg）。

### libmpv C API 包裝

- 完整覆蓋 libmpv stable v0.41.0 公開 API（`client.h` / `render.h` / `render_gl.h` / `stream_cb.h`）。
- `MpvNative` P/Invoke 54 個官方匯出函式，net7.0+ 走 `LibraryImport` source generator（NativeAOT / trim 友善）。
- 列舉、旗標、事件 / 節點 / stream callback / OpenGL / DRM / render frame info / `mpv_byte_array` 受控對應。
- `MPV_CLIENT_API_VERSION` 對齊上游 `MPV_MAKE_VERSION(2, 5)`。

### 高階播放 API

- `MpvPlayer` 與 fluent `MpvAppBuilder`。
- per-file 選項：`MpvMediaItem` + `MpvPlayer.Load(MpvMediaItem)`。
- `LoadAsync` / `WatchProperty<T>` / `IAsyncDisposable` graceful shutdown。
- `Microsoft.Extensions.Logging` / `Microsoft.Extensions.DependencyInjection` 整合（`AddMpvPlayer` / `AddMpvPlayerFactory`）。

### 高階 encoding API

- `MpvEncoder` 單／兩階段轉碼、stream-copy 重新封裝、單軌抽取、影格抽圖、多檔 EDL 串接、多段切割、字幕 / filter / metadata 操作。
- `IProgress<MpvEncodingProgress>` 進度回報、`CancellationToken` 取消。
- `MpvVideoCodecPreset` / `MpvAudioCodecPreset` 對齊 2026-05 yt-dlp/FFmpeg-Builds 實際內建編解碼器。

### UI 控制項（5 個框架，共通綁定屬性 + MVVM Commands）

- `MediaEmbedKit.Mpv.WinForms`、`MediaEmbedKit.Mpv.Wpf`（含 `HwndHost` + AirSpace 覆蓋層）、`MediaEmbedKit.Mpv.Avalonia`（OpenGL render API）、`MediaEmbedKit.Mpv.WinUI`、`MediaEmbedKit.Mpv.Maui`。
- 共通 DP / `BindableProperty` 系列：`Source` / `Position` / `Volume` / `IsPaused` / `IsMuted` / `Duration` / `PlaybackState`。
- 共通 `MpvRelayCommand`：`Play` / `Pause` / `Stop` / `TogglePause` / `ToggleMute`。

### Runtime Helper

#### 設計選擇（首次 release 即定型）

- **預設不下載 FFmpeg**（`MpvWindowsRuntimeDownloadOptions.IncludeFFmpeg = false`）。yt-dlp/FFmpeg-Builds 僅發 GPL build，避免使用者在不知情下背負 GPLv2+ 散發義務。需要 yt-dlp 後處理 / 自行編碼時明確設為 `true`，並接受 GPL 義務。詳見 [`docs/RUNTIME_ASSETS.md`](docs/RUNTIME_ASSETS.md) FFmpeg 段警示框。
- **預設 libmpv provider 為 Zhongfly**（`MpvWindowsBuildDownloadOptions.Provider = Zhongfly`）。兩家中唯一同時提供 GPL 與 LGPL libmpv build 的來源，搭配下方 `LicensePreference` 能實際拿到 LGPL build。`ProviderFallbackOrder` 預設含 `Shinchiro` 作為兜底。
- **預設 license preference 為 PreferLgpl**（`MpvWindowsBuildDownloadOptions.LicensePreference = PreferLgpl`）。在 Zhongfly 上取 LGPL 變體；切到 Shinchiro 因該 provider 無 LGPL 會 silently fallback 到 GPL（詳見 RUNTIME_ASSETS 真值表）。商用嚴格合規請設 `RequireLgpl` 讓不可用情境 fail-loud。
- **解壓成功後預設清掉下載壓縮檔**（`RetainArchive = false`）。一次完整 install 省 ~290 MB 死重。warm restart 強驗證需求請明確設 `RetainArchive = true`。
- **`InstallOrUpdateAsync` 為 idempotent**：第二次起呼叫透過 sidecar marker（`libmpv-2.dll.version.json`）+ 上游版本比對 short-circuit，不重複下載。要強制重抓設 `OverwriteExisting = true` 或呼叫 `UpdateLibMpvAsync`。
- **libmpv `.7z` 解壓走 4-tier fallback chain**（Windows 內建 `tar.exe` → 系統 7-Zip → WinRAR → 從 ip7z/7zip 下載 `7zr.exe`）。多數使用者透過 tar.exe 零前置依賴，不需自行安裝 7-Zip。

#### 元件下載 / 驗證

- libmpv：shinchiro / zhongfly Windows git build provider（x64 / ARM64），含 `LicensePreference` 篩選與 `ProviderFallbackOrder`。
- yt-dlp：主 channel / nightly-builds / master-builds，含 version 比對 skip、self-update wrapper。
- Deno：x64 / ARM64（自 Deno 2.7+）。
- FFmpeg：yt-dlp/FFmpeg-Builds（`tags/latest` endpoint 避免被 hourly autobuild 替換）。
- 驗證政策：`RequireGitHubDigest`（預設）/ `RequireProviderChecksum` / `RequirePinnedSha256` / `BestEffort`。商用環境請走 `RequirePinnedSha256` + `ExpectedSha256` 釘版（詳見 [`SECURITY.md`](SECURITY.md)）。

#### 健康檢查與授權稽核

- `MpvRuntimeHealthCheck.AnalyzeAsync` 兩層健康語意（`IsHealthy` / `IsComplete` / `IsHealthyFor` flag combination）。
- `MpvLicenseAuditor.AnalyzeAsync` 解析 `mpv-configuration` 與 `ffmpeg -version` 分類 LGPL / GPL / NonFree / Unknown，散發前合規檢查。
- `MpvLibraryUpdateScheduler` 4 階段：stage / apply on startup / rollback / list / prune staged updates，處理 libmpv 已載入時的 hot-reload 不能性。

### CI / 維運工具

- `tools/Invoke-PreReleaseValidation.ps1` 本機 release gate（依旗標選擇驗證階段）。
- `tools/Sync-ProviderDocs.ps1` 自動同步 catalog 與下游文件，含 `-Check` 模式可掛 release gate。
- `tools/libmpv/Check-LibMpvHeaderDrift.ps1` 追蹤 mpv git build 公開 header 變更。
- `.github/workflows/ci.yml` + `release.yml`：PR / push 跑非 GUI release gate；tag push 包含 `-IncludeDocSyncCheck` 阻擋下游文件 drift。

### 文件

- [`README.md`](README.md)：總覽與 quick-start。
- [`docs/RUNTIME_ASSETS.md`](docs/RUNTIME_ASSETS.md)：runtime 元件、授權真值表、4-tier 解壓 fallback chain、idempotency 語意、GPL vs LGPL 程式面差異。
- [`docs/HIGH_LEVEL_API.md`](docs/HIGH_LEVEL_API.md)：高階 API 完整指引。
- [`docs/CONTROLS_API.md`](docs/CONTROLS_API.md)：5 個 UI 框架控制項設計。
- [`docs/SUPPORT_MATRIX.md`](docs/SUPPORT_MATRIX.md)：TFM × 架構支援矩陣。
- [`SECURITY.md`](SECURITY.md)：threat model + 商用合規路徑 + 已知殘留風險。
- [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md)：第三方授權義務揭露。
- [`docs/RELEASE_CHECKLIST.md`](docs/RELEASE_CHECKLIST.md)：發佈前驗證流程。
- [`docs/CONSUMING_PACKAGES.md`](docs/CONSUMING_PACKAGES.md)：本機 NuGet feed 安裝流程（本專案不發行到 nuget.org）。

### 已知限制與後續工作

- **跨 process file lock 缺失**：兩個應用實例 / 並行 CI 共用同一 runtime 資料夾並發呼叫 `InstallOrUpdateAsync` 可能寫壞 `libmpv-2.dll`。預定下版本修補。
- **archive symlink 防護不足**：對應 [CVE-2025-11001](https://www.threatlocker.com/blog/analysis-of-7-zip-vulnerabilities-cve-2025-11001-and-cve-2025-11002) class 攻擊，本 helper 對含 symlink 的惡意 archive 未做防護。商用環境請搭配 SHA pin。預定下版本修補。
- **`MpvLicenseAuditor.AnalyzeAsync` `probeLibMpv=true` 副作用**：呼叫即不可逆載入 libmpv 至當前處理序。詳見其 XML doc `<remarks>`。
- **`LockReleaseSource` 不是供應鏈防線**：只擋 `ReleaseApiUriOverride` 注入與 download URL host mismatch，不防 maintainer 帳號被入侵。商用環境必須走 `RequirePinnedSha256` + `ExpectedSha256`。

[Unreleased]: https://github.com/rubujo/MediaEmbedKit.Mpv/compare/v0.0.1...HEAD
[0.0.1]: https://github.com/rubujo/MediaEmbedKit.Mpv/releases/tag/v0.0.1

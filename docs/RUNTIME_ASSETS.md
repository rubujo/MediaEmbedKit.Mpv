# 原生執行階段與下載政策

本文件定義 runtime helper 的支援範圍與限制。helper 僅在使用者明確呼叫時下載或更新第三方執行階段檔案。

## 支援範圍

目前 runtime helper 支援 Windows x64 與 Windows ARM64；架構依目前處理序自動偵測（`MpvWindowsArchitectureExtensions.CurrentProcess()`），呼叫端也可在各 `*DownloadOptions.Architecture` 顯式覆寫以進行跨架構 staging。預設 runtime 資料夾配置如下：

```text
runtime/
├── libmpv-2.dll
├── yt-dlp.exe
├── deno.exe
├── ffmpeg.exe
├── ffprobe.exe
├── mpv.conf
├── input.conf
└── scripts/
```

控制項建構函式、XAML 載入與播放器初始化流程不得自動下載任何二進位檔。

## 完整性驗證與來源鎖定

runtime helper 預設要求 GitHub Releases API 提供 `sha256:` digest，並驗證下載內容與該 digest 相符。若需要自訂 mirror、舊 release 或內部測試來源，可由呼叫端明確改用 `BestEffort` 相容模式。

- `MpvNativeAssetVerificationPolicy.RequireGitHubDigest`（**預設值**）：要求 GitHub 發行資產必須提供 `sha256:` digest，下載內容必須驗證一致。
- `MpvNativeAssetVerificationPolicy.RequireProviderChecksum`：要求 GitHub digest 與 provider 發行的 checksum 檔案同時通過驗證。
- `MpvNativeAssetVerificationPolicy.RequirePinnedSha256`：要求呼叫端提供 `ExpectedSha256`，以下載內容的 SHA-256 值作為鎖定紀錄。
- `MpvNativeAssetVerificationPolicy.BestEffort`：GitHub Releases API 提供 SHA-256 digest 時會驗證，未提供時不阻擋下載；**僅作為自訂來源或相容情境**的退路，不再作為預設。

`LockReleaseSource = true` 會鎖定內建 GitHub repository 與下載 URL。啟用後，helper 會拒絕非預設 GitHub Releases API 或非預期 repository 的資產 URL。

`yt-dlp` 支援使用 `SHA2-256SUMS` 驗證發行檔。Deno 支援使用發行資產同層的 `.sha256sum` 檔案驗證壓縮檔。yt-dlp FFmpeg-Builds 支援使用 `checksums.sha256` 驗證發行檔。libmpv 的 shinchiro 與 zhongfly provider 不在 `RequireProviderChecksum` 支援範圍內；更嚴格的生產下載請使用 `RequirePinnedSha256`、`ExpectedSha256` 與 `LockReleaseSource`。

## 下載壓縮檔保留策略

`FFmpegDownloadOptions.RetainArchive`、`MpvWindowsBuildDownloadOptions.RetainArchive` 與 `DenoDownloadOptions.RetainArchive` 控制解壓成功後是否保留下載的壓縮檔（zip / 7z）。預設值為 `false`：解壓成功後 helper 立即清掉壓縮檔，避免長期佔用磁碟（一次完整 runtime install 可省 ~290 MB：FFmpeg-Builds zip ~200 MB + libmpv .7z ~50–100 MB + Deno zip ~30 MB）。

需在「warm restart 重新驗證 SHA-256 而不重新下載」流程下保留壓縮檔的呼叫端，應明確設 `RetainArchive = true`。下次再呼叫 `Download*Async(...)` 時，FFmpeg helper 的 `CanVerifyExistingArchive` fast path 才會找到 archive 並重跑驗證。

清掉壓縮檔的失敗（檔案被其他處理序鎖、權限不足等）不會擲例外或失敗整個下載流程 —— archive 本身已不再被需要，留下也只是磁碟用量問題。

## libmpv

helper 可從 shinchiro `mpv-winbuild-cmake` 與 zhongfly `mpv-winbuild` 下載對應架構的 `mpv-dev-{token}-*.7z` 資產（x64 token 為 `x86_64`、ARM64 token 為 `aarch64`）。這些來源是 mpv Windows git build，不是 mpv stable release。兩個 provider 的命名規範完全相同，`ProviderFallbackOrder` 機制在 x64 與 ARM64 上行為一致。

下載後必須驗證封存檔包含 `libmpv-2.dll`。provider 對齊狀態記錄於 `docs/runtime/libmpv-git-builds.json`。

### .7z 解壓 4-tier fallback chain

mpv-dev archive 是 `.7z` 格式，需 7z 相容工具才能解壓。helper 依序嘗試以下 4 段 fallback，找到能用的就停；全部失敗才擲 `InvalidOperationException`，訊息含每層失敗細節與使用者可採取的解法。

| 順序 | 對象 | 偵測 | CLI |
| --- | --- | --- | --- |
| 0 | `MpvWindowsBuildDownloadOptions.SevenZipPath` 顯式 | 路徑檢查 | 走第 2 層格式 |
| 1 | **Windows 內建 `tar.exe`**（bsdtar / libarchive） | `%SystemRoot%\System32\tar.exe` 或 PATH | `tar -xf {archive} -C {dir} libmpv-2.dll` |
| 2 | 系統 **7-Zip** | `Program Files\7-Zip\7z.exe` / `(x86)` / PATH | `7z x -y -o{dir} {archive} libmpv-2.dll -r` |
| 3 | **WinRAR** | `Program Files\WinRAR\WinRAR.exe` / `(x86)` | `WinRAR x -ibck -y {archive} libmpv-2.dll {dir}\` |
| 4 | 下載 `7zr.exe` from [ip7z/7zip](https://github.com/ip7z/7zip)（兜底 fallback） | 既有檔重用；缺則拉 `releases/latest` 並驗證 GitHub digest | 同第 2 層 |

各層特性：

- **tar.exe**（Windows 10 1803+ / Server 2019+ 內建）：API 最簡單、速度最快、無前置依賴。實測 Win11 24H2 (bsdtar 3.8.4 / liblzma 5.8.1) 解 shinchiro mpv-dev `.7z` 約 ~1.8 秒。舊版 Windows 10 (1803–2004) 的 tar.exe LZMA2 支援未經完整驗證 —— 失敗自動跳下一層。
- **系統 7-Zip**：power user 常裝；CLI 穩定、Windows ARM64 有 native 版本。
- **WinRAR**：中文圈裝機率高；`-ibck` 旗標跑背景模式不彈 UI（試用期過後 WinRAR 自身可能在啟動時彈提醒，本程式無法避免）。
- **下載 7zr.exe**：終極 fallback。Igor Pavlov 官方 [ip7z/7zip](https://github.com/ip7z/7zip) 發佈的 standalone 32-bit x86 CLI（588 KB，純 LGPL-2.1+ 不含 unrar 限制）。既有 `7zr.exe` 存在時直接重用，不重複下載；首次下載驗證 GitHub asset digest。**Windows on ARM64**：7zr.exe 透過 x86 emulation 執行；Win11 ARM64 效能 OK，Win10 ARM64 emulation 較慢，建議 ARM64 使用者預裝 7-Zip ARM64 native 版（[7-zip.org/download.html](https://www.7-zip.org/download.html)）走第 2 層 fallback。

選擇性解壓：`mpv-dev-*.7z` 內除 `libmpv-2.dll` 外還含 `include/mpv/*.h` 與 `libmpv.dll.a` 等 C++ build-time 用的雜物，runtime 完全用不到。fallback chain 預設只解出 `libmpv-2.dll`，runtime 資料夾保持乾淨。

`libmpv-2.dll` 載入後不得在同一處理序 hot reload。更新流程必須：

1. 偵測 `MpvLibraryLoader.IsLoaded`。
2. 將新檔案暫存至 `.updates`。
3. 回傳 `RequiresProcessRestart = true`。
4. 由應用程式於下次啟動且載入 libmpv 前套用更新。

`MpvWindowsBuildDownloadOptions.ProviderFallbackOrder` 可指定主要 provider 失敗時的備援嘗試順序；下載端會依序嘗試並把每個失敗收進 `AggregateException`，全部失敗才擲出。

## 更新排程器與健康檢查

`MpvLibraryUpdateScheduler` 將 libmpv-2.dll 的暫存／套用／回滾包成四階段：

- `StageAsync(cancellationToken)` 下載最新 build 並暫存至 `.updates/<timestamp>/`。
- `ApplyStagedOnStartup()` 在 libmpv 尚未載入時，把目前版本搬到 `.previous/`，再把暫存版本提升為使用版本。
- `Rollback()` 從 `.previous/` 還原為使用版本。
- `ListStagedUpdates()` / `PruneStagedUpdates()` 提供暫存稽核與清理。

所有套用流程均以 `MpvLibraryLoader.IsLoaded` 守備，避免處理序內 hot reload。

`MpvRuntimeHealthCheck.AnalyzeAsync(runtimeDirectory, probeLibMpv: bool)` 報告：libmpv 是否存在與可載入、是否能建立並初始化 player、yt-dlp / Deno / FFmpeg / FFprobe 是否齊備。`probeLibMpv` 預設關閉以避免無意間在啟動流程觸發 libmpv 載入。

`MpvRuntimeHealthReport` 提供兩層健康語意，避免「能播媒體」與「完整 runtime 就緒」被混為一談：

- `IsHealthy`：核心 libmpv 已存在且無錯誤紀錄，「能播媒體」的最小條件。
- `IsComplete`：`IsHealthy` 且 yt-dlp / Deno / FFmpeg / FFprobe **全部齊備**；對應「能下載 URL + 後處理」場景。
- `IsHealthyFor(MpvRuntimeTools required)`：以 `[Flags]` 列舉自訂必備工具子集，例如 `MpvRuntimeTools.YtDlp | MpvRuntimeTools.FFmpeg`。核心 libmpv 永遠必備，不需在參數中重複列出。`MpvRuntimeTools.None` 等價於只檢查 `IsHealthy`、`MpvRuntimeTools.All` 等價於 `IsComplete`。

## 授權稽核

`MpvLicenseAuditor.AnalyzeAsync(runtimeDirectory, probeLibMpv: bool)` 從 `mpv-configuration` 與 `ffmpeg -version` 輸出解析授權旗標，分類為 `Unknown` / `Lgpl` / `Gpl` / `NonFree`，並回報整體判定（以較嚴格者為準）。使用者散發 runtime 前應依此判定確認義務。

## yt-dlp

yt-dlp helper 支援 Windows x64（`yt-dlp.exe`）與 ARM64（`yt-dlp_arm64.exe`，自 [2026-03-17 release](https://github.com/yt-dlp/yt-dlp/releases/tag/2026.03.17) 起）下載、自我更新與路徑回填。`MpvWindowsRuntimeInstaller` 會把下載的執行檔統一儲存為 `yt-dlp.exe`，讓 mpv 預設搜尋路徑與 `MpvPlayerOptions.YtdlpPath` 預設值在兩個架構上行為一致。

應用程式可透過下列 API 控制 mpv 使用的格式：

- `MpvPlayerOptions.YtdlpFormatPreset`
- `MpvPlayerOptions.YtdlpFormat`
- `MpvPlayerOptions.UseYtdlpFormat(...)`
- `MpvPlayerOptions.UseYtdlpMaximumHeight(...)`
- `MpvPlayer.SetYtdlpFormat(...)`
- `MpvPlayer.SetYtdlpMaximumHeight(...)`

若需要 yt-dlp 自身 stdout/stderr、格式清單、JSON 或下載進度，請直接使用 `YtDlpProcessRunner`。mpv ytdl hook 的 JSON 子程序結果則由 `MpvPlayer.GetYtdlJsonSubprocessResult()` 讀取，不應解析 `log-message`。

若需要可稽核的供應鏈驗證，應優先使用 `YtDlpDownloader.InstallOrUpdateLatestExecutableAsync(...)` 搭配驗證政策。`YtDlpDownloader.RunSelfUpdateAsync(...)` 是 yt-dlp 內建更新命令的薄型包裝，適合手動維護，不取代 helper 的 SHA-256 驗證流程。

## Deno

Deno helper 支援 Windows x64（`deno-x86_64-pc-windows-msvc.zip`）與 ARM64（`deno-aarch64-pc-windows-msvc.zip`，自 [Deno 2.7、2026-02 release](https://deno.com/blog/v2.7) 起官方提供）`deno.exe` 下載、自我更新與外部處理序輸出事件。Deno 不是本機檔案播放的必要條件，但 yt-dlp YouTube EJS 情境可能需要外部 JavaScript runtime，因此 helper 預設與 `yt-dlp.exe` 同層準備 `deno.exe`。

需要使用 Deno 內建升級流程且要求 checksum 時，使用 `DenoDownloader.RunSelfUpgradeWithChecksumAsync(...)`。若要維持完整下載紀錄與來源鎖定，應使用 `DenoDownloader.DownloadAndExtractLatestAsync(...)` 搭配驗證政策。

## FFmpeg

FFmpeg helper 支援從 yt-dlp `FFmpeg-Builds` 下載對應架構的 GPL build：x64 為 `ffmpeg-master-latest-win64-gpl.zip`、ARM64 為 `ffmpeg-master-latest-winarm64-gpl.zip`（兩者皆由 [yt-dlp/FFmpeg-Builds](https://github.com/yt-dlp/FFmpeg-Builds/releases) 與其上游 [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds/releases) 自動產製）。下載後 `ffmpeg.exe` 與 `ffprobe.exe` 放在 runtime 資料夾根目錄。`MpvWindowsRuntimeDownloadOptions.IncludeFFmpeg` **預設為 `false`**：yt-dlp/FFmpeg-Builds 目前僅發佈 GPL build，預設下載會讓使用者在不知情下背負 GPL 散發義務。需要 yt-dlp 後處理（merge audio/video、轉檔）或自行編碼的應用程式應明確設為 `true`，並在 release 文件揭露 FFmpeg 授權義務。`FFmpegDownloader.GetWindowsAssetName(MpvWindowsArchitecture)` 提供架構到資產名稱的 mapping。

FFmpeg 沒有本專案可呼叫的內建自我更新命令。若要更新，請重新呼叫 `FFmpegDownloader.DownloadAndExtractLatestAsync(...)` 或 `MpvWindowsRuntimeInstaller.InstallOrUpdateAsync(...)`，並於 `FFmpegDownloadOptions.OverwriteExisting = true` 時覆蓋既有檔案。

### yt-dlp/FFmpeg-Builds 的雙 release 結構

yt-dlp/FFmpeg-Builds 同 repo 並存兩種 release，命名規範完全不同：

| Release 類型 | tag 範例 | asset 命名 |
| --- | --- | --- |
| 穩定 URL release | `latest`（字面 tag） | `ffmpeg-master-latest-{arch}-gpl.zip`（**固定**） |
| 每小時 CI build | `autobuild-YYYY-MM-DD-HH-MM` | `ffmpeg-N-{buildnum}-g{commit10}-{arch}-gpl.zip`（**含 build number / commit**） |

GitHub API 的 `/releases/latest` 端點語意是「取 `created_at` 最新、非 draft 非 prerelease 的 release」——**不會**特別取 tag 名為 `latest` 的 release。由於 autobuild 每小時新增、`created_at` 必然新於 `latest` tag，`/releases/latest` 會回傳 autobuild release，其 asset 名稱含動態 build number/commit，與 `FFmpegDownloader.WindowsX64AssetName`（寫死 `ffmpeg-master-latest-win64-gpl.zip`）對不起來。

`FFmpegDownloader` 因此使用 `/releases/tags/latest` 端點，明確取 tag 名為 `latest` 的 release，asset 名稱穩定。此契約由 `VerifyFFmpegBuildsLatestTagAssetNamingAsync` 整合測試鎖住：上游若改命名或拆掉 `latest` tag，release gate 立即失敗。

**不要把這個改成 `/releases/latest`** —— 那是用來抓 hourly autobuild 的端點，與本專案期待的穩定 asset 名稱結構不相容。其他 downloader（yt-dlp / Deno / libmpv 兩家 provider）的 repo 都是「單軌 release」（tag 為日期或版本號，無另外的 `latest` tag），所以它們繼續使用 `/releases/latest` 是安全的，請勿「順手對齊」改成 `tags/latest` —— 那會直接 404。

yt-dlp 官方將 `ffmpeg` 與 `ffprobe` 列為 strongly recommended dependency；本 helper 僅將其視為 yt-dlp 附帶工具，不提供 FFmpeg wrapper、轉檔佇列或批次工作 API。

## mpv 設定與 scripts

使用者可選擇讓 runtime 資料夾同時作為 mpv 設定資料夾。啟用後，核心會設定 `config-dir` 並載入同層 `mpv.conf`、`input.conf` 與 `scripts`。

```csharp
MpvPlayerOptions options =
    MpvWindowsRuntimeInstaller.CreatePlayerOptions(
        runtimeDirectory,
        loadRuntimeConfiguration: true);
```

若只需指定特定檔案，使用 `MpvPlayerOptions.ConfigFiles`、`InputConfigFile`、`ScriptFiles`、`AddConfigFile(...)`、`AddScriptFile(...)` 或 `MpvPlayer.LoadScript(...)`。

## HTTP 要求

下載 helper 必須重複使用共用 `HttpClient`。現代 .NET 目標應使用 `SocketsHttpHandler.PooledConnectionLifetime`，降低 DNS 陳舊與連線用盡風險；.NET Framework 目標保留共用 client 策略。

瀏覽器標頭集中於 `BrowserRequestHeaders`，包含 Chrome Stable 桌面 `User-Agent` 與必要 client hints。

## 授權

本專案受控原始碼採用 CC0-1.0。此授權不涵蓋 mpv/libmpv、yt-dlp、Deno、FFmpeg 或其相依元件。

helper 預設值已往「對不確定散發授權的多數使用者較安全」方向收緊：

- `MpvWindowsBuildDownloadOptions.LicensePreference` 預設為 `PreferLgpl`：上游有 LGPL 變體時優先選用，無時 fallback 到 GPL（不打掛現有環境）。商用嚴格合規請設 `RequireLgpl`（沒 LGPL 直接 fail、不靜默 fallback）；不要授權偏好請設 `Any`。
- `MpvWindowsRuntimeDownloadOptions.IncludeFFmpeg` 預設為 `false`：yt-dlp/FFmpeg-Builds 為 GPL build，預設拉進 runtime 會讓使用者背負未必知情的 GPL 散發義務。需要 FFmpeg 後處理或自行編碼的應用程式應明確設為 `true`。

無論採何種預設值，使用者散發 runtime 前均應依 `MpvLicenseAuditor.AnalyzeAsync(runtimeDirectory, probeLibMpv: bool)` 的判定確認義務，並查證對應 provider build 的實際 `mpv-configuration` 與 `ffmpeg -version` 內容。

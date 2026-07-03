# 原生執行階段與下載政策

本文件定義執行階段輔助工具的支援範圍與限制。輔助工具僅在使用者明確呼叫時下載或更新第三方執行階段檔案。

## 支援範圍

目前執行階段輔助工具支援 Windows x64 與 Windows ARM64；架構依目前處理序自動偵測（`MpvWindowsArchitectureExtensions.CurrentProcess()`），呼叫端也可在各 `*DownloadOptions.Architecture` 顯式覆寫以進行跨架構 staging。預設執行階段資料夾配置如下：

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

執行階段輔助工具預設要求 GitHub Releases API 提供 `sha256:` digest，並驗證下載內容與該 digest 相符。若需要自訂鏡像來源、舊版發行或內部測試來源，可由呼叫端明確改用 `BestEffort` 相容模式。

- `MpvNativeAssetVerificationPolicy.RequireGitHubDigest`（**預設值**）：要求 GitHub Releases 資產必須提供 `sha256:` digest，下載內容必須驗證一致。
- `MpvNativeAssetVerificationPolicy.RequireProviderChecksum`：要求 GitHub digest 與提供者發行的總和檢查碼檔案同時通過驗證。
- `MpvNativeAssetVerificationPolicy.RequirePinnedSha256`：要求呼叫端提供 `ExpectedSha256`，以下載內容的 SHA-256 值作為鎖定紀錄。
- `MpvNativeAssetVerificationPolicy.BestEffort`：GitHub Releases API 提供 SHA-256 digest 時會驗證，未提供時不阻擋下載；**僅作為自訂來源或相容情境**的退路，不再作為預設。

`YtDlpDownloadOptions`、`DenoDownloadOptions`、`FFmpegDownloadOptions` 與 `MpvWindowsBuildDownloadOptions` 預設啟用 `LockReleaseSource = true`，會要求 GitHub Releases API 與資產下載 URL 同屬預期官方儲存庫。內部測試伺服器、鏡像站或人工驗證來源必須明確設為 `false`，並建議搭配 `RequirePinnedSha256` 記錄可稽核的 SHA-256 鎖定值。`tools/libmpv/Verify-LibMpvArchive.ps1` 若使用 `DownloadUri`，同樣預設只接受 shinchiro 與 zhongfly 官方 GitHub Releases 資產；自訂來源必須使用 `-AllowUntrustedDownloadSource`，並可用 `-ExpectedSha256` 在解壓前驗證封存檔。

`yt-dlp` 支援使用 `SHA2-256SUMS` 驗證發行檔。Deno 支援使用發行資產同層的 `.sha256sum` 檔案驗證壓縮檔。yt-dlp FFmpeg-Builds 支援使用 `checksums.sha256` 驗證發行檔。libmpv 的 shinchiro 與 zhongfly 提供者不在 `RequireProviderChecksum` 支援範圍內；更嚴格的生產下載請使用 `RequirePinnedSha256` + `ExpectedSha256`（從供應商自家可信通道取得 SHA pin），詳見 [`SECURITY_MODEL.md`](SECURITY_MODEL.md) 的供應鏈風險模型。

## 重複呼叫與更新語意（idempotency）

`MpvWindowsRuntimeInstaller.InstallOrUpdateAsync` 的設計意圖是「**有需要才下載**」：

| 元件 | Skip 條件（同時滿足才跳過下載） |
| --- | --- |
| **libmpv** | runtime/libmpv-2.dll 存在 + 同目錄有 `libmpv-2.dll.version.json` sidecar 標記 + marker 內 provider / releaseTag / assetName 全部對得上上游當前 release + `OverwriteExisting=false` + 無 `ExpectedSha256` |
| **yt-dlp** | runtime/yt-dlp.exe 存在 + `yt-dlp --version` 與上游 release tag 相符 + `OverwriteExisting=false` + 無 `ExpectedSha256` |
| **Deno** | runtime/deno.exe 存在 + `deno --version` 與上游 release tag 相符 + `OverwriteExisting=false` + 無 `ExpectedSha256` |
| **FFmpeg**（若啟用） | runtime/ffmpeg.exe + ffprobe.exe 都存在 + 同目錄有 `ffmpeg.exe.version` sidecar 標記 + marker 內 releaseTag / assetName / digest 全部對得上上游當前 release + `OverwriteExisting=false` + 無 `ExpectedSha256` |

對「裝完即用」與「CI 快取命中」情境，重複呼叫 `InstallOrUpdateAsync` 第二次起應該幾乎零成本（每元件最多打一次 GitHub Releases API 做版本比對）。要強制完整重新下載，設 `OverwriteExisting = true` 在對應選項（或呼叫 `UpdateLibMpvAsync(...)` 顯式重新下載 libmpv）。

libmpv 的 sidecar 標記檔（`libmpv-2.dll.version.json`）格式：

```json
{
  "schemaVersion": 1,
  "provider": "Zhongfly",
  "releaseTag": "2026-05-17-059bc7025b",
  "assetName": "mpv-dev-lgpl-x86_64-20260517-git-059bc7025b.7z"
}
```

輔助工具在每次成功安裝後寫入此檔，下次安裝時讀回比對。**手動刪除此檔**會強制下次呼叫走完整下載 + 解壓路徑。

FFmpeg 的 sidecar 標記檔（`ffmpeg.exe.version`）使用簡單 key-value 格式，記錄 `releaseTag`、`assetName` 與 GitHub Releases `digest`。既有 `ffmpeg.exe` / `ffprobe.exe` 只有在 marker 同步符合目前 `latest` 發行資產時才會被重用；手動放入兩個 exe 但沒有 marker 時，輔助工具會重新下載並寫入可稽核狀態。

### 同處理序內 libmpv 已載入後的暫存更新

libmpv-2.dll 一旦載入處理序就無法熱替換（檔案被鎖）。同處理序內呼叫 `UpdateLibMpvAsync` 會把新版暫存至 `runtime/.updates/<時戳>/`，回傳 `RequiresProcessRestart = true`。下次處理序啟動時先呼叫 `ApplyStagedLibMpvUpdate(...)` 把暫存版本提升為使用版本，再載入 libmpv。

輔助工具會自動清理 `.updates/` 內舊的時戳資料夾，僅保留最新 1 個供必要時 回復 / 稽核。需要更細的 暫存管理（多版本保留、明確 套用 / 回復）請改用 `MpvLibraryUpdateScheduler`。

## 下載壓縮檔保留策略

`FFmpegDownloadOptions.RetainArchive`、`MpvWindowsBuildDownloadOptions.RetainArchive` 與 `DenoDownloadOptions.RetainArchive` 控制解壓成功後是否保留下載的壓縮檔（zip / 7z）。預設值為 `false`：解壓成功後輔助工具立即清掉壓縮檔，避免長期佔用磁碟（一次完整執行階段安裝可省 ~290 MB：FFmpeg-Builds zip ~200 MB + libmpv .7z ~50–100 MB + Deno zip ~30 MB）。

需在「暖啟動重新驗證 SHA-256 而不重新下載」流程下保留壓縮檔的呼叫端，應明確設 `RetainArchive = true`。下次再呼叫 `Download*Async(...)` 時，FFmpeg 輔助工具即使已可重用既有 `ffmpeg.exe` / `ffprobe.exe` 與 sidecar marker，仍會先驗證保留的封存檔的 GitHub digest / 提供者總和檢查碼，確認後才走快速路徑。

清掉壓縮檔的失敗（檔案被其他處理序鎖、權限不足等）不會擲例外或失敗整個下載流程 —— 壓縮檔本身已不再被需要，留下也只是磁碟用量問題。

CI 的 `.cache/runtime` 使用 GitHub Actions cache；快取鍵值由 `docs/runtime/libmpv-git-builds.json`、`src/MediaEmbedKit.Mpv.Runtime/**.cs` 與 `src/MediaEmbedKit.Mpv.Externals/**.cs` 的 hash 組成。提供者目錄、執行階段輔助工具或下載器清理邏輯改變時會自然建立新快取，舊快取交由 GitHub Actions 清除政策清理，不在工作流程內主動刪除。

## libmpv

輔助工具可從 shinchiro `mpv-winbuild-cmake` 與 zhongfly `mpv-winbuild` 下載對應架構的 `mpv-dev-{token}-*.7z` 資產（x64 token 為 `x86_64`、ARM64 token 為 `aarch64`）。這些來源是 mpv Windows git 建置版，不是 mpv 穩定版發行。兩個提供者的命名規範完全相同，`ProviderFallbackOrder` 機制在 x64 與 ARM64 上行為一致。

**預設 `Provider = Zhongfly`** —— 兩家中唯一同時提供 GPL 與 LGPL libmpv 建置版的來源；搭配預設 `LicensePreference = PreferLgpl` 能實際拿到 LGPL 建置版。`ProviderFallbackOrder` 預設含 `Shinchiro` 作為備援，zhongfly 失效時自動備援。

下載後必須驗證封存檔包含 `libmpv-2.dll`。提供者對齊狀態記錄於 `docs/runtime/libmpv-git-builds.json`。

### libmpv 授權版本選擇（GPL vs LGPL）

libmpv 與其內嵌的 FFmpeg 兩種授權建置都存在，差異在於 mpv 編譯時 FFmpeg 是否帶 `--enable-gpl`：

- **GPL 建置版**：包含 libx264、libxvid、libxavs 等 GPL-only 編解碼器；散發時整套受 GPLv2+ 義務（公開原始碼、衍生作品 copyleft 等）。
- **LGPL 建置版**：排除 GPL-only 元件；商用閉源散發較容易合規（仍需履行 LGPL 對動態連結與授權聲明的義務）。

`MpvWindowsBuildDownloadOptions.LicensePreference` 控制偏好，**但實際拿到哪種建置取決於 `Provider` 是否真的提供 LGPL 變體**：

| Provider | 提供的變體 | `PreferLgpl`（預設）實際拿到 | `RequireLgpl` 實際拿到 |
| --- | --- | --- | --- |
| `Zhongfly`（預設） | GPL + LGPL | **LGPL 建置版** ✅ | LGPL 建置版 |
| `Shinchiro` | **僅 GPL** | GPL 建置版 ⚠️（無 LGPL 可選，靜默備援） | **明確失敗**（提醒無 LGPL 可選） |

> ⚠️ **「`PreferLgpl` 是偏好不是保證」**
>
> 若手動切到 `Provider = Shinchiro` 但保持 `PreferLgpl` 預設，**實際拿到的是 GPL 建置版** —— 與「我設了 LGPL 偏好就安全」的直覺不符。商用嚴格合規請採以下任一：
>
> - 維持 `Provider = Zhongfly`（預設），或
> - 切到 `Shinchiro` 但同時設 `LicensePreference = RequireLgpl` —— 因 Shinchiro 無 LGPL 會直接失敗，當作明確失敗訊號提醒應換提供者

`MpvLicenseAuditor.AnalyzeAsync(runtimeDirectory)` 可在執行階段解析 `mpv-configuration` 與 `ffmpeg -version` 確認實際拿到的授權標籤；散發前建議納入 發行檢查。

### GPL vs LGPL 程式面實際差異範圍

**核心結論**：LGPL libmpv **不是「低階版 API」**，C API 幾乎可視為同一套；差異主要在 建置時排除 GPL-only 的 mpv 內部模組與部分輸出 / 輸入後端。本專案 Windows desktop 嵌入用法上實際差異有限。

驗證來源：

- libmpv C API：[`include/mpv/client.h`](https://github.com/mpv-player/mpv/blob/master/include/mpv/client.h) `#define MPV_CLIENT_API_VERSION MPV_MAKE_VERSION(2, 5)` —— 單一 enum 涵蓋兩種建置版。
- mpv 建置選項：[`meson.options`](https://github.com/mpv-player/mpv/blob/master/meson.options) 的 `gpl` option 預設 `true`；設 `false` 即走 LGPL 建置版。
- GPL-only 模組清單：[`Copyright`](https://github.com/mpv-player/mpv/blob/master/Copyright)。

LGPL 建置版 排除的模組（取自上游 Copyright 完整清單）：

| 模組 | 何時影響 | 對本專案 Windows 嵌入用法的影響 |
| --- | --- | --- |
| `video/out/vo_direct3d.c` | 舊式 Direct3D VO（被 D3D11 / `vo=gpu` 取代） | ⚪ 現代設定不用 |
| `video/out/vo_x11.c`、`vo_xv.c`、`x11_common.*` | Linux X11 video output | ❌ Linux only |
| `video/out/vo_vaapi.c` | Linux VAAPI 硬體加速 | ❌ Linux only |
| `video/out/vo_vdpau.c`、`video/vdpau.c` | NVIDIA Linux 硬體解碼 / 輸出 | ❌ Linux only |
| `video/out/vo_caca.c` | terminal CACA 顯示 | ⚪ 不適用 desktop |
| `audio/out/ao_jack.c` | JACK audio | ❌ Linux 多 |
| `audio/out/ao_oss.c` | BSD OSS audio | ❌ BSD only |
| `stream/dvb*` | DVB 數位電視 | ⚪ 本專案不承諾 |
| `stream/stream_cdda.c` | CDDA 音樂 CD | ⚪ 本專案不承諾 |
| `stream/stream_dvdnav.c` | DVD 導航 | ⚪ 本專案不承諾 |

**沒在 GPL-only 清單上**（LGPL 建置版 仍可用）：

- `vo_gpu` / `vo_gpu_next`（D3D11 / Vulkan / OpenGL）—— 現代 Windows 主播放路徑
- libmpv render API（OpenGL context、D3D11 texture、Vulkan）
- WASAPI（Windows audio output）
- 所有 stream protocol（HTTP / HTTPS / file / smb / sftp 等）
- 所有編解碼器（這層由 FFmpeg `--enable-gpl` 決定，**不是** mpv `-Dgpl` 控制）

### 對本專案 5 套 UI 框架的實際影響評估

| 框架 / 路徑 | render 方式 | LGPL 影響 |
| --- | --- | --- |
| **WinForms** | `wid=` HWND 嵌入 + 預設 `vo=gpu`（D3D11） | ✅ 不影響 |
| **WPF** | 同上 | ✅ 不影響 |
| **WinUI 3** | 同上 | ✅ 不影響 |
| **MAUI Windows** | 同上 | ✅ 不影響 |
| **Avalonia** | libmpv render API（OpenGL context） | ✅ 不影響 |
| **Console / 純 API 呼叫端** | 視 `MpvPlayerOptions.InitialOptions` 而定 | ⚠️ 若刻意設 `vo=direct3d` 走 舊式路徑 → LGPL 建置版沒有；現代 `vo=gpu` 不影響 |

### 容易混淆：mpv `-Dgpl` 差異 vs FFmpeg `--enable-gpl` 差異

「GPL 建置版 比較完整」的觀感常常混到兩層獨立的 license toggle：

1. **mpv `-Dgpl=false`**：排除上表的 mpv 內部 GPL-only 模組（舊式 VO / Linux 後端 / DVB / CDDA / DVDNav）。
2. **FFmpeg `--enable-gpl`**：libx264、libxvid、libxavs 等 GPL-only 編解碼器是否內建（影響 `MpvEncoder` 可用的 preset）。

對 `MpvEncoder` 與相關 轉碼預設，**第 2 層差異比第 1 層更實質**。播放一般 mp4 / mkv / YouTube 通常差異不大；轉檔、硬體 encoder、特定 codec 預設才容易浮現。zhongfly 同一 release 內 `mpv-dev-lgpl-*` 與 `ffmpeg-lgpl-*` 是分開的兩個 asset，兩層 toggle 各自獨立。

### 建議：差異實測工具（未實作）

為了把「授權差異」轉成可審計的「功能矩陣差異」，建議加一支實測工具，分別載入 LGPL / GPL `libmpv-2.dll` 並 dump：

- `mpv-version`、`mpv-configuration` properties
- `protocols`、`decoders`、`demuxers`（透過 mpv property API）
- `vo=help`、`ao=help`、`hwdec=help`（透過 mpv message handler）
- `MpvEncoder` preset probe 結果（哪些 視訊 / 音訊 codec 預設在當前建置可用）
- 同一組媒體在 5 套 UI 框架範例上的播放結果

把輸出固定格式存到 `artifacts/license-matrix-{lgpl|gpl}.json`，發行前比對 兩份檔即知差異實際範圍。**目前未實作 —— 預定為 發行前修飾 任務之一。**

### .7z 解壓 4-tier 備援鏈

mpv-dev 壓縮檔是 `.7z` 格式，需 7z 相容工具才能解壓。輔助工具依序嘗試以下 4 段備援，找到能用的就停；全部失敗才擲 `InvalidOperationException`，訊息含每層失敗細節與使用者可採取的解法。

| 順序 | 對象 | 偵測 | CLI |
| --- | --- | --- | --- |
| 0 | `MpvWindowsBuildDownloadOptions.SevenZipPath` 顯式 | 路徑檢查 | 走第 2 層格式 |
| 1 | **Windows 內建 `tar.exe`**（bsdtar / libarchive） | `%SystemRoot%\System32\tar.exe` 或 PATH | `tar -xf {archive} -C {dir} libmpv-2.dll` |
| 2 | 系統 **7-Zip** | `Program Files\7-Zip\7z.exe` / `(x86)` / PATH | `7z x -y -o{dir} {archive} libmpv-2.dll -r` |
| 3 | **WinRAR** | `Program Files\WinRAR\WinRAR.exe` / `(x86)` | `WinRAR x -ibck -y {archive} libmpv-2.dll {dir}\` |
| 4 | 從 [ip7z/7zip](https://github.com/ip7z/7zip) 下載 `7zr.exe`（最後備援） | 既有檔重用；缺則拉 `releases/latest` 並驗證 GitHub digest | 同第 2 層 |

各層特性：

- **tar.exe**（Windows 10 1803+ / Server 2019+ 內建）：API 最簡單、速度最快、無前置依賴。實測 Windows 11 24H2 (bsdtar 3.8.4 / liblzma 5.8.1) 解 shinchiro mpv-dev `.7z` 約 ~1.8 秒。舊版 Windows 10 (1803–2004) 的 tar.exe LZMA2 支援未經完整驗證 —— 失敗自動跳下一層。
- **系統 7-Zip**：power user 常裝；CLI 穩定、Windows ARM64 有 原生版本。
- **WinRAR**：中文圈裝機率高；`-ibck` 旗標跑背景模式不彈 UI（試用期過後 WinRAR 自身可能在啟動時彈提醒，本程式無法避免）。
- **下載 7zr.exe**：終極備援。Igor Pavlov 官方 [ip7z/7zip](https://github.com/ip7z/7zip) 發佈的獨立版 32-bit x86 CLI（588 KB，純 LGPL-2.1+ 不含 unrar 限制）。既有 `7zr.exe` 存在時直接重用，不重複下載；首次下載驗證 GitHub 資產 digest。**Windows on Arm**：7zr.exe 透過 x86 模擬執行；Windows 11 ARM64 效能 OK，Windows 10 ARM64 模擬較慢，建議 ARM64 使用者預裝 7-Zip ARM64 原生版（[7-zip.org/download.html](https://www.7-zip.org/download.html)）走第 2 層備援。

選擇性解壓：`mpv-dev-*.7z` 內除 `libmpv-2.dll` 外還含 `include/mpv/*.h` 與 `libmpv.dll.a` 等 C++ 建置階段 用的雜物，執行階段完全用不到。備援鏈預設只解出 `libmpv-2.dll`，執行階段資料夾保持乾淨。

`libmpv-2.dll` 載入後不得在同一處理序 hot reload。更新流程必須：

1. 偵測 `MpvLibraryLoader.IsLoaded`。
2. 將新檔案暫存至 `.updates`。
3. 回傳 `RequiresProcessRestart = true`。
4. 由應用程式於下次啟動且載入 libmpv 前套用更新。

`MpvWindowsBuildDownloadOptions.ProviderFallbackOrder` 可指定主要提供者失敗時的備援嘗試順序；下載端會依序嘗試並把每個失敗收進 `AggregateException`，全部失敗才擲出。

## 更新排程器與健康檢查

`MpvLibraryUpdateScheduler` 將 libmpv-2.dll 的暫存／套用／回滾包成四階段：

- `StageAsync(cancellationToken)` 下載最新建置版 並暫存至 `.updates/<timestamp>/`。
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

`MpvLicenseAuditor.AnalyzeAsync(runtimeDirectory, probeLibMpv: bool)` 從 `mpv-configuration` 與 `ffmpeg -version` 輸出解析授權旗標，分類為 `Unknown` / `Lgpl` / `Gpl` / `NonFree`，並回報整體判定（以較嚴格者為準）。使用者散發執行階段 前應依此判定確認義務。

## yt-dlp

yt-dlp 輔助工具支援 Windows x64（`yt-dlp.exe`）與 ARM64（`yt-dlp_arm64.exe`，自 [2026-03-17 發行版](https://github.com/yt-dlp/yt-dlp/releases/tag/2026.03.17) 起）下載、自我更新與路徑回填。`MpvWindowsRuntimeInstaller` 會把下載的執行檔統一儲存為 `yt-dlp.exe`，讓 mpv 預設搜尋路徑與 `MpvPlayerOptions.YtdlpPath` 預設值在兩個架構上行為一致。

應用程式可透過下列 API 控制 mpv 使用的格式：

- `MpvPlayerOptions.YtdlpFormatPreset`
- `MpvPlayerOptions.YtdlpFormat`
- `MpvPlayerOptions.UseYtdlpFormat(...)`
- `MpvPlayerOptions.UseYtdlpMaximumHeight(...)`
- `MpvPlayer.SetYtdlpFormat(...)`
- `MpvPlayer.SetYtdlpMaximumHeight(...)`

若需要 yt-dlp 自身 stdout/stderr、格式清單、JSON 或下載進度，請直接使用 `YtDlpProcessRunner`。mpv ytdl hook 的 JSON 子處理序結果則由 `MpvPlayer.GetYtdlJsonSubprocessResult()` 讀取，不應解析 `log-message`。

若需要可稽核的供應鏈驗證，應優先使用 `YtDlpDownloader.InstallOrUpdateLatestExecutableAsync(...)` 搭配驗證政策。`YtDlpDownloader.RunSelfUpdateAsync(...)` 是 yt-dlp 內建更新命令的薄型包裝，適合手動維護，不取代 輔助工具的 SHA-256 驗證流程。

## Deno

Deno 輔助工具支援 Windows x64（`deno-x86_64-pc-windows-msvc.zip`）與 ARM64（`deno-aarch64-pc-windows-msvc.zip`，自 [Deno 2.7、2026-02 發行版](https://deno.com/blog/v2.7) 起官方提供）`deno.exe` 下載、自我更新與外部處理序輸出事件。Deno 不是本機檔案播放的必要條件，但 yt-dlp YouTube EJS 情境可能需要外部 JavaScript 執行階段，因此輔助工具預設與 `yt-dlp.exe` 同層準備 `deno.exe`。

需要使用 Deno 內建升級流程且要求總和檢查碼 時，使用 `DenoDownloader.RunSelfUpgradeWithChecksumAsync(...)`。若要維持完整下載紀錄與來源鎖定，應使用 `DenoDownloader.DownloadAndExtractLatestAsync(...)` 搭配驗證政策。

`DenoDownloader.DownloadAndExtractLatestAsync(...)` 會先解壓到 `runtime/.deno-extract/<guid>/` 暫存資料夾，只將驗證過的 `deno.exe` 複製到 執行階段根目錄，最後清掉暫存資料夾。即使未來 Deno zip 增加說明檔或子資料夾，也不會污染 執行階段根目錄。

## FFmpeg

FFmpeg 輔助工具支援從 yt-dlp `FFmpeg-Builds` 下載對應架構的 GPL 建置版：x64 為 `ffmpeg-master-latest-win64-gpl.zip`、ARM64 為 `ffmpeg-master-latest-winarm64-gpl.zip`（兩者皆由 [yt-dlp/FFmpeg-Builds](https://github.com/yt-dlp/FFmpeg-Builds/releases) 與其上游 [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds/releases) 自動產製）。下載後 `ffmpeg.exe` 與 `ffprobe.exe` 放在執行階段資料夾根目錄。`FFmpegDownloader.GetWindowsAssetName(MpvWindowsArchitecture)` 提供架構到資產名稱的對應表。

> ⚠️ **GPL 授權警示 —— `IncludeFFmpeg = true` 即視同接受 GPL 散發義務**
>
> `MpvWindowsRuntimeDownloadOptions.IncludeFFmpeg` **預設為 `false`**。明確設為 `true` 會下載 yt-dlp/FFmpeg-Builds 的 **GPL 建置版**（上游無 LGPL 變體 —— 與 libmpv 不同，FFmpeg 沒有「我只是偏好 LGPL」這種半安全選項，要嘛不要 FFmpeg，要嘛接受 GPL）。一旦下載並隨應用程式散發，使用者背負完整 GPLv2+ 義務：公開衍生作品原始碼、保留 license notice、不可加額外限制等。
>
> - **商用閉源散發 / 不確定能否履行 GPL 義務**：保持預設 `IncludeFFmpeg = false`。
> - **僅本機 yt-dlp 後處理、不打包散發**：啟用 FFmpeg 是 OK 的 —— 義務在散發環節。
> - **需要 LGPL FFmpeg**：本輔助工具不支援；可自行從 zhongfly `mpv-winbuild` 取 `ffmpeg-lgpl-*.7z` 解出 `ffmpeg.exe` / `ffprobe.exe` 放到執行階段資料夾。
>
> 本輔助工具僅提供下載與驗證工具，不對授權合規做進一步處理 —— 啟用此選項即視同自願接受 GPLv2+ 散發義務。

FFmpeg 沒有本專案可呼叫的內建自我更新命令。若要更新，請重新呼叫 `FFmpegDownloader.DownloadAndExtractLatestAsync(...)` 或 `MpvWindowsRuntimeInstaller.InstallOrUpdateAsync(...)`，並於 `FFmpegDownloadOptions.OverwriteExisting = true` 時覆蓋既有檔案。既有 `ffmpeg.exe` / `ffprobe.exe` 必須搭配符合目前發行資產的 `ffmpeg.exe.version` 才會重用；輔助工具仍會依 `RetainArchive` 處理 `ffmpeg-master-latest-*-gpl.zip`：預設 `false` 清掉所有 FFmpeg-Builds zip，明確 `true` 時驗證並只保留目前資產。

### yt-dlp/FFmpeg-Builds 的雙發行結構

yt-dlp/FFmpeg-Builds 同一儲存庫並存兩種發行，命名規範完全不同：

| 發行類型 | 標籤範例 | 資產命名 |
| --- | --- | --- |
| 穩定 URL 發行 | `latest`（字面標籤） | `ffmpeg-master-latest-{arch}-gpl.zip`（**固定**） |
| 每小時 CI 建置 | `autobuild-YYYY-MM-DD-HH-MM` | `ffmpeg-N-{buildnum}-g{commit10}-{arch}-gpl.zip`（**含建置編號 / commit**） |

GitHub API 的 `/releases/latest` 端點語意是「取 `created_at` 最新、非 draft 非 prerelease 的 release」——**不會**特別取 標籤名稱為 `latest` 的 release。由於 autobuild 每小時新增、`created_at` 必然新於 `latest` 標籤，`/releases/latest` 會回傳 autobuild 發行，其資產名稱含動態建置編號 / commit，與 `FFmpegDownloader.WindowsX64AssetName`（寫死 `ffmpeg-master-latest-win64-gpl.zip`）對不起來。

`FFmpegDownloader` 因此使用 `/releases/tags/latest` 端點，明確取 標籤名稱為 `latest` 的 release，資產名稱穩定。此契約由 `VerifyFFmpegBuildsLatestTagAssetNamingAsync` 整合測試鎖住：上游若改命名或拆掉 `latest` 標籤，發行檢查閘門立即失敗。

**不要把這個改成 `/releases/latest`** —— 那是用來抓 每小時 autobuild 的端點，與本專案期待的穩定資產名稱結構不相容。其他下載器（yt-dlp / Deno / libmpv 兩家提供者）的儲存庫都是「單軌發行」（tag 為日期或版本號，無另外的 `latest` 標籤），所以它們繼續使用 `/releases/latest` 是安全的，請勿「順手對齊」改成 `tags/latest` —— 那會直接 404。

yt-dlp 官方將 `ffmpeg` 與 `ffprobe` 列為 強烈建議的相依項目；本輔助工具僅將其視為 yt-dlp 附帶工具，不提供 FFmpeg 包裝器、轉檔佇列或批次工作 API。

## mpv 設定與 scripts

使用者可選擇讓執行階段資料夾同時作為 mpv 設定資料夾。啟用後，核心會設定 `config-dir` 並載入同層 `mpv.conf`、`input.conf` 與 `scripts`。

```csharp
MpvPlayerOptions options =
    MpvWindowsRuntimeInstaller.CreatePlayerOptions(
        runtimeDirectory,
        loadRuntimeConfiguration: true);
```

若只需指定特定檔案，使用 `MpvPlayerOptions.ConfigFiles`、`InputConfigFile`、`ScriptFiles`、`AddConfigFile(...)`、`AddScriptFile(...)` 或 `MpvPlayer.LoadScript(...)`。

## HTTP 要求

下載輔助工具必須重複使用共用 `HttpClient`。現代 .NET 目標應使用 `SocketsHttpHandler.PooledConnectionLifetime`，降低 DNS 陳舊與連線用盡風險；.NET Framework 目標保留共用 client 策略。

瀏覽器標頭集中於 `BrowserRequestHeaders`，包含 Chrome Stable 桌面 `User-Agent` 與必要 client hints。

## 授權

本專案受控原始碼採用 CC0-1.0。此授權不涵蓋 mpv/libmpv、yt-dlp、Deno、FFmpeg 或其相依元件。

輔助工具預設值已往「對不確定散發授權的多數使用者較安全」方向收緊：

- `MpvWindowsBuildDownloadOptions.Provider` 預設為 `Zhongfly`：兩家中唯一同時提供 GPL 與 LGPL libmpv 建置版的來源，搭配下方 `LicensePreference = PreferLgpl` 能實際拿到 LGPL 建置版。`ProviderFallbackOrder` 預設含 `Shinchiro` 作為備援。
- `MpvWindowsBuildDownloadOptions.LicensePreference` 預設為 `PreferLgpl`：上游有 LGPL 變體時優先選用，無時備援至 GPL（不中斷現有環境）。商用嚴格合規請設 `RequireLgpl`（沒 LGPL 直接失敗、不靜默備援）；不要授權偏好請設 `Any`。**`PreferLgpl` 是偏好不是保證 —— 切到 `Provider = Shinchiro` 會靜默備援至 GPL；詳見上方「libmpv 授權版本選擇」表。**
- `MpvWindowsRuntimeDownloadOptions.IncludeFFmpeg` 預設為 `false`：yt-dlp/FFmpeg-Builds 只發 GPL 建置版 且**無 LGPL 變體**，預設拉進執行階段會讓使用者背負未必知情的 GPL 散發義務。需要 FFmpeg 後處理或自行編碼的應用程式應明確設為 `true`，並接受 GPL 義務；詳見下方「FFmpeg」段。

無論採何種預設值，使用者散發執行階段 前均應依 `MpvLicenseAuditor.AnalyzeAsync(runtimeDirectory, probeLibMpv: bool)` 的判定確認義務，並查證對應提供者建置的實際 `mpv-configuration` 與 `ffmpeg -version` 內容。

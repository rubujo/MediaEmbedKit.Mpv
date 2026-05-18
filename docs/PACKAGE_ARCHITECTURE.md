# Package Architecture

本檔案定義 `MediaEmbedKit.Mpv` 的 NuGet package 拆分策略與遷移路線。Pre-v1 階段 API 仍可能調整；v1.0 後此架構 freeze。

## 設計目標

1. **功能完整**：保留 v0.0.1 已實作的所有功能（5 UI 框架、runtime helper、encoding API、license auditor、archive 解壓 4-tier fallback、Sigstore attestation）。
2. **使用者最大選擇**：每個功能域獨立 NuGet package，consumer 只裝需要的部分。
3. **單一維護者友善**：monorepo + multi-csproj，避免 multi-repo 維運負擔。
4. **避免「wrap with intention」反模式**：把 orthogonal 功能（FFmpeg / Deno / yt-dlp 工具鏈）獨立，不污染 libmpv binding 職責邊界。

## Package 拓樸

```
                 ┌─ MediaEmbedKit.Mpv ─────────────────────────────┐
                 │  核心 binding：Native P/Invoke、MpvPlayer、     │
                 │  render API、MpvLibraryLoader、value types       │
                 │  encoding 資料型別、shared Platforms enum        │
                 │  (~12 k LOC，僅 Logging.Abstractions 依賴)      │
                 └────────────────────────┬────────────────────────┘
                                          │
       ┌──────────────────────┬───────────┼──────────────┬───────────┬──────────┐
       │                      │           │              │           │          │
       ▼                      ▼           ▼              ▼           ▼          ▼
┌──────────────┐    ┌─────────────────┐ ┌─────────┐  ┌──────────┐ ┌──────────┐ ┌─────────┐
│ .Externals   │    │ .Runtime         │ │.Encoding│  │.Hosting  │ │.WinForms │ │.Wpf     │
│ FFmpeg/Deno/ │←───│ libmpv installer │ │MpvEncoder│ │ DI ext.  │ │MpvPlayer-│ │MpvWpf-  │
│ yt-dlp +     │    │ archive 4-tier   │ │ (~900)   │ │ (~50)    │ │Control   │ │Player   │
│ net infra +  │    │ cross-proc lock  │ └─────────┘  └──────────┘ │ (~800)   │ │+Airspace│
│ ArchiveSafety│    │ sidecar marker   │                           └──────────┘ │ (~1.4k) │
│ (~3 k LOC)   │    │ (~3 k LOC)       │  ┌─────────────┐                       └─────────┘
└──────────────┘    └────────┬─────────┘  │ .Avalonia   │  ┌─────────┐  ┌──────────────┐
                             │            │ OpenGL render│  │.WinUI 3 │  │.Maui Windows │
                             ▼            │ API (~1 k)   │  │ (~2.2k) │  │ handler      │
                    ┌──────────────────┐  └─────────────┘  └─────────┘  │ (~1.3k)      │
                    │ .Diagnostics     │                                └──────────────┘
                    │ LicenseAuditor + │
                    │ HealthCheck +    │            ┌────────────────────────────────────┐
                    │ UpdateScheduler  │            │ .Full (meta)                       │
                    │ (~1.2 k LOC)     │            │ PackageRef → 6 service-layer 套件 │
                    └──────────────────┘            │ (UI 套件 TFM 分散，不入 meta)      │
                                                    └────────────────────────────────────┘
```

**11 個 service / UI 套件 + 1 meta = 12 packages。**

## Package 清單

| Package | 內容 | 實際 LOC | 直接依賴 | 目標使用者 |
|---|---|---:|---|---|
| `MediaEmbedKit.Mpv` | Native P/Invoke、MpvPlayer、render、library loader、value types、events、`MpvEncodingOptions` / `MpvEncodingResult` 等資料型別、`MpvNativeRuntimePlatform` / `MpvWindowsArchitecture` 等共用 enum | ~12 k | `Microsoft.Extensions.Logging.Abstractions` | 所有人 |
| `MediaEmbedKit.Mpv.Externals` | FFmpeg / Deno / yt-dlp downloaders + ExternalTool primitives + 共用 net infra (`DownloadUtility` / `BrowserRequestHeaders` / `GitHubReleaseModels` 為 internal) + `ArchiveSafety` + `MpvNativeAssetVerificationPolicy` + `MpvNativeRuntime*` catalog | ~3 k | `.Mpv` | 要 yt-dlp 後處理鏈的人；可獨立用 |
| `MediaEmbedKit.Mpv.Runtime` | libmpv Windows runtime installer + 4-tier archive 解壓 + cross-process lock + sidecar marker + `MpvAppBuilder.UseWindowsRuntimeAutoInstall` extension | ~3 k | `.Mpv`, `.Externals` | 想要 helper 自動裝 runtime 的人 |
| `MediaEmbedKit.Mpv.Diagnostics` | `MpvLicenseAuditor` + `MpvRuntimeHealthCheck` + `MpvLibraryUpdateScheduler` | ~1.2 k | `.Mpv`, `.Externals`, `.Runtime` | 商用合規、staging update 需求 |
| `MediaEmbedKit.Mpv.Hosting` | `Microsoft.Extensions.DependencyInjection` integration (`AddMpvPlayer` / `AddMpvPlayerFactory`) | ~50 | `.Mpv` | DI 使用者 |
| `MediaEmbedKit.Mpv.Encoding` | `MpvEncoder` 高階轉碼 facade (9 個 recipe 方法) | ~900 | `.Mpv` | 做轉檔 / 編碼的人 |
| `MediaEmbedKit.Mpv.WinForms` | WinForms `MpvPlayerControl`（繼承 `System.Windows.Forms.Control`） | ~800 | `.Mpv` | WinForms 使用者 |
| `MediaEmbedKit.Mpv.Wpf` | WPF `MpvWpfPlayer`（繼承 `HwndHost`） + Airspace popup | ~1.4 k | `.Mpv` | WPF 使用者 |
| `MediaEmbedKit.Mpv.Avalonia` | Avalonia `MpvAvaloniaPlayer`（繼承 `OpenGlControlBase`，不走 HWND，走 OpenGL render API） | ~1 k | `.Mpv` | Avalonia 使用者 |
| `MediaEmbedKit.Mpv.WinUI` | WinUI 3 `MpvWinUiPlayer`（繼承 `Grid`，HWND child + overlay）+ `MpvWinUiHwndPlayer` | ~2.2 k | `.Mpv` | WinUI 使用者 |
| `MediaEmbedKit.Mpv.Maui` | MAUI `MpvView`（繼承 `View`） + `MpvViewHandler`（Windows 平台橋 WinUI） | ~1.3 k | `.Mpv`, `.WinUI` | MAUI 使用者 |
| `MediaEmbedKit.Mpv.Full`（meta） | `<IncludeBuildOutput>false</IncludeBuildOutput>`，`<ProjectReference>` 拉 6 個 service-layer 套件 (core + Externals + Runtime + Diagnostics + Hosting + Encoding)。UI 套件 TFM 分散不入 meta | 0 | 6 service-layer 套件 | 「service 層都裝」+ 不在意大小 |

**12 packages（11 actual + 1 meta）**。**沒有 `.UI.Core` 套件** —— 原 plan 把它列為「5 UI package 的內部依賴」是 architecturally infeasible 的設計（見下方「`.UI.Core` 為何 abandon」）。

## 設計決策

### 為何 `.Externals` 不依賴 libmpv

`FFmpegDownloader` / `DenoDownloader` / `YtDlpDownloader` 與 libmpv 完全無關 —— 是「3 個工具的 helper downloader」。獨立後：
- 純 yt-dlp 使用者（不用 mpv）可以單獨用 `.Externals`
- libmpv 使用者若不需要 yt-dlp 後處理，~2 k LOC 不會 pulled in
- 未來新工具（ImageMagick / Rclone 等 downloader）也是 `.Externals` 的事，不污染 mpv binding

業界共識：[Yusuf Aytas, On Writing Wrapper Libraries](https://yusufaytas.com/on-writing-wrapper-libraries) 「wrap with intention, not everything」。

### `.UI.Core` 為何 abandon

原 plan 提議抽 `MpvPlayerHostBase` 共用 abstract 基底給 5 UI 套件繼承。**實作前驗證後發現此設計 architecturally infeasible**：

**1. C# 不支援多重繼承。** 5 UI 控制項各自必須繼承自己框架的 UI base class，**無法**同時繼承一個共用的 `MpvPlayerHostBase`：

| 控制項 | 強制繼承的 framework base | 為什麼 |
|---|---|---|
| `MpvPlayerControl` | `System.Windows.Forms.Control` | WinForms 控制項必須 |
| `MpvWpfPlayer` | `System.Windows.Interop.HwndHost` | WPF 嵌入 native HWND 必須 |
| `MpvAvaloniaPlayer` | `Avalonia.OpenGL.Controls.OpenGlControlBase` | 用 OpenGL render API |
| `MpvWinUiPlayer` | `Microsoft.UI.Xaml.Controls.Grid` | 容納 HWND child + overlay panel |
| `MpvView` | `Microsoft.Maui.Controls.View` | MAUI 控制項必須 |

**2. Avalonia 不走 HWND embedding。** 原 plan 假定 5 框架共用「取得 native window handle 並 set `wid`」邏輯。但 [`MpvAvaloniaPlayer`](../src/MediaEmbedKit.Mpv.Avalonia/MpvAvaloniaPlayer.cs:20) 用 `OpenGlControlBase` + `mpv_render_context_create` / `mpv_render_context_render` —— 整套 native handle 邏輯**不適用**。

**3. 預估 LOC 節省被高估。** 原 plan 寫「6.2 k → 3.5–4 k LOC」（40% 節省）。但 5 控制項實際 LOC 大頭是 framework-specific DependencyProperty / StyledProperty / BindableProperty 等屬性系統宣告 —— 這些**無法共用**。剩下能 share 的 plumbing 約 500–800 行（< 12%）。即使改用 composition pattern 也只能省 ~10%。

**4. 「LibVLCSharp 業界先例」引用更正。** 原 doc 引用 [`LibVLCSharp.Shared`](https://www.nuget.org/profiles/videolan) 作為 base class 抽象先例。實際檢視該套件**只是核心 LibVLC 互動 + 跨平台 MediaPlayer**，**並非 UI 控制項 base class**。LibVLCSharp 的 5 個 UI 套件（WinForms / WPF / Forms.WPF / Avalonia / MAUI）各自獨立繼承框架 base，**也未共用 UI 基底**。原引用是錯誤類比。

**結論**：5 個 self-contained 的 UI 控制項（~800–2.2 k LOC each）反而對單人維護更友善 —— 每個讀完就能完全理解該框架的嵌入細節，不需要跳到 `.UI.Core` 看 base。`MpvPlayer` 核心 binding 已是共用點；UI 層的「共用」物理上不可行。

`MediaEmbedKit.Mpv.UI.Core` **不存在**於最終 12 packages 拓樸內。

### 為何不拆 multi-repo

| 維度 | Monorepo（建議） | Multi-repo |
|---|---|---|
| Git 歷史 | 單一、可追跨 package refactor | 每 repo 獨立 |
| CI / release pipeline | 一條 workflow `dotnet pack` 全部 | 12 條 workflow |
| 維護負擔 | 對單人維護者**友善** | 12 套 issue tracker |
| 業界先例 | dotnet/aspire、dotnet/runtime、AspNetCore（1000+ projects 同 repo） | nuget org 多 repo |

對單一維護者 scale，**monorepo + multi-csproj** 是 sweet spot。每個 csproj 輸出獨立 NuGet。

### 版本策略（pre-v1）

- **統一版號**：所有 packages 同步 bump。`Directory.Build.props` 設一個 `<PackageVersion>`。簡單但每次任何 package 改都全部 bump。適合 pre-v1 + 單人維護。
- **獨立版號**：v1.0 後再考慮。相依矩陣會炸，pre-v1 不值得。

## 遷移路線（**增量、不 big-bang**）

| Phase | 範圍 | 狀態 | 風險 |
|---|---|---|---|
| **v0.0.x** | 維持現狀（已 ship 路徑） | ✅ 已完成 | 零 |
| **Phase 1+2+4 合併** | 拆 `.Externals` + `.Runtime` + `.Diagnostics`（三者結構性綁定：`MpvWindowsRuntimeInstaller` 同時呼叫 libmpv 與外部工具下載；`MpvLicenseAuditor` 同時用 `ExternalToolProcessRunner` + `MpvLibraryLoader`）| ✅ 已完成 | 中 |
| **Phase 5** | 拆 `.Hosting`（DI extensions） | ✅ 已完成 | 低 |
| **Phase 6** | 拆 `.Encoding` + meta `.Full` + `tools/Invoke-PackageValidation.ps1` 12 個套件支援 + release.yml 註解更新 | ✅ 已完成 | 中 |
| **Phase 7** | dotnet format / Tests 38/38 / IntegrationTests 50/50 / build slnx / `Invoke-PackageValidation.ps1` 12/12 通過 | ✅ 已完成 | 低 |
| ~~**Phase 3**~~ | ~~拆 `.UI.Core` + 重寫 5 UI package 用 base~~ | ❌ Abandon | — |
| **v1.0** | 穩定 API freeze | 12 packages 經 real-world 使用 6+ 個月後 | — |

**Phase 1+2+4 合併原因**：原 plan 把這 3 phase 拆開做，但實際依賴分析發現：
- `.Externals` 拆出後，core 仍剩 `MpvWindowsRuntimeInstaller`（orchestrator），它呼叫 `FFmpegDownloader/DenoDownloader/YtDlpDownloader`，core 反向依賴 `.Externals`（錯方向）。
- `.Diagnostics` 內 `MpvLicenseAuditor` 用 `ExternalToolProcessRunner`（.Externals）+ `MpvLibraryLoader`（core），不拆 `.Externals` 就不能拆 `.Diagnostics`。

合併一個 commit 同時拆完三 package、繞開 transient 不一致狀態。

**Phase 3 abandon 原因**：見上方「`.UI.Core` 為何 abandon」段。簡述：C# 無多重繼承 + Avalonia 不走 HWND + LOC 節省被高估 + 業界先例引用錯誤。**Phase 3 不會做。**

## 風險與對策

| 風險 | 機率 | 對策 |
|---|---|---|
| 版本相依矩陣失控 | 中 | 統一版號 + monorepo 同步 release |
| Consumer 不知裝哪個 package | 中 | `.Full` meta（service 層）+ README 明寫「最小 vs 完整」決策樹 + UI 套件依框架單獨 ref |
| Pre-v1 拆 package 後 API 還要動 | 高 | 接受。pre-v1 本來就會動 |
| CI 時間爆增（12 個 pack） | 低 | `dotnet pack` 並行；現有 `release.yml` 用 glob `*.nupkg` / `*.snupkg`，無需逐套件加 file pattern |
| 拆 package 期間使用者卡在中間版本 | 中 | CHANGELOG 明寫 phase；每 phase 維持「previous + new package 並存一輪」轉換期 |

## 拒絕的替代方案

### 方案 1：維持單 package（現狀）
- ❌ Consumer 必須 pull in 所有功能（FFmpeg downloader、license auditor 等多數使用者用不到）。
- ❌ 違反 「wrap with intention」原則。
- ❌ 對單一維護者的長期負擔不友善（無法局部 deprecate）。

### 方案 2：超細拆（15+ packages）
- ❌ 維護負擔對單一維護者過重。
- ❌ Consumer 難以選擇正確 package。
- ❌ 業界少有單人維護專案做到這個粒度。

### 方案 3：Multi-repo
- ❌ 12 套 CI、12 套 issue tracker、cross-package refactor 困難。
- ❌ 單一維護者 scale 不適用。

## 與「精簡化」決策的關係

本架構**不會自動精簡**程式碼 —— 只是把現有功能切成獨立 packages。要同時精簡（砍 Tar/WinRAR、Samples helper bloat、雙 WinUI 實作），應在 phase 0.x 期間以獨立 PR 進行，**先精簡再拆**避免重複改動。

詳細精簡建議見 [`docs/RUNTIME_ASSETS.md`](RUNTIME_ASSETS.md) 與深度分析 backlog（GitHub issues 追蹤）。

## Source / 業界先例

- [LibVLCSharp 多 package 拆法](https://www.nuget.org/profiles/videolan)（核心 + 5 UI + 多平台；**5 UI 各自獨立繼承框架 base，無共用 UI 基底**）
- [dotnet/aspire monorepo + multi-csproj 模式](https://github.com/dotnet/aspire)
- [Microsoft.Extensions.* 套件分離模式](https://learn.microsoft.com/dotnet/standard/microservices-architecture/multi-container-microservice-net-applications/use-stack-of-microsoft-tools-and-libraries)
- [Yusuf Aytas, On Writing Wrapper Libraries](https://yusufaytas.com/on-writing-wrapper-libraries)
- [Increment: The rise of few-maintainer projects](https://increment.com/open-source/the-rise-of-few-maintainer-projects/)
- [OSS Maintainer's Guide to Saying No](https://jlowin.dev/blog/oss-maintainers-guide-to-saying-no)

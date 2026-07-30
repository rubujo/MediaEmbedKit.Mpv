# Package Architecture

本檔案描述 `MediaEmbedKit.Mpv` 的 NuGet package 拓樸與設計決策。Pre-v1 階段公開 API 仍可能調整；v1.0 後此架構 freeze。

## 設計目標

1. **功能完整**：保留全部功能 —— 核心 binding、5 UI 框架控制項、執行階段輔助工具、編碼 API、license auditor、壓縮檔解壓 備援鏈、Sigstore attestation。
2. **使用者最大選擇**：每個功能域獨立 NuGet package，使用端只裝需要的部分。
3. **單一維護者友善**：monorepo + multi-csproj，避免 multi-repo 維運負擔。
4. **避免「wrap with intention」反模式**：把 正交 功能（FFmpeg / Deno / yt-dlp 工具鏈）獨立，不污染 libmpv binding 職責邊界。

## Package 拓樸

```
                ┌─ MediaEmbedKit.Mpv ────────────────────────────┐
                │  核心 binding：Native P/Invoke、MpvPlayer、    │
                │  render API、MpvLibraryLoader、value types、   │
                │  MpvEncoder + encoding 資料型別、shared enum   │
                │  (~13 k LOC，僅 Logging.Abstractions 依賴)     │
                └────────────────────────┬───────────────────────┘
                                         │
                  ┌──────────────────────┴──────────────────┬───────────────┐
                  │                                         │               │
                  ▼                                         ▼               ▼
┌──────────────┐     ┌──────────────────┐            ┌──────────────┐
│ .Externals   │←────│ .Runtime         │            │ .Hosting     │
│ FFmpeg/Deno/ │     │ libmpv 安裝器 │            │ DI ext.      │
│ yt-dlp +     │     │ archive 4-tier   │            │ (~50)        │
│ 網路基礎設施 +  │     │ cross-proc lock  │            └──────────────┘
│ ArchiveSafety│     │ sidecar 標記   │
│ (~3 k LOC)   │     │ (~3 k LOC)       │     ┌──────────────────────────────────────┐
└──────────────┘     └────────┬─────────┘     │ UI controls (各自獨立繼承框架 base) │
                              │                │  .WinForms (~800)                    │
                              ▼                │  .Wpf      (~1.4 k, 含 Airspace)     │
                     ┌──────────────────┐      │  .Avalonia (~1 k, OpenGL render API) │
                     │ .Diagnostics     │      │  .WinUI    (~2.2 k)                  │
                     │ LicenseAuditor + │      │  .Maui.Windows (~1.3 k, 橋接 WinUI)  │
                     │ HealthCheck +    │      └──────────────────────────────────────┘
                     │ UpdateScheduler  │
                     │ (~1.2 k LOC)     │      ┌──────────────────────────────────────┐
                     └──────────────────┘      │ .Full (meta)                         │
                                               │ ProjectRef → 5 個 service 套件       │
                                               │ (UI 套件 TFM 分散，不入 meta)        │
                                               └──────────────────────────────────────┘
```

**10 actual + 1 meta = 11 packages。**

## Package 清單

| Package | 內容 | 依賴 | 目標使用者 |
|---|---|---|---|
| `MediaEmbedKit.Mpv` | Native P/Invoke、MpvPlayer、render、library loader、value types、events、`MpvEncoder` 高階轉碼 facade、encoding 資料型別、shared enum (`MpvNativeRuntimePlatform` / `MpvWindowsArchitecture`) | `Microsoft.Extensions.Logging.Abstractions` | 所有人 |
| `MediaEmbedKit.Mpv.Externals` | FFmpeg / Deno / yt-dlp 下載器 + ExternalTool 基礎型別 + 共用網路基礎設施 (`DownloadUtility` / `BrowserRequestHeaders` / `GitHubReleaseModels`) + `ArchiveSafety` + `MpvNativeAssetVerificationPolicy` + `MpvNativeRuntime*` catalog | `.Mpv` | 要 yt-dlp / Deno / FFmpeg 輔助工具的人 |
| `MediaEmbedKit.Mpv.Runtime` | libmpv Windows 執行階段 安裝器 + 4-tier 壓縮檔解壓 + 跨處理序鎖定 + sidecar 標記 + `MpvAppBuilder.UseWindowsRuntimeAutoInstall` 擴充方法 | `.Mpv`, `.Externals` | 想要 輔助工具自動裝 runtime 的人 |
| `MediaEmbedKit.Mpv.Diagnostics` | `MpvLicenseAuditor` + `MpvRuntimeHealthCheck` + `MpvLibraryUpdateScheduler` | `.Mpv`, `.Externals`, `.Runtime` | 商用合規、staging update 需求 |
| `MediaEmbedKit.Mpv.Hosting` | `Microsoft.Extensions.DependencyInjection` 整合（`IMpvPlayerFactory` / `AddMpvPlayerFactory`） | `.Mpv` | DI 使用者 |
| `MediaEmbedKit.Mpv.WinForms` | `MpvPlayerControl`（繼承 `System.Windows.Forms.Control`） | `.Mpv` | WinForms 使用者 |
| `MediaEmbedKit.Mpv.Wpf` | `MpvWpfPlayer`（繼承 `HwndHost`）+ Airspace popup | `.Mpv` | WPF 使用者 |
| `MediaEmbedKit.Mpv.Avalonia` | `MpvAvaloniaPlayer`（繼承 `OpenGlControlBase`，OpenGL render API，不走 HWND） | `.Mpv` | Avalonia 使用者 |
| `MediaEmbedKit.Mpv.WinUI` | `MpvWinUiPlayer`（繼承 `Grid`，HWND child + overlay） | `.Mpv` | WinUI 3 使用者 |
| `MediaEmbedKit.Mpv.Maui.Windows` | `MpvView`（繼承 `View`）+ `MpvViewHandler`（Windows 平台橋 WinUI）。**Windows-only** TFM (`net10.0-windows10.0.19041.0`) —— 套件名自帶 `.Windows` 即明示 scope，避免被誤認為跨平台 | `.Mpv`, `.WinUI` | MAUI Windows 使用者 |
| `MediaEmbedKit.Mpv.Full`（中繼套件） | `<IncludeBuildOutput>false</IncludeBuildOutput>` + `<ProjectReference>` 拉入 5 個服務層套件 | 5 服務層套件 | 「服務層都裝」 |

## 設計決策

### 為何 `.Externals` 不只服務 libmpv

`FFmpegDownloader` / `DenoDownloader` / `YtDlpDownloader` 與 libmpv 完全正交 —— 是 3 個工具的 輔助下載器。獨立後：

- 純 yt-dlp / FFmpeg 使用者（不用 libmpv）可以單獨用 `.Externals`。
- libmpv 使用者若不需要 yt-dlp / Deno / FFmpeg，~3 k LOC 不會被拉入。
- 未來新工具（ImageMagick / Rclone 等 下載器）也歸 `.Externals`，不污染 mpv binding 邊界。

### 為何沒有 UI 共用 base class

原本考慮抽 `MediaEmbedKit.Mpv.UI.Core` 含 abstract `MpvPlayerHostBase`，讓 5 UI 控制項繼承後只剩 framework-specific 轉接。實作前驗證後 abandon，**架構上不可行**：

- **C# 不支援多重繼承**：5 控制項各自必須繼承自己框架的 UI base（`Control` / `HwndHost` / `OpenGlControlBase` / `Grid` / `View`），無法同時繼承共用 base。
- **Avalonia 不走 HWND 嵌入**：用 `mpv_render_context_create` + OpenGL render API，原 plan 的「set `wid`」邏輯只適用 4/5 框架。
- **可 share 程式碼很少**：5 控制項實際 LOC 大頭是 framework-specific DependencyProperty / StyledProperty / BindableProperty 等屬性系統宣告 —— 這些**無法共用**。剩可 share 的 plumbing 不到 12%。
- **LibVLCSharp 業界先例引用更正**：`LibVLCSharp.Shared` 只是核心 LibVLC 互動 + 跨平台 `MediaPlayer`，**並非 UI 控制項 base class**。LibVLCSharp 5 個 UI 套件也各自獨立繼承框架 base。

結論：5 個 self-contained 的 UI 控制項對單人維護更友善。`MpvPlayer` 核心 binding 已是共用點；UI 層的「共用」物理上不可行。

### Monorepo + multi-csproj

| 維度 | Monorepo（採用） | Multi-repo |
|---|---|---|
| Git 歷史 | 單一、可追跨 package refactor | 每 repo 獨立 |
| CI / release pipeline | 一條工作流程 `dotnet pack` 全部 | 12 條工作流程 |
| 維護負擔 | 對單人維護者友善 | 12 套 issue tracker |
| 業界先例 | dotnet/aspire、dotnet/runtime、AspNetCore | 各 NuGet org |

對單人維護 scale，**monorepo + multi-csproj** 是 sweet spot。每個 csproj 輸出獨立 NuGet。

### 統一版號（pre-v1）

所有 packages 同步 bump。`Directory.Build.props` 設一個 `<PackageVersion>`。簡單但每次任何 package 改都全部 bump。適合 pre-v1 + 單人維護。**獨立版號**等 v1.0 後再考慮，相依矩陣會炸，pre-v1 不值得。

## 拒絕的替代方案

- **方案 1：維持單 套件**。Consumer 必須 pull in 所有功能（FFmpeg 下載器、license auditor 等多數使用者用不到）。違反「wrap with intention」原則。
- **方案 2：超細拆（15+ packages）**。維護負擔對單人維護過重，使用端難以選擇。業界少有單人專案做到這個粒度。
- **方案 3：Multi-repo**。12 套 CI、12 套 issue tracker、cross-package refactor 困難。單人維護不適用。

## 業界先例

- [LibVLCSharp 多 package 拆法](https://www.nuget.org/profiles/videolan)（核心 + 5 UI；5 UI 各自獨立繼承框架 base，無共用 UI 基底）
- [dotnet/aspire monorepo + multi-csproj 模式](https://github.com/dotnet/aspire)
- [Microsoft.Extensions.* 套件分離模式](https://learn.microsoft.com/dotnet/standard/microservices-architecture/multi-container-microservice-net-applications/use-stack-of-microsoft-tools-and-libraries)
- [Yusuf Aytas, On Writing Wrapper Libraries](https://yusufaytas.com/on-writing-wrapper-libraries)

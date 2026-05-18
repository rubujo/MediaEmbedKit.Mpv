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
                 │  (~12 k LOC，零 NuGet 依賴)                     │
                 └────────────────────────┬────────────────────────┘
                                          │
       ┌──────────────────────┬───────────┼──────────────┬──────────────────────┐
       │                      │           │              │                      │
       ▼                      ▼           ▼              ▼                      ▼
┌──────────────┐    ┌─────────────────┐ ┌──────────────────┐    ┌──────────────────────┐
│ .Runtime     │    │ .Encoding       │ │ .UI.Core         │    │ .Hosting             │
│ libmpv 下載 │    │ Encoder +        │ │ MpvPlayerHostBase│    │ DI extensions        │
│ idempotency  │    │ recipes (Split/ │ │ (HWND embedding +│    │ (~50 LOC, 已有)      │
│ archive 解壓 │    │ TwoPass/Concat) │ │  property forward│    └──────────────────────┘
│ cross-proc   │    │ codec preset 表 │ │  + lifecycle)    │
│ lock         │    │ (~2 k LOC)      │ │ (internal-ish)   │
│ (~3.5 k LOC) │    └─────────────────┘ └────────┬─────────┘
└──────┬───────┘                                  │
       │                                          ├── .WinForms (~700 LOC)
       │                                          ├── .Wpf (~1 k LOC，含 Airspace popup)
       ▼                                          ├── .Avalonia (~900 LOC)
┌─────────────────────┐  ┌──────────────────┐    ├── .WinUI (~1 k LOC，砍掉雙實作)
│ .Externals          │  │ .Diagnostics      │    └── .Maui (~900 LOC)
│ FFmpeg / Deno /     │  │ LicenseAuditor +  │
│ yt-dlp downloaders  │  │ HealthCheck +     │
│ Format selectors    │  │ UpdateScheduler   │
│ (~2 k LOC)          │  │ (~1.2 k LOC)      │
│ ⚠️ 不依賴 libmpv    │  └──────────────────┘
└─────────────────────┘
```

## Package 清單

| Package | 內容 | 行數估 | 直接依賴 | 目標使用者 |
|---|---|---:|---|---|
| `MediaEmbedKit.Mpv` | Native P/Invoke、MpvPlayer、render、library loader、value types、events | ~12 k | （無） | 所有人 |
| `MediaEmbedKit.Mpv.Runtime` | libmpv 下載、idempotent install、cross-process lock、archive 4-tier 解壓、sidecar marker | ~3.5 k | `.Mpv` | 想要 helper 自動裝 runtime 的人 |
| `MediaEmbedKit.Mpv.Externals` | FFmpeg / Deno / yt-dlp downloaders、format selector、ytdl JSON parser | ~2 k | （無） | 要 yt-dlp 後處理鏈的人；可獨立用 |
| `MediaEmbedKit.Mpv.Encoding` | `MpvEncoder` + 9 個 recipe 方法 + codec preset 表 | ~2 k | `.Mpv` | 做轉檔 / 編碼的人 |
| `MediaEmbedKit.Mpv.Diagnostics` | `MpvLicenseAuditor` + `MpvRuntimeHealthCheck` + `MpvLibraryUpdateScheduler` | ~1.2 k | `.Mpv`, `.Runtime` | 商用合規、staging update 需求 |
| `MediaEmbedKit.Mpv.UI.Core` | `MpvPlayerHostBase` 共用基底（HWND wid 嵌入、property forwarding、lifecycle、DesignMode sentinel、DPI / visibility） | ~800 | `.Mpv` | 5 UI package 的內部依賴 |
| `MediaEmbedKit.Mpv.WinForms` | WinForms 控制項，僅 framework-specific 適配 | ~700 | `.UI.Core` | WinForms 使用者 |
| `MediaEmbedKit.Mpv.Wpf` | WPF `HwndHost` + Airspace popup | ~1 k | `.UI.Core` | WPF 使用者 |
| `MediaEmbedKit.Mpv.Avalonia` | Avalonia OpenGL render API 控制項 | ~900 | `.UI.Core` | Avalonia 使用者 |
| `MediaEmbedKit.Mpv.WinUI` | WinUI 3 控制項（砍 `MpvWinUiHwndPlayer` 雙實作） | ~1 k | `.UI.Core` | WinUI 使用者 |
| `MediaEmbedKit.Mpv.Maui` | MAUI Windows handler | ~900 | `.UI.Core` | MAUI 使用者 |
| `MediaEmbedKit.Mpv.Hosting` | `Microsoft.Extensions.DependencyInjection` integration | ~50 | `.Mpv` | DI 使用者 |
| `MediaEmbedKit.Mpv.Full`（meta） | 空 csproj，`<PackageReference>` 拉所有上面的 | 0 | 全部 | 「我什麼都要」+ 不在意大小 |

**12 packages（11 actual + 1 meta）**。

## 設計決策

### 為何 `.Externals` 不依賴 libmpv

`FFmpegDownloader` / `DenoDownloader` / `YtDlpDownloader` 與 libmpv 完全無關 —— 是「3 個工具的 helper downloader」。獨立後：
- 純 yt-dlp 使用者（不用 mpv）可以單獨用 `.Externals`
- libmpv 使用者若不需要 yt-dlp 後處理，~2 k LOC 不會 pulled in
- 未來新工具（ImageMagick / Rclone 等 downloader）也是 `.Externals` 的事，不污染 mpv binding

業界共識：[Yusuf Aytas, On Writing Wrapper Libraries](https://yusufaytas.com/on-writing-wrapper-libraries) 「wrap with intention, not everything」。

### 為何 `.UI.Core` 抽象

5 UI 控制項共同職責 ~80% 相同：
- 取得 native window handle 並 set `wid`
- property forward（Position / Volume / IsPaused 等）
- IsLoaded / Unloaded lifecycle
- DesignMode sentinel
- DPI / visibility 觀察

抽 `MpvPlayerHostBase` → 每個 framework 控制項只剩「framework-specific 屬性系統適配」（5 種 DP / BindableProperty / StyledProperty / AttachedProperty / MAUI handler 寫法）。預估 5 UI 合計 6.2 k → 3.5–4 k LOC。

LibVLCSharp 採用同樣模式（[`LibVLCSharp.Shared`](https://github.com/videolan/libvlcsharp)）。

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

| Phase | 範圍 | 何時 | 風險 |
|---|---|---|---|
| **v0.0.x** | 維持現狀（已 ship 路徑） | 現在 | 零 |
| **v0.1** | 拆 `.Externals`（最獨立、零依賴 libmpv、最低風險開頭） | v0.0 stable 後 1–2 月 | 低 |
| **v0.2** | 拆 `.Runtime`（從 `.Mpv` 移出 install / extraction） | v0.1 收 feedback 後 | 中 |
| **v0.3** | 拆 `.UI.Core` + 重寫 5 UI package 用 base（最大重構） | v0.2 後 | 高 |
| **v0.4** | 拆 `.Encoding` + `.Diagnostics` | v0.3 後 | 中 |
| **v0.5** | meta `.Full` 上架 | 最後 | 零 |
| **v1.0** | 穩定 API freeze | 全部拆完且穩定運行 6+ 個月 | — |

**每 phase 都是獨立 PR / minor version bump，任一 phase 出問題不影響核心。**

## 風險與對策

| 風險 | 機率 | 對策 |
|---|---|---|
| 版本相依矩陣失控 | 中 | 統一版號 + monorepo 同步 release |
| Consumer 不知裝哪個 package | 中 | `.Full` meta + README 明寫「最小 vs 完整」決策樹 |
| `.UI.Core` 內部 API 變動拖累 5 UI package | 中 | `MpvPlayerHostBase` 標 `[EditorBrowsable(Never)]` + XML doc 明寫「為 UI package 內部使用」；外部直接用 caller 自負風險 |
| Pre-v1 拆 package 後 API 還要動 | 高 | 接受。pre-v1 本來就會動 |
| CI 時間爆增（12 個 pack） | 低 | `dotnet pack` 並行；現有 `release.yml` 加 12 個 file glob + 12 個 attest-build-provenance subject 即可 |
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

- [LibVLCSharp 多 package 拆法](https://www.nuget.org/profiles/videolan)（核心 + 5 UI + 多平台）
- [dotnet/aspire monorepo + multi-csproj 模式](https://github.com/dotnet/aspire)
- [Microsoft.Extensions.* 套件分離模式](https://learn.microsoft.com/dotnet/standard/microservices-architecture/multi-container-microservice-net-applications/use-stack-of-microsoft-tools-and-libraries)
- [Yusuf Aytas, On Writing Wrapper Libraries](https://yusufaytas.com/on-writing-wrapper-libraries)
- [Increment: The rise of few-maintainer projects](https://increment.com/open-source/the-rise-of-few-maintainer-projects/)
- [OSS Maintainer's Guide to Saying No](https://jlowin.dev/blog/oss-maintainers-guide-to-saying-no)

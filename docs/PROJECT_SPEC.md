# MediaEmbedKit.Mpv 專案規範

本文件是專案規範入口。細節由專責文件維護；變更支援目標、平台狀態、runtime 政策、工程規則或 AI agent 指示時，必須同步更新對應文件。

| 主題 | 文件 |
| --- | --- |
| 目標框架與平台狀態 | `docs/SUPPORT_MATRIX.md` |
| UI 後端與 AirSpace 限制 | `docs/UI_BACKENDS.md` |
| libmpv、yt-dlp、Deno、FFmpeg runtime 政策 | `docs/RUNTIME_ASSETS.md` |
| 供應鏈風險模型與 SHA pin 建議 | `docs/SECURITY_MODEL.md` |
| libmpv C API 覆蓋與測試矩陣 | `docs/LIBMPV_C_API_TEST_MATRIX.md` |
| 高階 API 與 ergonomics 指南 | `docs/HIGH_LEVEL_API.md` |
| 控制項共通綁定 API | `docs/CONTROLS_API.md` |
| 從 GitHub Release 安裝本地 NuGet 套件 | `docs/CONSUMING_PACKAGES.md` |
| 發佈前本機檢查 | `docs/RELEASE_CHECKLIST.md` |
| Windows 設計階段檢查 | `docs/DESIGN_TIME_CHECKLIST.md` |
| 工程、文件、提交與驗證規則 | `docs/ENGINEERING_STANDARDS.md` |
| AI agent 與 Agent Skills 結構 | `docs/AI_AGENT_INTEGRATION.md` |
| 參考來源 | `docs/REFERENCE_SOURCES.md` |

## 目標

MediaEmbedKit.Mpv 提供 .NET libmpv 包裝器與 Windows 桌面 UI 控制項。核心套件需覆蓋 libmpv stable v0.41.0 公開 C API，並提供常用高階播放 API。UI 套件不得重複宣告核心 P/Invoke。

目前產品支援範圍為 Windows x64 與 Windows ARM64。支援的目標框架、架構與判準由 `docs/SUPPORT_MATRIX.md` 維護。

## 設計原則

- 核心 API 盡量保持平台中立。
- 控制項建構函式與初始化流程不得自動下載第三方二進位檔。
- `libmpv-2.dll` 載入後不可在同一處理序 hot reload；更新必須暫存並提示重新啟動。
- runtime helper 必須提供 SHA-256 驗證、來源鎖定與可由使用者選擇的驗證策略；生產環境不得只依賴未鎖定的 latest 下載。
- 高階 API 可提供薄型 fluent helper，但不得引入會隱藏下載、初始化或釋放責任的 pipeline/flow 引擎。
- C# 區域變數、`using` 陳述式與 `foreach` 迴圈變數使用明確型別；只有必要時才使用 `var`。
- 提交訊息必須遵循慣例式提交 1.0.0，且同時包含主旨與正文。

平台支援判準與目標框架／架構由 `docs/SUPPORT_MATRIX.md` 維護，不在本文件重複。

## 驗證

```powershell
dotnet format .\MediaEmbedKit.Mpv.slnx --no-restore
dotnet run --project .\tests\MediaEmbedKit.Mpv.Tests\MediaEmbedKit.Mpv.Tests.csproj
dotnet run --project .\tests\MediaEmbedKit.Mpv.IntegrationTests\MediaEmbedKit.Mpv.IntegrationTests.csproj
dotnet build .\MediaEmbedKit.Mpv.slnx
```

整合測試需要 Windows `libmpv-2.dll`。URL 播放需要 `yt-dlp.exe` 可被 mpv 找到，或透過 `MpvPlayerOptions.YtdlpPath` 指定。FFmpeg-Builds 下載驗證需可連線至 GitHub Releases。

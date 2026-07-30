# 技能：MediaEmbedKit.Mpv

本技能用於編修 MediaEmbedKit.Mpv 的核心 API、UI 後端、執行階段輔助工具、範例、測試與文件。

## 讀取順序

1. `AGENTS.md`
2. `docs/PROJECT_SPEC.md`
3. 與任務相關的專責文件

常用專責文件：

- `docs/SUPPORT_MATRIX.md`
- `docs/UI_BACKENDS.md`
- `docs/RUNTIME_ASSETS.md`
- `docs/LIBMPV_C_API_TEST_MATRIX.md`
- `docs/HIGH_LEVEL_API.md`
- `docs/ENGINEERING_STANDARDS.md`
- `docs/AI_AGENT_INTEGRATION.md`

## 必守規則

- 目前產品支援範圍為 Windows x64 與 Windows ARM64。ARM64 程式碼路徑與執行階段資產對應已就緒，實體機器驗證狀態以 `docs/SUPPORT_MATRIX.md` 為準。
- 核心 libmpv 包裝需維持 stable v0.41.0 公開 C API 覆蓋。
- WinForms、WPF、WinUI 3 與 MAUI Windows UI 控制項使用 HWND 後端。
- Avalonia 使用 OpenGL render API 後端。
- 控制項建構函式不得下載執行階段資產。
- libmpv 更新需在已載入時暫存並提示重新啟動，不得實作處理序內 Hot Reload。
- 執行階段輔助工具可同層管理 `libmpv-2.dll`、`yt-dlp.exe`、`deno.exe`、`ffmpeg.exe` 與 `ffprobe.exe`。
- yt-dlp 格式控制使用 `MpvYtdlpFormatPreset`、`MpvYtdlpFormatSelector` 或自訂選擇器。
- 新使用端程式碼建議入口為 `MpvAppBuilder.BuildAsync` 搭配 `await using MpvPlayer`、`MpvMediaItem`、`WatchProperty<T>`；舊式 `new MpvPlayer(options) + Initialize()` 仍保留但不再是首選範例。
- 執行階段資產更新走 `MpvLibraryUpdateScheduler.StageAsync` + `ApplyStagedOnStartup`；不要直接覆蓋已載入處理序的 `libmpv-2.dll`。
- 執行階段啟動前的健檢呼叫 `MpvRuntimeHealthCheck.AnalyzeAsync`；散發授權判定呼叫 `MpvLicenseAuditor.AnalyzeAsync`。
- C# XML 註解只能使用正體中文臺灣地區用語，且不得共用註解；具有內容的 XML 文件註解標籤不得使用一行式排版，必須將開始標籤、內容與結束標籤分行。
- 語言、排版、C# 型別宣告、提交與任務授權等共通規則以 `AGENTS.md` 與 `docs/ENGINEERING_STANDARDS.md` 為準，本技能不重複定義。

## 驗證

依 `AGENTS.md` 的範圍判準與驗證清單執行。使用 `rg` 搜尋程式碼；變更 API 行為、支援範圍或平台宣告時，必須同步更新文件。
發佈品質相關任務以 `tools/Invoke-PreReleaseValidation.ps1` 為主流程；需要完整 Windows 發行檢查閘門時使用 `-IncludeWindowsReleaseGate`。

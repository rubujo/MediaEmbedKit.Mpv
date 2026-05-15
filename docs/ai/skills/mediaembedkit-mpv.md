# 技能：MediaEmbedKit.Mpv

本 skill 用於編修 MediaEmbedKit.Mpv 的核心 API、UI 後端、runtime helper、範例、測試與文件。

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

- 目前產品支援範圍為 Windows x64 與 Windows ARM64。ARM64 程式碼路徑與 runtime 資產 mapping 已就緒，物理機驗證狀態以 `docs/SUPPORT_MATRIX.md` 為準。
- 核心 libmpv 包裝需維持 stable v0.41.0 公開 C API 覆蓋。
- WinForms、WPF、WinUI 3 與 MAUI Windows UI 控制項使用 HWND 後端。
- Avalonia 使用 OpenGL render API 後端。
- 控制項建構函式不得下載 runtime asset。
- libmpv 更新需在已載入時暫存並提示重新啟動，不得實作處理序內 hot reload。
- runtime helper 可同層管理 `libmpv-2.dll`、`yt-dlp.exe`、`deno.exe`、`ffmpeg.exe` 與 `ffprobe.exe`。
- yt-dlp 格式控制使用 `MpvYtdlpFormatPreset`、`MpvYtdlpFormatSelector` 或自訂 selector。
- 新 consumer 程式碼建議入口為 `MpvAppBuilder.BuildAsync` 搭配 `await using MpvPlayer`、`MpvMediaItem`、`WatchProperty<T>`；舊式 `new MpvPlayer(options) + Initialize()` 仍保留但不再是首選範例。
- runtime asset 更新走 `MpvLibraryUpdateScheduler.StageAsync` + `ApplyStagedOnStartup`；不要直接覆蓋已載入處理序的 `libmpv-2.dll`。
- runtime 啟動前的健檢呼叫 `MpvRuntimeHealthCheck.AnalyzeAsync`；散發授權判定呼叫 `MpvLicenseAuditor.AnalyzeAsync`。
- C# XML 註解只能使用正體中文，且不得共用註解。
- 區域變數、`using` 陳述式與 `foreach` 迴圈變數使用明確型別；只有必要時才使用 `var`。
- Markdown 文件使用正式、精煉的正體中文。
- 提交訊息遵循慣例式提交，必須包含主旨與正文。

## 驗證

```powershell
dotnet format .\MediaEmbedKit.Mpv.slnx --no-restore
dotnet run --project .\tests\MediaEmbedKit.Mpv.Tests\MediaEmbedKit.Mpv.Tests.csproj
dotnet run --project .\tests\MediaEmbedKit.Mpv.IntegrationTests\MediaEmbedKit.Mpv.IntegrationTests.csproj
dotnet build .\MediaEmbedKit.Mpv.slnx
```

使用 `rg` 搜尋程式碼。變更 API 行為、支援範圍或平台宣告時，必須同步更新文件。
發佈品質相關任務以 `tools/Invoke-PreReleaseValidation.ps1` 為主流程；需要完整 Windows release gate 時使用 `-IncludeWindowsReleaseGate`。

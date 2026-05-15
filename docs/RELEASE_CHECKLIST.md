# 發佈前檢查清單

本文件定義 Windows 發佈前可在本機完成的檢查項目（x64 / ARM64 共用同一驗證鏈，runtime helper 依目前處理序架構自動選擇對應資產）。現階段發佈品質以本機驗證鏈為準，不建立 CI 工作流程。

## 必要檢查

執行完整本機驗證：

```powershell
.\tools\Invoke-PreReleaseValidation.ps1
```

此腳本是唯一主流程，預設使用 Release configuration，並依序執行還原、專案規範檢查、格式檢查、核心測試、整合測試、方案建置、NuGet 套件內容驗證與乾淨 consumer 專案驗證。

完整 Windows release gate 會額外執行第一階段壓力測試、Console minimal 播放驗證、GUI consumer 實際播放驗證與 GUI 播放壓力測試：

```powershell
.\tools\Invoke-PreReleaseValidation.ps1 -IncludeWindowsReleaseGate -GuiPlaybackSeconds 20
```

預估資源（Windows，純機器跑、開發人員無互動）：

| 場景 | 大約耗時 | 額外磁碟需求 | 備註 |
| --- | --- | --- | --- |
| 不帶任何旗標的最小驗證 | 約 5–10 分鐘 | < 1 GB | 還原、專案規範檢查、格式、核心測試、整合測試、方案建置、套件驗證、consumer 驗證。 |
| `-IncludeStressTests` | 加約 5–10 分鐘 | 與上同 | 第一階段壓力測試包含播放器重複建立與釋放。 |
| `-IncludeConsoleMinimalPlaybackValidation` | 加約 1–2 分鐘 | 與上同 | 需 Windows runtime；單次 console minimal 播放。 |
| `-IncludeGuiConsumerPlaybackValidation` | 加約 10–15 分鐘 | 加約 2–3 GB | 為 5 個 GUI sample 各建一份臨時 consumer 專案、本機 NuGet 套件、實際開視窗播放。 |
| `-IncludeGuiPlaybackStress` | 加約 10–15 分鐘 / iteration | 與上同 | 重複啟動 5 個 GUI sample。可調整 `-GuiPlaybackIterations`。 |
| `-IncludeWindowsReleaseGate` | 約 35–60 分鐘 | 約 3–5 GB | 上述全部開啟，建議在 release 前一次跑完。 |

實際耗時受 SSD 與網路（首次下載 mpv / yt-dlp / Deno / FFmpeg）影響。GUI consumer 與 GUI 壓力測試建議以 `-RuntimeDirectory .\.tmp\gui-playback-runtime` 重用 runtime 資料夾，避免重複下載。

預覽要執行的步驟而不真正執行：

```powershell
.\tools\Invoke-PreReleaseValidation.ps1 -IncludeWindowsReleaseGate -DryRun
```

若目前環境沒有可用的 Windows `libmpv-2.dll`，可先略過整合測試：

```powershell
.\tools\Invoke-PreReleaseValidation.ps1 -SkipIntegrationTests
```

若要一併執行第一階段壓力測試：

```powershell
.\tools\Invoke-PreReleaseValidation.ps1 -IncludeStressTests
```

若要一併執行 GUI consumer 實際播放驗證與 GUI 播放壓力測試：

```powershell
.\tools\Invoke-PreReleaseValidation.ps1 -IncludeGuiConsumerPlaybackValidation -IncludeGuiPlaybackStress -GuiPlaybackSeconds 20
```

若要讓 Console minimal、GUI consumer 與 GUI 壓力測試共用既有 runtime，可指定工作區內的 runtime 資料夾：

```powershell
.\tools\Invoke-PreReleaseValidation.ps1 -IncludeWindowsReleaseGate -RuntimeDirectory .\.tmp\gui-playback-runtime
```

## NuGet 套件檢查

單獨產生並驗證 NuGet 套件：

```powershell
.\tools\Invoke-PackageValidation.ps1
```

驗證項目包含：

- 每個可發佈專案都產生唯一 `.nupkg`。
- 套件包含 `README.md` 與 `THIRD_PARTY_NOTICES.md`。
- 套件包含受控組件。
- 套件不包含 `libmpv-2.dll`、`yt-dlp.exe`、`deno.exe`、`ffmpeg.exe`、`ffprobe.exe` 或 FFmpeg-Builds 壓縮檔。

## 乾淨 consumer 檢查

單獨建立乾淨 consumer 專案並驗證本機 NuGet 套件：

```powershell
.\tools\Invoke-ConsumerPackageValidation.ps1
```

驗證項目包含：

- `MediaEmbedKit.Mpv` console consumer。
- `MediaEmbedKit.Mpv.WinForms` WinForms consumer。
- `MediaEmbedKit.Mpv.Wpf` WPF consumer。
- `MediaEmbedKit.Mpv.Avalonia` Avalonia consumer。
- `MediaEmbedKit.Mpv.WinUI` WinUI 3 consumer。
- `MediaEmbedKit.Mpv.Maui` MAUI Windows consumer。

## GUI consumer 播放檢查

單獨建立乾淨 GUI consumer sample，並以本機 NuGet 套件實際播放到指定秒數：

```powershell
.\tools\Invoke-GuiConsumerPlaybackValidation.ps1 -Seconds 20
```

此腳本會將範例專案複製到臨時工作資料夾，移除 `ProjectReference`，改用本機 `.nupkg`，再啟動 WinForms、WPF、Avalonia、WinUI 3 與 MAUI Windows sample 進行實際播放驗證。

## 第一階段壓力測試

單獨執行第一階段自動化壓力測試：

```powershell
dotnet run --project .\tests\MediaEmbedKit.Mpv.StressTests\MediaEmbedKit.Mpv.StressTests.csproj
```

測試範圍包含播放器重複建立與釋放、多 client 生命週期、自訂 stream callback 重複播放、stream 取消、外部工具大量輸出與逾時，以及 runtime helper 失敗路徑。

## 長時間 GUI 播放壓力測試

單獨執行長時間 GUI 播放壓力測試：

```powershell
.\tools\Invoke-GuiPlaybackStress.ps1 -Seconds 120 -Iterations 2
```

此腳本會重複啟動支援的 GUI sample，要求每次播放到指定秒數後關閉，用於觀察開關視窗、初始化、播放與釋放流程的穩定性。

GUI 播放相關腳本會在啟動視窗前先準備共用 runtime 資料夾，避免下載失敗以 sample 視窗訊息框中斷自動化流程。

## 發佈前人工確認

- 確認 package version 與 release notes。
- 確認第三方授權與散發義務。
- 依 `docs/DESIGN_TIME_CHECKLIST.md` 確認 WinForms、WPF、WinUI 3、MAUI Windows 與 Avalonia 設計階段行為。
- 確認 NuGet 套件不包含 `libmpv-2.dll`、`yt-dlp.exe`、`deno.exe`、`ffmpeg.exe`、`ffprobe.exe` 或 FFmpeg-Builds 壓縮檔。

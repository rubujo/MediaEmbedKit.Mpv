# 發佈前檢查清單

本文件定義發佈前可在本機完成的檢查項目。CI 工作流程需等待平台與發佈策略確認後再建立。

## 必要檢查

執行完整本機驗證：

```powershell
.\tools\Invoke-PreReleaseValidation.ps1
```

此腳本會依序執行還原、格式檢查、核心測試、整合測試、方案建置、NuGet 套件內容驗證與乾淨 consumer 專案驗證。

若目前環境沒有可用的 Windows x64 `libmpv-2.dll`，可先略過整合測試：

```powershell
.\tools\Invoke-PreReleaseValidation.ps1 -SkipIntegrationTests
```

若要一併執行第一階段壓力測試：

```powershell
.\tools\Invoke-PreReleaseValidation.ps1 -IncludeStressTests
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
- 套件不包含 `libmpv-2.dll`、`yt-dlp.exe` 或 `deno.exe`。

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

## 第一階段壓力測試

單獨執行第一階段自動化壓力測試：

```powershell
dotnet run --project .\tests\MediaEmbedKit.Mpv.StressTests\MediaEmbedKit.Mpv.StressTests.csproj
```

測試範圍包含播放器重複建立與釋放、多 client 生命週期、自訂 stream callback 重複播放、stream 取消、外部工具大量輸出與逾時，以及 runtime helper 失敗路徑。

## 發佈前人工確認

- 確認 package version 與 release notes。
- 確認第三方授權與散發義務。
- 以實際 GUI 執行乾淨 consumer 專案，驗證基本播放。
- 在 Visual Studio 設計工具中確認 WinForms、WPF、WinUI 3 與 MAUI Windows 設計階段行為。
- 若要啟用 CI，先確認支援平台、runner 映像、runtime asset 取得方式與 secrets 管理策略。

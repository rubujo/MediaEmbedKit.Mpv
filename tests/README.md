# 測試

本資料夾包含核心 API 測試、原生整合測試、範例播放冒煙測試、第一階段壓力測試與 5 套 UI 框架的控制項層無頭測試。

## 核心 API 測試

```powershell
dotnet run --project .\tests\MediaEmbedKit.Mpv.Tests\MediaEmbedKit.Mpv.Tests.csproj
```

此測試不初始化 libmpv，也不需要原生執行階段。

## 原生整合測試

```powershell
dotnet run --project .\tests\MediaEmbedKit.Mpv.IntegrationTests\MediaEmbedKit.Mpv.IntegrationTests.csproj
```

此測試需要 Windows `libmpv-2.dll`，涵蓋初始化、屬性、錯誤路徑、本機播放事件、stream callback 與 FFmpeg-Builds 下載驗證。

## 播放冒煙測試

```powershell
dotnet run --project .\tests\MediaEmbedKit.Mpv.PlaybackSmoke\MediaEmbedKit.Mpv.PlaybackSmoke.csproj -- --seconds 20
```

此測試會啟動範例應用程式，並等待影片播放到指定秒數後關閉。

## UI 控制項無頭測試

```powershell
dotnet run --project .\tests\MediaEmbedKit.Mpv.WinForms.HeadlessTests\MediaEmbedKit.Mpv.WinForms.HeadlessTests.csproj
dotnet run --project .\tests\MediaEmbedKit.Mpv.Wpf.HeadlessTests\MediaEmbedKit.Mpv.Wpf.HeadlessTests.csproj
dotnet run --project .\tests\MediaEmbedKit.Mpv.Avalonia.HeadlessTests\MediaEmbedKit.Mpv.Avalonia.HeadlessTests.csproj
dotnet run --project .\tests\MediaEmbedKit.Mpv.WinUI.HeadlessTests\MediaEmbedKit.Mpv.WinUI.HeadlessTests.csproj
dotnet run --project .\tests\MediaEmbedKit.Mpv.Maui.HeadlessTests\MediaEmbedKit.Mpv.Maui.HeadlessTests.csproj
```

每套框架各自驗證控制項層的 DependencyProperty / BindableProperty / INPC 屬性 / 5 個命令 / CanExecute / Dispose 重入。**不**啟動真實 libmpv、不註冊 handler，純粹覆蓋屬性系統與 CLR 包裝層。WinUI / MAUI 以 WinUI / MauiWinUIApplication host 啟動，在 UI 執行緒跑完後立即結束處理序。`tools/Invoke-PreReleaseValidation.ps1` 會自動依序執行這 5 個無頭測試。

## 第一階段壓力測試

```powershell
dotnet run --project .\tests\MediaEmbedKit.Mpv.StressTests\MediaEmbedKit.Mpv.StressTests.csproj
```

此測試涵蓋播放器生命週期、多 client、stream callback、外部工具輸出與執行階段輔助工具失敗路徑。長時間 GUI 播放壓力測試由 `tools/Invoke-GuiPlaybackStress.ps1` 執行。

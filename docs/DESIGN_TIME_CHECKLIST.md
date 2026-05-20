# Windows 設計階段檢查清單

本文件定義 Windows UI 控制項在 Visual Studio 設計階段應符合的行為（x64 / ARM64 共用）。設計階段不得下載 runtime、不得初始化 libmpv，也不得建立實際播放工作。

## VS 2026 設計階段支援現況

Visual Studio 2022/2026 的可視 XAML Designer 僅支援 WPF 與 UWP；WinUI 3 與 .NET MAUI 沒有靜態 designer surface，只能透過 XAML Live Preview 或 XAML Hot Reload 在 執行階段觀察 UI。Avalonia 透過官方 VS 擴充套件提供 previewer。WinForms 設計工具同時涵蓋 .NET Framework 與 .NET 10。本檢查表涵蓋上述所有設計時情境（含 IntelliSense / XAML 編輯器載入），凡是會「載入控制項組件」的時機都適用，不限於拖放編輯。

## 通用準則

- 先執行 `dotnet build .\MediaEmbedKit.Mpv.slnx --configuration Release`，確認控制項組件可被設計工具載入。
- 設計工具載入控制項時，不得呼叫執行階段輔助工具。
- 設計工具載入控制項時，不得載入 `libmpv-2.dll`、啟動 yt-dlp、啟動 Deno 或啟動 FFmpeg。
- 設計工具應顯示替代預覽內容或維持可選取、可調整大小的控制項外框。
- 設計工具關閉或重新整理時，不應產生未處理例外。

## 控制項檢查

| 套件 | 控制項 | VS 設計階段支援 | 設計階段預期 |
| --- | --- | --- | --- |
| `MediaEmbedKit.Mpv.WinForms` | `MpvPlayerControl` | WinForms designer surface | 可加入表單並調整大小；只顯示替代預覽，不建立播放器。 |
| `MediaEmbedKit.Mpv.Wpf` | `MpvWpfPlayer` | WPF XAML Designer | 可加入 XAML 視覺樹並調整大小；不建立 `HwndHost` 播放器或 AirSpace popup。 |
| `MediaEmbedKit.Mpv.WinUI` | `MpvWinUiPlayer` | 無 designer surface（XAML Live Preview / Hot Reload only） | XAML 編輯器與 Hot Reload 載入時顯示替代內容；不建立 HWND 後端。 |
| `MediaEmbedKit.Mpv.Maui.Windows` | `MpvView` | 無 designer surface（XAML Live Preview / Hot Reload only） | XAML 編輯器與 Hot Reload 載入 handler 時不初始化 WinUI 播放後端。 |
| `MediaEmbedKit.Mpv.Avalonia` | `MpvAvaloniaPlayer` | Avalonia for Visual Studio previewer | 可顯示 Avalonia 預覽內容；不建立 libmpv render context。 |

## 人工確認流程

1. 開啟 Visual Studio 2026。
2. 建置 `MediaEmbedKit.Mpv.slnx`。
3. 在 designer surface 確認（WinForms / WPF / Avalonia）：
   - 開啟 `samples/WinFormsSample/MainForm.cs`，切到 designer，確認 `MpvPlayerControl` 可選取、可調整大小、無 runtime 行為。
   - 開啟 `samples/WpfSample/MainWindow.xaml`，切到 designer，確認 `MpvWpfPlayer` 顯示替代內容、沒有 AirSpace popup。
   - 開啟 `samples/AvaloniaSample/MainWindow.axaml`，於 Avalonia previewer 確認預覽渲染不觸發 OpenGL context。
4. 在 XAML Hot Reload / Live Preview 確認（WinUI 3 / MAUI）：
   - 啟動 `samples/WinUISample` 與 `samples/MauiSample`，於執行階段開啟 XAML Live Preview，修改 `MainWindow.xaml` / `MainPage.xaml` 觀察 Hot Reload 行為，確認沒有 執行階段重新初始化原生播放器。
5. 關閉設計工具與 Hot Reload session 後重新建置方案，確認沒有設計階段殘留檔案或鎖定組件造成建置失敗。

## 失敗處理

- 若設計工具嘗試載入 `libmpv-2.dll`，應檢查對應控制項的 design mode 判斷。
- 若 WinUI 或 MAUI 在 XAML 編輯器 / Hot Reload 載入時建立 HWND 後端，應檢查 handler 連線流程是否在設計模式中提前傳回。
- 若 WPF 覆蓋層在設計階段建立 popup，應檢查 AirSpace overlay 初始化是否只在執行階段發生。
- 若 Avalonia 預覽建立 OpenGL render context，應檢查 preview 設定與 `Design.IsDesignMode` 防線。

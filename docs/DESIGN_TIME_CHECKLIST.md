# Windows 設計階段檢查清單

本文件定義 Windows x64 UI 控制項在 Visual Studio 設計階段應符合的行為。設計階段不得下載 runtime、不得初始化 libmpv，也不得建立實際播放工作。

## 通用準則

- 先執行 `dotnet build .\MediaEmbedKit.Mpv.slnx --configuration Release`，確認控制項組件可被設計工具載入。
- 設計工具載入控制項時，不得呼叫 runtime helper。
- 設計工具載入控制項時，不得載入 `libmpv-2.dll`、啟動 yt-dlp、啟動 Deno 或啟動 FFmpeg。
- 設計工具應顯示替代預覽內容或維持可選取、可調整大小的控制項外框。
- 設計工具關閉或重新整理時，不應產生未處理例外。

## 控制項檢查

| 套件 | 控制項 | 設計階段預期 |
| --- | --- | --- |
| `MediaEmbedKit.Mpv.WinForms` | `MpvPlayerControl` | 可加入表單並調整大小；只顯示替代預覽，不建立播放器。 |
| `MediaEmbedKit.Mpv.Wpf` | `MpvWpfPlayer` | 可加入 XAML 視覺樹並調整大小；不建立 `HwndHost` 播放器或 AirSpace popup。 |
| `MediaEmbedKit.Mpv.WinUI` | `MpvWinUiPlayer` | 可在 XAML 設計階段顯示替代內容；不建立 HWND 後端。 |
| `MediaEmbedKit.Mpv.Maui` | `MpvView` | 可在 MAUI Windows 設計階段載入；handler 不初始化 WinUI 播放後端。 |
| `MediaEmbedKit.Mpv.Avalonia` | `MpvAvaloniaPlayer` | 可顯示 Avalonia 預覽內容；不建立 libmpv render context。 |

## 人工確認流程

1. 開啟 Visual Studio。
2. 建置 `MediaEmbedKit.Mpv.slnx`。
3. 依序開啟 WinForms、WPF、WinUI 3、MAUI Windows 與 Avalonia 範例專案。
4. 在設計工具中確認控制項可選取、可調整大小，且沒有 runtime 下載或原生播放器初始化。
5. 關閉設計工具後重新建置方案，確認沒有設計階段殘留檔案或鎖定組件造成建置失敗。

## 失敗處理

- 若設計工具嘗試載入 `libmpv-2.dll`，應檢查對應控制項的 design mode 判斷。
- 若 WinUI 或 MAUI 設計階段建立 HWND 後端，應檢查 handler 連線流程是否在設計模式中提前返回。
- 若 WPF 覆蓋層在設計階段建立 popup，應檢查 AirSpace overlay 初始化是否只在執行階段發生。
- 若 Avalonia 預覽建立 OpenGL render context，應檢查 preview 設定與 `Design.IsDesignMode` 防線。

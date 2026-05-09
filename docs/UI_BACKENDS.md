# UI 後端規範

本文件定義 UI 套件可宣稱支援的播放後端。核心 `MediaEmbedKit.Mpv` 保留 libmpv render API 包裝；UI 控制項支援範圍則以本文件列出的後端為準。

## 後端策略

WinForms、WPF、WinUI 3 與 .NET MAUI Windows 使用 HWND `wid` 後端。Avalonia 使用 Windows x64 OpenGL render API 後端。

## 後端差異

| 後端 | 定位 | 本專案狀態 |
| --- | --- | --- |
| HWND | 將 libmpv 輸出接到原生視窗控制代碼。 | WinForms、WPF、WinUI 3 與 .NET MAUI Windows 使用。 |
| OpenGL render API | 由 UI framework 提供 OpenGL surface，再交由 libmpv render API 繪製。 | Avalonia 使用。 |

## WinForms

`MpvPlayerControl` 使用 WinForms HWND 與 libmpv `wid`。設計階段只顯示替代預覽內容，不初始化 libmpv。

## WPF

`MpvWpfPlayer` 使用 `HwndHost` 與 libmpv `wid`。控制項內建 `OverlayContent`，由控制項管理 AirSpace 覆蓋層；使用者不需自行建立 Popup。

設計階段透過 WPF design mode 偵測避免初始化 libmpv，並顯示替代預覽內容。

## Avalonia

`MpvAvaloniaPlayer` 使用 Avalonia `OpenGlControlBase` 與 libmpv OpenGL render API。它不是 HWND `wid` 嵌入後端。

## WinUI 3

`MpvWinUiPlayer` 使用 `MpvWinUiHwndPlayer` 的 Windows HWND 後端。控制項內建 `OverlayContent`，並透過 XAML Island 覆蓋層處理 AirSpace。

設計階段只顯示替代 XAML 預覽內容，不建立 HWND 或 libmpv player。

## .NET MAUI Windows

`MpvViewHandler` 在 Windows 對應到 WinUI 3 `MpvWinUiPlayer`，因此使用相同 HWND 後端。設計階段不初始化播放後端。

# UI 後端規範

本文件定義 UI 套件可宣稱支援的播放後端。核心 `MediaEmbedKit.Mpv` 仍保留 libmpv render API 包裝；該包裝不代表所有 UI 框架皆提供對應控制項後端。

## 後端策略

WinForms、WPF、WinUI 3 與 .NET MAUI Windows 只保留 HWND `wid` 後端。此路線在 Windows x64 範圍內最直接，且維護成本最低。

不提供下列 UI 控制項後端：

- WPF `D3DImage`。
- WinUI 3 或 MAUI Windows `SwapChainPanel`。
- software render fallback。
- 其他 GPU composition 候選後端。

Avalonia 目前保留 Windows x64 OpenGL render API 預覽，不列入 HWND-only 完成範圍。

## 後端差異

| 後端 | 定位 | 本專案狀態 |
| --- | --- | --- |
| HWND | 將 libmpv 輸出接到原生視窗控制代碼。 | UI 主線後端。 |
| software render | 將影格寫入 CPU 記憶體後由 UI framework 繪製。 | 僅保留核心 C API 包裝。 |
| GPU composition | 透過圖形互通放入 framework GPU surface。 | 不列入 UI 控制項支援範圍。 |

## WinForms

`MpvPlayerControl` 使用 WinForms HWND 與 libmpv `wid`。設計階段只顯示替代預覽內容，不初始化 libmpv。

## WPF

`MpvWpfPlayer` 使用 `HwndHost` 與 libmpv `wid`。控制項內建 `OverlayContent`，由控制項管理 AirSpace 覆蓋層；使用者不需自行建立 Popup。

設計階段透過 WPF design mode 偵測避免初始化 libmpv，並顯示替代預覽內容。

## Avalonia

`MpvAvaloniaPlayer` 目前為 Windows x64 預覽控制項，使用 Avalonia `OpenGlControlBase` 與 libmpv OpenGL render API。它不是 HWND `wid` 嵌入後端。

若未來正式支援 Avalonia HWND，需另行完成 Windows 原生控制項生命週期、大小同步、Z 順序與覆蓋層策略。

## WinUI 3

`MpvWinUiPlayer` 使用 `MpvWinUiHwndPlayer` 的 Windows HWND 後端。控制項內建 `OverlayContent`，並透過 XAML Island 覆蓋層處理 AirSpace。

設計階段只顯示替代 XAML 預覽內容，不建立 HWND 或 libmpv player。

## .NET MAUI Windows

`MpvViewHandler` 在 Windows 對應到 WinUI 3 `MpvWinUiPlayer`，因此使用相同 HWND 後端。設計階段不初始化播放後端。

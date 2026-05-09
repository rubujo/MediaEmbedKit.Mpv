# UI 後端規範

## 收斂原則

WinForms、WPF、WinUI 3 與 .NET MAUI Windows 主線控制項只保留 HWND 後端。libmpv 原生 `wid` 嵌入是 Windows x64 範圍內最直接、效能穩定且維護成本最低的路線；WPF、WinUI 3 與 .NET MAUI Windows 的控制項不得再公開 D3DImage、SwapChainPanel、software render fallback 或其他 GPU composition 候選後端。Avalonia 目前保留 OpenGL render API 預覽，不列入 HWND-only 完成範圍。

核心 `MediaEmbedKit.Mpv` 仍保留 OpenGL render API 與 software render API 包裝，因為它們屬於 libmpv C API 覆蓋範圍；但這些核心 API 不等同於 UI 控制項層的支援後端。

## 後端關係

HWND、software render 與 GPU composition 是三種不同定位：

- HWND：把 libmpv 影片輸出接到原生視窗控制代碼。這是目前 UI 控制項層唯一保留的路線。
- software render：使用 libmpv render API 將影格寫入 CPU 記憶體，再由 UI framework 畫出來。這會增加 CPU 複製、色彩轉換與縮放成本，目前不作為 UI 控制項後端。
- GPU composition：使用圖形互通把影格放進 framework 可組合的 GPU surface。WPF `D3DImage` 與 WinUI `SwapChainPanel` 需要額外 Direct3D/DXGI 或 ANGLE/GL-DX 互通層，目前不列入本專案 UI 控制項支援範圍。

## WinForms

目前後端：`MpvPlayerControl` 使用 WinForms HWND，透過 libmpv `wid` 選項播放。這是 WinForms 的高效能預設。

設計階段：控制項已標示可放入 Toolbox，並在 WinForms Designer 中避免建立 libmpv player，只顯示替代預覽文字。設計工具內可調整大小與一般 WinForms 版面屬性。

## WPF

目前後端：`MpvWpfPlayer` 使用 `HwndHost` 與 libmpv `wid`。控制項內建 `OverlayContent`，由控制項自行建立並同步 `MpvAirspacePopup`，使用者不需手動管理 Popup。

設計階段：控制項已使用 WPF `DesignerProperties.GetIsInDesignMode(...)` 偵測 XAML Designer。設計階段只建立替代 STATIC 子視窗，不初始化 libmpv，也不要求 `libmpv-2.dll` 存在。

不支援項目：WPF UI 套件不提供 `D3DImage` 後端，不提供 software render 後端，也不保留相關公開 API。

## Avalonia

目前狀態：Avalonia 套件與範例保留 Windows x64 OpenGL render API 預覽，但不列入「HWND-only」完成範圍。現有 `MpvAvaloniaPlayer` 繼承 `OpenGlControlBase`，由 Avalonia 管理 OpenGL 內容，並透過 libmpv render API 將影格轉譯到 Avalonia 提供的 framebuffer。

HWND 關係：Avalonia 在 Windows 可透過 `NativeControlHost` 承載 HWND 原生控制項，但原生控制項會落在 Avalonia 轉譯表面之外，並有透明、轉換、Z 順序與裁切限制。因此本專案目前不把 Avalonia 預覽改成 HWND 主線；若未來要正式化，需另行設計 Windows 專屬 HWND 主控生命週期、大小同步與覆蓋層策略。

設計階段：依 Avalonia 官方 `Design` helper 設計，使用 `Design.IsDesignMode` 避免在 previewer 中建立 libmpv player 或 render context，並用 `Design.SetPreviewWith(...)` 提供替代預覽控制項。

## WinUI 3

目前後端：`MpvWinUiPlayer` 只使用 `MpvWinUiHwndPlayer` 的 libmpv `wid` HWND 後端。控制項會嘗試取得父視窗控制代碼、建立播放用子 HWND，並將播放器指向該子視窗。

HWND AirSpace 處理：`MpvWinUiHwndPlayer` 內建 `OverlayContent` 與 `IsOverlayOpen`，並使用 `DesktopWindowXamlSource` 建立同父視窗的 XAML Island 覆蓋層 HWND，再由控制項同步位置與 Z 順序。使用者不需要自行建立覆蓋視窗來處理影片上方 UI。

設計階段：WinUI 控制項使用 `Windows.ApplicationModel.DesignMode` 偵測設計階段，並只顯示替代 XAML 預覽內容，不建立 HWND 或 libmpv player。

不支援項目：WinUI UI 套件不提供 `SwapChainPanel` 後端，不提供 software render fallback，也不保留後端選擇列舉。

## .NET MAUI Windows

目前後端：`MpvViewHandler` 在 Windows 對應到 `MpvWinUiPlayer`，因此 MAUI Windows 使用 WinUI 3 HWND 後端。

設計階段：`MpvView` 使用 `Microsoft.Maui.Controls.DesignMode.IsDesignModeEnabled` 避免在 MAUI 設計階段載入媒體或初始化平台播放後端。Windows handler 也會在設計階段跳過 libmpv 初始化，僅保留平台替代預覽。

不支援項目：MAUI Windows 不公開後端選擇屬性，也不提供 software render 或 GPU composition 後端。

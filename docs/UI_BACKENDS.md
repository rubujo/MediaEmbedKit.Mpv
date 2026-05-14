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

OpenGL render API 是目前 Avalonia 後端的實作細節，未額外暴露獨立類型；應用程式統一使用 `MpvAvaloniaPlayer`。

## WinUI 3

`MpvWinUiPlayer` 使用 `MpvWinUiHwndPlayer` 的 Windows HWND 後端。控制項內建 `OverlayContent`，並透過 XAML Island 覆蓋層處理 AirSpace。

設計階段只顯示替代 XAML 預覽內容，不建立 HWND 或 libmpv player。

## .NET MAUI Windows

`MpvViewHandler` 在 Windows 對應到 WinUI 3 `MpvWinUiPlayer`，因此使用相同 HWND 後端。設計階段不初始化播放後端。

MAUI 應用程式優先使用 `MpvView.OverlayView` 提供 MAUI `View` 覆蓋層。`MpvView.OverlayContent` 保留為 Windows 原生 WinUI escape hatch；兩者同時設定時，`OverlayContent` 優先。

## 已知限制與替代方案

本專案的範圍是「Windows x64 上 libmpv 的 5 框架 .NET 封裝」。當前 HWND `wid` 與 Avalonia OpenGL render API 後端對 95% 桌面播放場景已足夠，不在 roadmap 上的進一步重寫如下，請使用者依需求自行評估替代方案。

### WPF：AirSpace 與視覺效果限制

`MpvWpfPlayer` 採 `HwndHost` 與 libmpv `wid` 嵌入，這是 WPF 上 libmpv 的事實標準做法（[libVLCSharp](https://github.com/videolan/libvlcsharp)、[CefSharp.Wpf.HwndHost](https://github.com/cefsharp/CefSharp.Wpf.HwndHost) 同樣採用）。HwndHost 路徑天生帶有 [AirSpace 限制](https://dwayneneed.github.io/wpf/2013/02/26/mitigating-airspace-issues-in-wpf-applications.html)：

- `Opacity` / `Effect` / `RenderTransform` 對視訊區域無效
- `RenderTargetBitmap.Render(playerControl)` 取不到視訊畫面
- WPF 元素（除了內建 `OverlayContent` 的 Popup 機制）難以覆蓋於視訊上

理論上可走 libmpv OpenGL render API → `WGL_NV_DX_interop2` 共享紋理 → `D3DImage` / `D3D11Image` 完全消除 AirSpace，但：

- 官方 [`Microsoft.Wpf.Interop.DirectX-x64`](https://www.nuget.org/packages/Microsoft.Wpf.Interop.DirectX-x64) 仍停在 `0.9.0-beta-22856` 且 [`microsoft/WPFDXInterop`](https://github.com/microsoft/WPFDXInterop) 已不維護
- [`dotnet/wpf#2062`（Add D3D11Image to WPF）](https://github.com/dotnet/wpf/issues/2062) 在 .NET 10 仍 open
- 必須自家寫 D3D11 wrapper（建議用 [Vortice](https://github.com/amerkoleci/Vortice.Windows) 或 [Silk.NET](https://github.com/dotnet/Silk.NET)）並做 GPU vendor capability probe + HwndHost fallback

若您的應用程式真的需要影片之上的 WPF transform / opacity / 截圖等能力，請評估：

- [SuRGeoNix/Flyleaf](https://github.com/SuRGeoNix/Flyleaf)（FFmpeg + Vortice D3D11 + 自家 swap chain，WPF / WinForms / WinUI 3 皆支援）
- 自行 fork 本專案並加上 D3D11Image 路徑

### WinUI 3：XAML Island margin transform 延遲

`MpvWinUiPlayer` 用 `DesktopWindowXamlSource` XAML Island 嵌入 HWND。在動態 layout / resize 時，HWND 位置追蹤 XAML 元素 layout 會有半幀延遲。

理論上可走 libmpv → DXGI shared texture → `SwapChainPanel`（[asklar/WinAppSDK-MediaPlayer](https://github.com/asklar/WinAppSDK-MediaPlayer) 與 [Richasy/mpv-winui](https://github.com/Richasy/mpv-winui) 都示範了此路徑）。但要注意 libmpv 本身未在 render.h 公開 D3D11 backend（[mpv#5979](https://github.com/mpv-player/mpv/issues/5979) 自 2018 開到 2026 仍 open），仍要靠 OpenGL → DXGI shared texture 走完整路徑。

若應用程式對影片動畫 / 變形 / 同層 XAML 合成有強烈需求，請評估：

- [SuRGeoNix/Flyleaf](https://github.com/SuRGeoNix/Flyleaf)（已示範 SwapChainPanel + D3D11，活躍維護）

### Avalonia：OpenGL FBO readback 延遲

`MpvAvaloniaPlayer` 採 [`OpenGlControlBase`](https://docs.avaloniaui.net/api/avalonia/opengl/controls/openglcontrolbase) 與 libmpv OpenGL render API，這是 Avalonia 上 libmpv 的標準做法（[LibMpv-OpenGL](https://github.com/mysteryx93/LibMpv-OpenGL) 同樣採用）。FBO → Avalonia compositor 之間有一次 GPU 同步成本，在 4K / 120Hz 高刷新率場景下 frame pacing 不理想。

理論上可改走 [Avalonia 12 的 `ICompositionGpuInterop`](https://api-docs.avaloniaui.net/docs/T_Avalonia_Rendering_Composition_ICompositionGpuInterop)（已於 2026-04-07 [GA](https://avaloniaui.net/blog/avalonia-12/)）共享 D3D11 紋理，但需要處理已知問題如 [#19244 resize memory spike](https://github.com/AvaloniaUI/Avalonia/issues/19244) 與 [#15758 DXGI mutex release](https://github.com/AvaloniaUI/Avalonia/issues/15758)；此外此路徑必須改採 [Silk.NET](https://github.com/dotnet/Silk.NET)（[Avalonia 官方 sample 已切換](https://github.com/AvaloniaUI/Avalonia/blob/master/samples/GpuInterop/D3DDemo/D3D11DemoControl.cs)）。

若需要 4K / HDR / 高刷新率影片，請評估：

- 直接使用 mpv 主程式或 [mpv.net](https://github.com/mpvnet-player/mpv.net) 作為獨立播放器
- [SuRGeoNix/Flyleaf](https://github.com/SuRGeoNix/Flyleaf) 走 D3D11

### 為何不做？

這些重寫項目皆屬於 render path 等級的工程（每項約 1–2 個 sprint，共用核心 D3D11 / DXGI / shared texture 程式碼，加總約 2–3 個月）。在本專案範圍內，當前做法已能滿足絕大多數桌面播放使用情境，且已有活躍維護的對標方案；繼續投入此等工程而沒有具體用戶需求，不符合 KISS 原則。若您的應用情境確實需要其中之一，歡迎開 issue 描述具體場景，再評估個案。

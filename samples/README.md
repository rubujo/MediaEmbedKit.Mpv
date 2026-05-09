# 範例總覽

此資料夾包含 MediaEmbedKit.Mpv 的 Windows x64 桌面範例。所有範例都會在啟動時呼叫共用 helper 下載或更新執行階段資產，並示範播放器初始化、事件訂閱、播放控制與事件橋接器釋放流程。

## 範例專案

- `WinFormsSample`：示範 `MpvPlayerControl` 的 HWND 嵌入、播放器生命週期與事件輸出。
- `WpfSample`：示範 `MpvWpfPlayer`、內建 `OverlayContent` AirSpace 覆蓋層與一般 WPF 覆蓋層差異。
- `AvaloniaSample`：示範 `MpvAvaloniaPlayer` 的 OpenGL render API 預覽方式、一般 Avalonia 覆蓋層與事件輸出。
- `WinUISample`：示範 WinUI 3 Windows HWND 控制項、內建 AirSpace 覆蓋層與一般 WinUI 覆蓋層差異。
- `MauiSample`：示範 MAUI Windows 預覽 handler、一般 MAUI 覆蓋層與事件輸出。

## 執行前準備

WinForms、WPF、Avalonia、WinUI 3 與 MAUI 範例會在啟動時呼叫共用 `SampleRuntime.InstallOrUpdateAsync()`，再由該 helper 委派到 `MpvRuntimeInstaller.InstallOrUpdateAsync(...)`。目前範例支援範圍收斂為 Windows x64，會自動建立輸出資料夾下的 `runtime` 目錄，並將 `libmpv-2.dll`、`yt-dlp.exe` 與 `deno.exe` 放在同一層。下載的二進位檔不應簽入儲存庫。

WinUI 3 與 MAUI Windows 範例採用 Windows App SDK self-contained 部署設定與 `win-x64` RID，讓範例輸出資料夾帶著 Windows App SDK 相依項目。這個設定只屬於範例 app 專案，不代表 class library 套件會強制使用同一種部署模式。

## 播放冒煙測試

可使用測試執行器依序啟動範例，並等待影片實際播放到指定秒數：

```powershell
dotnet run --project .\tests\MediaEmbedKit.Mpv.PlaybackSmoke\MediaEmbedKit.Mpv.PlaybackSmoke.csproj -- --seconds 20
```

若只測單一範例，可使用 `--sample WinForms`、`--sample WPF`、`--sample Avalonia`、`--sample WinUI` 或 `--sample MAUI`。

## 共用展示內容

- 啟動階段先執行 `SampleRuntime.InstallOrUpdateAsync()`，再建立實際播放視窗或頁面。
- 控制項建立播放器後會建立 `SamplePlayerEventBridge`，並在下方事件清單輸出 libmpv 原始事件、常用專用事件、記錄訊息、常用屬性變更與 mpv ytdl hook 的 JSON 子程序結果。
- 關閉視窗或頁面離開時會釋放 `SamplePlayerEventBridge`，示範取消事件訂閱與屬性觀察。
- 支援 AirSpace 覆蓋層的控制項會同時顯示控制項內建覆蓋層與一般框架覆蓋層，方便比較 HWND 視訊區域的疊放限制。
- 功能列提供 yt-dlp 格式預設值切換，範例預設使用最高 720p，重新載入媒體後會依選取格式交給 mpv 的 `ytdl-format`。
- 功能列示範常用高階 API：OSD 文字、相對跳轉、音量、靜音、播放速度、外部字幕、播放軌清單、截圖、載入 `mpv.conf`、載入 Lua 指令碼與 script message。
- 功能列也提供 yt-dlp 與 Deno 診斷，以及自我更新命令輸出。yt-dlp 與 Deno 診斷會透過程式庫的處理序執行器接收 stdout/stderr 事件，再寫入同一個事件清單，方便和 libmpv 記錄訊息對照。
- 共用 helper 會在 `runtime` 資料夾產生範例用 `mpv.conf`、Lua 指令碼、SRT 字幕與 `screenshots` 截圖資料夾，示範如何由 API 指定載入檔案。

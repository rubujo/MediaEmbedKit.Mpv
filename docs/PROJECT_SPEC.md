# MediaEmbedKit.Mpv 專案規範

本文件是專案規範入口。細節由專責文件維護；變更支援目標、平台狀態、runtime 政策、工程規則或 AI agent 指示時，必須同步更新對應文件。

| 主題 | 文件 |
| --- | --- |
| 目標框架與平台狀態 | `docs/SUPPORT_MATRIX.md` |
| UI 後端與 AirSpace 限制 | `docs/UI_BACKENDS.md` |
| libmpv、yt-dlp、Deno runtime 政策 | `docs/RUNTIME_ASSETS.md` |
| libmpv C API 覆蓋與測試矩陣 | `docs/LIBMPV_C_API_TEST_MATRIX.md` |
| 工程、文件、提交與驗證規則 | `docs/ENGINEERING_STANDARDS.md` |
| AI agent 與 Agent Skills 結構 | `docs/AI_AGENT_INTEGRATION.md` |
| 參考來源 | `docs/REFERENCE_SOURCES.md` |

## 目標

MediaEmbedKit.Mpv 提供 .NET libmpv 包裝器與 Windows 桌面 UI 控制項。核心套件需覆蓋 libmpv stable v0.41.0 公開 C API，並提供常用高階播放 API。UI 套件不得重複宣告核心 P/Invoke。

目前產品支援範圍為 Windows x64。未符合支援準則的平台、架構或 UI 後端不得列入支援矩陣。

## 設計原則

- 核心 API 盡量保持平台中立。
- 控制項建構函式與初始化流程不得自動下載第三方二進位檔。
- `libmpv-2.dll` 載入後不可在同一處理序 hot reload；更新必須暫存並提示重新啟動。
- Windows x64 runtime 資料夾可同層放置 `libmpv-2.dll`、`yt-dlp.exe`、`deno.exe`、mpv 設定檔與 scripts。
- WinForms、WPF、WinUI 3 與 MAUI Windows 主線控制項只保留 HWND 後端。
- Avalonia 目前保留 Windows x64 OpenGL render API 預覽，不列入 HWND-only 完成範圍。
- C# 區域變數、`using` 陳述式與 `foreach` 迴圈變數使用明確型別；只有必要時才使用 `var`。
- 提交訊息必須遵循慣例式提交 1.0.0，且同時包含主旨與正文。

## 支援準則

新增平台或處理器架構前，必須符合下列條件：

- 有可重複取得且可授權評估的 libmpv 原生執行階段來源。
- runtime helper 可下載、指向或驗證該原生程式庫。
- yt-dlp 與 Deno 也有相同平台與架構可用來源。
- UI 後端完成 surface、resize、生命週期、AirSpace 或組合限制處理。
- 範例能以本機檔案與 URL 播放通過冒煙測試。
- 文件、sample、solution 與 catalog 只列出已符合條件的目標。

## 目前狀態

- 核心 `MediaEmbedKit.Mpv` 支援 `netstandard2.0;net472;net48;net8.0;net10.0`。
- libmpv C API 包裝已對齊 stable v0.41.0，公開 P/Invoke 匯出函式 54/54。
- 已比對 shinchiro `20260421` 與 zhongfly `2026-05-08-e0eb42c303` provider git build header，未發現需新增 P/Invoke 的差異。
- 已提供命令、屬性、節點、事件、render API、stream callback 與常用高階播放 API。
- WinForms/WPF 使用 Windows HWND 後端。
- WinUI 3/MAUI Windows 使用 Windows HWND 預覽後端。
- Avalonia 使用 Windows x64 OpenGL render API 預覽後端。
- Windows x64 runtime helper 支援 libmpv、yt-dlp 與 Deno 下載、更新與同層配置。
- 範例播放冒煙測試涵蓋 WinForms、WPF、Avalonia、WinUI 3 與 MAUI Windows。

不得宣稱已完成範圍外平台 UI 後端、WPF `D3DImage`、WinUI 3/MAUI Windows `SwapChainPanel`、software render fallback 或其他非 HWND UI 後端。

libmpv C API wrapper 可宣稱已完成 stable v0.41.0 公開 C API 包裝覆蓋。若要宣稱所有實戰情境皆已驗證，必須依 `docs/LIBMPV_C_API_TEST_MATRIX.md` 完成事件、節點、render path 與錯誤情境驗證。

## 驗證

```powershell
dotnet format .\MediaEmbedKit.Mpv.slnx --no-restore
dotnet run --project .\tests\MediaEmbedKit.Mpv.Tests\MediaEmbedKit.Mpv.Tests.csproj
dotnet run --project .\tests\MediaEmbedKit.Mpv.IntegrationTests\MediaEmbedKit.Mpv.IntegrationTests.csproj
dotnet build .\MediaEmbedKit.Mpv.slnx
```

整合測試需要 Windows x64 `libmpv-2.dll`。URL 播放需要 `yt-dlp.exe` 可被 mpv 找到，或透過 `MpvPlayerOptions.YtdlpPath` 指定。

# MediaEmbedKit.Mpv 專案規範

## 文件定位

此檔案是專案規範入口，不放置所有細節。修改支援目標、平台狀態、原生執行階段政策、工程規則或 AI agent 指示時，請同步更新下列專責文件。

| 主題 | 權威文件 |
| --- | --- |
| 目標框架與平台狀態 | `docs/SUPPORT_MATRIX.md` |
| UI 後端、AirSpace 與高效能終局 | `docs/UI_BACKENDS.md` |
| libmpv、yt-dlp、Deno 執行階段下載與更新 | `docs/RUNTIME_ASSETS.md` |
| libmpv C API 包裝與測試矩陣 | `docs/LIBMPV_C_API_TEST_MATRIX.md` |
| 程式碼註解、文件、格式、提交、授權與驗證規則 | `docs/ENGINEERING_STANDARDS.md` |
| AI agent、AGENTS.md 與 Agent Skills 結構 | `docs/AI_AGENT_INTEGRATION.md` |
| 最新官方參考來源 | `docs/REFERENCE_SOURCES.md` |
| 跨工具 AI agent 指示 | `docs/ai/AGENT_GUIDE.md` |

## 專案目標

建立獨立的 .NET libmpv 包裝器與 UI 控制項套件，讓使用者可以在 Windows x64 的 WinForms、WPF、Avalonia、WinUI 3 與 .NET MAUI Windows 應用程式中裝載 mpv 播放能力。

核心專案必須完整包住 libmpv stable v0.41.0 時期公開 C API 的資料型別、命令、屬性、事件、節點與 render API。UI 專案不得繞過核心 API 直接重複宣告相同 P/Invoke。libmpv C API 的 100% 支援以 `client.h`、`render.h`、`render_gl.h` 與 `stream_cb.h` 的公開匯出函式、列舉值與資料結構為基準，並以 `docs/runtime/libmpv-git-builds.json` 追蹤 shinchiro 與 zhongfly provider git build 的最新對齊狀態。

## 設計原則

- 核心 API 盡量保持平台中立；目前產品支援範圍收斂為 Windows x64，平台差異放在 UI 後端或 runtime asset helper。
- 下載 helper 只能由使用者明確呼叫；控制項建構函式與初始化流程不得自動下載二進位檔。
- `libmpv-2.dll` 已載入後不可在處理序內 hot reload；更新需暫存並提示重新啟動。
- Windows x64 的 `yt-dlp`、Deno 與 libmpv runtime asset 應放在同一個執行階段資料夾，由使用者決定是否下載、更新或隨應用程式散發。
- runtime folder 可由使用者選擇作為 mpv 設定資料夾；啟用後會透過 `config-dir` 載入同層 `mpv.conf`、`input.conf`、`scripts` 等設定，Lua 與 JavaScript 腳本也可透過 `ScriptFiles` 或 `LoadScript(...)` 指定載入。
- WinForms、WPF、WinUI 3 與 MAUI Windows 主線控制項只保留 HWND 後端；WinUI 3/MAUI Windows 不提供 software render fallback，WPF/WinUI 3/MAUI Windows 不提供 D3DImage、SwapChainPanel 或其他 GPU composition 候選後端。Avalonia 目前保留 OpenGL render API 預覽，不列入 HWND-only 完成範圍。
- 未符合支援範圍收斂準則的目標不列入目前支援範圍；不得只因 UI 框架宣稱支援就標示為 libmpv 已支援。
- C# 程式碼應使用明確型別宣告區域變數、`using` 陳述式與 `foreach` 迴圈變數；只有匿名型別或編譯器要求時才使用 `var`。
- Git 提交訊息必須遵循慣例式提交 1.0.0，且必須同時包含主旨與正文，不得使用一行式提交訊息；提交描述、正文與一般頁腳內容必須使用正體中文與臺灣地區用語。詳細規則以 `docs/ENGINEERING_STANDARDS.md` 為準。

## 支援範圍收斂準則

目前只接受 Windows x64 作為產品支援平台。新增平台或處理器架構前，必須同時符合下列條件：

- 有可重複取得且可授權評估的 libmpv 原生執行階段來源。
- runtime helper 能下載或指向該來源，並能驗證原生程式庫存在。
- yt-dlp 與 Deno 也有相同平台與架構可用的官方或明確可信來源。
- UI 後端已完成平台 surface、resize、生命週期、AirSpace 或組合限制處理。
- 範例可在該平台以本機檔案與 URL 播放通過冒煙測試。
- 支援矩陣、runtime catalog、UI catalog、sample、solution 與 README 只列出已符合上述條件的平台、處理器架構與 UI 架構，不保留未支援目標的來源清單、範例資料夾或預告式支援宣告。

## 目前狀態

目前完成：

- 核心 `MediaEmbedKit.Mpv`：`netstandard2.0;net472;net48;net8.0;net10.0`。
- libmpv C API 包裝：已對齊 stable v0.41.0 官方標頭，P/Invoke 匯出函式 54/54，列舉值與 render 旗標完成比對；shinchiro `20260421` / mpv `5921fe5` 與 zhongfly `2026-05-08-e0eb42c303` / mpv `e0eb42c303` 的公開 header 已比對，未發現需要新增 P/Invoke 的差異。
- libmpv managed API：`mpv_node`、命令回傳、具名命令、命令清單探索、非同步命令、屬性、基本播放狀態、速度、音量、靜音、循環播放、屬性清單探索、profile 清單、解碼器清單、通訊協定清單、demuxer 清單、非同步屬性回覆、事件轉節點、typed 事件資料、播放清單、播放軌、章節、版本、媒體中繼資料、音訊裝置、音訊與視訊參數、demuxer 快取狀態、字幕與網路字幕掛載、外部音訊與視訊軌、OSD 文字與 ASS 覆疊、截圖、濾鏡、鍵鼠輸入、輸入綁定與 section、A-B 重複播放、指令碼訊息、外部處理序、mpv ytdl hook JSON 子程序結果、mpv 設定資料夾、指定設定檔、Lua 與 JavaScript 腳本載入、wakeup、hook、stream callback、OpenGL render API、software render API、ICC profile、環境光、skip rendering、DRM 建立參數與原始 render 參數入口。
- yt-dlp 播放格式控制：`MpvYtdlpFormatPreset` 常用畫質列舉、`MpvYtdlpFormatSelector` selector 轉換、`MpvPlayerOptions.YtdlpFormatPreset`、`MpvPlayerOptions.YtdlpFormat` 與執行階段切換方法。
- WinForms/WPF：Windows HWND 後端。
- Avalonia Windows：OpenGL render API 預覽套件保留，尚未列入 HWND-only 完成範圍。
- WinUI 3/MAUI Windows：Windows HWND 後端；WinUI HWND 路線內建 AirSpace 覆蓋層。
- design-time 支援：WinForms Designer、WPF XAML Designer、WinUI 3 XAML 載入、MAUI Windows handler 與 Avalonia previewer 皆避免初始化 libmpv，並顯示替代預覽內容或安全控制項。
- Windows x64 runtime helper：libmpv、yt-dlp 與 Deno 下載、更新、同層資料夾配置，以及 yt-dlp/Deno 處理序輸出事件執行器。
- 測試入口：`tests/MediaEmbedKit.Mpv.Tests` 提供不需原生執行階段的核心 API 測試，`tests/MediaEmbedKit.Mpv.IntegrationTests` 提供需要 `libmpv-2.dll` 的原生整合測試，`tests/MediaEmbedKit.Mpv.PlaybackSmoke` 提供啟動範例並等待實際播放的冒煙測試執行器。
- 播放冒煙測試：WinForms、WPF、Avalonia、WinUI 3 與 MAUI Windows 範例已以 YouTube 測試網址播放超過 20 秒並正常關閉。

目前不得宣稱完成：

- 目前範圍外的平台 UI 後端。
- WPF `D3DImage`、WinUI 3/MAUI Windows `SwapChainPanel`、software render fallback 或其他非 HWND UI 後端。

libmpv C API wrapper 可宣稱已完成 stable v0.41.0 公開 C API 的包裝覆蓋。若要宣稱「所有實戰情境皆已完全驗證」，仍必須依 `docs/LIBMPV_C_API_TEST_MATRIX.md` 完成事件、節點、render path 與錯誤情境的執行階段驗證。

## 驗證

程式碼、專案檔或共用 API 變更後，請執行：

```powershell
dotnet format .\MediaEmbedKit.Mpv.slnx --no-restore
dotnet run --project .\tests\MediaEmbedKit.Mpv.Tests\MediaEmbedKit.Mpv.Tests.csproj
dotnet run --project .\tests\MediaEmbedKit.Mpv.IntegrationTests\MediaEmbedKit.Mpv.IntegrationTests.csproj
dotnet build .\MediaEmbedKit.Mpv.slnx
```

執行階段播放驗證需要相符平台的 libmpv 原生程式庫。URL 播放也需要 `yt-dlp` 可被 mpv 找到，或透過 `MpvPlayerOptions.YtdlpPath` 設定。

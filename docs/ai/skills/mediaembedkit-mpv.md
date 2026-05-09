# 技能：MediaEmbedKit.Mpv

編修 libmpv 包裝器、UI 後端、下載 helper、範例、專案檔或專案規範文件時，請使用此共用 skill。

## 優先閱讀

- `docs/PROJECT_SPEC.md`：專案規範入口。
- `docs/SUPPORT_MATRIX.md`：目標框架、作業系統與 UI 狀態。
- `docs/UI_BACKENDS.md`：AirSpace、composition-friendly fallback 與高效能終局。
- `docs/RUNTIME_ASSETS.md`：libmpv、yt-dlp、Deno 下載與更新政策。
- `docs/ENGINEERING_STANDARDS.md`：註解、文件、格式、授權與驗證規則。
- `docs/AI_AGENT_INTEGRATION.md`：AGENTS.md、CLI 入口與 Agent Skills 結構。

## 必須保留

- 核心 API 在可行範圍內保持平台中立。
- Windows UI 程式碼保留在 `src/MediaEmbedKit.Mpv.WinForms` 與 `src/MediaEmbedKit.Mpv.Wpf`。
- WinUI 3 與 MAUI Windows 預覽套件需明確標示，並提供對應範例；Avalonia 預覽套件保留 OpenGL render API 範例，但不得宣稱已列入 HWND-only 完成範圍。
- 目前產品支援範圍收斂為 Windows x64；不要宣稱支援未符合支援範圍收斂準則的目標。
- WinForms、WPF、WinUI 3 與 MAUI Windows 的 UI 控制項層只保留 HWND `wid` 後端。
- Avalonia 預覽控制項目前使用 libmpv OpenGL render API，不屬於 HWND-only 主線。
- WPF 不提供 `D3DImage`、software render 或其他 GPU composition 控制項後端。
- WinUI 3 與 MAUI Windows 不提供 `SwapChainPanel`、software render fallback 或其他後端選擇 API。
- Windows 執行階段 helper 可以在同一個執行階段資料夾管理 `libmpv-2.dll`、`yt-dlp.exe` 與 `deno.exe`。
- Deno 不是所有播放情境都必需，但為完整 YouTube 支援應與 `yt-dlp.exe` 同層準備；yt-dlp 與 Deno 的直接診斷或進度輸出使用 `YtDlpProcessRunner` 與 `DenoProcessRunner`。
- mpv ytdl hook 的子程序輸出讀取 `MpvPlayer.GetYtdlJsonSubprocessResult()`；不得解析 `log-message` 來取得 yt-dlp 結構化結果。
- runtime folder 可選擇作為 mpv 設定資料夾與腳本來源；不得預設強制載入使用者未要求的設定或腳本。
- yt-dlp 畫質控制使用 `MpvYtdlpFormatPreset` 與 `MpvYtdlpFormatSelector`；常用情境使用列舉，進階情境保留自訂 selector 字串。
- libmpv 下載維持明確 helper 呼叫；控制項與建構函式永遠不執行下載。
- `MpvWpfPlayer` 是 `HwndHost`；影片上方 UI 必須透過控制項內建 `OverlayContent` 管理，不要求使用者自行建立 `MpvAirspacePopup`。
- `MpvWinUiPlayer` 僅使用 HWND 後端；`MpvWinUiHwndPlayer` 的影片上方 UI 必須透過控制項內建 `OverlayContent` 管理，不要求使用者自行建立 HWND 覆蓋視窗。
- WinForms、WPF、Avalonia、WinUI 3 與 MAUI Windows 控制項在設計階段或 previewer 中不得初始化 libmpv、建立播放用 HWND 或 render context；Avalonia 依官方 `Design.IsDesignMode` 與 `Design.PreviewWith` 處理。
- 核心 libmpv 包裝需維持 stable v0.41.0 公開 C API 100% 覆蓋；事件資料、render 參數與原生資料結構不可只停留在內部 P/Invoke。
- 追蹤 shinchiro 與 zhongfly 最新 provider git build 時，使用 `docs/ai/skills/libmpv-git-build-tracker.md`，並更新 `docs/runtime/libmpv-git-builds.json`。
- 核心便利 API 需維持以通用 command/property/node/event 入口為基礎；基本播放狀態、profile/解碼器/通訊協定/demuxer 清單、播放清單、播放軌、章節、版本、音訊裝置、外部字幕、網路字幕、OSD、截圖、濾鏡、輸入事件、指令碼訊息與外部處理序不得繞過 `MpvPlayer`。
- `MpvLibraryLoader.IsLoaded` 代表 libmpv 更新必須暫存，並要求重新啟動處理序。
- 保留 `MediaEmbedKit` 作為專案品牌，`mpv` 僅作描述用途。不要暗示本專案隸屬 mpv 官方。
- 下載器瀏覽器標頭集中在 `BrowserRequestHeaders`；包含 `User-Agent` 與 `sec-ch-ua` client hints。
- 透過 `DownloadUtility` 重複使用 `HttpClient`；不要每個下載要求都建立並處置新的 client。
- CC0-1.0 僅適用於受控原始碼。第三方原生執行階段二進位檔保留各自授權。
- libmpv 下載預設維持授權中立。當使用者明確需要 LGPL 或非 LGPL 行為時，使用 `MpvWindowsBuildDownloadOptions.LicensePreference`。
- 所有 C# 註解都必須使用 XML 文件註解，且只能使用正體中文。用語優先採用 Microsoft 在地化慣用詞，其次採用臺灣地區常見技術用語。
- 不得共用 XML 文件註解。避免 `<inheritdoc>`、`<include>` 與外部共用註解檔。
- 每個參數都要補 `<param>`，每個回傳值都要補 `<returns>`，屬性要補 `<value>`，泛型型別參數要補 `<typeparam>`。
- C# 區域變數、`using` 陳述式與 `foreach` 迴圈變數使用明確型別；只有匿名型別或編譯器要求時才使用 `var`。
- README 檔與專案規範文件需使用相同的正體中文用語風格。
- 建立提交時必須遵循慣例式提交 1.0.0。`type` 與可選的 `scope` 使用規範格式，且提交必須同時包含主旨與正文，不得使用一行式提交訊息。描述、正文與一般頁腳內容必須使用正體中文與臺灣地區用語。
- 未符合支援範圍收斂準則的目標目前不得標示為支援平台。
- 所有文字檔必須使用 CRLF。`.cs`、Visual Studio、MSBuild、XAML 與 manifest 相關檔案使用 UTF-8 BOM，其餘文字檔使用 UTF-8 無 BOM。

## 常用命令

```powershell
dotnet format .\MediaEmbedKit.Mpv.slnx --no-restore
dotnet run --project .\tests\MediaEmbedKit.Mpv.Tests\MediaEmbedKit.Mpv.Tests.csproj
dotnet build .\MediaEmbedKit.Mpv.slnx
```

使用 `rg` 搜尋程式碼。編修範圍需保持聚焦；API 行為、支援目標或平台宣告變更時，請同步更新文件。

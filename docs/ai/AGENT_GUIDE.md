# 共用 AI Agent 指南

這是 Codex、Claude Code、Gemini CLI 與 GitHub Copilot/Copilot CLI 共用的工作規則來源。工具專屬入口檔只負責轉接；完整結構請參閱 `docs/AI_AGENT_INTEGRATION.md`。

共用 skill 內容：`docs/ai/skills/mediaembedkit-mpv.md`

追蹤 shinchiro 與 zhongfly 最新 mpv git build、比對 libmpv header 或更新 provider 對齊紀錄時，使用 `docs/ai/skills/libmpv-git-build-tracker.md`。

## 專案範圍

此儲存庫會為 Windows 桌面應用程式建立受控 libmpv 控制項。

- 核心程式庫目標：`netstandard2.0;net472;net48;net8.0;net10.0`
- WinForms/WPF 目標：`net472;net48;net8.0-windows;net10.0-windows`
- Avalonia 目標：`net8.0-windows;net10.0-windows`
- MAUI 目標：`net10.0-windows10.0.19041.0`
- 除非使用者明確重新開啟支援政策，否則不要加入 `net40`、`net45`、`net6` 或 `net9`。
- 目前產品支援範圍收斂為 Windows x64。未符合支援範圍收斂準則的目標不列入目前支援矩陣。
- 修改支援目標或平台狀態前，請先閱讀 `docs/SUPPORT_MATRIX.md`、`docs/UI_BACKENDS.md` 與 `docs/RUNTIME_ASSETS.md`。

## 工程規則

- 核心 libmpv 包裝需維持 stable v0.41.0 公開 C API 100% 覆蓋；新增或調整時請同步檢查 `client.h`、`render.h`、`render_gl.h` 與 `stream_cb.h`，並用 `docs/runtime/libmpv-git-builds.json` 對齊最新 shinchiro 與 zhongfly provider git build。
- 使用 mpv 命令、屬性、選項、事件與節點提供廣泛功能涵蓋，不要為每個 mpv 選項建立強型別包裝。
- 常用 mpv 功能已透過核心便利 API 暴露：基本播放狀態、具名命令、命令清單、屬性清單、profile/解碼器/通訊協定/demuxer 清單、播放清單、播放軌、章節、版本、音訊裝置、外部字幕與網路字幕、OSD、截圖、濾鏡、輸入事件、指令碼訊息與外部處理序；新增功能時優先延伸這些通道。
- WinForms、WPF、WinUI 3 與 MAUI Windows 的 UI 控制項層只保留 libmpv `wid`/HWND 嵌入後端。
- WPF 不提供 `D3DImage`、software render 或其他 GPU composition 控制項後端。
- WinUI 3 與 MAUI Windows 不提供 `SwapChainPanel`、software render fallback 或其他後端選擇 API。
- 將 WPF `HwndHost`、Avalonia `NativeControlHost`、WinUI 子 HWND 主控與 MAUI Windows 原生主控視為 airspace 敏感區域。
- 控制項在設計階段或 previewer 中不得初始化 libmpv、下載 runtime、建立播放用 HWND 或 render context；需顯示替代預覽內容或直接跳過播放初始化。
- 保留核心 OpenGL render API 包裝；目前只允許在 Windows Avalonia 預覽後端與範例宣稱支援。
- 不要在原始碼中內含第三方 mpv、yt-dlp 或 Deno 二進位檔。
- 下載必須維持選擇性呼叫。控制項建構函式絕不可下載或更新原生二進位檔。
- runtime folder 設定檔與腳本載入必須是使用者明確選擇；使用 `CreatePlayerOptions(..., loadRuntimeConfiguration: true)` 或設定 `MpvPlayerOptions.ConfigDirectory`、`ConfigFiles`、`InputConfigFile`、`ScriptFiles`。
- yt-dlp 畫質控制以 `MpvYtdlpFormatPreset` 作為常用列舉入口，並保留自訂 format selector 字串。
- 已載入的 `libmpv-2.dll` 更新時必須要求重新啟動；不要實作處理序內 DLL hot reload。
- 不要宣稱支援未符合支援範圍收斂準則的目標，除非使用者明確重新擴張支援範圍並完成 native runtime、surface、生命週期與播放驗證。
- 除非使用者核准相依套件與目標架構影響，否則不要新增 NuGet 相依套件。
- NuGet 套件版本集中放在 `Directory.Packages.props`，且只使用最新穩定版本。
- 保留 `MediaEmbedKit` 作為專案品牌，`mpv` 僅作描述用途。不要暗示本專案隸屬 mpv 官方。
- 受控原始碼維持使用 `CC0-1.0`；不要將 CC0 套用到第三方執行階段二進位檔。
- Windows libmpv 下載預設維持授權中立；透過 `MpvWindowsBuildDownloadOptions.LicensePreference` 提供明確的使用者選擇。
- 所有 C# XML 文件註解只能使用正體中文。用語優先採用 Microsoft 在地化慣用詞，其次採用臺灣地區常見技術用語。
- 每個程式碼項目都要有自己的註解。不得使用 `<inheritdoc>`、`<include>` 或共用註解片段。
- C# XML 註解需在適用時包含 `<summary>`、`<param>`、`<returns>`、`<value>` 與 `<typeparam>`。
- C# 區域變數、`using` 陳述式與 `foreach` 迴圈變數使用明確型別；只有匿名型別或編譯器要求時才使用 `var`。
- 主要 README、範例 README 與專案規範文件需遵循相同的正體中文用語政策。
- 建立提交時必須遵循慣例式提交 1.0.0。`type` 與可選的 `scope` 使用規範格式，且提交必須同時包含主旨與正文，不得使用一行式提交訊息。描述、正文與一般頁腳內容必須使用正體中文與臺灣地區用語。
- 所有文字檔必須使用 CRLF。`.cs`、Visual Studio、MSBuild、XAML 與 manifest 相關檔案使用 UTF-8 BOM，其餘文字檔使用 UTF-8 無 BOM。

## 驗證

程式碼、專案或共用 API 變更後，請執行：

```powershell
dotnet format .\MediaEmbedKit.Mpv.slnx --no-restore
dotnet build .\MediaEmbedKit.Mpv.slnx --no-restore
```

執行階段播放驗證需要 `libmpv-2.dll`。URL 播放也需要 `PATH` 中有 `yt-dlp`，或透過 `MpvPlayerOptions.YtdlpPath` 設定。

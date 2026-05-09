# MediaEmbedKit.Mpv

MediaEmbedKit.Mpv 是獨立的 .NET libmpv 包裝器與桌面控制項專案。`MediaEmbedKit` 是專案品牌，`Mpv` 僅用來描述與 mpv/libmpv 的整合目標；本專案不是 mpv 官方專案。

## 專案內容

- `MediaEmbedKit.Mpv`：核心 libmpv P/Invoke 包裝、命令、具名命令、屬性、非同步屬性回覆、節點、typed 事件資料、命令/屬性/profile/解碼器/通訊協定/demuxer 清單探索、播放清單、播放軌、章節、版本、音訊裝置、字幕、OSD、截圖、濾鏡、輸入、外部處理序、stream callback、render API 進階參數、yt-dlp 選項、ytdl hook 結果讀取、yt-dlp/Deno 處理序執行器與執行階段下載 helper。
- `MediaEmbedKit.Mpv.WinForms`：使用 HWND 嵌入的 `MpvPlayerControl`；這是 WinForms 目前的高效能預設後端。
- `MediaEmbedKit.Mpv.Wpf`：使用 `HwndHost` 的 `MpvWpfPlayer`，並由控制項內建 `OverlayContent` AirSpace 覆蓋層。
- `MediaEmbedKit.Mpv.Avalonia`：Windows x64 Avalonia OpenGL render API 預覽套件；尚未列入 HWND-only 完成範圍。
- `MediaEmbedKit.Mpv.WinUI`：WinUI 3 Windows HWND 控制項。
- `MediaEmbedKit.Mpv.Maui`：透過 WinUI 3 HWND 控制項提供的 MAUI Windows 預覽 handler。

目前支援範圍先收斂為 Windows x64。核心程式庫已對齊 libmpv stable v0.41.0 公開 C API，並已比對 shinchiro `20260421` 與 zhongfly `2026-05-08-e0eb42c303` provider git build 的公開 header；目前不需要新增 P/Invoke，具名命令節點已改用最新 libmpv 建議的 `_name` 欄位。核心層包含 54/54 P/Invoke 匯出函式、官方列舉值、事件資料結構、OpenGL、software render API 與 stream callback 包裝。核心層也提供通用 command/property/node/event 入口，並補上常用便利 API：基本播放狀態、速度、音量、靜音、循環播放、命令與屬性清單探索、播放清單管理、外部字幕與網路字幕掛載、外部音訊與視訊軌掛載、播放軌選取、章節與版本切換、音訊裝置選取、A-B 重複播放、文字展開、OSD 文字與 ASS 覆疊、截圖、濾鏡、鍵鼠輸入、輸入 section、指令碼訊息與外部處理序。WinForms、WPF、WinUI 3 與 MAUI Windows 主線控制項只保留 HWND 後端；WPF 與 WinUI HWND 路線皆內建 AirSpace 覆蓋層。Avalonia 目前保留 OpenGL render API 預覽，不列入 HWND-only 完成範圍。未符合支援範圍收斂準則的目標不列入目前支援範圍。

設計階段支援已補入控制項本身。WinForms 可放入 Designer 並調整大小；WPF、WinUI 3、MAUI Windows 與 Avalonia 會在設計工具或 previewer 中顯示替代預覽內容，並避免初始化 libmpv、HWND 播放子視窗或 render context。WinUI 3 與 MAUI Windows 不宣稱提供 WPF/WinForms 式拖拉 Visual Designer 體驗；支援重點是 XAML/code 加入時的設計階段安全行為。

## 核心 API

完整 mpv 功能以通用入口涵蓋：`Command(...)`、`CommandNode(...)`、`CommandNamed(...)`、`GetProperty*()`、`SetProperty*()`、`ObserveProperty(...)` 與 `EventNodeReceived` 可操作官方手冊列出的命令、具名命令、屬性與事件節點。常用功能則提供強型別入口：

```csharp
using MpvPlayer player = new MpvPlayer(options);
player.Initialize();
player.LoadFile("https://www.youtube.com/watch?v=dQw4w9WgXcQ");
player.AddSubtitle("https://example.com/subtitle.vtt", MpvTrackLoadMode.Select, title: "網路字幕");
player.SelectSubtitleTrack(player.GetTracks().First(t => t.Type == MpvTrackType.Subtitle).Id);
player.ShowText("字幕已載入", 1500);
```

## 原生執行階段

應用程式可以將 `libmpv-2.dll` 放在可執行檔旁、設定 `MPV_LIBRARY_PATH`，或主動呼叫 helper 安裝同層執行階段資料夾：

```csharp
MpvWindowsRuntimeDownloadResult runtime = await MpvWindowsRuntimeInstaller.InstallOrUpdateAsync("runtime");
MpvPlayerOptions options = MpvWindowsRuntimeInstaller.CreatePlayerOptions(runtime.RuntimeDirectory);
```

此流程會把 `libmpv-2.dll`、`yt-dlp.exe` 與 `deno.exe` 放在同一個資料夾。Deno 不是播放本機檔案時的必要條件，但 yt-dlp 官方已將外部 JavaScript runtime 列為完整 YouTube 支援的需求，且 Deno 是預設啟用與建議項目，因此 helper 預設一併準備。若目前處理序已載入 libmpv，libmpv 更新會先暫存，並由結果物件提示需要重新啟動處理序。

共用入口可使用 `MpvRuntimeInstaller.InstallOrUpdateAsync(...)`。目前此入口只執行 Windows x64 安裝流程；不符合支援範圍時會傳回未列入 catalog 的狀態與提示訊息。

若要讓 runtime folder 同時作為 mpv 設定資料夾，可建立播放器選項時啟用載入。這會讓 mpv 從同一資料夾讀取 `mpv.conf`、`input.conf`、`scripts` 與其他 mpv 設定資料：

```csharp
MpvPlayerOptions options = MpvWindowsRuntimeInstaller.CreatePlayerOptions(
    runtime.RuntimeDirectory,
    loadRuntimeConfiguration: true);
```

也可以使用 API 指定特定設定檔，適合不同播放器執行個體使用不同設定：

```csharp
MpvPlayerOptions options = MpvWindowsRuntimeInstaller.CreatePlayerOptions(runtime.RuntimeDirectory);
options.ConfigFiles.Add(Path.Combine(runtime.RuntimeDirectory, "profiles", "cinema.conf"));
options.InputConfigFile = Path.Combine(runtime.RuntimeDirectory, "profiles", "input.conf");
options.ScriptFiles.Add(Path.Combine(runtime.RuntimeDirectory, "scripts", "overlay.lua"));
options.ScriptFiles.Add(Path.Combine(runtime.RuntimeDirectory, "scripts", "bridge.js"));
```

直接使用核心播放器時，仍可在 `Initialize()` 前呼叫 `LoadConfigFile(...)`，或在初始化後使用 `LoadInputConfigFile(...)` 載入輸入設定、使用 `LoadScript(...)` 載入 Lua 或 JavaScript 腳本。

yt-dlp 格式選擇可使用常用列舉或自訂 selector。格式設定會在 mpv 解析下一個 URL 時生效；若要改變目前 URL，請設定後重新載入。

```csharp
MpvPlayerOptions options = new MpvPlayerOptions
{
    YtdlpFormatPreset = MpvYtdlpFormatPreset.UpTo1080p
};

using MpvPlayer player = new MpvPlayer(options);
player.Initialize();
player.SetYtdlpFormat(MpvYtdlpFormatPreset.UpTo720p);
player.LoadFile("https://www.youtube.com/watch?v=dQw4w9WgXcQ");
```

若要讀取 mpv ytdl hook 執行 yt-dlp JSON 子程序後留下的結果，可使用 `GetYtdlJsonSubprocessResult()`；若要接收 yt-dlp 或 Deno 自己的 stdout/stderr 事件流，使用 `YtDlpProcessRunner` 或 `DenoProcessRunner`。

```csharp
YtDlpProcessRunner runner = new YtDlpProcessRunner(Path.Combine(runtime.RuntimeDirectory, "yt-dlp.exe"));
runner.WorkingDirectory = runtime.RuntimeDirectory;
runner.OutputReceived += (sender, e) => Console.WriteLine(e.Stream + ": " + e.Line);
ExternalToolProcessResult result = await runner.ListFormatsAsync("https://www.youtube.com/watch?v=dQw4w9WgXcQ");
```

自訂資料來源可使用 `RegisterStreamProtocol(...)` 註冊唯讀通訊協定，再用 `loadfile` 載入對應 URI：

```csharp
using MpvPlayer player = new MpvPlayer(options);
player.RegisterStreamProtocol("appmedia", uri => File.OpenRead(@"D:\media\sample.mp4"));
player.Initialize();
player.LoadFile("appmedia://sample");
```

## 範例

範例總覽請參閱 `samples/README.md`。各範例專案也都有自己的 `README.md`：

- `samples/WinFormsSample`
- `samples/WpfSample`
- `samples/AvaloniaSample`
- `samples/WinUISample`
- `samples/MauiSample`

WinUI 3 與 MAUI Windows 範例是未封裝 app，範例專案會使用 Windows App SDK self-contained 部署設定與 `win-x64` RID，讓建置輸出帶著所需的 Windows App SDK 相依項目。此設定僅適用於範例 app，不套用到 class library 套件。

## 測試

不需原生執行階段的核心 API 測試：

```powershell
dotnet run --project .\tests\MediaEmbedKit.Mpv.Tests\MediaEmbedKit.Mpv.Tests.csproj
```

需要 GUI、網路與 Windows x64 執行階段的範例播放冒煙測試：

```powershell
dotnet run --project .\tests\MediaEmbedKit.Mpv.PlaybackSmoke\MediaEmbedKit.Mpv.PlaybackSmoke.csproj -- --seconds 20
```

播放冒煙測試會啟動各範例應用程式，並等待影片實際播放到指定秒數後關閉。第一次執行可能需要由 sample helper 下載 `libmpv-2.dll`、`yt-dlp.exe` 與 `deno.exe`。

## 文件與註解規範

所有 `.cs` 檔案的型別與成員註解需使用 C# XML 文件註解。註解文字只能使用正體中文，優先採用 Microsoft 在地化慣用詞，其次採用臺灣地區常見技術用語；不得使用簡體中文或中國大陸慣用詞。

方法與建構函式需為每個參數撰寫獨立 `<param>`，有回傳值時需撰寫 `<returns>`，屬性需撰寫 `<value>`。每個成員都要有自己的註解內容，不使用 `<inheritdoc>`、`<include>` 或外部共用註解片段。

## AI Agent 規範

`AGENTS.md` 是 Codex 與支援 AGENTS.md 工具的主要入口。Claude Code、Gemini CLI 與 GitHub Copilot/Copilot CLI 入口檔只保留薄型轉接；詳細結構請參閱 `docs/AI_AGENT_INTEGRATION.md`。

共用 skill 內容放在 `docs/ai/skills/mediaembedkit-mpv.md`。追蹤 shinchiro 與 zhongfly 最新 libmpv git build 時，使用 `docs/ai/skills/libmpv-git-build-tracker.md`。工具專屬 `SKILL.md` 只保留轉接用途，並透過 `.agents/skills`、`.codex/skills` 與 `.claude/skills` 提供發現入口。

## 授權

受控原始碼與文件採用 CC0-1.0。第三方原生執行階段二進位檔不會被簽入此儲存庫；散發 mpv、yt-dlp 或 Deno 前，請先閱讀 `THIRD_PARTY_NOTICES.md` 並自行確認授權義務。

執行階段下載 helper 預設保持授權中立。應用程式可在檢視原生建置授權影響後，透過 `MpvWindowsBuildDownloadOptions.LicensePreference` 選擇 `Any`、`PreferLgpl`、`RequireLgpl`、`PreferNonLgpl` 或 `RequireNonLgpl`。

## 建置

```powershell
dotnet format .\MediaEmbedKit.Mpv.slnx --no-restore
dotnet run --project .\tests\MediaEmbedKit.Mpv.Tests\MediaEmbedKit.Mpv.Tests.csproj
dotnet build .\MediaEmbedKit.Mpv.slnx
```

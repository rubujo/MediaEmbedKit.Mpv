# 原生執行階段與下載政策

## 目標

runtime helper 負責讓使用者明確下載或更新 libmpv、yt-dlp 與 Deno。helper 不得在控制項建構函式、XAML 載入或播放器初始化時自動執行下載。

## 資料夾配置

目前支援範圍收斂為 Windows x64。Windows x64 預設將下列工具放在同一個執行階段資料夾；若使用者啟用 runtime 設定載入，也可以將 mpv 設定檔放在同層：

```text
runtime/
├── libmpv-2.dll
├── yt-dlp.exe
├── deno.exe
├── mpv.conf
├── input.conf
└── scripts/
```

mpv 的 `ytdl_hook` 可以透過 `MpvPlayerOptions.YtdlpPath` 指到 `yt-dlp.exe`。yt-dlp 格式選擇可以透過 `MpvPlayerOptions.YtdlpFormatPreset`、`MpvPlayerOptions.YtdlpFormat`、`MpvPlayer.SetYtdlpFormat(...)` 或 `MpvPlayer.SetYtdlpMaximumHeight(...)` 設定；格式設定會在 mpv 解析下一個 URL 時生效。Deno 不是本機檔案或每個 extractor 都必需，但 yt-dlp 官方已將外部 JavaScript runtime 列為完整 YouTube 支援所需條件，且 Deno 是預設啟用與優先建議項目，因此 Windows x64 runtime helper 預設與 `yt-dlp.exe` 同層安裝 `deno.exe`。

使用者可選擇讓同一個 runtime folder 也成為 mpv 設定資料夾。呼叫 `MpvWindowsRuntimeInstaller.CreatePlayerOptions(runtimeDirectory, loadRuntimeConfiguration: true)` 或 `MpvRuntimeInstaller.CreatePlayerOptions(runtimeDirectory, loadRuntimeConfiguration: true)` 後，核心會設定 `config-dir` 並載入該資料夾中的 `mpv.conf`、`input.conf`、`scripts` 與其他 mpv 設定資料。若只要載入特定檔案，可使用 `MpvPlayerOptions.ConfigFiles`；若只要指定輸入設定檔，可使用 `MpvPlayerOptions.InputConfigFile`；若要指定 Lua 或 JavaScript 腳本，可使用 `MpvPlayerOptions.ScriptFiles` 或 `MpvPlayer.LoadScript(...)`。

## libmpv

Windows x64 helper 可從 shinchiro `mpv-winbuild-cmake` 與 zhongfly `mpv-winbuild` 下載 x64 `mpv-dev*.7z` 資產。mpv 官方安裝頁將這兩個來源標示為 Windows git build；它們不是 stable release。下載後必須驗證壓縮檔內存在 `libmpv-2.dll`。

provider 對齊紀錄保存在 `docs/runtime/libmpv-git-builds.json`。更新 provider 對齊狀態時，先使用 `tools/libmpv/Resolve-MpvGitBuild.ps1` 解析最新 release，再用 `tools/libmpv/Compare-LibMpvHeaders.ps1` 比對 `client.h`、`render.h`、`render_gl.h` 與 `stream_cb.h`。若需要實際驗證壓縮檔內容，使用 `tools/libmpv/Verify-LibMpvArchive.ps1` 檢查 `libmpv-2.dll` 是否存在。

`libmpv-2.dll` 已載入後不可在同一處理序 hot reload。更新流程必須：

1. 偵測 `MpvLibraryLoader.IsLoaded`。
2. 將新檔案暫存到 `.updates`。
3. 回傳 `RequiresProcessRestart = true`。
4. 由應用程式在下次啟動、載入 libmpv 前套用。

目前 runtime helper 只列入 Windows x64。核心載入器仍避免把平台差異擴散到高階 API；未符合支援範圍收斂準則的目標不保留下載與安裝承諾。

## yt-dlp

yt-dlp helper 需支援：

- 下載指定 release channel 的 Windows x64 執行檔。
- 呼叫 `yt-dlp.exe -U` 或相容的自我更新命令。
- 將工具路徑回填至 `MpvPlayerOptions.YtdlpPath`。
- 透過 `MpvYtdlpFormatPreset` 提供常用畫質切換，同時保留 `YtdlpFormat` 自訂 selector。
- 透過 `YtDlpProcessRunner` 直接執行 yt-dlp，並以事件接收標準輸出與標準錯誤；這用於需要完整 yt-dlp 診斷、格式清單、單行 JSON 或下載進度輸出的應用程式情境。

mpv 內建 ytdl hook 的子程序輸出不應從 `log-message` 解析。若要查看 mpv 觸發 yt-dlp 解析 URL 時的 JSON 子程序結果，使用 `MpvPlayer.GetYtdlJsonSubprocessResult()` 或 `TryGetYtdlJsonSubprocessResult(...)` 讀取 `user-data/mpv/ytdl/json-subprocess-result`；若要接收 yt-dlp 自己的完整 stdout/stderr 事件流，直接使用 `YtDlpProcessRunner`。

目前只支援 Windows x64 `yt-dlp.exe`。未符合支援範圍收斂準則的目標不提供 helper，也不假設可執行外部處理序。

## Deno

Deno helper 需支援：

- 下載官方 Windows x64 執行檔。
- 呼叫 `deno upgrade` 或相容的自我更新命令。
- 與 libmpv、yt-dlp 放在同一個 runtime 目錄。
- 透過 `DenoProcessRunner` 直接執行 Deno，並以事件接收標準輸出與標準錯誤。

Deno 官方下載 tuple 目前只採用本專案 Windows x64 範圍需要的發行檔。yt-dlp 官方 EJS 指南指出 Windows 上 JavaScript runtime 可位於 `PATH`，或與 `yt-dlp.exe` 放在同一資料夾；因此本專案預設同層配置可以支援 YouTube EJS 情境。未符合支援範圍收斂準則的目標不列入目前 helper 支援。

## HTTP 要求

下載 helper 應重複使用共用 `HttpClient`。新版 .NET 目標使用 `SocketsHttpHandler.PooledConnectionLifetime`，避免長期 DNS 陳舊並減少連線用盡風險；.NET Framework 目標保留共用 client 策略。

瀏覽器標頭集中在 `BrowserRequestHeaders`。若下載提供者需要瀏覽器相容標頭，更新 Chrome Stable 桌面 `User-Agent`，並同步維護 `sec-ch-ua`、`sec-ch-ua-mobile` 與 `sec-ch-ua-platform`。

## 授權

受控原始碼採用 CC0-1.0。這不會改變 mpv、libmpv、yt-dlp、Deno 或其相依元件的授權。helper 預設保持授權中立，使用者可透過 `MpvWindowsBuildDownloadOptions.LicensePreference` 明確選擇 LGPL 或非 LGPL 偏好。

# 原生執行階段與下載政策

本文件定義 runtime helper 的支援範圍與限制。helper 僅在使用者明確呼叫時下載或更新第三方執行階段檔案。

## 支援範圍

目前 runtime helper 只支援 Windows x64。預設 runtime 資料夾配置如下：

```text
runtime/
├── libmpv-2.dll
├── yt-dlp.exe
├── deno.exe
├── mpv.conf
├── input.conf
└── scripts/
```

控制項建構函式、XAML 載入與播放器初始化流程不得自動下載任何二進位檔。

## libmpv

Windows x64 helper 可從 shinchiro `mpv-winbuild-cmake` 與 zhongfly `mpv-winbuild` 下載 x64 `mpv-dev*.7z` 資產。這些來源是 mpv Windows git build，不是 mpv stable release。

下載後必須驗證封存檔包含 `libmpv-2.dll`。provider 對齊狀態記錄於 `docs/runtime/libmpv-git-builds.json`。

`libmpv-2.dll` 載入後不得在同一處理序 hot reload。更新流程必須：

1. 偵測 `MpvLibraryLoader.IsLoaded`。
2. 將新檔案暫存至 `.updates`。
3. 回傳 `RequiresProcessRestart = true`。
4. 由應用程式於下次啟動且載入 libmpv 前套用更新。

## yt-dlp

yt-dlp helper 支援 Windows x64 `yt-dlp.exe` 下載、自我更新與路徑回填。應用程式可透過下列 API 控制 mpv 使用的格式：

- `MpvPlayerOptions.YtdlpFormatPreset`
- `MpvPlayerOptions.YtdlpFormat`
- `MpvPlayer.SetYtdlpFormat(...)`
- `MpvPlayer.SetYtdlpMaximumHeight(...)`

若需要 yt-dlp 自身 stdout/stderr、格式清單、JSON 或下載進度，請直接使用 `YtDlpProcessRunner`。mpv ytdl hook 的 JSON 子程序結果則由 `MpvPlayer.GetYtdlJsonSubprocessResult()` 讀取，不應解析 `log-message`。

## Deno

Deno helper 支援 Windows x64 `deno.exe` 下載、自我更新與外部處理序輸出事件。Deno 不是本機檔案播放的必要條件，但 yt-dlp YouTube EJS 情境可能需要外部 JavaScript runtime，因此 helper 預設與 `yt-dlp.exe` 同層準備 `deno.exe`。

## mpv 設定與 scripts

使用者可選擇讓 runtime 資料夾同時作為 mpv 設定資料夾。啟用後，核心會設定 `config-dir` 並載入同層 `mpv.conf`、`input.conf` 與 `scripts`。

```csharp
MpvPlayerOptions options =
    MpvWindowsRuntimeInstaller.CreatePlayerOptions(
        runtimeDirectory,
        loadRuntimeConfiguration: true);
```

若只需指定特定檔案，使用 `MpvPlayerOptions.ConfigFiles`、`InputConfigFile`、`ScriptFiles` 或 `MpvPlayer.LoadScript(...)`。

## HTTP 要求

下載 helper 必須重複使用共用 `HttpClient`。現代 .NET 目標應使用 `SocketsHttpHandler.PooledConnectionLifetime`，降低 DNS 陳舊與連線用盡風險；.NET Framework 目標保留共用 client 策略。

瀏覽器標頭集中於 `BrowserRequestHeaders`，包含 Chrome Stable 桌面 `User-Agent` 與必要 client hints。

## 授權

本專案受控原始碼採用 CC0-1.0。此授權不涵蓋 mpv/libmpv、yt-dlp、Deno 或其相依元件。helper 預設保持授權中立，使用者可透過 `MpvWindowsBuildDownloadOptions.LicensePreference` 指定 LGPL 或非 LGPL 偏好。

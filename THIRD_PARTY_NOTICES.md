# 第三方注意事項

MediaEmbedKit.Mpv 是獨立 .NET 包裝器與控制項專案，不是 mpv、yt-dlp、Deno 或 FFmpeg 官方專案。

本儲存庫不簽入第三方執行階段二進位檔。受控原始碼與文件採用 CC0-1.0；此授權不涵蓋應用程式下載、載入或散發的第三方元件。

## mpv / libmpv

- 專案：https://github.com/mpv-player/mpv
- 網站：https://mpv.io/
- 授權：請依上游專案與實際建置設定判定。mpv 預設為 GPLv2-or-later，特定建置可採 LGPLv2.1-or-later。
- 注意事項：FFmpeg 與其他原生相依項目可能另有授權義務。
- helper 行為：Windows helper 可從 mpv.io 列出的提供者下載 libmpv 開發封存檔，並以 `MpvWindowsBuildDownloadOptions.LicensePreference` 提供授權偏好選項。**預設為 `PreferLgpl`**（上游有 LGPL 變體時優先選用），商用嚴格合規請設 `RequireLgpl`。

## yt-dlp

- 專案：https://github.com/yt-dlp/yt-dlp
- 授權：請參閱上游儲存庫。
- helper 行為：只有在應用程式明確呼叫時，才會下載或更新外部 `yt-dlp.exe`。

## Deno

- 專案：https://github.com/denoland/deno
- 網站：https://deno.com/
- 授權：請參閱上游儲存庫。
- helper 行為：只有在應用程式明確呼叫時，才會下載或更新外部 `deno.exe`。

## FFmpeg / FFmpeg-Builds

- 專案：https://github.com/FFmpeg/FFmpeg
- yt-dlp 建置來源：https://github.com/yt-dlp/FFmpeg-Builds
- 授權：請依 FFmpeg 與實際建置內容判定；yt-dlp FFmpeg-Builds 目前提供 GPL build。
- helper 行為：只有在應用程式明確呼叫時，才會下載或更新外部 `ffmpeg.exe` 與 `ffprobe.exe`。本專案 NuGet 套件不包含 FFmpeg 二進位檔。

## 7-Zip（僅 fallback 情境）

- 專案：https://github.com/ip7z/7zip（Igor Pavlov 官方）
- 網站：https://www.7-zip.org/
- 授權：LGPL-2.1-or-later（`7zr.exe` 不含 unrar 程式碼，純 LGPL 可自由 redistribute）。
- helper 行為：libmpv `.7z` 解壓 4-tier fallback chain 在系統未裝 7-Zip / WinRAR、Windows 內建 `tar.exe` 也無法處理時，從 [ip7z/7zip releases/latest](https://github.com/ip7z/7zip/releases/latest) 下載 standalone `7zr.exe`（588 KB，32-bit x86 standalone CLI）。下載驗證 GitHub asset digest；既有檔存在則重用不重複下載。本專案 NuGet 套件不包含 7-Zip 二進位檔。

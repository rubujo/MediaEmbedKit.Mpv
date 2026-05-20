# 第三方注意事項

MediaEmbedKit.Mpv 是獨立 .NET 包裝器與控制項專案，不是 mpv、yt-dlp、Deno 或 FFmpeg 官方專案。

本儲存庫不簽入第三方執行階段二進位檔。受控原始碼與文件採用 CC0-1.0；此授權不涵蓋應用程式下載、載入或散發的第三方元件。

## mpv / libmpv

- 專案：[mpv-player/mpv](https://github.com/mpv-player/mpv)
- 網站：[mpv.io](https://mpv.io/)
- 授權：請依上游專案與實際建置設定判定。mpv 預設為 GPLv2-or-later，特定建置可採 LGPLv2.1-or-later。
- 注意事項：FFmpeg 與其他原生相依項目可能另有授權義務。
- 輔助工具行為：Windows 輔助工具可從 mpv.io 列出的提供者下載 libmpv 開發封存檔，並以 `MpvWindowsBuildDownloadOptions.LicensePreference` 提供授權偏好選項。**預設組合 `Provider = Zhongfly` + `LicensePreference = PreferLgpl`** 實際拿到 LGPL libmpv 建置版（zhongfly 是兩家中唯一同時提供 LGPL 變體的來源）。`ProviderFallbackOrder` 預設含 `Shinchiro` 作為備援。
- ⚠️ **若手動切到 `Provider = Shinchiro`**：該提供者不提供 LGPL 變體，`PreferLgpl` 偏好設定會靜默備援至 GPL 建置版。商用閉源散發需明確確認授權義務，或改用 `RequireLgpl` 讓「無 LGPL 可選」情境直接明確失敗。

## yt-dlp

- 專案：[yt-dlp/yt-dlp](https://github.com/yt-dlp/yt-dlp)
- 授權：請參閱上游儲存庫。
- 輔助工具行為：只有在應用程式明確呼叫時，才會下載或更新外部 `yt-dlp.exe`。

## Deno

- 專案：[denoland/deno](https://github.com/denoland/deno)
- 網站：[deno.com](https://deno.com/)
- 授權：請參閱上游儲存庫。
- 輔助工具行為：只有在應用程式明確呼叫時，才會下載或更新外部 `deno.exe`。

## FFmpeg / FFmpeg-Builds

- 專案：[FFmpeg/FFmpeg](https://github.com/FFmpeg/FFmpeg)
- yt-dlp 建置來源：[yt-dlp/FFmpeg-Builds](https://github.com/yt-dlp/FFmpeg-Builds)
- 授權：請依 FFmpeg 與實際建置內容判定；**yt-dlp FFmpeg-Builds 僅發 GPL 建置版，無 LGPL 變體**。
- 輔助工具行為：**預設 `MpvWindowsRuntimeDownloadOptions.IncludeFFmpeg = false`，不會自動下載 FFmpeg**。應用程式明確設為 `true` 才下載 yt-dlp/FFmpeg-Builds 的 GPL 建置版。**使用者啟用此選項即視同自願接受 GPLv2+ 散發義務** —— 本專案僅提供下載與驗證工具，不對授權合規做進一步處理。需要 LGPL FFmpeg 的使用者請自行從 zhongfly `mpv-winbuild` 取 `ffmpeg-lgpl-*.7z`。本專案 NuGet 套件不包含 FFmpeg 二進位檔。

## 7-Zip（僅備援情境）

- 專案：[ip7z/7zip](https://github.com/ip7z/7zip)（Igor Pavlov 官方）
- 網站：[7-Zip](https://www.7-zip.org/)
- 授權：LGPL-2.1-or-later（`7zr.exe` 不含 unrar 程式碼，純 LGPL 可自由 redistribute）。
- 輔助工具行為：libmpv `.7z` 解壓 4-tier 備援鏈在系統未裝 7-Zip / WinRAR、Windows 內建 `tar.exe` 也無法處理時，從 [ip7z/7zip latest release](https://github.com/ip7z/7zip/releases/latest) 下載獨立版 `7zr.exe`（588 KB，32-bit x86 獨立版 CLI）。下載驗證 GitHub 資產 digest；既有檔存在則重用不重複下載。本專案 NuGet 套件不包含 7-Zip 二進位檔。

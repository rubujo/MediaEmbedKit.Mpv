# 第三方注意事項

MediaEmbedKit.Mpv 是獨立的 .NET 包裝器與控制項專案。它不是 mpv、yt-dlp 或 Deno 官方專案。

此儲存庫預設不包含第三方執行階段二進位檔。

此儲存庫中的受控原始碼與文件採用 CC0-1.0。此授權不會改變應用程式下載或載入的任何第三方執行階段二進位檔授權。

## mpv / libmpv

- 專案：https://github.com/mpv-player/mpv
- 網站：https://mpv.io/
- 授權：mpv 預設為 GPLv2-or-later，並可在適當建置選項下建置為 LGPLv2.1-or-later。FFmpeg 與其他原生相依項目可能另有授權義務。
- 執行階段 helper：Windows helper 可從 mpv.io 列出的提供者下載 libmpv 開發封存檔。helper 預設保持授權中立，並提供 `MpvWindowsBuildDownloadOptions.LicensePreference`，讓使用者選擇任意、優先 LGPL、必須 LGPL、優先非 LGPL 或必須非 LGPL 建置。使用者仍需自行滿足所散發原生二進位檔的授權條款。

## yt-dlp

- 專案：https://github.com/yt-dlp/yt-dlp
- 授權：請參閱上游儲存庫授權。
- 執行階段 helper：下載與更新 helper 只有在應用程式明確呼叫時，才會安裝外部 yt-dlp 可執行檔。

## Deno

- 專案：https://github.com/denoland/deno
- 網站：https://deno.com/
- 授權：請參閱上游儲存庫授權。
- 執行階段 helper：下載與更新 helper 只有在應用程式明確呼叫時，才會安裝外部 Deno 可執行檔。

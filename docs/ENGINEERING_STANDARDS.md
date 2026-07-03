# 工程標準

本文件定義專案的程式碼、文件、提交與驗證規則。

## 語言與排版

專案文件、程式碼註解、使用者可見字串與提交訊息必須使用正式、精煉且一致的正體中文臺灣地區用語，避免中國地區慣用詞。

除官方名稱、API 名稱、程式識別字、命令、路徑、URL、授權原文、標準規格與必要技術術語外，避免直接使用英文詞彙；可用中文清楚表達時，優先使用中文。

提及第三方函式庫、軟體、服務、工具、規格或品牌時，必須使用其官方正式名稱與大小寫。例如 GitHub Actions、GitHub Releases、NuGet、Visual Studio、.NET、Windows、FFmpeg、yt-dlp、Deno、mpv、libmpv、7-Zip 與 WinRAR。

中英文與中文數字混排需保留盤古之白。行內程式碼、URL、檔案路徑、命令列片段與 API 名稱依 Markdown 或 C# 語法處理。

英文縮寫與產品名稱需依句首、官方名稱與技術縮寫規則使用大小寫，不得自行改成全小寫或全大寫。

## 目標框架與相依性

- 不新增 `net40`、`net45`、`net6` 或 `net9`，除非使用者明確重新核准支援政策。
- NuGet 版本集中於 `Directory.Packages.props`，並使用最新穩定版本。
- 平台專屬 API 不得洩漏至核心 `MediaEmbedKit.Mpv`。

## C# XML 註解

所有 `.cs` 型別與成員必須使用 C# XML 文件註解。註解文字只能使用正體中文臺灣地區用語，並優先採用 Microsoft 臺灣在地化慣用詞；上游專案名稱、命令、API 名稱與 URL 可保留原文。

XML 文件註解不得使用一行式標籤排版。`<summary>`、`<param>`、`<returns>`、`<value>`、`<typeparam>` 等具有內容的標籤，必須將開始標籤、內容與結束標籤分成三行。

必要標籤：

- 型別與成員：`<summary>`。
- 參數：`<param>`。
- 回傳值：`<returns>`。
- 屬性：`<value>`。
- 泛型型別參數：`<typeparam>`.

不得使用 `<inheritdoc>`、`<include>` 或共用註解片段。

## C# 型別宣告

遵循微軟官方 C# 編碼慣例（Microsoft C# Coding Conventions）：
- **使用 `var`**：當指派右側的型別顯而易見時（例如右側為 `new` 建構子、明確的 `(Type)` 強制轉型或明確的字面值），建議使用 `var` 以提高可讀性。匿名型別與 `foreach` 迴圈變數（右側顯而易見時）亦建議使用 `var`。
- **使用明確型別**：當指派右側的型別不明顯時（例如方法呼叫、複雜的 LINQ 或無明確型別提示的運算），必須使用明確型別，以方便無 IDE 的代碼審查（例如 GitHub Pull Request）。

## 文件

Markdown 文件應使用正式、精煉且一致的正體中文臺灣地區用語。英文授權原文、第三方專案名稱、API 名稱與 URL 例外。

文件分工：

- `README.md`：使用者入口與風險聲明。
- `docs/PROJECT_SPEC.md`：專案規範入口。
- `docs/SUPPORT_MATRIX.md`：支援狀態。
- `docs/UI_BACKENDS.md`：UI 後端。
- `docs/RUNTIME_ASSETS.md`：執行階段政策。
- `docs/HIGH_LEVEL_API.md`：高階 API 與編碼操作指南。
- `docs/CONTROLS_API.md`：五個 UI 框架控制項共通綁定屬性與命令。
- `docs/CONSUMING_PACKAGES.md`：從 GitHub Releases 安裝本機 NuGet 套件。
- `docs/LIBMPV_C_API_TEST_MATRIX.md`：libmpv C API 覆蓋與驗證矩陣。
- `docs/RELEASE_CHECKLIST.md`：發佈前本機檢查。
- `docs/DESIGN_TIME_CHECKLIST.md`：Windows UI 控制項設計階段檢查。
- `docs/AI_AGENT_INTEGRATION.md`：AI 代理與技能結構。
- `docs/REFERENCE_SOURCES.md`：上游與第三方參考來源。

## 方案結構

`.slnx` 中的方案項目只放置根目錄檔案與跨專案共用文件。若檔案位於某個 `.csproj` 所在資料夾或其子資料夾，該檔案不得另外列在方案項目下，應由該專案節點或檔案系統結構呈現。

## 原生資產下載

- 執行階段輔助工具不得在控制項建構函式、XAML 載入或播放器初始化流程中自動下載第三方二進位檔。
- 新增下載來源時，必須支援 SHA-256 驗證與來源鎖定，並於 `docs/RUNTIME_ASSETS.md` 記錄檢查碼來源。
- 供生產使用的流程應支援呼叫端釘選 SHA-256；無獨立檢查碼資產的提供者不得宣稱具備完整供應鏈驗證。
- 自我更新命令只能作為外部工具的薄型包裝；若需要可稽核下載紀錄，應走輔助工具下載與驗證流程。

## Git 提交

提交訊息必須遵循慣例式提交 1.0.0，格式為：

```text
<type>[optional scope]: <description>

<body>
```

規則：

- `type` 使用小寫 ASCII，例如 `feat`、`fix`、`docs`、`refactor`、`test`、`build`、`chore`。
- `scope` 可省略；使用時應為小寫 ASCII 名詞。
- `description` 與 `body` 使用正體中文與臺灣地區用語。
- 不得使用一行式提交訊息。
- 重大變更依慣例式提交使用 `!` 或 `BREAKING CHANGE:`。

## 編碼與換行

- 所有文字檔使用 CRLF。
- `.cs`、Visual Studio、MSBuild、XAML 與 manifest 相關檔案使用 UTF-8 BOM。
- `.md`、`.json`、`.yml`、`.yaml`、`.editorconfig`、`.gitignore`、授權與注意事項文字檔使用 UTF-8 無 BOM。

## 驗證

程式碼、專案檔、套件版本或共用 API 變更後執行：

```powershell
dotnet format .\MediaEmbedKit.Mpv.slnx --no-restore
dotnet run --project .\tests\MediaEmbedKit.Mpv.Tests\MediaEmbedKit.Mpv.Tests.csproj
dotnet run --project .\tests\MediaEmbedKit.Mpv.IntegrationTests\MediaEmbedKit.Mpv.IntegrationTests.csproj
dotnet build .\MediaEmbedKit.Mpv.slnx
```

播放驗證使用：

```powershell
dotnet run --project .\tests\MediaEmbedKit.Mpv.PlaybackSmoke\MediaEmbedKit.Mpv.PlaybackSmoke.csproj -- --seconds 20
```

GUI 使用端實際播放驗證使用：

```powershell
.\tools\Invoke-GuiConsumerPlaybackValidation.ps1 -Seconds 20
```

長時間 GUI 播放壓力測試使用：

```powershell
.\tools\Invoke-GuiPlaybackStress.ps1 -Seconds 120 -Iterations 2
```

發佈前本機驗證使用：

```powershell
.\tools\Invoke-PreReleaseValidation.ps1
```

發佈前主流程預設使用 Release 組態。若要執行完整 Windows 發行檢查閘門，可使用：

```powershell
.\tools\Invoke-PreReleaseValidation.ps1 -IncludeWindowsReleaseGate -GuiPlaybackSeconds 20
```

NuGet 套件內容驗證使用：

```powershell
.\tools\Invoke-PackageValidation.ps1
```

乾淨使用端套件驗證使用：

```powershell
.\tools\Invoke-ConsumerPackageValidation.ps1
```

第一階段壓力測試使用：

```powershell
dotnet run --project .\tests\MediaEmbedKit.Mpv.StressTests\MediaEmbedKit.Mpv.StressTests.csproj
```

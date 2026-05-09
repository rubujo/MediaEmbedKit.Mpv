# 工程標準

本文件定義專案的程式碼、文件、提交與驗證規則。

## 目標框架與相依性

- 不新增 `net40`、`net45`、`net6` 或 `net9`，除非使用者明確重新核准支援政策。
- NuGet 版本集中於 `Directory.Packages.props`，並使用最新穩定版本。
- 平台專屬 API 不得洩漏至核心 `MediaEmbedKit.Mpv`。

## C# XML 註解

所有 `.cs` 型別與成員必須使用 C# XML 文件註解。註解文字只能使用正體中文，並優先採用 Microsoft 在地化慣用詞；上游專案名稱、命令、API 名稱與 URL 可保留原文。

必要標籤：

- 型別與成員：`<summary>`。
- 參數：`<param>`。
- 回傳值：`<returns>`。
- 屬性：`<value>`。
- 泛型型別參數：`<typeparam>`.

不得使用 `<inheritdoc>`、`<include>` 或共用註解片段。

## C# 型別宣告

區域變數、`using` 陳述式與 `foreach` 迴圈變數應使用明確型別。只有匿名型別、編譯器要求或明確型別明顯降低可讀性時，才可使用 `var`。

## 文件

Markdown 文件應使用正式、精煉且一致的正體中文。英文授權原文、第三方專案名稱、API 名稱與 URL 例外。

文件分工：

- `README.md`：使用者入口與風險聲明。
- `docs/PROJECT_SPEC.md`：專案規範入口。
- `docs/SUPPORT_MATRIX.md`：支援狀態。
- `docs/UI_BACKENDS.md`：UI 後端。
- `docs/RUNTIME_ASSETS.md`：runtime 政策。
- `docs/AI_AGENT_INTEGRATION.md`：AI agent 與 skills 結構。

## 方案結構

`.slnx` 中的方案項目只放置根目錄檔案與跨專案共用文件。若檔案位於某個 `.csproj` 所在資料夾或其子資料夾，該檔案不得另外列在方案項目下，應由該專案節點或檔案系統結構呈現。

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

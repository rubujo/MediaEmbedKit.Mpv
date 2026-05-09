# 工程標準

## 語言與目標

- 不新增 `net40`、`net45`、`net6` 或 `net9`，除非使用者明確重新核准支援政策。
- NuGet 版本集中管理於 `Directory.Packages.props`，並使用最新穩定版本。
- UI 套件必須避免將平台專屬 API 泄漏到核心 `MediaEmbedKit.Mpv`。

## C# 註解

所有 `.cs` 檔案都必須為型別、欄位、建構函式、方法、事件、屬性、委派與列舉值使用 C# XML 文件註解。

XML 文件註解規則：

- 每個程式碼項目都要使用 `<summary>`。
- 每個方法、建構函式、委派與運算子的參數都要使用 `<param>`。
- 每個會傳回值的方法或委派都要使用 `<returns>`。
- 每個屬性都要使用 `<value>`。
- 每個泛型型別參數都要使用 `<typeparam>`。
- 不使用 `<inheritdoc>`、`<include>` 或共用註解片段。

註解只能使用正體中文。用語優先採用 Microsoft 在地化慣用詞，其次採用臺灣地區常見技術用語；不得使用簡體中文或中國大陸慣用詞。上游專案名稱、命令、程式碼符號與 URL 可以保留原文。

## C# 型別宣告

區域變數、`using` 陳述式與 `foreach` 迴圈變數應使用明確型別，避免使用 `var`。只有匿名型別、編譯器要求使用隱含型別，或明確型別會降低可讀性的少數情境才可使用 `var`。

## 文件

主要 `README.md`、`samples/README.md`、各範例 `README.md`、專案規範與 agent 文件都必須使用正體中文。英文授權原文與第三方專案名稱例外。

文件應分工明確：

- `README.md` 面向使用者。
- `docs/PROJECT_SPEC.md` 是規範入口。
- `docs/SUPPORT_MATRIX.md` 描述支援狀態。
- `docs/UI_BACKENDS.md` 描述 UI 後端與高效能目標。
- `docs/RUNTIME_ASSETS.md` 描述原生工具下載與更新政策。
- `docs/AI_AGENT_INTEGRATION.md` 描述 AI agent 與 skills 結構。

## Git 提交

所有提交訊息都必須遵循慣例式提交 1.0.0，並且必須同時包含主旨與正文。主旨格式為 `<type>[optional scope]: <description>`，主旨後必須有一個空行，再撰寫至少一行正文；不得使用只有主旨的一行式提交訊息。

提交訊息規則：

- `type` 使用慣例式提交規範中的小寫 ASCII 類型，例如 `feat`、`fix`、`docs`、`style`、`refactor`、`perf`、`test`、`build`、`ci`、`chore` 或 `revert`。
- `scope` 可省略；若使用，應以小寫 ASCII 名詞描述受影響區域，例如 `docs`、`runtime`、`wpf`、`winui` 或 `samples`。
- `description`、正文與一般頁腳內容必須使用正體中文，並優先採用臺灣地區常見技術用語。
- 正文必須補充這次變更的背景、內容或驗證資訊；不得只重複主旨。
- 重大變更必須依慣例式提交使用 `!` 或 `BREAKING CHANGE:` 標示；`BREAKING CHANGE` 符記依規範保留英文大寫。
- 可以保留套件名稱、API 名稱、命令、檔案路徑、URL、issue 編號與標準頁腳符記的原文。
- 不得使用簡體中文或中國大陸慣用詞撰寫提交描述。

範例：

```text
feat(wpf): 加入內建 AirSpace 覆蓋層

讓 `MpvWpfPlayer` 透過內建覆蓋層承載影片上方 UI，使用者不需要自行建立 popup。

docs: 更新 Windows x64 支援矩陣

補齊目前支援的 UI 框架、目標框架與預覽狀態，並移除未支援平台的預告式描述。
```

## 編碼與換行

所有文字檔換行字元必須使用 CRLF。

Visual Studio 原始碼、MSBuild、XAML 與 manifest 相關檔案必須使用 UTF-8 BOM，包括 `.cs`、`.sln`、`.slnx`、`.csproj`、`.props`、`.targets`、`.xaml`、`.appxmanifest`、`.manifest` 與 `.resx`。

其他文字檔必須使用 UTF-8 無 BOM，包括 `.md`、`.json`、`.yml`、`.yaml`、`.editorconfig`、`.gitignore`、授權與注意事項文字檔。

## 驗證

程式碼、專案檔、套件版本或共用 API 變更後執行：

```powershell
dotnet format .\MediaEmbedKit.Mpv.slnx --no-restore
dotnet build .\MediaEmbedKit.Mpv.slnx
```

若清除 `obj` 後缺少 `project.assets.json`，請先執行不帶 `--no-restore` 的建置或 `dotnet restore .\MediaEmbedKit.Mpv.slnx`，再執行 `dotnet format --no-restore`。WinUI 3 與 MAUI Windows 範例是 app 專案，可使用 Windows App SDK self-contained 部署與 `win-x64` RID；class library 專案不得設定 `WindowsAppSDKSelfContained`。

播放驗證需要平台相符的 libmpv 原生程式庫。URL 播放需要 yt-dlp 可被 mpv 找到，或透過 `MpvPlayerOptions.YtdlpPath` 指定。

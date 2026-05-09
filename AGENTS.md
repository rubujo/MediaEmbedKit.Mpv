# AGENTS.md

這是本儲存庫給 AI coding agent 使用的唯一主要入口。`CLAUDE.md`、`GEMINI.md` 與 `.github/copilot-instructions.md` 只作為相容橋接，規則不得在那些檔案重複維護。

## 先讀文件

- 專案規範入口：`docs/PROJECT_SPEC.md`
- 跨 CLI 與 skill 結構：`docs/AI_AGENT_INTEGRATION.md`
- 需要最新依據時：`docs/REFERENCE_SOURCES.md`

`docs/PROJECT_SPEC.md` 會索引支援矩陣、UI 後端、執行階段資產、工程規範與測試矩陣。請依任務載入專責文件，不要把所有規則複製到工具專屬入口。

## 共用 Skill

- 編修 libmpv 包裝、UI 後端、下載 helper、範例或文件：使用 `docs/ai/skills/mediaembedkit-mpv.md`。
- 追蹤 shinchiro/zhongfly mpv git build、比對 libmpv header 或更新 provider 紀錄：使用 `docs/ai/skills/libmpv-git-build-tracker.md`。

`.agents` 與 `.claude` 下的 `SKILL.md` 僅供工具發現；實際流程以 `docs/ai/skills/*.md` 為準。

## 工作規則

- 保持修改範圍聚焦，避免重寫不相關檔案。
- 變更支援平台、UI 後端、runtime 政策、授權、提交規則或驗證方式時，同步更新 `docs/PROJECT_SPEC.md` 與對應專責文件。
- 所有文件使用正體中文；外部授權原文、產品名稱與 URL 除外。
- 建立提交時遵循慣例式提交 1.0.0，提交訊息必須有主旨與正文，且使用正體中文與臺灣地區用語。

## 驗證

程式碼、專案檔或共用 API 變更後，請執行：

```powershell
dotnet format .\MediaEmbedKit.Mpv.slnx --no-restore
dotnet run --project .\tests\MediaEmbedKit.Mpv.Tests\MediaEmbedKit.Mpv.Tests.csproj
dotnet run --project .\tests\MediaEmbedKit.Mpv.IntegrationTests\MediaEmbedKit.Mpv.IntegrationTests.csproj
dotnet build .\MediaEmbedKit.Mpv.slnx
```

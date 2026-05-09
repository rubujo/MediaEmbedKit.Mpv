# AGENTS.md

這是本儲存庫給 AI coding agent 使用的主要入口。請先閱讀並遵循 `docs/ai/AGENT_GUIDE.md`。

## 文件入口

- 專案規範：`docs/PROJECT_SPEC.md`
- 支援矩陣：`docs/SUPPORT_MATRIX.md`
- UI 後端：`docs/UI_BACKENDS.md`
- 執行階段資產：`docs/RUNTIME_ASSETS.md`
- 工程規範：`docs/ENGINEERING_STANDARDS.md`
- AI agent 整合：`docs/AI_AGENT_INTEGRATION.md`
- 參考來源：`docs/REFERENCE_SOURCES.md`

## 共用 Skill

編修 libmpv 包裝、UI 後端、下載 helper、範例或文件時，請使用 `docs/ai/skills/mediaembedkit-mpv.md` 作為共用 skill 內容。工具專屬 skill 檔只作為發現與轉接用途。

追蹤 shinchiro 與 zhongfly 最新 mpv git build、比對 libmpv C API header 或更新 provider 對齊紀錄時，請使用 `docs/ai/skills/libmpv-git-build-tracker.md` 作為共用 skill 內容。

## 驗證

程式碼、專案檔或共用 API 變更後，請執行：

```powershell
dotnet format .\MediaEmbedKit.Mpv.slnx --no-restore
dotnet run --project .\tests\MediaEmbedKit.Mpv.Tests\MediaEmbedKit.Mpv.Tests.csproj
dotnet run --project .\tests\MediaEmbedKit.Mpv.IntegrationTests\MediaEmbedKit.Mpv.IntegrationTests.csproj
dotnet build .\MediaEmbedKit.Mpv.slnx
```

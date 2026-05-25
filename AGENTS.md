# AGENTS.md

本文件是本儲存庫提供給 AI 代理的唯一主要入口。其他 CLI 專屬入口僅作必要橋接用途，不應放置重複規則。

## 文件入口

- 專案規範：`docs/PROJECT_SPEC.md`
- AI 代理整合：`docs/AI_AGENT_INTEGRATION.md`
- 參考來源：`docs/REFERENCE_SOURCES.md`

請依任務載入專責文件，不要將規則複製到工具專屬入口。

## 共用技能

- 專案編修：`docs/ai/skills/mediaembedkit-mpv.md`
- 提供者 git build 追蹤：`docs/ai/skills/libmpv-git-build-tracker.md`

`.agents/skills` 為 Codex CLI、GitHub Copilot CLI 與 Google Antigravity CLI 的跨工具技能入口；`.claude/skills` 僅作為 Claude Code 橋接。所有 `SKILL.md` 必須保留 `name` 與 `description`，且僅供工具發現；實際流程以 `docs/ai/skills/*.md` 為準。

## 基本規則

- 修改範圍應與任務直接相關。
- 變更支援平台、UI 後端、執行階段、授權、提交或驗證規則時，必須同步更新文件。
- 文件、程式碼註解、使用者可見字串與提交訊息使用正式、精煉的正體中文臺灣地區用語；避免中國地區用語。
- 除官方名稱、API 名稱、程式識別字、命令、路徑、URL、授權原文與必要技術術語外，避免直接使用英文詞彙。
- 提及第三方函式庫、軟體、服務、工具、規格或品牌時，必須使用其官方正式名稱與大小寫。
- 中英文與中文數字混排需保留盤古之白。
- 提交訊息遵循慣例式提交，必須包含主旨與正文。

## 驗證

```powershell
dotnet format .\MediaEmbedKit.Mpv.slnx --no-restore
dotnet run --project .\tests\MediaEmbedKit.Mpv.Tests\MediaEmbedKit.Mpv.Tests.csproj
dotnet run --project .\tests\MediaEmbedKit.Mpv.IntegrationTests\MediaEmbedKit.Mpv.IntegrationTests.csproj
dotnet build .\MediaEmbedKit.Mpv.slnx
```

<!--
此檔案由 tools/Sync-CopilotInstructions.ps1 從 AGENTS.md 自動同步。
請勿手動編輯；要更動規則請改 AGENTS.md，再執行：
  pwsh tools/Sync-CopilotInstructions.ps1
GitHub Copilot 不支援 @import 機制，因此必須 inline 內容。
-->

# GitHub Copilot 指示

本文件是 GitHub Copilot 與 Copilot CLI 看到的 system instructions。內容必須與 `AGENTS.md` 保持一致。

---
# AGENTS.md

本文件是本儲存庫提供給 AI coding agent 的主要入口。其他 CLI 專屬入口僅作橋接用途，不應放置重複規則。

## 文件入口

- 專案規範：`docs/PROJECT_SPEC.md`
- AI agent 整合：`docs/AI_AGENT_INTEGRATION.md`
- 參考來源：`docs/REFERENCE_SOURCES.md`

請依任務載入專責文件，不要將規則複製到工具專屬入口。

## 共用 Skill

- 專案編修：`docs/ai/skills/mediaembedkit-mpv.md`
- provider git build 追蹤：`docs/ai/skills/libmpv-git-build-tracker.md`

`.agents` 與 `.claude` 下的 `SKILL.md` 僅供工具發現；實際流程以 `docs/ai/skills/*.md` 為準。

## 基本規則

- 修改範圍應與任務直接相關。
- 變更支援平台、UI 後端、runtime、授權、提交或驗證規則時，必須同步更新文件。
- Markdown 使用正式、精煉的正體中文；產品名稱、API 名稱、URL 與授權原文例外。
- 提交訊息遵循慣例式提交，必須包含主旨與正文。

## 驗證

```powershell
dotnet format .\MediaEmbedKit.Mpv.slnx --no-restore
dotnet run --project .\tests\MediaEmbedKit.Mpv.Tests\MediaEmbedKit.Mpv.Tests.csproj
dotnet run --project .\tests\MediaEmbedKit.Mpv.IntegrationTests\MediaEmbedKit.Mpv.IntegrationTests.csproj
dotnet build .\MediaEmbedKit.Mpv.slnx
```

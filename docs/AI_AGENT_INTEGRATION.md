# AI 代理整合規範

本專案採用單一主要入口與薄型橋接設計，避免不同 CLI 工具維護重複規則。主要支援 Codex CLI、Claude Code、GitHub Copilot CLI 與 Google Antigravity CLI；舊有 Gemini CLI 入口僅保留相容資訊。

## 結構

| 位置 | 角色 |
| --- | --- |
| `AGENTS.md` | 主要入口；Codex CLI、GitHub Copilot CLI 與 Google Antigravity CLI 可直接讀取。 |
| `CLAUDE.md` | Claude Code 橋接，以 `@AGENTS.md` 匯入主要入口。 |
| `GEMINI.md` | Google Antigravity CLI 相容橋接，以 `@AGENTS.md` 匯入主要入口。 |
| `.gemini/settings.json` | 舊有 Gemini CLI 相容設定，將 context `fileName` 指向 `AGENTS.md`。 |
| `.github/copilot-instructions.md` | GitHub Copilot 儲存庫自訂指示相容鏡像，由 `tools/Sync-CopilotInstructions.ps1` 從 `AGENTS.md` 產生。 |
| `.agents/skills/*/SKILL.md` | Codex CLI、GitHub Copilot CLI 與 Google Antigravity CLI 的跨工具技能發現檔。 |
| `.claude/skills/*/SKILL.md` | Claude Code 專案技能發現檔。 |
| `docs/ai/AGENT_GUIDE.md` | 舊路徑相容索引，不作為規則來源。 |

## 四工具能力矩陣

| 機制 | Codex CLI | Claude Code | GitHub Copilot CLI | Google Antigravity CLI |
| --- | :-: | :-: | :-: | :-: |
| 主要規則入口 | `AGENTS.md` | `CLAUDE.md` 匯入 `AGENTS.md` | `AGENTS.md` | `AGENTS.md`，並保留 `GEMINI.md` 相容橋接 |
| 專案技能發現 | `.agents/skills/` | `.claude/skills/` | `.agents/skills/` | `.agents/skills/` |
| 共用技能流程指南 | `docs/ai/skills/*.md` | `docs/ai/skills/*.md` | `docs/ai/skills/*.md` | `docs/ai/skills/*.md` |
| 工具專屬橋接 | 不需要 | `CLAUDE.md`、`.claude/skills/` | `.github/copilot-instructions.md` 相容鏡像 | `GEMINI.md` |

> `AGENTS.md` 是唯一主要規則來源。工具專屬入口只做匯入、鏡像或技能發現，不放置平行規則。

## 技能

| 位置 | 角色 |
| --- | --- |
| `docs/ai/skills/mediaembedkit-mpv.md` | 本專案共用技能流程。 |
| `docs/ai/skills/libmpv-git-build-tracker.md` | 提供者 git build 與 libmpv 標頭追蹤流程。 |
| `.agents/skills/*/SKILL.md` | 跨工具主要技能發現檔。 |
| `.claude/skills/*/SKILL.md` | Claude Code 專案技能橋接檔。 |

`SKILL.md` 只保留 front matter、簡短標題與共用技能路徑。實際規則一律維護於 `docs/ai/skills/*.md`。

## 維護原則

- 修改跨工具入口時，只更新 `AGENTS.md` 與本文件。
- 修改 `AGENTS.md` 後必須執行 `pwsh tools/Sync-CopilotInstructions.ps1` 產生 `.github/copilot-instructions.md` 相容鏡像；或在 CI 跑 `Sync-CopilotInstructions.ps1 -Check` 驗證同步狀態。
- 修改專案規範時，更新 `docs/PROJECT_SPEC.md` 與對應專責文件。
- 修改技能行為時，更新 `docs/ai/skills/*.md`。
- 不新增平行 CLI 專屬規則；必要入口只能轉接、鏡像 `AGENTS.md`，或指向共用技能文件。

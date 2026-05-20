# AI 代理整合規範

本專案採用單一主要入口與薄型橋接設計，避免不同 CLI 工具維護重複規則。明確支援 Claude Code、Gemini CLI、GitHub Copilot 與 OpenAI Codex CLI 四個工具。

## 結構

| 位置 | 角色 |
| --- | --- |
| `AGENTS.md` | 主要入口；OpenAI Codex CLI 原生讀取。 |
| `CLAUDE.md` | Claude Code 橋接，以 `@AGENTS.md` 匯入。 |
| `GEMINI.md` | Gemini CLI 橋接，以 `@AGENTS.md` 匯入。 |
| `.gemini/settings.json` | 將 Gemini CLI context fileName 指向 `AGENTS.md`。 |
| `.github/copilot-instructions.md` | GitHub Copilot 系統指令；以 `tools/Sync-CopilotInstructions.ps1` 自動內嵌同步 `AGENTS.md`。 |
| `.codex/skills/*/SKILL.md` | OpenAI Codex CLI 專案技能發現檔。 |
| `.claude/skills/*/SKILL.md` | Claude Code 專案技能發現檔。 |
| `.agents/skills/*/SKILL.md` | Agent Skills 可攜發現檔。 |
| `docs/ai/AGENT_GUIDE.md` | 舊路徑相容索引，不作為規則來源。 |

## 四工具能力矩陣

| 機制 | Claude Code | Gemini CLI | GitHub Copilot | OpenAI Codex CLI |
| --- | :-: | :-: | :-: | :-: |
| `AGENTS.md` 原生讀取 | 透過 `CLAUDE.md` `@AGENTS.md` | 透過 `GEMINI.md` `@AGENTS.md` + `.gemini/settings.json` | ❌ 不支援 `@import`；採內嵌同步 | ✅ 原生 |
| 技能發現 | `.claude/skills/` | ❌ 無技能系統 | ❌ 無技能系統 | `.codex/skills/`（自訂） |
| 工具呼叫前／後 hook | ✅ `.claude/settings.json` | ❌ 不支援 | ❌ 不支援 | ❌ 不支援 |
| 跨工作階段記憶 | ✅ `~/.claude/projects/.../memory/` | 部分 | ❌ | 部分 |
| 共用技能流程指南 (`docs/ai/skills/*.md`) | ✅ | ✅（透過 AGENTS.md 引用） | ✅（內嵌後可看到技能路徑） | ✅ |

> Copilot 因為不支援 `@import`，**必須**內嵌完整內容；其他三個工具走 `@AGENTS.md` 或原生讀取，內容只維護一份。Hook 與記憶為 Claude Code 專屬，無法跨工具實作。

## 技能

| 位置 | 角色 |
| --- | --- |
| `docs/ai/skills/mediaembedkit-mpv.md` | 本專案共用技能流程。 |
| `docs/ai/skills/libmpv-git-build-tracker.md` | 提供者 git build 與 libmpv 標頭追蹤流程。 |
| `.agents/skills/*/SKILL.md` | Agent Skills 可攜發現檔。 |
| `.claude/skills/*/SKILL.md` | Claude Code 專案技能發現檔。 |
| `.codex/skills/*/SKILL.md` | OpenAI Codex CLI 專案技能發現檔。 |

工具專屬 `SKILL.md` 只保留 front matter、簡短標題與共用技能路徑。實際規則一律維護於 `docs/ai/skills/*.md`。

## 維護原則

- 修改跨工具入口時，只更新 `AGENTS.md` 與本文件。
- 修改 `AGENTS.md` 後必須執行 `pwsh tools/Sync-CopilotInstructions.ps1` 把內容同步到 `.github/copilot-instructions.md`；或在 CI 跑 `Sync-CopilotInstructions.ps1 -Check` 驗證同步狀態。
- 修改專案規範時，更新 `docs/PROJECT_SPEC.md` 與對應專責文件。
- 修改技能行為時，更新 `docs/ai/skills/*.md`。
- 不新增平行 CLI 專屬規則；必要入口只能轉接至 `AGENTS.md`，或 Copilot 自動同步。

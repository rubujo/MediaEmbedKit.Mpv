# AI Agent 整合規範

本專案採用單一主要入口與薄型橋接設計，避免不同 CLI 工具維護重複規則。

## 結構

| 位置 | 角色 |
| --- | --- |
| `AGENTS.md` | 主要入口。 |
| `CLAUDE.md` | Claude Code 橋接，匯入 `AGENTS.md`。 |
| `GEMINI.md` | Gemini CLI 橋接，匯入 `AGENTS.md`。 |
| `.gemini/settings.json` | 將 Gemini CLI context fileName 指向 `AGENTS.md`。 |
| `.github/copilot-instructions.md` | GitHub Copilot 橋接，指向 `AGENTS.md`。 |
| `docs/ai/AGENT_GUIDE.md` | 舊路徑相容索引，不作為規則來源。 |

## Skill

| 位置 | 角色 |
| --- | --- |
| `docs/ai/skills/mediaembedkit-mpv.md` | 本專案共用 skill 流程。 |
| `docs/ai/skills/libmpv-git-build-tracker.md` | provider git build 與 libmpv header 追蹤流程。 |
| `.agents/skills/*/SKILL.md` | Agent Skills 可攜發現檔。 |
| `.claude/skills/*/SKILL.md` | Claude Code 專案 skill 發現檔。 |

工具專屬 `SKILL.md` 只保留 frontmatter、簡短標題與共用 skill 路徑。實際規則一律維護於 `docs/ai/skills/*.md`。

## 維護原則

- 修改跨工具入口時，只更新 `AGENTS.md` 與本文件。
- 修改專案規範時，更新 `docs/PROJECT_SPEC.md` 與對應專責文件。
- 修改 skill 行為時，更新 `docs/ai/skills/*.md`。
- 不新增平行 CLI 專屬規則；必要入口只能轉接至 `AGENTS.md`。

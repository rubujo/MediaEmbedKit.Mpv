# AI Agent 整合規範

## 結論

跨 CLI 設計只保留三層：

1. `AGENTS.md` 是唯一主要入口。
2. `docs/PROJECT_SPEC.md` 與各專責文件保存專案規則。
3. `docs/ai/skills/*.md` 保存共用 skill 流程。

其他工具專屬檔案只做發現與轉接，不放置可與上述三層衝突的規則。

## 入口檔

| 位置 | 角色 |
| --- | --- |
| `AGENTS.md` | 根目錄主要入口，供 Codex、Copilot agent 與支援 AGENTS.md 的工具使用。 |
| `CLAUDE.md` | Claude Code 相容橋接，僅匯入 `AGENTS.md`。 |
| `GEMINI.md` | Gemini CLI 相容橋接，僅匯入 `AGENTS.md`。 |
| `.gemini/settings.json` | 將 Gemini CLI context fileName 指向 `AGENTS.md`。 |
| `.github/copilot-instructions.md` | GitHub Copilot/Copilot CLI 相容橋接，僅指向 `AGENTS.md`。 |
| `docs/ai/AGENT_GUIDE.md` | 舊路徑相容索引；不得成為第二份規則來源。 |

## Skill 位置

| 位置 | 角色 |
| --- | --- |
| `docs/ai/skills/mediaembedkit-mpv.md` | 編修本專案的共用 skill 內容。 |
| `docs/ai/skills/libmpv-git-build-tracker.md` | 追蹤 provider git build 與 libmpv header 的共用流程。 |
| `.agents/skills/*/SKILL.md` | 符合 Agent Skills 規格的可攜發現檔。 |
| `.claude/skills/*/SKILL.md` | Claude Code 專案 skill 發現檔。 |

工具專屬 `SKILL.md` 只保留 YAML frontmatter、簡短標題與共用 skill 路徑。詳細工作規則一律放在 `docs/ai/skills/*.md`。

## 維護規則

- 修改跨工具入口順序時，只更新 `AGENTS.md` 與本檔。
- 修改專案支援目標、平台宣告、runtime、授權、註解、提交或驗證規則時，更新 `docs/PROJECT_SPEC.md` 與對應專責文件。
- 修改 skill 行為時，更新 `docs/ai/skills/*.md`；工具專屬 `SKILL.md` 只有在路徑、名稱或描述需要變更時才更新。
- 不新增平行的 CLI 專屬規則檔；若工具需要自己的入口，內容只允許轉接到 `AGENTS.md`。
- 所有 Markdown 使用正體中文；外部授權原文、產品名稱與 URL 除外。

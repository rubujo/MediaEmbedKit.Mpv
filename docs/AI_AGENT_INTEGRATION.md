# AI 代理整合規範

本專案採用 `AGENTS.md` 單一主要入口與薄型橋接設計。主要支援 Codex CLI、Claude Code、GitHub Copilot CLI 與 Google Antigravity CLI；過時的非目標工具設定與 GitHub Copilot 鏡像檔已移除，避免同一組規則被載入多次。

## 結構

| 位置 | 角色 |
| --- | --- |
| `AGENTS.md` | 唯一主要規則入口；Codex CLI、GitHub Copilot CLI 與 Google Antigravity CLI 可直接讀取。 |
| `CLAUDE.md` | Claude Code 橋接，以 `@AGENTS.md` 匯入主要入口。 |
| `.agents/skills/*/SKILL.md` | Codex CLI、GitHub Copilot CLI 與 Google Antigravity CLI 的跨工具技能發現檔。 |
| `.claude/skills/*/SKILL.md` | Claude Code 專案技能橋接檔。 |
| `docs/ai/skills/*.md` | 技能實際流程來源。 |
| `docs/ai/AGENT_GUIDE.md` | 舊路徑相容索引，不作為規則來源。 |

不保留 `.github/copilot-instructions.md`，因為 GitHub Copilot CLI 會同時讀取根目錄 `AGENTS.md` 與 `.github/copilot-instructions.md`；保留鏡像會造成規則重複。不保留 `GEMINI.md` 與 `.gemini/settings.json`，因為 Google Antigravity CLI 已支援 `AGENTS.md` 與 `.agents/skills`。

## 四工具能力矩陣

| 機制 | Codex CLI | Claude Code | GitHub Copilot CLI | Google Antigravity CLI |
| --- | :-: | :-: | :-: | :-: |
| 主要規則入口 | `AGENTS.md` | `CLAUDE.md` 匯入 `AGENTS.md` | `AGENTS.md` | `AGENTS.md` |
| 專案技能發現 | `.agents/skills/` | `.claude/skills/` | `.agents/skills/` | `.agents/skills/` |
| 共用技能流程指南 | `docs/ai/skills/*.md` | `docs/ai/skills/*.md` | `docs/ai/skills/*.md` | `docs/ai/skills/*.md` |
| 工具專屬橋接 | 不需要 | `CLAUDE.md`、`.claude/skills/` | 不需要 | 不需要 |

> `AGENTS.md` 是唯一主要規則來源。工具專屬入口只做必要橋接或技能發現，不放置平行規則。

## 技能

| 位置 | 角色 |
| --- | --- |
| `docs/ai/skills/mediaembedkit-mpv.md` | 本專案共用技能流程。 |
| `docs/ai/skills/libmpv-git-build-tracker.md` | 提供者 git build 與 libmpv 標頭追蹤流程。 |
| `.agents/skills/*/SKILL.md` | 跨工具主要技能發現檔。 |
| `.claude/skills/*/SKILL.md` | Claude Code 專案技能橋接檔。 |

每個 `SKILL.md` 必須使用獨立資料夾，並保留 YAML front matter 的 `name` 與 `description`。`description` 要清楚說明何時使用技能，讓 Codex CLI、Claude Code、GitHub Copilot CLI 與 Google Antigravity CLI 都能正確觸發。`SKILL.md` 本體只保留簡短標題與共用技能路徑，實際規則一律維護於 `docs/ai/skills/*.md`。

## 維護原則

- 修改跨工具入口時，只更新 `AGENTS.md`、`CLAUDE.md` 與本文件。
- 修改技能行為時，只更新 `docs/ai/skills/*.md`，再確認 `.agents/skills` 與 `.claude/skills` 的橋接檔仍指向正確文件。
- 不新增 `.github/copilot-instructions.md`、`GEMINI.md`、`.gemini/settings.json` 或 `.codex/skills`，除非官方文件改變且本文件同步更新。
- 不新增平行 CLI 專屬規則；必要入口只能轉接 `AGENTS.md` 或指向共用技能文件。

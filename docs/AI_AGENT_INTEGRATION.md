# AI Agent 整合規範

## 設計原則

本專案採用「單一主要入口、工具薄型橋接、共用 skill 內容」的結構。專案規則不得分散在各工具專屬檔案中重複維護。

## 入口檔

| 工具 | 入口檔 | 角色 |
| --- | --- | --- |
| Codex 與支援 AGENTS.md 的工具 | `AGENTS.md` | 主要入口 |
| Claude Code | `CLAUDE.md` | 匯入 `AGENTS.md` |
| Gemini CLI | `GEMINI.md` | 匯入 `AGENTS.md` |
| GitHub Copilot/Copilot CLI | `.github/copilot-instructions.md` | 指向 `AGENTS.md` 與共用指南 |

`docs/ai/AGENT_GUIDE.md` 是跨工具的工作規則來源。`AGENTS.md` 負責提供工具最容易發現的入口與文件索引。

## Skill 位置

| 位置 | 用途 |
| --- | --- |
| `docs/ai/skills/mediaembedkit-mpv.md` | 共用 skill 內容與專案工作規則 |
| `docs/ai/skills/libmpv-git-build-tracker.md` | 追蹤 shinchiro 與 zhongfly 最新 mpv git build 的共用流程 |
| `.agents/skills/mediaembedkit-mpv/SKILL.md` | agent 相容工具的可攜 skill 入口 |
| `.codex/skills/mediaembedkit-mpv/SKILL.md` | Codex 專案 skill 入口 |
| `.claude/skills/mediaembedkit-mpv/SKILL.md` | Claude Code 專案 skill 入口 |
| `.agents/skills/libmpv-git-build-tracker/SKILL.md` | provider git build 追蹤的可攜 skill 入口 |
| `.codex/skills/libmpv-git-build-tracker/SKILL.md` | Codex provider git build 追蹤入口 |
| `.claude/skills/libmpv-git-build-tracker/SKILL.md` | Claude Code provider git build 追蹤入口 |

工具專屬 `SKILL.md` 只保留最少 frontmatter 與轉接指示。詳細工作規則一律放在 `docs/ai/skills/*.md`，避免 Codex、Claude Code、Gemini CLI 與 Copilot CLI 之間產生不一致。

## 維護規則

- 修改專案支援目標、平台宣告、執行階段下載政策、授權政策或註解政策時，同步更新 `docs/PROJECT_SPEC.md` 與相關專責文件。
- 修改 AI agent 行為時，先更新 `docs/ai/AGENT_GUIDE.md`，再視需要調整工具入口檔。
- 修改 skill 內容時，先更新 `docs/ai/skills/*.md` 的共用內容，再確認 `.agents`、`.codex` 與 `.claude` 的 `SKILL.md` 仍只作為轉接。
- 不要在工具專屬檔案放置彼此衝突的規則。
- AI agent 代為提交時，必須依 `docs/ENGINEERING_STANDARDS.md` 使用慣例式提交，且提交訊息必須同時包含主旨與正文，不得使用一行式提交訊息；提交描述、正文與一般頁腳內容使用正體中文與臺灣地區用語。
- 所有 Markdown 檔案須使用正體中文；外部授權原文與來源連結除外。

# AI 代理最大公因數規範

本專案依 Codex CLI、Claude Code、GitHub Copilot CLI 與 Google Antigravity CLI 的官方文件，只保留四者共同可穩定使用的規則與技能結構。目標是降低重複入口、避免同一規則被載入多次，並移除非必要的歷史相容檔。

## 最大公因數

| 類型 | 保留結構 | 原因 |
| --- | --- | --- |
| 主要規則 | `AGENTS.md` | Codex CLI、GitHub Copilot CLI 與 Google Antigravity CLI 可直接讀取；Claude Code 可透過 `CLAUDE.md` 匯入。 |
| Claude Code 橋接 | `CLAUDE.md` | Claude Code 官方仍讀取 `CLAUDE.md`，且官方建議既有 `AGENTS.md` 專案用 `@AGENTS.md` 匯入。 |
| 跨工具技能 | `.agents/skills/*/SKILL.md` | Codex CLI、GitHub Copilot CLI 與 Google Antigravity CLI 共同支援的專案技能位置。 |
| Claude Code 技能 | `.claude/skills/*/SKILL.md` | Claude Code 官方專案技能位置；這是唯一必要的技能橋接副本。 |
| 技能正文 | `docs/ai/skills/*.md` | 兩個技能發現根目錄共用同一份實際流程，避免重複維護。 |

`SKILL.md` 的共通格式是獨立資料夾、`SKILL.md` 檔案、YAML front matter 與清楚的 `description`。本專案一律保留 `name` 與 `description`，讓 Codex CLI、Claude Code、GitHub Copilot CLI 與 Google Antigravity CLI 都能穩定辨識。

## 不保留項目

- 不保留 `.github/copilot-instructions.md`：GitHub Copilot CLI 會同時讀取根目錄 `AGENTS.md` 與此檔，保留會造成規則重複。
- 不保留 `GEMINI.md` 與 `.gemini/settings.json`：Google Antigravity CLI 已支援 `AGENTS.md` 與 `.agents/skills`，本專案不再以 Gemini CLI 作為支援目標。
- 不保留 `.codex/skills` 或 `.github/skills`：`.agents/skills` 已是 Codex CLI、GitHub Copilot CLI 與 Google Antigravity CLI 的共同位置。
- 不保留工具專屬 hooks、subagents、MCP 或 plugins 規範：四套工具的格式與行為不同，不屬於最大公因數。
- 不保留舊路徑索引文件：代理入口只從 `AGENTS.md` 與本文件說明，不再維護平行指南。

## 維護原則

- 修改專案規則時，只更新 `AGENTS.md` 與必要專責文件。
- 修改技能流程時，只更新 `docs/ai/skills/*.md`，並確認 `.agents/skills` 與 `.claude/skills` 的 `SKILL.md` 仍指向正確文件。
- 新增技能時，同步新增 `.agents/skills/<skill-name>/SKILL.md` 與 `.claude/skills/<skill-name>/SKILL.md`，兩者只放 front matter、簡短標題與共用技能路徑。
- 若官方文件新增真正共同入口，才重新評估是否移除現有橋接；不得為單一工具新增平行規則來源。

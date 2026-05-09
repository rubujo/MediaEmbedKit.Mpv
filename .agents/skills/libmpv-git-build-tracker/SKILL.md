---
name: libmpv-git-build-tracker
description: 追蹤 shinchiro/mpv-winbuild-cmake 與 zhongfly/mpv-winbuild 最新 release，對齊 MediaEmbedKit.Mpv 的 libmpv C API 包裝、runtime helper 與文件。用於檢查 provider git build、mpv commit、client.h/render.h/render_gl.h/stream_cb.h 差異與版本紀錄。
---

# libmpv git build 追蹤

此 skill 是跨 agent 工具的可攜入口。詳細規則一律使用儲存庫共用內容。

## 指示

- 先閱讀 `docs/ai/skills/libmpv-git-build-tracker.md`。
- 再閱讀 `docs/ai/AGENT_GUIDE.md`。
- 需要執行階段下載政策或 C API 覆蓋狀態時，閱讀 `docs/RUNTIME_ASSETS.md` 與 `docs/LIBMPV_C_API_TEST_MATRIX.md`。


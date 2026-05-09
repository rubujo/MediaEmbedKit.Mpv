# 技能：libmpv git build 追蹤

追蹤 shinchiro 與 zhongfly Windows mpv git build 時使用此共用 skill。此流程用來判斷最新 provider build 是否需要調整 `MediaEmbedKit.Mpv` 的 libmpv C API 包裝、runtime helper 或文件宣告。

## 讀取順序

- `docs/ai/AGENT_GUIDE.md`：專案共用規則。
- `docs/RUNTIME_ASSETS.md`：provider、下載與更新政策。
- `docs/LIBMPV_C_API_TEST_MATRIX.md`：C API 覆蓋與驗證矩陣。
- `docs/runtime/libmpv-git-builds.json`：最近一次對齊的 provider release 與 mpv commit。

## 查核流程

1. 以 GitHub API 解析最新 release：
   - shinchiro：`https://api.github.com/repos/shinchiro/mpv-winbuild-cmake/releases/latest`
   - zhongfly：`https://api.github.com/repos/zhongfly/mpv-winbuild/releases/latest`
2. 從 release body 或 `mpv-*.7z` / `mpv-dev-*.7z` asset 名稱解析 mpv commit。
3. 使用 `tools/libmpv/Compare-LibMpvHeaders.ps1` 比對 `v0.41.0`、shinchiro commit 與 zhongfly commit 的 `client.h`、`render.h`、`render_gl.h`、`stream_cb.h`。
4. 若新增或移除公開匯出函式、列舉值、旗標或資料結構，先更新 `src/MediaEmbedKit.Mpv/Native` 與對應受控型別，再更新測試矩陣。
5. 若只有註解或語意變更，更新高階 API 與文件，不要新增不必要的 P/Invoke。
6. 使用 `tools/libmpv/Update-LibMpvGitBuildManifest.ps1` 更新 `docs/runtime/libmpv-git-builds.json`。

## 判讀規則

- provider release 是 git build，不是 mpv stable release。stable v0.41.0 仍是本專案 C API 100% 覆蓋基準。
- provider commit 的 header 若與 stable 相容，可宣稱「已對齊最新 provider git build 的公開 C API」；不得因此宣稱所有播放情境皆已驗證。
- `mpv_command_node` 的具名命令 map 應優先使用 `_name` 作為命令名稱欄位，避免和命令本身的 `name` 引數衝突。
- `mpv_wait_async_requests` 的註解若只移除 suspend counter 語意，不需要改 P/Invoke；需避免在高階 API 文件中宣稱會重設 suspend counter。

## 建議命令

```powershell
.\tools\libmpv\Resolve-MpvGitBuild.ps1 -Provider Shinchiro
.\tools\libmpv\Resolve-MpvGitBuild.ps1 -Provider Zhongfly
.\tools\libmpv\Compare-LibMpvHeaders.ps1 -TargetCommit 5921fe5
.\tools\libmpv\Compare-LibMpvHeaders.ps1 -TargetCommit e0eb42c303
dotnet format .\MediaEmbedKit.Mpv.slnx --no-restore
dotnet build .\MediaEmbedKit.Mpv.slnx --no-restore
```


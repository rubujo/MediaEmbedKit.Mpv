# 技能：libmpv git build 追蹤

本 skill 用於追蹤 shinchiro 與 zhongfly Windows mpv git build，並判斷是否需要調整 libmpv C API 包裝、runtime helper 或文件。

## 讀取順序

1. `AGENTS.md`
2. `docs/RUNTIME_ASSETS.md`
3. `docs/LIBMPV_C_API_TEST_MATRIX.md`
4. `docs/runtime/libmpv-git-builds.json`

## 查核流程

1. 使用 GitHub API 解析最新 release。
2. 從 release body 或 asset 名稱解析 mpv commit。
3. 使用 `tools/libmpv/Compare-LibMpvHeaders.ps1` 比對 `client.h`、`render.h`、`render_gl.h` 與 `stream_cb.h`。
4. 若公開匯出函式、列舉、旗標或資料結構異動，先更新 `src/MediaEmbedKit.Mpv/Native` 與受控型別，再更新測試矩陣。
5. 若只有註解或語意說明異動，更新高階 API 文件，不新增不必要的 P/Invoke。
6. 使用 `tools/libmpv/Update-LibMpvGitBuildManifest.ps1` 更新 provider 對齊紀錄。
7. 使用 `tools/libmpv/Sync-ProviderDocs.ps1` 把 catalog 的 build / 查核日期同步到下游事實宣告型文件（`LIBMPV_C_API_TEST_MATRIX.md`、`HIGH_LEVEL_API.md`、`REFERENCE_SOURCES.md`）。release workflow（`release.yml`）已透過 `Invoke-PreReleaseValidation.ps1 -IncludeDocSyncCheck` 自動跑 `-Check` 模式當 drift gate；本地 tag push 前可手動 `pwsh tools/libmpv/Sync-ProviderDocs.ps1 -Check` 預演。

## 判讀規則

- provider release 是 git build，不是 mpv stable release。
- stable v0.41.0 仍是本專案 C API 覆蓋基準。
- provider header 相容時，只能宣稱已對齊最新 provider git build 公開 C API。
- 不得因 header 比對通過而宣稱所有播放情境皆已驗證。

## 建議命令

```powershell
.\tools\libmpv\Resolve-MpvGitBuild.ps1 -Provider Shinchiro
.\tools\libmpv\Resolve-MpvGitBuild.ps1 -Provider Zhongfly
.\tools\libmpv\Compare-LibMpvHeaders.ps1 -TargetCommit 5921fe5
.\tools\libmpv\Compare-LibMpvHeaders.ps1 -TargetCommit e0eb42c303
dotnet format .\MediaEmbedKit.Mpv.slnx --no-restore
dotnet build .\MediaEmbedKit.Mpv.slnx --no-restore
```

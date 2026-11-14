# Direct3D 11 實際繪製驗證

查核日期：2026-11-14。環境：Windows x64、.NET SDK 10.0.404。

## 結果

| 提供者 | 資產 | 實際 mpv 版本 | 硬體裝置 | WARP |
| --- | --- | --- | --- | --- |
| shinchiro | `mpv-dev-x86_64-20261111-git-bcde6b93cc.7z` | `v0.42.0-38-gbcde6b93c` | 通過 | 通過 |
| zhongfly | `mpv-dev-lgpl-x86_64-20261113-git-fe753a7824.7z` | `v0.42.0-39-gfe753a782` | 通過 | 通過 |

兩者均回報 C API 2.6，兩種裝置均使用功能層級 11.0。下載封存檔先與對應 GitHub Releases API 的 SHA-256 digest 比對，再解壓縮。

| 提供者 | 封存檔 SHA-256 | 載入的 DLL SHA-256 |
| --- | --- | --- |
| shinchiro | `273c079b11ea5575822f972756f4c9a71149a5c10130353abeb8586738938452` | `88504ee5560942a955405e99dfb5971368e4cf851321f36565bf2a667271db1b` |
| zhongfly | `d4f9e4ae675685d1a35d926aac65573cc05e71ea7391c344a91f325f73e4386e` | `f1f58e018a1560f6918a2a491c746dd5fee68eca1a2ec7e9831778751f861155` |

## 視窗呈現與硬體解碼

追加驗證已通過：兩家建置在硬體及 WARP 裝置都以 DXGI 交換鏈呈現至可見視窗，並在桌面合成完成後讀取實際視窗中心像素確認紅、藍色彩。硬體裝置另載入 6 秒 H.264 影片，前三秒為紅色、後三秒為藍色；每個計數影格均確認 hwdec-current 為 d3d11va，再繪製、呈現並讀回影像。shinchiro 記錄 92 個影格、zhongfly 記錄 91 個影格，播放時間均跨越紅、藍片段。

4 組測試的 libmpv 警告與錯誤計數皆為 0；測試開始先將結果標為未通過，避免後續失敗時誤讀舊的成功紀錄。WARP 僅驗證呈現，不宣稱具備硬體解碼。

## 方法與重現

`D3D11RenderProbe` 直接使用既有 libmpv 原生包裝及新增結構，建立 Direct3D 11 裝置、64 × 64 BGRA 紋理及 CPU 可讀紋理。每個裝置連續載入純紅、純藍 PPM，將 libmpv 輸出複製至 CPU 可讀紋理，確認中心 32 × 32 像素的預期色彩通道大於 200，其餘通道小於 40。第二張影像能排除只讀取前一張殘留內容的情況；逾時、錯誤或不支援皆視為失敗，不會略過。

每份執行階段須於獨立處理序執行，避免已載入的 DLL 被重用：

```powershell
$env:MEDIAEMBEDKIT_MPV_RUNTIME_DIR = (Resolve-Path .\artifacts\d3d11-runtime\shinchiro).Path
dotnet run --project .\tests\MediaEmbedKit.Mpv.IntegrationTests\MediaEmbedKit.Mpv.IntegrationTests.csproj -- --d3d11-only
$env:MEDIAEMBEDKIT_MPV_RUNTIME_DIR = (Resolve-Path .\artifacts\d3d11-runtime\zhongfly).Path
dotnet run --project .\tests\MediaEmbedKit.Mpv.IntegrationTests\MediaEmbedKit.Mpv.IntegrationTests.csproj -- --d3d11-only
Remove-Item Env:MEDIAEMBEDKIT_MPV_RUNTIME_DIR
```

指定資料夾應先放入表列 DLL；一般整合測試預設仍可使用 C API 2.5，因此此要求 C API 2.6 的探測使用明確選項執行。每個資料夾的 `d3d11-probe/hardware` 與 `d3d11-probe/warp` 內保留 `result.json`、來源 PPM 及 `*-readback.ppm`。結果包含實際版本、功能層級、C API 版本與 DLL SHA-256。

## 完整驗證重現

先準備測試影片，再分別執行兩家執行階段：

```powershell
ffmpeg -hide_banner -loglevel error -y -f lavfi -i "color=c=red:s=64x64:r=30:d=3" -f lavfi -i "color=c=blue:s=64x64:r=30:d=3" -filter_complex "[0:v][1:v]concat=n=2:v=1:a=0[v]" -map "[v]" -c:v libx264 -pix_fmt yuv420p -crf 18 .\artifacts\d3d11-runtime\red-blue-h264.mp4
$env:MEDIAEMBEDKIT_MPV_D3D11_VIDEO = (Resolve-Path .\artifacts\d3d11-runtime\red-blue-h264.mp4).Path
$env:MEDIAEMBEDKIT_MPV_RUNTIME_DIR = (Resolve-Path .\artifacts\d3d11-runtime\shinchiro).Path
dotnet run --project .\tests\MediaEmbedKit.Mpv.IntegrationTests\MediaEmbedKit.Mpv.IntegrationTests.csproj -- --d3d11-full
$env:MEDIAEMBEDKIT_MPV_RUNTIME_DIR = (Resolve-Path .\artifacts\d3d11-runtime\zhongfly).Path
dotnet run --project .\tests\MediaEmbedKit.Mpv.IntegrationTests\MediaEmbedKit.Mpv.IntegrationTests.csproj -- --d3d11-full
Remove-Item Env:MEDIAEMBEDKIT_MPV_RUNTIME_DIR, Env:MEDIAEMBEDKIT_MPV_D3D11_VIDEO
```

`--d3d11-full` 要求可見桌面與支援 H.264 硬體解碼的裝置；不支援、逾時、軟體解碼備援或任何 libmpv 警告與錯誤均導致測試失敗。原有 `--d3d11-only` 保留為純離屏紋理檢查。

## 範圍限制

本次涵蓋軟體解碼靜態影像、H.264 d3d11va 硬體解碼、連續影格繪製、交換鏈呈現及實際視窗像素確認。不涵蓋其他編解碼器、HDR、跨裝置同步或既有 UI 控制項整合。ARM64 因本機無環境，依使用者指示略過；既有 UI 後端與公開高階 API 不因本測試改變。

## 同次工程驗證

- `dotnet build .\MediaEmbedKit.Mpv.slnx -m:1 -nr:false`：成功，0 個警告、0 個錯誤，涵蓋完整方案與 MAUI Windows 範例及測試專案。
- 單元測試：49 項通過，0 項失敗。
- 一般整合測試：使用表列 shinchiro 執行階段，50 項通過，0 項失敗。
- `dotnet format .\MediaEmbedKit.Mpv.slnx --no-restore`：結束碼 0；Hosting 各目標產生參考組件後，工作區載入警告已排除。
- 提供者文件同步檢查與 `git diff --check`：通過。

- 發佈前檢查另涵蓋 5 套 UI 無頭測試、NuGet 套件內容及乾淨使用端建置；ARM64 實機依指示略過。最終檢查使用 ContinuousIntegrationBuild=true，啟用警告視為錯誤。

最終發佈前檢查全部通過，紀錄 artifacts/d3d11-runtime/pre-release-clean.log 經掃描無任何警告與錯誤。

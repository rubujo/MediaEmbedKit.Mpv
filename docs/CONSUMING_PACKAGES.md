# 從 GitHub Releases 安裝本機 NuGet 套件

本專案 **不發行到 nuget.org**。所有 `.nupkg` 與 `.snupkg`（符號套件）改由 GitHub Releases 提供，使用端自行下載後，以本機 NuGet 套件來源安裝。本文件說明從零開始的完整流程。

如果你需要產生套件（maintainer 端）請改參考 `docs/RELEASE_CHECKLIST.md`。

## 為什麼不上 nuget.org

- 本專案的散發政策（見 README）強調受控原始碼採 CC0-1.0、第三方原生二進位不簽入、由 輔助工具於執行階段下載。發行到 nuget.org 會把這個責任邊界模糊化。
- 早期版本（1.0 之前）公開 API 仍可能小幅調整，鎖在 GitHub Releases 比較容易在出現嚴重問題時撤回。
- 1.0 之後若決定上 nuget.org，本文件的本機套件來源 流程仍然可用，會作為「pinning 特定版本」的退路。

## 套件總覽

每個 release tag 會上傳 11 個 `.nupkg` 與對應的 11 個 `.snupkg`，分為 服務層、UI 層、與 meta 三類：

### 服務層（5 個）

| Package | 用途 | TFM |
|---|---|---|
| `MediaEmbedKit.Mpv` | 核心 libmpv binding、高階 `MpvPlayer` / `MpvAppBuilder` / `MpvEncoder`、value types、events | `netstandard2.0;net472;net48;net10.0` |
| `MediaEmbedKit.Mpv.Externals` | FFmpeg / Deno / yt-dlp 下載器 + 共用網路基礎設施 | `netstandard2.0;net472;net48;net10.0` |
| `MediaEmbedKit.Mpv.Runtime` | libmpv Windows 執行階段 安裝器 + 4-tier 壓縮檔解壓 + 跨處理序鎖定 | `netstandard2.0;net472;net48;net10.0` |
| `MediaEmbedKit.Mpv.Diagnostics` | `MpvLicenseAuditor` / `MpvRuntimeHealthCheck` / `MpvLibraryUpdateScheduler` | `netstandard2.0;net472;net48;net10.0` |
| `MediaEmbedKit.Mpv.Hosting` | `Microsoft.Extensions.DependencyInjection` 整合（`IMpvPlayerFactory` / `AddMpvPlayerFactory`） | `netstandard2.0;net472;net48;net10.0` |

### UI 層（5 個）

| Package | 用途 | TFM |
|---|---|---|
| `MediaEmbedKit.Mpv.WinForms` | WinForms 控制項 | `net472;net48;net10.0-windows` |
| `MediaEmbedKit.Mpv.Wpf` | WPF `HwndHost` 控制項 + AirSpace 覆蓋層 | `net472;net48;net10.0-windows` |
| `MediaEmbedKit.Mpv.Avalonia` | Avalonia OpenGL render API 控制項 | `net10.0-windows` |
| `MediaEmbedKit.Mpv.WinUI` | WinUI 3 控制項 | `net10.0-windows10.0.19041.0` |
| `MediaEmbedKit.Mpv.Maui.Windows` | .NET MAUI Windows handler（橋接 WinUI）。**Windows-only** — 套件名自帶 `.Windows` 明示 scope | `net10.0-windows10.0.19041.0` |

### Meta（1 個）

| Package | 用途 | TFM |
|---|---|---|
| `MediaEmbedKit.Mpv.Full` | `<PackageReference>` 拉入 5 個服務層套件（不含 UI 套件，因 TFM 分散） | `netstandard2.0;net472;net48;net10.0` |

**只裝你需要的**：依賴鏈會自動拉入相依套件 —— 例如裝 `MediaEmbedKit.Mpv.WinForms` 會自動拉入 `MediaEmbedKit.Mpv`；裝 `MediaEmbedKit.Mpv.Diagnostics` 會自動拉入 `.Mpv` / `.Externals` / `.Runtime`。

> **本文件以 `<version>` 作為版號 placeholder**；複製範例時請替換為實際版號。目前 `Directory.Build.props` 的 `<PackageVersion>` 決定 標籤名稱與 nupkg 檔名（例：`PackageVersion=0.0.1` → tag `v0.0.1` → `MediaEmbedKit.Mpv.0.0.1.nupkg`）。升版時用 `tools/Bump-Version.ps1` 一鍵改全部。

## 步驟 1：下載

到儲存庫的 [GitHub Releases](https://github.com/rubujo/MediaEmbedKit.Mpv/releases) 頁面，挑選想要的標籤（例如 `v<version>`），把 `.nupkg` 與 `.snupkg` 一併下載。建議下載**所有 11 個套件**：用不到的留著也沒成本，未來想加 UI 框架或 .Diagnostics 等服務套件就不用再回頭下載。

`.snupkg` 是符號套件，IDE 除錯時會自動展開 PDB 與原始碼導航（Source Link 已在發行檢查閘門配置好）。可以選擇不下載，但有的話除錯體驗會明顯好很多。

## 步驟 2：建立本機套件來源 資料夾

挑一個固定位置放下載的檔案；任何資料夾都可以，但要選**不會被 IDE / 建置工具清掉**的地方。扁平版面配置 範例（`<version>` 替換為實際版號）：

```text
C:\NuGet\MediaEmbedKit.Mpv\
├── MediaEmbedKit.Mpv.<version>.nupkg
├── MediaEmbedKit.Mpv.<version>.snupkg
├── MediaEmbedKit.Mpv.Externals.<version>.nupkg
├── MediaEmbedKit.Mpv.Externals.<version>.snupkg
├── MediaEmbedKit.Mpv.Runtime.<version>.nupkg
├── MediaEmbedKit.Mpv.Runtime.<version>.snupkg
├── MediaEmbedKit.Mpv.Diagnostics.<version>.nupkg
├── MediaEmbedKit.Mpv.Diagnostics.<version>.snupkg
├── MediaEmbedKit.Mpv.Hosting.<version>.nupkg
├── MediaEmbedKit.Mpv.Hosting.<version>.snupkg
├── MediaEmbedKit.Mpv.WinForms.<version>.nupkg
├── MediaEmbedKit.Mpv.WinForms.<version>.snupkg
├── MediaEmbedKit.Mpv.Wpf.<version>.nupkg
├── MediaEmbedKit.Mpv.Wpf.<version>.snupkg
├── MediaEmbedKit.Mpv.Avalonia.<version>.nupkg
├── MediaEmbedKit.Mpv.Avalonia.<version>.snupkg
├── MediaEmbedKit.Mpv.WinUI.<version>.nupkg
├── MediaEmbedKit.Mpv.WinUI.<version>.snupkg
├── MediaEmbedKit.Mpv.Maui.Windows.<version>.nupkg
├── MediaEmbedKit.Mpv.Maui.Windows.<version>.snupkg
├── MediaEmbedKit.Mpv.Full.<version>.nupkg
└── MediaEmbedKit.Mpv.Full.<version>.snupkg
```

要支援多版本並存：放在 `C:\NuGet\MediaEmbedKit.Mpv\` 根目錄就好，NuGet 會自己以檔名解析版本。**不要**手動分子資料夾（如 `<version>/`）—— 那是 hierarchical layout，需要不同的 source 設定，本流程用扁平最簡單。

## 步驟 3：把資料夾註冊成 NuGet source

兩種方式擇一。

### 方式 A：solution 級 `nuget.config`（推薦）

在方案根目錄建 `nuget.config`（與 `.sln` / `.slnx` 同層）：

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
    <add key="MediaEmbedKit.Mpv (local)" value="C:\NuGet\MediaEmbedKit.Mpv" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="MediaEmbedKit.Mpv (local)">
      <package pattern="MediaEmbedKit.Mpv*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
```

優點：

- 可 提交到你的儲存庫，團隊共享同一份設定，新成員 clone 後直接能用（路徑記得改成相對或環境變數，例如 `value="$(NuGetLocalFeed)"` 搭配環境變數）
- `<packageSourceMapping>` 把 `MediaEmbedKit.Mpv*` 明確綁到本機套件來源，避免 NuGet 在多個 source 之間繞遠路；其他套件 仍走 nuget.org

如果 套件來源之中已有同名 套件來源，記得保留 `<clear />` 避免機器級 `nuget.config` 污染。

### 方式 B：全機層級 一次性註冊

```powershell
dotnet nuget add source "C:\NuGet\MediaEmbedKit.Mpv" --name "MediaEmbedKit.Mpv (local)"
```

優點：簡單一行；缺點：全機層級，其他專案也看得到（通常不是問題，但別用在 CI 映像檔 等共享環境）。

## 步驟 4：在專案安裝套件

在你的 `.csproj` 加 `<PackageReference>`：

```xml
<ItemGroup>
  <!-- 依需要選裝；UI 套件會自動拉入核心 .Mpv，.Runtime 會拉入 .Externals + .Mpv -->

  <!-- UI 框架（依你用的 UI 二擇一或多選） -->
  <PackageReference Include="MediaEmbedKit.Mpv.Wpf" Version="<version>" />
  <PackageReference Include="MediaEmbedKit.Mpv.WinForms" Version="<version>" />
  <PackageReference Include="MediaEmbedKit.Mpv.Avalonia" Version="<version>" />
  <PackageReference Include="MediaEmbedKit.Mpv.WinUI" Version="<version>" />
  <PackageReference Include="MediaEmbedKit.Mpv.Maui.Windows" Version="<version>" />

  <!-- 想要 輔助工具自動下載 libmpv / yt-dlp / Deno / FFmpeg -->
  <PackageReference Include="MediaEmbedKit.Mpv.Runtime" Version="<version>" />

  <!-- 商用合規與健康檢查（需要 LicenseAuditor / HealthCheck / UpdateScheduler 才裝） -->
  <PackageReference Include="MediaEmbedKit.Mpv.Diagnostics" Version="<version>" />

  <!-- DI 整合（需要 IMpvPlayerFactory / AddMpvPlayerFactory 才裝） -->
  <PackageReference Include="MediaEmbedKit.Mpv.Hosting" Version="<version>" />

  <!-- 一次裝 5 個 service 套件的 meta（UI 仍需另加） -->
  <PackageReference Include="MediaEmbedKit.Mpv.Full" Version="<version>" />
</ItemGroup>
```

或用 CLI：

```powershell
dotnet add package MediaEmbedKit.Mpv.Wpf --version <version>
```

**alpha / preview 版本注意：** 若使用 `0.x.y-alpha.z` 等含 pre-release 後綴 的版號，預設 `dotnet add package` 在不指定 `--version` 時會跳過。請 **明確帶 `--version`** 或加 `--prerelease`。

```powershell
dotnet restore  # 若 IDE 沒自動觸發
```

成功的話 `dotnet restore` 會從本機套件來源解到對應的 .nupkg，不會打 nuget.org 找 MediaEmbedKit.Mpv（因為 packageSourceMapping 已經綁定）。

## 驗證安裝

```powershell
dotnet list package
```

應該會列出你加的 `MediaEmbedKit.Mpv.*` package 與解析到的版本。如果版本是空白或顯示 "（不適用）"，通常是 套件來源沒被找到 — 回 `nuget.config` 確認路徑。

## IDE 整合

| IDE | 行為 |
|---|---|
| **Visual Studio 2022 / 2026** | 自動讀 `nuget.config`（solution / project / 機器層級）。NuGet 套件管理員的「套件來源」下拉會顯示 `MediaEmbedKit.Mpv (local)`。 |
| **Rider** | 同 VS，自動讀 `nuget.config`。Preferences → NuGet → Sources 也能驗證。 |
| **VS Code（C# Dev Kit）** | 透過 `dotnet` CLI 解析，直接吃 `nuget.config`。 |

## Source Link 與符號套件

發行檢查閘門已對所有 11 個可打包專案 配置 Source Link 與 `.snupkg`。本機套件來源安裝後：

- **F12 / Go to Definition** 會看到我們的 C# 原始碼（不只是 metadata）
- **Step Into** 進函式時 IDE 會自動下載／開啟對應 commit 的原始碼
- 前提：`.snupkg` 跟 `.nupkg` **同目錄**（步驟 2 的扁平版面配置 就符合）

Visual Studio / JetBrains Rider 預設啟用 Source Link；如果跳不進去，檢查 IDE 設定的「Enable source server support」與「Enable Source Link support」。

## 升級到新版本

新標籤建立後：

1. 從 GitHub Releases 下載新版的 11 個 `.nupkg`（與 `.snupkg`）
2. **丟進同一個資料夾**（不要刪舊版，扁平版面配置 允許多版本共存）
3. 在 `.csproj` 把 `Version="<舊版號>"` 改成新版號
4. `dotnet restore`

NuGet local feed 不會自動「拉新版」— 因為它沒有上游可以查；版號要 使用端自己更新。

## 限制與已知坑

- **本機套件來源沒有列舉 API**：`dotnet list package --outdated` 不會回報新版可用，因為 NuGet 不會掃 GitHub Releases 看有沒有新版 .nupkg。要追新版只能手動看 [Releases 頁](https://github.com/rubujo/MediaEmbedKit.Mpv/releases)。
- **多人共用本機套件來源**：每個開發者各自下載，或用 SMB / 網路共享資料夾再 `value="\\server\share\NuGet\MediaEmbedKit.Mpv"`。後者要注意網路延遲對 `dotnet restore` 的影響。
- **CI 環境**：別在 CI runner 上手動裝；CI 通常會自己用 GitHub Actions cache 或成品機制把 .nupkg 帶進來再加 source。若要在 GitHub Actions 上用，可以 `actions/download-artifact` 或 `gh release download` 抓取發行成品 後再 `dotnet nuget add source`。
- **`packageSourceCredentials`**：本機套件來源不需要驗證；如果你看到 NuGet 跳出帳密對話框，多半是其他 source 設錯，跟本機套件來源無關。

## 相關文件

- `docs/RELEASE_CHECKLIST.md`：maintainer 端如何產生 .nupkg
- `docs/HIGH_LEVEL_API.md`：安裝後怎麼使用高階 API
- `docs/CONTROLS_API.md`：UI 套件的控制項繫結屬性
- `docs/RUNTIME_ASSETS.md`：libmpv 與外部工具 執行階段取得政策

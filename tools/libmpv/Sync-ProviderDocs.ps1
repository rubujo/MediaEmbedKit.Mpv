#requires -Version 7.0
<#
.SYNOPSIS
從 docs/runtime/libmpv-git-builds.json 同步 provider build / 查核日期到下游文件。

.DESCRIPTION
catalog（docs/runtime/libmpv-git-builds.json）是 provider build 狀態的單一事實
來源。下列下游文件含有「目前對齊到哪個 release / 哪個 mpv commit / catalog 何時
最後查核」的事實宣告，每次 provider bump 都需手動同步，容易漂移。本腳本以
catalog 為來源端，用 regex 錨點更新這些固定位置。

同步範圍（4 處事實宣告）：
- docs/LIBMPV_C_API_TEST_MATRIX.md  「provider 對齊」表格列、render.h 比對段
- docs/HIGH_LEVEL_API.md             encoder 可用編碼器基線敘述
- docs/REFERENCE_SOURCES.md          最後查核日期

刻意不同步（屬最低版本門檻 / 歷史 sample，無關當前狀態）：
- docs/HIGH_LEVEL_API.md:385         SVT-AV1 patch 內含條件「20260421+」(≥ 語意)
- docs/ai/skills/libmpv-git-build-tracker.md  Compare-LibMpvHeaders 命令範例

呼叫端：
- 跑完 Update-LibMpvGitBuildManifest.ps1 後執行本腳本同步下游文件。
- CI / release gate 可用 -Check 模式驗證同步狀態，未同步時退出非零。

.PARAMETER Check
僅驗證下游文件是否已同步 catalog；不修改檔案，未同步時以非零代碼結束。

.EXAMPLE
pwsh tools/libmpv/Sync-ProviderDocs.ps1
pwsh tools/libmpv/Sync-ProviderDocs.ps1 -Check
#>
[CmdletBinding()]
param(
    [switch]$Check
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '../..')
$catalogPath = Join-Path $repoRoot 'docs/runtime/libmpv-git-builds.json'

if (-not (Test-Path -LiteralPath $catalogPath)) {
    throw "catalog 不存在：$catalogPath"
}

$catalog = Get-Content -LiteralPath $catalogPath -Raw | ConvertFrom-Json
$shinchiro = $catalog.providers | Where-Object { $_.name -eq 'Shinchiro' }
$zhongfly  = $catalog.providers | Where-Object { $_.name -eq 'Zhongfly' }

if (-not $shinchiro) { throw 'catalog 缺少 Shinchiro provider 條目。' }
if (-not $zhongfly)  { throw 'catalog 缺少 Zhongfly provider 條目。' }

# zhongfly releaseTag 格式為 YYYY-MM-DD-<commit10>；HIGH_LEVEL_API encoder 基線
# 敘述只用日期前綴。若上游改格式，這裡會明確失敗而非寫出錯誤內容。
if ($zhongfly.releaseTag -notmatch '^(\d{4}-\d{2}-\d{2})-') {
    throw "zhongfly releaseTag 格式不符預期（YYYY-MM-DD-<commit>）：$($zhongfly.releaseTag)"
}
$zhongflyDate = $Matches[1]

# 描述「兩 provider 對 mpv commit 對齊狀況」的句子。自動處理「同一 commit」與
# 「不同 commit」兩種情況，省得每次 provider 分歧/匯流時調手寫敘述。
$bt = [char]0x60  # 反引號（markdown 行內碼框）
$builtPhrase = if ($shinchiro.mpvCommit -eq $zhongfly.mpvCommit) {
    "shinchiro 與 zhongfly 的 git build（皆對齊到 mpv $bt$($shinchiro.mpvCommit)$bt）"
} else {
    "shinchiro git build $bt$($shinchiro.mpvCommit)$bt 與 zhongfly git build $bt$($zhongfly.mpvCommit)$bt"
}

# 每個 edit 條目：
#   Path        — 相對 repoRoot 的目標檔
#   Pattern     — 用以定位「整行內容」的 .NET regex；以 (?m)^ 起頭、用 [^\r\n]+
#                 收尾，避免吃到 CRLF/LF 結尾。
#   Replacement — 取代為的行內容（不含換行）。
$edits = @(
    @{
        Path        = 'docs/LIBMPV_C_API_TEST_MATRIX.md'
        Pattern     = '(?m)^\| provider 對齊 \|[^\r\n]+'
        Replacement = "| provider 對齊 | shinchiro $bt$($shinchiro.releaseTag)$bt / mpv $bt$($shinchiro.mpvCommit)$bt；zhongfly $bt$($zhongfly.releaseTag)$bt / mpv $bt$($zhongfly.mpvCommit)$bt |"
    }
    @{
        Path        = 'docs/LIBMPV_C_API_TEST_MATRIX.md'
        Pattern     = '(?m)^已比對[^\r\n]+未發現相對 stable v0\.41\.0 的公開 header 形狀差異。'
        Replacement = "已比對 $builtPhrase，未發現相對 stable v0.41.0 的公開 header 形狀差異。"
    }
    @{
        Path        = 'docs/HIGH_LEVEL_API.md'
        Pattern     = '(?m)^可用編碼器（依 shinchiro \S+ / zhongfly \S+ build）：'
        Replacement = "可用編碼器（依 shinchiro $($shinchiro.releaseTag) / zhongfly $zhongflyDate build）："
    }
    @{
        Path        = 'docs/REFERENCE_SOURCES.md'
        Pattern     = '(?m)^本文件列出專案規範採用的主要來源。最後查核日期：\d{4}-\d{2}-\d{2}。'
        Replacement = "本文件列出專案規範採用的主要來源。最後查核日期：$($catalog.lastCheckedDate)。"
    }
)

$utf8NoBom = New-Object System.Text.UTF8Encoding $false
$driftFound = $false

foreach ($edit in $edits) {
    $fullPath = Join-Path $repoRoot $edit.Path
    if (-not (Test-Path -LiteralPath $fullPath)) {
        throw "目標文件不存在：$($edit.Path)"
    }

    $original = [System.IO.File]::ReadAllText($fullPath, [System.Text.Encoding]::UTF8)

    if ($original -notmatch $edit.Pattern) {
        throw "在 $($edit.Path) 找不到預期錨點。若該行已被改寫，請更新本腳本對應的 Pattern。Pattern: $($edit.Pattern)"
    }

    # PowerShell -replace 會把替換字串中的 `$` 視為反向參照。catalog 欄位（tag /
    # commit / date / ISO 時戳）實務上不含 `$`，但保險起見明確 guard，避免未來新欄位
    # 引入靜默誤替換。
    if ($edit.Replacement -match '\$') {
        throw "Replacement 含字面 `$`，需手動跳脫成 `$$` 才能安全用 -replace。Replacement: $($edit.Replacement)"
    }
    $updated = $original -replace $edit.Pattern, $edit.Replacement

    if ($updated -eq $original) {
        Write-Host "[ok]      $($edit.Path)"
        continue
    }

    if ($Check) {
        $driftFound = $true
        Write-Warning "[drift]   $($edit.Path)"
        Write-Warning "  expected line: $($edit.Replacement)"
        continue
    }

    [System.IO.File]::WriteAllText($fullPath, $updated, $utf8NoBom)
    Write-Host "[updated] $($edit.Path)"
}

if ($Check -and $driftFound) {
    Write-Error 'provider 下游文件與 catalog 不同步；請執行：pwsh tools/libmpv/Sync-ProviderDocs.ps1'
    exit 1
}

<#
.SYNOPSIS
    定期檢查 shinchiro / zhongfly Windows libmpv git build 的公開 header 是否相對
    stable baseline 有變動，協助維護者決定是否需要更新 P/Invoke 對齊。

.DESCRIPTION
    本腳本串接 Resolve-MpvGitBuild.ps1（解析最新 provider release → mpv commit）
    與 Compare-LibMpvHeaders.ps1（對 stable baseline 與 target commit 做 client.h /
    render.h / render_gl.h / stream_cb.h diff），輸出人類可讀的 drift 報告。

    用途：建議每月或每季跑一次。連續多次「無差異」可建立信心；偵測到 header diff
    時手動評估是否需要更新 managed P/Invoke 對齊與 docs/runtime/libmpv-git-builds.json。

.PARAMETER BaseCommit
    stable baseline 的 mpv commit / tag；預設為 v0.41.0。

.PARAMETER OutputDirectory
    暫存下載 header 與 diff 報告的資料夾；預設為 artifacts/libmpv-api-check。

.PARAMETER ForceDownload
    指定後即使本地已有 header 副本仍重新下載。

.PARAMETER UserAgent
    HTTP User-Agent 字串。

.PARAMETER ExitCodeOnDrift
    偵測到任何 header 差異時要回傳的結束代碼；預設 0（僅報告不失敗）。
    在 CI / scheduled job 中可設為非 0 以觸發 alert。

.EXAMPLE
    pwsh tools/libmpv/Check-LibMpvHeaderDrift.ps1

.EXAMPLE
    pwsh tools/libmpv/Check-LibMpvHeaderDrift.ps1 -ExitCodeOnDrift 1
#>
param(
    [string] $BaseCommit = 'v0.41.0',
    [string] $OutputDirectory = 'artifacts/libmpv-api-check',
    [switch] $ForceDownload,
    [string] $UserAgent = 'MediaEmbedKit-Mpv-Agent/1.0',
    [int] $ExitCodeOnDrift = 0
)

$ErrorActionPreference = 'Stop'
$scriptDirectory = Split-Path -Parent $PSCommandPath
$resolveScript = Join-Path $scriptDirectory 'Resolve-MpvGitBuild.ps1'
$compareScript = Join-Path $scriptDirectory 'Compare-LibMpvHeaders.ps1'

if (-not (Test-Path -LiteralPath $resolveScript)) {
    throw "找不到必要的子腳本：$resolveScript"
}
if (-not (Test-Path -LiteralPath $compareScript)) {
    throw "找不到必要的子腳本：$compareScript"
}

function Invoke-ProviderCheck {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('Shinchiro', 'Zhongfly')]
        [string] $Provider
    )

    Write-Host ""
    Write-Host "=== Provider: $Provider ===" -ForegroundColor Cyan

    $resolveOutput = & $resolveScript -Provider $Provider -UserAgent $UserAgent
    $resolved = $resolveOutput | ConvertFrom-Json
    Write-Host "  releaseTag : $($resolved.releaseTag)"
    Write-Host "  publishedAt: $($resolved.publishedAt)"
    Write-Host "  mpvCommit  : $($resolved.mpvCommit)"

    $compareOutput = & $compareScript `
        -BaseCommit $BaseCommit `
        -TargetCommit $resolved.mpvCommit `
        -OutputDirectory $OutputDirectory `
        -ForceDownload:$ForceDownload.IsPresent `
        -UserAgent $UserAgent
    $compared = $compareOutput | ConvertFrom-Json

    Write-Host "  target API : $($compared.targetClientApiVersion) (uses _name=$($compared.targetUsesUnderscoreName))"

    $changed = @($compared.headers | Where-Object { $_.hasDifference })
    if ($changed.Count -eq 0) {
        Write-Host "  header diff: ✓ 與 $BaseCommit 一致（client.h / render.h / render_gl.h / stream_cb.h）" -ForegroundColor Green
        return [ordered]@{
            provider = $Provider
            releaseTag = $resolved.releaseTag
            mpvCommit = $resolved.mpvCommit
            clientApiVersion = $compared.targetClientApiVersion
            changedHeaders = @()
        }
    }

    Write-Host "  header diff: ✗ 偵測到 $($changed.Count) 個 header 變動：" -ForegroundColor Yellow
    foreach ($entry in $changed) {
        Write-Host "    - $($entry.name) (base=$($entry.baseSha256.Substring(0,12))… target=$($entry.targetSha256.Substring(0,12))…)" -ForegroundColor Yellow
    }
    return [ordered]@{
        provider = $Provider
        releaseTag = $resolved.releaseTag
        mpvCommit = $resolved.mpvCommit
        clientApiVersion = $compared.targetClientApiVersion
        changedHeaders = @($changed | ForEach-Object { $_.name })
    }
}

Write-Host "libmpv header drift check"
Write-Host "  baseCommit       : $BaseCommit"
Write-Host "  outputDirectory  : $OutputDirectory"
Write-Host "  runDate          : $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"

$summaries = @()
$summaries += Invoke-ProviderCheck -Provider 'Shinchiro'
$summaries += Invoke-ProviderCheck -Provider 'Zhongfly'

Write-Host ""
Write-Host "=== Summary ===" -ForegroundColor Cyan
$anyDrift = $false
foreach ($summary in $summaries) {
    $marker = if ($summary.changedHeaders.Count -eq 0) { '✓' } else { '✗' }
    $color = if ($summary.changedHeaders.Count -eq 0) { 'Green' } else { 'Yellow' }
    Write-Host "  $marker $($summary.provider) $($summary.releaseTag) → mpv $($summary.mpvCommit) (api $($summary.clientApiVersion))" -ForegroundColor $color
    if ($summary.changedHeaders.Count -gt 0) {
        $anyDrift = $true
    }
}

if ($anyDrift) {
    Write-Host ""
    Write-Host "  偵測到 header 變動。建議步驟：" -ForegroundColor Yellow
    Write-Host "    1. 檢視 $OutputDirectory 下的 diff" -ForegroundColor Yellow
    Write-Host "    2. 評估是否需要更新 managed P/Invoke（src/MediaEmbedKit.Mpv/Native/MpvNative.cs）" -ForegroundColor Yellow
    Write-Host "    3. 跑 Update-LibMpvGitBuildManifest.ps1 更新 docs/runtime/libmpv-git-builds.json" -ForegroundColor Yellow
    if ($ExitCodeOnDrift -ne 0) {
        exit $ExitCodeOnDrift
    }
}
else {
    Write-Host ""
    Write-Host "  所有提供者的 header 與 $BaseCommit 一致。" -ForegroundColor Green
}

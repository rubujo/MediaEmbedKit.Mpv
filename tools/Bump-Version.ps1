<#
.SYNOPSIS
    將本專案的套件版號統一改為新版號。

.DESCRIPTION
    讀取 Directory.Build.props 內的 <PackageVersion> 當作目前版號，把新版號寫回 props，
    並同步替換 docs/CONSUMING_PACKAGES.md 內顯示目前版號的具體範例段落（檔名 layout
    與 PackageVersion 提示行）。其他已改用 <version> placeholder 的範例不動。

    若有非 SemVer 變更（pre-release 後綴 變更等）會出現警告但不阻擋；最終確認權在
    使用者。Script 不會自動 commit / push / tag，請手動完成 git 操作。

.PARAMETER NewVersion
    要設定的新版號。必須符合 SemVer 2.0（例：0.0.2、0.1.0-alpha.1、1.0.0+build.5）。

.PARAMETER DryRun
    只輸出將要做的變更，不實際寫檔。

.EXAMPLE
    .\tools\Bump-Version.ps1 -NewVersion 0.0.2

.EXAMPLE
    .\tools\Bump-Version.ps1 -NewVersion 1.0.0 -DryRun
#>
param(
    [Parameter(Mandatory = $true)]
    [string] $NewVersion,

    [switch] $DryRun
)

$ErrorActionPreference = 'Stop'

# SemVer 2.0.0 規範（簡化版正則，足夠 sanity check）
$semverPattern = '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-((?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+([0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$'

if ($NewVersion -notmatch $semverPattern) {
    Write-Error "新版號 '$NewVersion' 不符合 SemVer 2.0 格式。"
    exit 2
}

# 解析路徑（script 可在任何工作目錄下執行）
$rootDir = Split-Path -Parent $PSScriptRoot
$propsPath = Join-Path $rootDir 'Directory.Build.props'
$consumingDocPath = Join-Path $rootDir 'docs\CONSUMING_PACKAGES.md'

if (-not (Test-Path -LiteralPath $propsPath)) {
    Write-Error "找不到 $propsPath。"
    exit 3
}

# 讀取目前版號
$propsContent = Get-Content -LiteralPath $propsPath -Raw -Encoding UTF8
$currentMatch = [regex]::Match($propsContent, '<PackageVersion>([^<]+)</PackageVersion>')
if (-not $currentMatch.Success) {
    Write-Error "在 $propsPath 找不到 <PackageVersion>...</PackageVersion>。"
    exit 4
}

$currentVersion = $currentMatch.Groups[1].Value
if ($currentVersion -eq $NewVersion) {
    Write-Host "目前版號已是 $NewVersion，無變更。" -ForegroundColor Yellow
    exit 0
}

Write-Host "目前版號：$currentVersion" -ForegroundColor Cyan
Write-Host "新版號：  $NewVersion" -ForegroundColor Cyan
Write-Host ""

$utf8Bom = New-Object System.Text.UTF8Encoding $true
$utf8NoBom = New-Object System.Text.UTF8Encoding $false

# Helper：寫檔（CRLF + 指定編碼）
function Save-Text {
    param(
        [string] $Path,
        [string] $Content,
        [System.Text.Encoding] $Encoding
    )

    $normalized = ($Content -replace "`r`n", "`n") -replace "`n", "`r`n"
    if ($DryRun) {
        return
    }

    [System.IO.File]::WriteAllText($Path, $normalized, $Encoding)
}

# 1. Directory.Build.props（UTF-8 BOM，符合 ENGINEERING_STANDARDS）
$newPropsContent = $propsContent -replace '<PackageVersion>[^<]+</PackageVersion>', "<PackageVersion>$NewVersion</PackageVersion>"
$propsChanges = ($propsContent -split "`n").Count
Write-Host "[1/2] Directory.Build.props" -ForegroundColor Green
Write-Host "      <PackageVersion>$currentVersion</PackageVersion> → <PackageVersion>$NewVersion</PackageVersion>"
Save-Text -Path $propsPath -Content $newPropsContent -Encoding $utf8Bom

# 2. docs/CONSUMING_PACKAGES.md（UTF-8 無 BOM）
# 替換所有 currentVersion literal（含檔名 layout 與 PackageVersion 提示行）
if (-not (Test-Path -LiteralPath $consumingDocPath)) {
    Write-Warning "找不到 $consumingDocPath，略過。"
} else {
    $docContent = Get-Content -LiteralPath $consumingDocPath -Raw -Encoding UTF8
    $escapedCurrent = [regex]::Escape($currentVersion)
    $matches = [regex]::Matches($docContent, $escapedCurrent)
    if ($matches.Count -eq 0) {
        Write-Host "[2/2] docs/CONSUMING_PACKAGES.md：未找到 '$currentVersion'，略過。" -ForegroundColor Yellow
    } else {
        $newDocContent = [regex]::Replace($docContent, $escapedCurrent, $NewVersion)
        Write-Host "[2/2] docs/CONSUMING_PACKAGES.md" -ForegroundColor Green
        Write-Host "      $($matches.Count) 處 '$currentVersion' → '$NewVersion'"
        Save-Text -Path $consumingDocPath -Content $newDocContent -Encoding $utf8NoBom
    }
}

Write-Host ""
if ($DryRun) {
    Write-Host "DryRun 完成：未實際寫檔。" -ForegroundColor Yellow
} else {
    Write-Host "版號升級完成。建議接下來：" -ForegroundColor Green
    Write-Host "  1. dotnet format .\MediaEmbedKit.Mpv.slnx --verify-no-changes"
    Write-Host "  2. .\tools\Invoke-PreReleaseValidation.ps1"
    Write-Host "  3. git diff 確認所有變更合理"
    Write-Host "  4. git commit -m `"chore(release): bump version to $NewVersion`""
    Write-Host "  5. git tag v$NewVersion && git push origin main v$NewVersion"
}

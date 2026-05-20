param(
    [string] $Configuration = "Release",
    [switch] $SkipIntegrationTests,
    [switch] $SkipPackageValidation,
    [switch] $SkipConsumerValidation,
    [switch] $IncludeStressTests,
    [switch] $IncludeConsoleMinimalPlaybackValidation,
    [switch] $IncludeGuiConsumerPlaybackValidation,
    [switch] $IncludeGuiPlaybackStress,
    [switch] $IncludeWindowsReleaseGate,
    [switch] $IncludeDocSyncCheck,
    [switch] $DryRun,
    [string] $RuntimeDirectory = ".tmp/gui-playback-runtime",
    [double] $GuiPlaybackSeconds = 20,
    [int] $GuiPlaybackIterations = 1,
    [int] $GuiPlaybackTimeoutSeconds = 420
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$utf8NoBomEncoding = [System.Text.UTF8Encoding]::new($false)
if ([System.Environment]::OSVersion.Platform -eq [System.PlatformID]::Win32NT) {
    & chcp.com 65001 > $null
}

[Console]::InputEncoding = $utf8NoBomEncoding
[Console]::OutputEncoding = $utf8NoBomEncoding
$OutputEncoding = $utf8NoBomEncoding

$rootDirectory = Split-Path -Parent $PSScriptRoot
$resolvedRoot = [System.IO.Path]::GetFullPath($rootDirectory)
Set-Location $rootDirectory

function Resolve-WorkspacePath {
    param(
        [string] $Path
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $rootDirectory $Path))
}

$resolvedRuntimeDirectory = Resolve-WorkspacePath -Path $RuntimeDirectory
if (-not $resolvedRuntimeDirectory.StartsWith($resolvedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "執行階段資料夾必須位於工作區內。"
}

if ($IncludeWindowsReleaseGate) {
    $IncludeStressTests = $true
    $IncludeConsoleMinimalPlaybackValidation = $true
    $IncludeGuiConsumerPlaybackValidation = $true
    $IncludeGuiPlaybackStress = $true
}

function Invoke-Step {
    param(
        [string] $Name,
        [scriptblock] $Body
    )

    if ($DryRun) {
        Write-Host "[DryRun] -> $Name"
        return
    }

    Write-Host "==> $Name"
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $global:LASTEXITCODE = 0
        & $Body
        if ($LASTEXITCODE -ne 0) {
            throw "步驟結束代碼為 $LASTEXITCODE。"
        }

        Write-Host ("<== {0} 完成，耗時 {1:n1} 秒" -f $Name, $stopwatch.Elapsed.TotalSeconds)
    }
    catch {
        throw "步驟失敗：$Name。$($_.Exception.Message)"
    }
    finally {
        $stopwatch.Stop()
    }
}

function Invoke-ProjectPolicyCheck {
    # 「C# XML 註解不得使用 <inheritdoc>」規則只適用於原始碼；build 產物（bin/obj/*.xml）
    # 由編譯器生成，會合法出現 <inheritdoc/>，必須排除。
    $inheritdocMatches = @()
    foreach ($dir in @("src", "samples", "tests")) {
        if (Test-Path $dir) {
            $matchesInDir = Get-ChildItem -Path $dir -Recurse -File -Filter "*.cs" -ErrorAction SilentlyContinue |
                Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
                Select-String -Pattern "<inheritdoc" -SimpleMatch
            if ($matchesInDir) {
                $inheritdocMatches += $matchesInDir
            }
        }
    }

    if ($inheritdocMatches.Count -gt 0) {
        $lines = $inheritdocMatches | ForEach-Object { "$($_.Path):$($_.LineNumber): $($_.Line.Trim())" }
        throw "C# XML 註解不得使用 <inheritdoc>。`n$($lines -join "`n")"
    }

    $commitSubject = & git log -1 --pretty=%s 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "無法讀取 Git 提交主旨，略過提交格式檢查。"
        return
    }

    $conventionalCommitSubjectPattern = '^(feat|fix|docs|refactor|test|build|chore|style|perf|ci|revert)(\+(feat|fix|docs|refactor|test|build|chore|style|perf|ci|revert))*(\([a-z0-9-]+(,[a-z0-9-]+)*\))?!?: .+'
    if ($commitSubject -notmatch $conventionalCommitSubjectPattern) {
        throw "最新提交主旨不符合專案慣例式提交規範：$commitSubject"
    }
}

if ($DryRun) {
    Write-Host "DryRun 模式：以下步驟將會被預覽，不會真正執行。"
    Write-Host ("Configuration = {0}" -f $Configuration)
    Write-Host ("RuntimeDirectory = {0}" -f $resolvedRuntimeDirectory)
}

Invoke-Step "還原套件" { dotnet restore .\MediaEmbedKit.Mpv.slnx }
Invoke-Step "專案規範檢查" { Invoke-ProjectPolicyCheck }

if ($IncludeDocSyncCheck) {
    # 驗證 提供者下游事實宣告型文件（LIBMPV_C_API_TEST_MATRIX / HIGH_LEVEL_API /
    # REFERENCE_SOURCES）與 catalog（docs/runtime/libmpv-git-builds.json）同步。
    # 由 release.yml 顯式開啟；ci.yml 不啟用，避免 PR 階段半成品狀態被 drift 卡住。
    # drift 時 Sync-ProviderDocs.ps1 -Check 會 exit 1，Invoke-Step 會抓 LASTEXITCODE 並 throw。
    Invoke-Step "Provider 下游文件同步檢查" { & .\tools\libmpv\Sync-ProviderDocs.ps1 -Check }
}

Invoke-Step "格式檢查" { dotnet format .\MediaEmbedKit.Mpv.slnx --no-restore --verify-no-changes }
Invoke-Step "核心測試" { dotnet run --project .\tests\MediaEmbedKit.Mpv.Tests\MediaEmbedKit.Mpv.Tests.csproj --configuration $Configuration --no-restore }

if (-not $SkipIntegrationTests) {
    Invoke-Step "libmpv 整合測試" { dotnet run --project .\tests\MediaEmbedKit.Mpv.IntegrationTests\MediaEmbedKit.Mpv.IntegrationTests.csproj --configuration $Configuration --no-restore }
}

Invoke-Step "方案建置" { dotnet build .\MediaEmbedKit.Mpv.slnx --configuration $Configuration --no-restore }

Invoke-Step "UI 控制項無頭測試" {
    # 5 套 UI 框架各自的控制項層無頭測試（DP / BindableProperty / 5 個命令 /
    # CanExecute / Dispose 重入 / 唯讀 property 守備）。不啟動真實 libmpv，純驗證屬性系統
    # 與 CLR 包裝層；WinUI / MAUI 走 WinUI / MauiWinUI host，啟動後即在 UI thread 跑完後退出。
    dotnet run --project .\tests\MediaEmbedKit.Mpv.WinForms.HeadlessTests\MediaEmbedKit.Mpv.WinForms.HeadlessTests.csproj --configuration $Configuration --no-restore
    dotnet run --project .\tests\MediaEmbedKit.Mpv.Wpf.HeadlessTests\MediaEmbedKit.Mpv.Wpf.HeadlessTests.csproj --configuration $Configuration --no-restore
    dotnet run --project .\tests\MediaEmbedKit.Mpv.Avalonia.HeadlessTests\MediaEmbedKit.Mpv.Avalonia.HeadlessTests.csproj --configuration $Configuration --no-restore
    dotnet run --project .\tests\MediaEmbedKit.Mpv.WinUI.HeadlessTests\MediaEmbedKit.Mpv.WinUI.HeadlessTests.csproj --configuration $Configuration --no-restore
    dotnet run --project .\tests\MediaEmbedKit.Mpv.Maui.HeadlessTests\MediaEmbedKit.Mpv.Maui.HeadlessTests.csproj --configuration $Configuration --no-restore
}

if (-not $SkipPackageValidation) {
    Invoke-Step "NuGet 套件驗證" { & .\tools\Invoke-PackageValidation.ps1 -Configuration $Configuration }
}

if (-not $SkipConsumerValidation) {
    if ($SkipPackageValidation) {
        Invoke-Step "乾淨 使用端套件 驗證" { & .\tools\Invoke-ConsumerPackageValidation.ps1 -Configuration $Configuration }
    }
    else {
        Invoke-Step "乾淨 使用端套件 驗證" { & .\tools\Invoke-ConsumerPackageValidation.ps1 -Configuration $Configuration -SkipPackageValidation }
    }
}

if ($IncludeStressTests) {
    Invoke-Step "第一階段壓力測試" { dotnet run --project .\tests\MediaEmbedKit.Mpv.StressTests\MediaEmbedKit.Mpv.StressTests.csproj --configuration $Configuration --no-restore }
}

if ($IncludeConsoleMinimalPlaybackValidation) {
    Invoke-Step "ConsoleMinimal 播放驗證" {
        $previousRuntimeDirectory = $env:MEDIAEMBEDKIT_MPV_RUNTIME_DIR
        try {
            $env:MEDIAEMBEDKIT_MPV_RUNTIME_DIR = $resolvedRuntimeDirectory
            dotnet run --project .\samples\ConsoleMinimalSample\MediaEmbedKit.Mpv.Samples.ConsoleMinimal.csproj --configuration $Configuration --no-restore
        }
        finally {
            $env:MEDIAEMBEDKIT_MPV_RUNTIME_DIR = $previousRuntimeDirectory
        }
    }
}

if ($IncludeGuiConsumerPlaybackValidation) {
    Invoke-Step "GUI 使用端實際播放驗證" { & .\tools\Invoke-GuiConsumerPlaybackValidation.ps1 -Configuration $Configuration -SkipPackageValidation -RuntimeDirectory $resolvedRuntimeDirectory -Seconds $GuiPlaybackSeconds -Iterations $GuiPlaybackIterations -TimeoutSeconds $GuiPlaybackTimeoutSeconds }
}

if ($IncludeGuiPlaybackStress) {
    Invoke-Step "GUI 播放壓力測試" { & .\tools\Invoke-GuiPlaybackStress.ps1 -Configuration $Configuration -RuntimeDirectory $resolvedRuntimeDirectory -Seconds $GuiPlaybackSeconds -Iterations $GuiPlaybackIterations -TimeoutSeconds $GuiPlaybackTimeoutSeconds }
}

if ($DryRun) {
    Write-Host "DryRun 模式結束：以上為預計執行步驟清單。"
}
else {
    Write-Host "發佈前本機驗證完成。"
}

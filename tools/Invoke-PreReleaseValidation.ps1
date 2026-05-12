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
    [string] $RuntimeDirectory = ".tmp/gui-playback-runtime",
    [double] $GuiPlaybackSeconds = 20,
    [int] $GuiPlaybackIterations = 1,
    [int] $GuiPlaybackTimeoutSeconds = 420
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

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
    throw "runtime 資料夾必須位於工作區內。"
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

Invoke-Step "還原套件" { dotnet restore .\MediaEmbedKit.Mpv.slnx }
Invoke-Step "格式檢查" { dotnet format .\MediaEmbedKit.Mpv.slnx --no-restore --verify-no-changes }
Invoke-Step "核心測試" { dotnet run --project .\tests\MediaEmbedKit.Mpv.Tests\MediaEmbedKit.Mpv.Tests.csproj --configuration $Configuration --no-restore }

if (-not $SkipIntegrationTests) {
    Invoke-Step "libmpv 整合測試" { dotnet run --project .\tests\MediaEmbedKit.Mpv.IntegrationTests\MediaEmbedKit.Mpv.IntegrationTests.csproj --configuration $Configuration --no-restore }
}

Invoke-Step "方案建置" { dotnet build .\MediaEmbedKit.Mpv.slnx --configuration $Configuration --no-restore }

if (-not $SkipPackageValidation) {
    Invoke-Step "NuGet 套件驗證" { & .\tools\Invoke-PackageValidation.ps1 -Configuration $Configuration }
}

if (-not $SkipConsumerValidation) {
    if ($SkipPackageValidation) {
        Invoke-Step "乾淨 consumer package 驗證" { & .\tools\Invoke-ConsumerPackageValidation.ps1 -Configuration $Configuration }
    }
    else {
        Invoke-Step "乾淨 consumer package 驗證" { & .\tools\Invoke-ConsumerPackageValidation.ps1 -Configuration $Configuration -SkipPackageValidation }
    }
}

if ($IncludeStressTests) {
    Invoke-Step "第一階段壓力測試" { dotnet run --project .\tests\MediaEmbedKit.Mpv.StressTests\MediaEmbedKit.Mpv.StressTests.csproj --configuration $Configuration --no-restore }
}

if ($IncludeConsoleMinimalPlaybackValidation) {
    Invoke-Step "Console minimal 播放驗證" {
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
    Invoke-Step "GUI consumer 實際播放驗證" { & .\tools\Invoke-GuiConsumerPlaybackValidation.ps1 -Configuration $Configuration -SkipPackageValidation -RuntimeDirectory $resolvedRuntimeDirectory -Seconds $GuiPlaybackSeconds -Iterations $GuiPlaybackIterations -TimeoutSeconds $GuiPlaybackTimeoutSeconds }
}

if ($IncludeGuiPlaybackStress) {
    Invoke-Step "GUI 播放壓力測試" { & .\tools\Invoke-GuiPlaybackStress.ps1 -Configuration $Configuration -RuntimeDirectory $resolvedRuntimeDirectory -Seconds $GuiPlaybackSeconds -Iterations $GuiPlaybackIterations -TimeoutSeconds $GuiPlaybackTimeoutSeconds }
}

Write-Host "發佈前本機驗證完成。"

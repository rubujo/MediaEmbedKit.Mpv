param(
    [switch] $SkipIntegrationTests,
    [switch] $SkipPackageValidation
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$rootDirectory = Split-Path -Parent $PSScriptRoot
Set-Location $rootDirectory

function Invoke-Step {
    param(
        [string] $Name,
        [scriptblock] $Body
    )

    Write-Host "==> $Name"
    & $Body
    if ($LASTEXITCODE -ne 0) {
        throw "步驟失敗：$Name"
    }
}

Invoke-Step "還原套件" { dotnet restore .\MediaEmbedKit.Mpv.slnx }
Invoke-Step "格式檢查" { dotnet format .\MediaEmbedKit.Mpv.slnx --no-restore --verify-no-changes }
Invoke-Step "核心測試" { dotnet run --project .\tests\MediaEmbedKit.Mpv.Tests\MediaEmbedKit.Mpv.Tests.csproj --no-restore }

if (-not $SkipIntegrationTests) {
    Invoke-Step "libmpv 整合測試" { dotnet run --project .\tests\MediaEmbedKit.Mpv.IntegrationTests\MediaEmbedKit.Mpv.IntegrationTests.csproj --no-restore }
}

Invoke-Step "方案建置" { dotnet build .\MediaEmbedKit.Mpv.slnx --no-restore }

if (-not $SkipPackageValidation) {
    Invoke-Step "NuGet 套件驗證" { & .\tools\Invoke-PackageValidation.ps1 }
}

Write-Host "發佈前本機驗證完成。"

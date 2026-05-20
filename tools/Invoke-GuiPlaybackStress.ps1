param(
    [string] $Configuration = "Release",
    [string] $Sample = "all",
    [double] $Seconds = 120,
    [int] $Iterations = 2,
    [int] $TimeoutSeconds = 420,
    [string] $RuntimeDirectory = ".tmp/gui-playback-runtime",
    [switch] $NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$rootDirectory = Split-Path -Parent $PSScriptRoot
$resolvedRoot = [System.IO.Path]::GetFullPath($rootDirectory)

if ([System.IO.Path]::IsPathRooted($RuntimeDirectory)) {
    $resolvedRuntimeDirectory = [System.IO.Path]::GetFullPath($RuntimeDirectory)
}
else {
    $resolvedRuntimeDirectory = [System.IO.Path]::GetFullPath((Join-Path $rootDirectory $RuntimeDirectory))
}

if (-not $resolvedRuntimeDirectory.StartsWith($resolvedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "GUI 播放壓力測試執行階段資料夾必須位於工作區內。"
}

if ($Seconds -le 0) {
    throw "播放秒數必須大於零。"
}

if ($Iterations -le 0) {
    throw "重複次數必須大於零。"
}

if ($TimeoutSeconds -le 0) {
    throw "逾時秒數必須大於零。"
}

$playbackSmokeProject = Join-Path $rootDirectory "tests/MediaEmbedKit.Mpv.PlaybackSmoke/MediaEmbedKit.Mpv.PlaybackSmoke.csproj"
$arguments = @(
    "run",
    "--project",
    $playbackSmokeProject,
    "--configuration",
    $Configuration,
    "--",
    "--sample",
    $Sample,
    "--seconds",
    $Seconds.ToString([System.Globalization.CultureInfo]::InvariantCulture),
    "--iterations",
    $Iterations.ToString([System.Globalization.CultureInfo]::InvariantCulture),
    "--timeout-seconds",
    $TimeoutSeconds.ToString([System.Globalization.CultureInfo]::InvariantCulture),
    "--configuration",
    $Configuration,
    "--runtime-directory",
    $resolvedRuntimeDirectory
)

if ($NoBuild) {
    $arguments += "--no-build"
}

& dotnet @arguments
if ($LASTEXITCODE -ne 0) {
    throw "GUI 播放壓力測試失敗。"
}

Write-Host "GUI 播放壓力測試完成。"

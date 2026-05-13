param(
    [string] $Configuration = "Release",
    [string] $OutputDirectory = "artifacts/packages"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$rootDirectory = Split-Path -Parent $PSScriptRoot
$packageDirectory = Join-Path $rootDirectory $OutputDirectory
$resolvedRoot = [System.IO.Path]::GetFullPath($rootDirectory)
$resolvedPackageDirectory = [System.IO.Path]::GetFullPath($packageDirectory)

if (-not $resolvedPackageDirectory.StartsWith($resolvedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "套件輸出資料夾必須位於工作區內。"
}

if (Test-Path -LiteralPath $resolvedPackageDirectory) {
    Remove-Item -LiteralPath $resolvedPackageDirectory -Recurse -Force
}

New-Item -ItemType Directory -Path $resolvedPackageDirectory | Out-Null

$packableProjects = @(
    "src/MediaEmbedKit.Mpv/MediaEmbedKit.Mpv.csproj",
    "src/MediaEmbedKit.Mpv.WinForms/MediaEmbedKit.Mpv.WinForms.csproj",
    "src/MediaEmbedKit.Mpv.Wpf/MediaEmbedKit.Mpv.Wpf.csproj",
    "src/MediaEmbedKit.Mpv.Avalonia/MediaEmbedKit.Mpv.Avalonia.csproj",
    "src/MediaEmbedKit.Mpv.WinUI/MediaEmbedKit.Mpv.WinUI.csproj",
    "src/MediaEmbedKit.Mpv.Maui/MediaEmbedKit.Mpv.Maui.csproj"
)

foreach ($project in $packableProjects) {
    $projectPath = Join-Path $rootDirectory $project
    & dotnet pack $projectPath --configuration $Configuration --output $resolvedPackageDirectory
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet pack 失敗：$project"
    }
}

Add-Type -AssemblyName System.IO.Compression.FileSystem

$expectedPackageIds = @(
    "MediaEmbedKit.Mpv",
    "MediaEmbedKit.Mpv.WinForms",
    "MediaEmbedKit.Mpv.Wpf",
    "MediaEmbedKit.Mpv.Avalonia",
    "MediaEmbedKit.Mpv.WinUI",
    "MediaEmbedKit.Mpv.Maui"
)

$forbiddenRuntimeFiles = @(
    "libmpv-2.dll",
    "yt-dlp.exe",
    "deno.exe",
    "ffmpeg.exe",
    "ffprobe.exe",
    "ffmpeg-master-latest-win64-gpl.zip"
)

foreach ($packageId in $expectedPackageIds) {
    $packagePattern = "^" + [System.Text.RegularExpressions.Regex]::Escape($packageId) + "\.\d"
    $packages = @(Get-ChildItem -LiteralPath $resolvedPackageDirectory -Filter "$packageId.*.nupkg" |
        Where-Object { $_.Name -notlike "*.symbols.nupkg" -and $_.BaseName -match $packagePattern })

    if ($packages.Count -ne 1) {
        throw "找不到唯一的套件：$packageId"
    }

    $package = $packages[0]
    $archive = [System.IO.Compression.ZipFile]::OpenRead($package.FullName)
    try {
        $entryNames = @($archive.Entries | ForEach-Object { $_.FullName })
        if (-not ($entryNames | Where-Object { $_ -like "*.nuspec" })) {
            throw "套件缺少 nuspec：$($package.Name)"
        }

        if (-not ($entryNames -contains "README.md")) {
            throw "套件缺少 README.md：$($package.Name)"
        }

        if (-not ($entryNames -contains "THIRD_PARTY_NOTICES.md")) {
            throw "套件缺少 THIRD_PARTY_NOTICES.md：$($package.Name)"
        }

        if (-not ($entryNames | Where-Object { $_ -like "lib/*/*.dll" })) {
            throw "套件缺少 lib DLL：$($package.Name)"
        }

        foreach ($runtimeFile in $forbiddenRuntimeFiles) {
            if ($entryNames | Where-Object { [System.IO.Path]::GetFileName($_) -eq $runtimeFile }) {
                throw "套件不應包含第三方 runtime：$runtimeFile"
            }
        }
    }
    finally {
        $archive.Dispose()
    }
}

Write-Host "NuGet 套件驗證完成：$resolvedPackageDirectory"

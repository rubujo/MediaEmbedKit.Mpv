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
    "src/MediaEmbedKit.Mpv.Externals/MediaEmbedKit.Mpv.Externals.csproj",
    "src/MediaEmbedKit.Mpv.Runtime/MediaEmbedKit.Mpv.Runtime.csproj",
    "src/MediaEmbedKit.Mpv.Diagnostics/MediaEmbedKit.Mpv.Diagnostics.csproj",
    "src/MediaEmbedKit.Mpv.Hosting/MediaEmbedKit.Mpv.Hosting.csproj",
    "src/MediaEmbedKit.Mpv.WinForms/MediaEmbedKit.Mpv.WinForms.csproj",
    "src/MediaEmbedKit.Mpv.Wpf/MediaEmbedKit.Mpv.Wpf.csproj",
    "src/MediaEmbedKit.Mpv.Avalonia/MediaEmbedKit.Mpv.Avalonia.csproj",
    "src/MediaEmbedKit.Mpv.WinUI/MediaEmbedKit.Mpv.WinUI.csproj",
    "src/MediaEmbedKit.Mpv.Maui.Windows/MediaEmbedKit.Mpv.Maui.Windows.csproj",
    "src/MediaEmbedKit.Mpv.Full/MediaEmbedKit.Mpv.Full.csproj"
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
    "MediaEmbedKit.Mpv.Externals",
    "MediaEmbedKit.Mpv.Runtime",
    "MediaEmbedKit.Mpv.Diagnostics",
    "MediaEmbedKit.Mpv.Hosting",
    "MediaEmbedKit.Mpv.WinForms",
    "MediaEmbedKit.Mpv.Wpf",
    "MediaEmbedKit.Mpv.Avalonia",
    "MediaEmbedKit.Mpv.WinUI",
    "MediaEmbedKit.Mpv.Maui.Windows",
    "MediaEmbedKit.Mpv.Full"
)

# 中繼套件 (.Full) 內部沒有 lib DLL，僅靠 PackageReference 把所有子套件
# 拉進來。後續驗證迴圈中個別跳過 lib/*.dll 檢查即可。
$metaPackageIds = @(
    "MediaEmbedKit.Mpv.Full"
)

$expectedLibTfms = @{
    "MediaEmbedKit.Mpv" = @("netstandard2.0", "net472", "net48", "net10.0")
    "MediaEmbedKit.Mpv.Externals" = @("netstandard2.0", "net472", "net48", "net10.0")
    "MediaEmbedKit.Mpv.Runtime" = @("netstandard2.0", "net472", "net48", "net10.0")
    "MediaEmbedKit.Mpv.Diagnostics" = @("netstandard2.0", "net472", "net48", "net10.0")
    "MediaEmbedKit.Mpv.Hosting" = @("netstandard2.0", "net472", "net48", "net10.0")
    "MediaEmbedKit.Mpv.WinForms" = @("net472", "net48", "net10.0-windows7.0")
    "MediaEmbedKit.Mpv.Wpf" = @("net472", "net48", "net10.0-windows7.0")
    "MediaEmbedKit.Mpv.Avalonia" = @("net10.0-windows7.0")
    "MediaEmbedKit.Mpv.WinUI" = @("net10.0-windows10.0.19041")
    "MediaEmbedKit.Mpv.Maui.Windows" = @("net10.0-windows10.0.19041")
}

$expectedFullDependencies = @(
    "MediaEmbedKit.Mpv",
    "MediaEmbedKit.Mpv.Externals",
    "MediaEmbedKit.Mpv.Runtime",
    "MediaEmbedKit.Mpv.Diagnostics",
    "MediaEmbedKit.Mpv.Hosting"
)

$forbiddenRuntimeFiles = @(
    "libmpv-2.dll",
    "yt-dlp.exe",
    "yt-dlp_arm64.exe",
    "deno.exe",
    "ffmpeg.exe",
    "ffprobe.exe",
    "7zr.exe"
)

$forbiddenRuntimeFilePatterns = @(
    "^ffmpeg-master-latest-win(64|arm64)-gpl\.zip$",
    "^deno-(x86_64|aarch64)-pc-windows-msvc\.zip$",
    "^mpv-.*\.7z$",
    "^mpv-dev-.*\.7z$"
)

function Read-ZipEntryText {
    param(
        [System.IO.Compression.ZipArchive] $Archive,
        [string] $EntryName
    )

    $entry = $Archive.GetEntry($EntryName)
    if ($null -eq $entry) {
        throw "套件缺少項目：$EntryName"
    }

    $stream = $entry.Open()
    try {
        $reader = [System.IO.StreamReader]::new($stream, [System.Text.Encoding]::UTF8)
        try {
            return $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

foreach ($packageId in $expectedPackageIds) {
    $packagePattern = "^" + [System.Text.RegularExpressions.Regex]::Escape($packageId) + "\.\d"
    $packages = @(Get-ChildItem -LiteralPath $resolvedPackageDirectory -Filter "$packageId.*.nupkg" |
        Where-Object { $_.Name -notlike "*.symbols.nupkg" -and $_.BaseName -match $packagePattern })
    $symbolPackages = @(Get-ChildItem -LiteralPath $resolvedPackageDirectory -Filter "$packageId.*.snupkg" |
        Where-Object { $_.BaseName -match $packagePattern })

    if ($packages.Count -ne 1) {
        throw "找不到唯一的套件：$packageId"
    }

    if ($symbolPackages.Count -ne 1) {
        throw "找不到唯一的符號套件：$packageId"
    }

    $package = $packages[0]
    $symbolPackage = $symbolPackages[0]
    $packageVersion = $package.BaseName.Substring($packageId.Length + 1)
    $symbolVersion = $symbolPackage.BaseName.Substring($packageId.Length + 1)
    if ($packageVersion -ne $symbolVersion) {
        throw "主套件與符號套件版本不一致：$packageId"
    }

    $archive = [System.IO.Compression.ZipFile]::OpenRead($package.FullName)
    try {
        $entryNames = @($archive.Entries | ForEach-Object { $_.FullName })
        $nuspecName = @($entryNames | Where-Object { $_ -like "*.nuspec" } | Select-Object -First 1)
        if ($nuspecName.Count -eq 0) {
            throw "套件缺少 nuspec：$($package.Name)"
        }

        if (-not ($entryNames -contains "README.md")) {
            throw "套件缺少 README.md：$($package.Name)"
        }

        if (-not ($entryNames -contains "THIRD_PARTY_NOTICES.md")) {
            throw "套件缺少 THIRD_PARTY_NOTICES.md：$($package.Name)"
        }

        if ($metaPackageIds -notcontains $packageId) {
            if (-not ($entryNames | Where-Object { $_ -like "lib/*/*.dll" })) {
                throw "套件缺少 lib DLL：$($package.Name)"
            }

            foreach ($tfm in $expectedLibTfms[$packageId]) {
                $expectedDll = "lib/$tfm/$packageId.dll"
                if ($entryNames -notcontains $expectedDll) {
                    throw "套件缺少目標框架 DLL：$expectedDll"
                }
            }
        }
        else {
            [xml] $nuspec = Read-ZipEntryText -Archive $archive -EntryName $nuspecName[0]
            $dependencies = @($nuspec.package.metadata.dependencies.group.dependency | ForEach-Object { $_.id })
            foreach ($dependencyId in $expectedFullDependencies) {
                if ($dependencies -notcontains $dependencyId) {
                    throw "中繼套件缺少相依套件：$dependencyId"
                }
            }
        }

        foreach ($runtimeFile in $forbiddenRuntimeFiles) {
            if ($entryNames | Where-Object { [System.IO.Path]::GetFileName($_) -eq $runtimeFile }) {
                throw "套件不應包含第三方 runtime：$runtimeFile"
            }
        }

        foreach ($runtimeFilePattern in $forbiddenRuntimeFilePatterns) {
            $matchedForbiddenRuntime = @($entryNames | Where-Object {
                [System.IO.Path]::GetFileName($_) -match $runtimeFilePattern
            })
            if ($matchedForbiddenRuntime.Count -gt 0) {
                throw "套件不應包含第三方 runtime 或封存檔：$($matchedForbiddenRuntime[0])"
            }
        }
    }
    finally {
        $archive.Dispose()
    }

    $symbolArchive = [System.IO.Compression.ZipFile]::OpenRead($symbolPackage.FullName)
    try {
        $symbolEntryNames = @($symbolArchive.Entries | ForEach-Object { $_.FullName })
        if ($metaPackageIds -notcontains $packageId -and -not ($symbolEntryNames | Where-Object { $_ -like "lib/*/*.pdb" })) {
            throw "符號套件缺少 PDB：$($symbolPackage.Name)"
        }
    }
    finally {
        $symbolArchive.Dispose()
    }
}

Write-Host "NuGet 套件驗證完成：$resolvedPackageDirectory"

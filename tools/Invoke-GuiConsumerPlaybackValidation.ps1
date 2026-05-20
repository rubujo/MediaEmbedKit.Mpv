param(
    [string] $Configuration = "Release",
    [string] $PackageDirectory = "artifacts/packages",
    [string] $WorkDirectory = ".tmp/gui-consumer-playback",
    [string] $RuntimeDirectory = ".tmp/gui-playback-runtime",
    [double] $Seconds = 20,
    [int] $Iterations = 1,
    [int] $TimeoutSeconds = 360,
    [string] $Sample = "all",
    [switch] $SkipPackageValidation,
    [switch] $KeepWorkDirectory,
    [switch] $NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$rootDirectory = Split-Path -Parent $PSScriptRoot
$resolvedRoot = [System.IO.Path]::GetFullPath($rootDirectory)

function Resolve-WorkspacePath {
    param(
        [string] $Path
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $rootDirectory $Path))
}

function Assert-WorkspacePath {
    param(
        [string] $Path,
        [string] $Description
    )

    if (-not $Path.StartsWith($resolvedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "$Description 必須位於工作區內。"
    }
}

function Get-PackageVersionFromDirectoryProps {
    param(
        [string] $PackageId
    )

    $escapedPackageId = [System.Text.RegularExpressions.Regex]::Escape($PackageId)
    $pattern = 'PackageVersion Include="' + $escapedPackageId + '" Version="([^"]+)"'
    $matches = @(Select-String -Path (Join-Path $rootDirectory "Directory.Packages.props") -Pattern $pattern)
    if ($matches.Count -ne 1) {
        throw "無法解析唯一的 $PackageId 版本。"
    }

    return $matches[0].Matches[0].Groups[1].Value
}

function Get-ProjectPackageVersion {
    $matches = @(Select-String -Path (Join-Path $rootDirectory "Directory.Build.props") -Pattern "<PackageVersion>([^<]+)</PackageVersion>")
    if ($matches.Count -ne 1) {
        throw "無法解析唯一的 PackageVersion。"
    }

    return $matches[0].Matches[0].Groups[1].Value
}

function Copy-SampleTree {
    param(
        [string] $Source,
        [string] $Destination
    )

    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    foreach ($item in Get-ChildItem -LiteralPath $Source -Force) {
        if ($item.PSIsContainer -and ($item.Name -in @("bin", "obj"))) {
            continue
        }

        $target = Join-Path $Destination $item.Name
        if ($item.PSIsContainer) {
            Copy-SampleTree -Source $item.FullName -Destination $target
        }
        else {
            Copy-Item -LiteralPath $item.FullName -Destination $target -Force
        }
    }
}

function Save-XmlDocument {
    param(
        [xml] $Document,
        [string] $Path
    )

    $settings = [System.Xml.XmlWriterSettings]::new()
    $settings.Encoding = [System.Text.UTF8Encoding]::new($true)
    $settings.Indent = $true
    $settings.NewLineChars = "`r`n"
    $settings.NewLineHandling = [System.Xml.NewLineHandling]::Replace
    $writer = [System.Xml.XmlWriter]::Create($Path, $settings)
    try {
        $Document.Save($writer)
    }
    finally {
        $writer.Close()
    }
}

function Convert-SampleProjectToPackageReference {
    param(
        [string] $ProjectPath,
        [string] $PackageId
    )

    [xml] $project = Get-Content -LiteralPath $ProjectPath -Raw
    # 用 SelectNodes XPath 走訪而非屬性存取，避免 Set-StrictMode 對「ItemGroup 沒有
    # ProjectReference 子節點」的情境擲 PropertyNotFoundException（如 MauiSample 把
    # MauiIcon / MauiSplashScreen 放在獨立 ItemGroup 時）。
    foreach ($projectReference in @($project.Project.SelectNodes("ItemGroup/ProjectReference"))) {
        [void] $projectReference.ParentNode.RemoveChild($projectReference)
    }

    $targetItemGroup = @($project.Project.ItemGroup)[0]
    if ($null -eq $targetItemGroup) {
        $targetItemGroup = $project.CreateElement("ItemGroup")
        [void] $project.Project.AppendChild($targetItemGroup)
    }

    # Samples 共用 SampleRuntime.cs / SampleFeatureController.cs / SampleEncodingHelper.cs
    # 等 helper，這些 helper 直接使用 .Externals 的 downloader / process runner，
    # .Runtime 的 MpvRuntimeInstaller，.Diagnostics 的 LicenseAuditor 等。所以
    # 從 ProjectReference 轉 PackageReference 時必須補齊所有相依套件，否則
    # consumer 模擬會 build 失敗。
    $packagesToAdd = @(
        $PackageId,
        "MediaEmbedKit.Mpv.Externals",
        "MediaEmbedKit.Mpv.Runtime",
        "MediaEmbedKit.Mpv.Diagnostics"
    )
    foreach ($pkg in $packagesToAdd) {
        $packageReference = $project.CreateElement("PackageReference")
        [void] $packageReference.SetAttribute("Include", $pkg)
        [void] $targetItemGroup.AppendChild($packageReference)
    }

    Save-XmlDocument -Document $project -Path $ProjectPath
}

function Write-TextFile {
    param(
        [string] $Path,
        [string] $Content
    )

    $directory = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $Content | Set-Content -LiteralPath $Path -Encoding utf8
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

$resolvedPackageDirectory = Resolve-WorkspacePath -Path $PackageDirectory
$resolvedWorkDirectory = Resolve-WorkspacePath -Path $WorkDirectory
$resolvedRuntimeDirectory = Resolve-WorkspacePath -Path $RuntimeDirectory

Assert-WorkspacePath -Path $resolvedPackageDirectory -Description "套件資料夾"
Assert-WorkspacePath -Path $resolvedWorkDirectory -Description "GUI consumer 驗證工作資料夾"
Assert-WorkspacePath -Path $resolvedRuntimeDirectory -Description "GUI consumer runtime 資料夾"

if (-not $SkipPackageValidation) {
    & (Join-Path $rootDirectory "tools/Invoke-PackageValidation.ps1") -Configuration $Configuration -OutputDirectory $PackageDirectory
    if ($LASTEXITCODE -ne 0) {
        throw "NuGet 套件驗證失敗。"
    }
}

if ((Test-Path -LiteralPath $resolvedWorkDirectory) -and -not $KeepWorkDirectory) {
    Remove-Item -LiteralPath $resolvedWorkDirectory -Recurse -Force
}

New-Item -ItemType Directory -Path $resolvedWorkDirectory -Force | Out-Null
$env:NUGET_PACKAGES = Join-Path $resolvedWorkDirectory ".nuget-packages"
Copy-SampleTree -Source (Join-Path $rootDirectory "samples") -Destination (Join-Path $resolvedWorkDirectory "samples")

$packageVersion = Get-ProjectPackageVersion
$packageSource = [System.Security.SecurityElement]::Escape($resolvedPackageDirectory)
$avaloniaVersion = Get-PackageVersionFromDirectoryProps -PackageId "Avalonia"
$avaloniaDesktopVersion = Get-PackageVersionFromDirectoryProps -PackageId "Avalonia.Desktop"
$avaloniaFluentVersion = Get-PackageVersionFromDirectoryProps -PackageId "Avalonia.Themes.Fluent"
$mauiControlsVersion = Get-PackageVersionFromDirectoryProps -PackageId "Microsoft.Maui.Controls"
$windowsAppSdkVersion = Get-PackageVersionFromDirectoryProps -PackageId "Microsoft.WindowsAppSDK"

Write-TextFile -Path (Join-Path $resolvedWorkDirectory "NuGet.config") -Content @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local-packages" value="$packageSource" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
"@

Write-TextFile -Path (Join-Path $resolvedWorkDirectory "Directory.Build.props") -Content @"
<Project>
  <PropertyGroup>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <Deterministic>true</Deterministic>
  </PropertyGroup>
</Project>
"@

Write-TextFile -Path (Join-Path $resolvedWorkDirectory "Directory.Packages.props") -Content @"
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="MediaEmbedKit.Mpv" Version="$packageVersion" />
    <PackageVersion Include="MediaEmbedKit.Mpv.Externals" Version="$packageVersion" />
    <PackageVersion Include="MediaEmbedKit.Mpv.Runtime" Version="$packageVersion" />
    <PackageVersion Include="MediaEmbedKit.Mpv.Diagnostics" Version="$packageVersion" />
    <PackageVersion Include="MediaEmbedKit.Mpv.Hosting" Version="$packageVersion" />
    <PackageVersion Include="MediaEmbedKit.Mpv.WinForms" Version="$packageVersion" />
    <PackageVersion Include="MediaEmbedKit.Mpv.Wpf" Version="$packageVersion" />
    <PackageVersion Include="MediaEmbedKit.Mpv.Avalonia" Version="$packageVersion" />
    <PackageVersion Include="MediaEmbedKit.Mpv.WinUI" Version="$packageVersion" />
    <PackageVersion Include="MediaEmbedKit.Mpv.Maui.Windows" Version="$packageVersion" />
    <PackageVersion Include="MediaEmbedKit.Mpv.Full" Version="$packageVersion" />
    <PackageVersion Include="Avalonia" Version="$avaloniaVersion" />
    <PackageVersion Include="Avalonia.Desktop" Version="$avaloniaDesktopVersion" />
    <PackageVersion Include="Avalonia.Themes.Fluent" Version="$avaloniaFluentVersion" />
    <PackageVersion Include="Microsoft.Maui.Controls" Version="$mauiControlsVersion" />
    <PackageVersion Include="Microsoft.WindowsAppSDK" Version="$windowsAppSdkVersion" />
  </ItemGroup>
</Project>
"@

$sampleProjects = @(
    @{
        Project = "samples/WinFormsSample/MediaEmbedKit.Mpv.Samples.WinForms.csproj"
        Package = "MediaEmbedKit.Mpv.WinForms"
    },
    @{
        Project = "samples/WpfSample/MediaEmbedKit.Mpv.Samples.Wpf.csproj"
        Package = "MediaEmbedKit.Mpv.Wpf"
    },
    @{
        Project = "samples/AvaloniaSample/MediaEmbedKit.Mpv.Samples.Avalonia.csproj"
        Package = "MediaEmbedKit.Mpv.Avalonia"
    },
    @{
        Project = "samples/WinUISample/MediaEmbedKit.Mpv.Samples.WinUI.csproj"
        Package = "MediaEmbedKit.Mpv.WinUI"
    },
    @{
        Project = "samples/MauiSample/MediaEmbedKit.Mpv.Samples.Maui.csproj"
        Package = "MediaEmbedKit.Mpv.Maui.Windows"
    }
)

foreach ($sampleProject in $sampleProjects) {
    Convert-SampleProjectToPackageReference `
        -ProjectPath (Join-Path $resolvedWorkDirectory $sampleProject.Project) `
        -PackageId $sampleProject.Package
}

$playbackSmokeProject = Join-Path $rootDirectory "tests/MediaEmbedKit.Mpv.PlaybackSmoke/MediaEmbedKit.Mpv.PlaybackSmoke.csproj"
$arguments = @(
    "run",
    "--project",
    $playbackSmokeProject,
    "--configuration",
    $Configuration,
    "--",
    "--sample-root",
    $resolvedWorkDirectory,
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
    throw "GUI consumer 實際播放驗證失敗。"
}

Write-Host "GUI consumer 實際播放驗證完成：$resolvedWorkDirectory"

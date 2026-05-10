param(
    [string] $Configuration = "Release",
    [string] $PackageDirectory = "artifacts/packages",
    [string] $WorkDirectory = ".tmp/consumer-package-validation",
    [switch] $SkipPackageValidation
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$rootDirectory = Split-Path -Parent $PSScriptRoot
$resolvedRoot = [System.IO.Path]::GetFullPath($rootDirectory)
$resolvedPackageDirectory = [System.IO.Path]::GetFullPath((Join-Path $rootDirectory $PackageDirectory))
$resolvedWorkDirectory = [System.IO.Path]::GetFullPath((Join-Path $rootDirectory $WorkDirectory))

if (-not $resolvedWorkDirectory.StartsWith($resolvedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "consumer 驗證工作資料夾必須位於工作區內。"
}

if (-not $resolvedPackageDirectory.StartsWith($resolvedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "套件資料夾必須位於工作區內。"
}

if (-not $SkipPackageValidation) {
    & (Join-Path $rootDirectory "tools/Invoke-PackageValidation.ps1") -Configuration $Configuration -OutputDirectory $PackageDirectory
    if ($LASTEXITCODE -ne 0) {
        throw "NuGet 套件驗證失敗。"
    }
}

if (Test-Path -LiteralPath $resolvedWorkDirectory) {
    Remove-Item -LiteralPath $resolvedWorkDirectory -Recurse -Force
}

New-Item -ItemType Directory -Path $resolvedWorkDirectory | Out-Null
$env:NUGET_PACKAGES = Join-Path $resolvedWorkDirectory ".nuget-packages"

$versionMatch = @(Select-String -Path (Join-Path $rootDirectory "Directory.Build.props") -Pattern "<PackageVersion>([^<]+)</PackageVersion>")
if ($versionMatch.Count -ne 1) {
    throw "無法解析唯一的 PackageVersion。"
}

$packageVersion = $versionMatch.Matches[0].Groups[1].Value
$mauiControlsVersionMatch = @(Select-String -Path (Join-Path $rootDirectory "Directory.Packages.props") -Pattern 'PackageVersion Include="Microsoft\.Maui\.Controls" Version="([^"]+)"')
if ($mauiControlsVersionMatch.Count -ne 1) {
    throw "無法解析唯一的 Microsoft.Maui.Controls 版本。"
}

$mauiControlsVersion = $mauiControlsVersionMatch.Matches[0].Groups[1].Value
$nugetConfigPath = Join-Path $resolvedWorkDirectory "NuGet.config"
$packageSource = [System.Security.SecurityElement]::Escape($resolvedPackageDirectory)

@"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local-packages" value="$packageSource" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
"@ | Set-Content -LiteralPath $nugetConfigPath -Encoding utf8

@"
<Project>
  <PropertyGroup>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>false</ImplicitUsings>
  </PropertyGroup>
</Project>
"@ | Set-Content -LiteralPath (Join-Path $resolvedWorkDirectory "Directory.Build.props") -Encoding utf8

@"
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
  </PropertyGroup>
</Project>
"@ | Set-Content -LiteralPath (Join-Path $resolvedWorkDirectory "Directory.Packages.props") -Encoding utf8

function Write-ConsumerFile {
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

function New-ConsumerProject {
    param(
        [string] $Name,
        [string] $ProjectText,
        [string] $CodeText
    )

    $projectDirectory = Join-Path $resolvedWorkDirectory $Name
    New-Item -ItemType Directory -Path $projectDirectory | Out-Null
    Write-ConsumerFile -Path (Join-Path $projectDirectory "$Name.csproj") -Content $ProjectText
    Write-ConsumerFile -Path (Join-Path $projectDirectory "Probe.cs") -Content $CodeText
    return Join-Path $projectDirectory "$Name.csproj"
}

function Invoke-ConsumerBuild {
    param(
        [string] $ProjectPath
    )

    & dotnet restore $ProjectPath --configfile $nugetConfigPath
    if ($LASTEXITCODE -ne 0) {
        throw "consumer restore 失敗：$ProjectPath"
    }

    & dotnet build $ProjectPath --configuration $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "consumer build 失敗：$ProjectPath"
    }
}

$projects = @()

$projects += New-ConsumerProject -Name "Consumer.Core" -ProjectText @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>false</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MediaEmbedKit.Mpv" Version="$packageVersion" />
  </ItemGroup>
</Project>
"@ -CodeText @"
using System;
using MediaEmbedKit.Mpv;

namespace Consumer.Core
{
    internal static class Probe
    {
        private static void Main()
        {
            MpvPlayerOptions options = new MpvPlayerOptions()
                .UseYtdlpMaximumHeight(720)
                .WithInitialOption("terminal", "no");
            Console.WriteLine(options.YtdlpFormat);
        }
    }
}
"@

$projects += New-ConsumerProject -Name "Consumer.WinForms" -ProjectText @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <EnableWindowsTargeting>true</EnableWindowsTargeting>
    <Nullable>enable</Nullable>
    <ImplicitUsings>false</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MediaEmbedKit.Mpv.WinForms" Version="$packageVersion" />
  </ItemGroup>
</Project>
"@ -CodeText @"
using System;
using System.Windows.Forms;
using MediaEmbedKit.Mpv.WinForms;

namespace Consumer.WinForms
{
    internal static class Probe
    {
        [STAThread]
        private static void Main()
        {
            using (MpvPlayerControl control = new MpvPlayerControl())
            {
                Console.WriteLine(control.GetType().FullName);
            }
        }
    }
}
"@

$projects += New-ConsumerProject -Name "Consumer.Wpf" -ProjectText @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <EnableWindowsTargeting>true</EnableWindowsTargeting>
    <Nullable>enable</Nullable>
    <ImplicitUsings>false</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MediaEmbedKit.Mpv.Wpf" Version="$packageVersion" />
  </ItemGroup>
</Project>
"@ -CodeText @"
using System;
using MediaEmbedKit.Mpv.Wpf;

namespace Consumer.Wpf
{
    public sealed class Probe
    {
        public Type ControlType
        {
            get { return typeof(MpvWpfPlayer); }
        }
    }
}
"@

$projects += New-ConsumerProject -Name "Consumer.Avalonia" -ProjectText @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <EnableWindowsTargeting>true</EnableWindowsTargeting>
    <Nullable>enable</Nullable>
    <ImplicitUsings>false</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MediaEmbedKit.Mpv.Avalonia" Version="$packageVersion" />
  </ItemGroup>
</Project>
"@ -CodeText @"
using System;
using MediaEmbedKit.Mpv.Avalonia;

namespace Consumer.Avalonia
{
    public sealed class Probe
    {
        public Type ControlType
        {
            get { return typeof(MpvAvaloniaPlayer); }
        }
    }
}
"@

$projects += New-ConsumerProject -Name "Consumer.WinUI" -ProjectText @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
    <UseWinUI>true</UseWinUI>
    <WindowsPackageType>None</WindowsPackageType>
    <Nullable>enable</Nullable>
    <ImplicitUsings>false</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MediaEmbedKit.Mpv.WinUI" Version="$packageVersion" />
  </ItemGroup>
</Project>
"@ -CodeText @"
using System;
using MediaEmbedKit.Mpv.WinUI;

namespace Consumer.WinUI
{
    public sealed class Probe
    {
        public Type ControlType
        {
            get { return typeof(MpvWinUiPlayer); }
        }
    }
}
"@

$projects += New-ConsumerProject -Name "Consumer.Maui" -ProjectText @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
    <UseMaui>true</UseMaui>
    <SingleProject>true</SingleProject>
    <WindowsPackageType>None</WindowsPackageType>
    <Nullable>enable</Nullable>
    <ImplicitUsings>false</ImplicitUsings>
    <SupportedOSPlatformVersion>10.0.17763.0</SupportedOSPlatformVersion>
    <TargetPlatformMinVersion>10.0.17763.0</TargetPlatformMinVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Maui.Controls" Version="$mauiControlsVersion" />
    <PackageReference Include="MediaEmbedKit.Mpv.Maui" Version="$packageVersion" />
  </ItemGroup>
</Project>
"@ -CodeText @"
using System;
using MediaEmbedKit.Mpv.Maui;

namespace Consumer.Maui
{
    public sealed class Probe
    {
        public Type ControlType
        {
            get { return typeof(MpvView); }
        }
    }
}
"@

foreach ($project in $projects) {
    Invoke-ConsumerBuild -ProjectPath $project
}

Write-Host "乾淨 consumer package 驗證完成：$resolvedWorkDirectory"

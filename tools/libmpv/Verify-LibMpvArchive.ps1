param(
    [string] $ArchivePath,

    [string] $DownloadUri,

    [string] $OutputDirectory = 'artifacts/libmpv-archive-check',

    [string] $SevenZipPath = '7z',

    [string] $UserAgent = 'MediaEmbedKit-Mpv-Agent/1.0'
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($ArchivePath) -and [string]::IsNullOrWhiteSpace($DownloadUri)) {
    throw '必須指定 ArchivePath 或 DownloadUri。'
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

if ([string]::IsNullOrWhiteSpace($ArchivePath)) {
    $fileName = [System.IO.Path]::GetFileName(([uri] $DownloadUri).AbsolutePath)
    $ArchivePath = Join-Path $OutputDirectory $fileName
    Invoke-WebRequest -Uri $DownloadUri -OutFile $ArchivePath -Headers @{ 'User-Agent' = $UserAgent }
}

if (-not (Test-Path -LiteralPath $ArchivePath)) {
    throw "找不到壓縮檔：$ArchivePath"
}

$extractDirectory = Join-Path $OutputDirectory ([System.IO.Path]::GetFileNameWithoutExtension($ArchivePath))
$resolvedOutputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path
$fullExtractDirectory = [System.IO.Path]::GetFullPath($extractDirectory)
$outputDirectoryPrefix = $resolvedOutputDirectory.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
if (-not $fullExtractDirectory.StartsWith($outputDirectoryPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "解壓縮目錄不在輸出目錄內：$fullExtractDirectory"
}

if (Test-Path -LiteralPath $extractDirectory) {
    Remove-Item -LiteralPath $extractDirectory -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $extractDirectory | Out-Null
& $SevenZipPath x $ArchivePath "-o$extractDirectory" -y | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "7z 解壓縮失敗，結束碼：$LASTEXITCODE"
}

$libMpv = Get-ChildItem -LiteralPath $extractDirectory -Recurse -File -Filter 'libmpv-2.dll' | Select-Object -First 1
[ordered]@{
    archivePath = (Resolve-Path -LiteralPath $ArchivePath).Path
    extractDirectory = (Resolve-Path -LiteralPath $extractDirectory).Path
    containsLibMpv2Dll = ($null -ne $libMpv)
    libMpv2DllPath = if ($null -eq $libMpv) { $null } else { $libMpv.FullName }
} | ConvertTo-Json -Depth 4


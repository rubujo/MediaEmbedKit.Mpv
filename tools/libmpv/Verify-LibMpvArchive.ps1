param(
    [string] $ArchivePath,

    [string] $DownloadUri,

    [string] $OutputDirectory = 'artifacts/libmpv-archive-check',

    [string] $SevenZipPath = '7z',

    [string] $UserAgent = 'MediaEmbedKit-Mpv-Agent/1.0',

    [string] $ExpectedSha256,

    [switch] $AllowUntrustedDownloadSource
)

$ErrorActionPreference = 'Stop'

function Test-OfficialLibMpvDownloadUri {
    param(
        [uri] $Uri
    )

    if ($Uri.Host -ne 'github.com') {
        return $false
    }

    return $Uri.AbsolutePath.StartsWith('/shinchiro/mpv-winbuild-cmake/releases/download/', [System.StringComparison]::OrdinalIgnoreCase) -or
        $Uri.AbsolutePath.StartsWith('/zhongfly/mpv-winbuild/releases/download/', [System.StringComparison]::OrdinalIgnoreCase)
}

if ([string]::IsNullOrWhiteSpace($ArchivePath) -and [string]::IsNullOrWhiteSpace($DownloadUri)) {
    throw '必須指定 ArchivePath 或 DownloadUri。'
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

if ([string]::IsNullOrWhiteSpace($ArchivePath)) {
    $downloadUriValue = [uri] $DownloadUri
    if (-not $AllowUntrustedDownloadSource -and -not (Test-OfficialLibMpvDownloadUri -Uri $downloadUriValue)) {
        throw 'DownloadUri 必須指向 shinchiro 或 zhongfly 官方 GitHub Releases 資產；若要驗證自訂來源，請明確指定 -AllowUntrustedDownloadSource。'
    }

    $fileName = [System.IO.Path]::GetFileName($downloadUriValue.AbsolutePath)
    $ArchivePath = Join-Path $OutputDirectory $fileName
    Invoke-WebRequest -Uri $downloadUriValue -OutFile $ArchivePath -Headers @{ 'User-Agent' = $UserAgent }
}

if (-not (Test-Path -LiteralPath $ArchivePath)) {
    throw "找不到壓縮檔：$ArchivePath"
}

$archiveSha256 = $null
if (-not [string]::IsNullOrWhiteSpace($ExpectedSha256)) {
    if ($ExpectedSha256 -notmatch '^[0-9a-fA-F]{64}$') {
        throw 'ExpectedSha256 必須是 64 個十六進位字元。'
    }

    $archiveSha256 = (Get-FileHash -LiteralPath $ArchivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($archiveSha256 -ne $ExpectedSha256.ToLowerInvariant()) {
        throw "壓縮檔 SHA-256 不符。預期：$($ExpectedSha256.ToLowerInvariant())，實際：$archiveSha256"
    }
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

$reparseEntries = @(Get-ChildItem -LiteralPath $extractDirectory -Recurse -Force | Where-Object {
    ($_.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0
})
if ($reparseEntries.Count -gt 0) {
    throw "壓縮檔解出 reparse point，拒絕驗證：$($reparseEntries[0].FullName)"
}

$libMpv = Get-ChildItem -LiteralPath $extractDirectory -Recurse -File -Filter 'libmpv-2.dll' | Select-Object -First 1
[ordered]@{
    archivePath = (Resolve-Path -LiteralPath $ArchivePath).Path
    sha256 = $archiveSha256
    extractDirectory = (Resolve-Path -LiteralPath $extractDirectory).Path
    containsReparsePoint = $false
    containsLibMpv2Dll = ($null -ne $libMpv)
    libMpv2DllPath = if ($null -eq $libMpv) { $null } else { $libMpv.FullName }
} | ConvertTo-Json -Depth 4


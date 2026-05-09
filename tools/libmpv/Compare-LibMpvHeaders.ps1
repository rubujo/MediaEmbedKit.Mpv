param(
    [string] $BaseCommit = 'v0.41.0',

    [Parameter(Mandatory = $true)]
    [string] $TargetCommit,

    [string] $OutputDirectory = 'artifacts/libmpv-api-check',

    [switch] $ForceDownload,

    [string] $UserAgent = 'MediaEmbedKit-Mpv-Agent/1.0'
)

$ErrorActionPreference = 'Stop'
$headerNames = @('client.h', 'render.h', 'render_gl.h', 'stream_cb.h')

function Save-Header {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Commit,

        [Parameter(Mandatory = $true)]
        [string] $HeaderName,

        [Parameter(Mandatory = $true)]
        [string] $RootDirectory,

        [Parameter(Mandatory = $true)]
        [bool] $DownloadAlways,

        [Parameter(Mandatory = $true)]
        [string] $HeaderUserAgent
    )

    $targetDirectory = Join-Path $RootDirectory $Commit
    New-Item -ItemType Directory -Force -Path $targetDirectory | Out-Null
    $targetPath = Join-Path $targetDirectory $HeaderName
    if ((Test-Path -LiteralPath $targetPath) -and -not $DownloadAlways) {
        return $targetPath
    }

    $uri = "https://raw.githubusercontent.com/mpv-player/mpv/$Commit/include/mpv/$HeaderName"
    Invoke-WebRequest -Uri $uri -OutFile $targetPath -Headers @{ 'User-Agent' = $HeaderUserAgent }
    return $targetPath
}

function Get-ClientApiVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string] $ClientHeaderPath
    )

    $content = Get-Content -LiteralPath $ClientHeaderPath -Raw
    $match = [regex]::Match($content, 'MPV_CLIENT_API_VERSION\s+MPV_MAKE_VERSION\((\d+),\s*(\d+)\)')
    if (-not $match.Success) {
        return $null
    }

    return "$($match.Groups[1].Value).$($match.Groups[2].Value)"
}

function Get-HeaderHash {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    return (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToLowerInvariant()
}

function Test-UnderscoreNameSupport {
    param(
        [Parameter(Mandatory = $true)]
        [string] $ClientHeaderPath
    )

    $content = Get-Content -LiteralPath $ClientHeaderPath -Raw
    return $content.Contains('key "_name"')
}

function Invoke-HeaderDiff {
    param(
        [Parameter(Mandatory = $true)]
        [string] $BasePath,

        [Parameter(Mandatory = $true)]
        [string] $TargetPath
    )

    $output = & git diff --no-index -- $BasePath $TargetPath 2>&1
    $exitCode = $LASTEXITCODE
    return [ordered]@{
        hasDifference = ($exitCode -ne 0)
        exitCode = $exitCode
        text = ($output -join [Environment]::NewLine)
    }
}

$baseHeaderPaths = @{}
$targetHeaderPaths = @{}
foreach ($headerName in $headerNames) {
    $baseHeaderPaths[$headerName] = Save-Header -Commit $BaseCommit -HeaderName $headerName -RootDirectory $OutputDirectory -DownloadAlways:$ForceDownload.IsPresent -HeaderUserAgent $UserAgent
    $targetHeaderPaths[$headerName] = Save-Header -Commit $TargetCommit -HeaderName $headerName -RootDirectory $OutputDirectory -DownloadAlways:$ForceDownload.IsPresent -HeaderUserAgent $UserAgent
}

$diffs = @{}
foreach ($headerName in $headerNames) {
    $diffs[$headerName] = Invoke-HeaderDiff -BasePath $baseHeaderPaths[$headerName] -TargetPath $targetHeaderPaths[$headerName]
}

$clientHeaderPath = $targetHeaderPaths['client.h']
[ordered]@{
    baseCommit = $BaseCommit
    targetCommit = $TargetCommit
    targetClientApiVersion = Get-ClientApiVersion -ClientHeaderPath $clientHeaderPath
    targetUsesUnderscoreName = Test-UnderscoreNameSupport -ClientHeaderPath $clientHeaderPath
    headers = @($headerNames | ForEach-Object {
        [ordered]@{
            name = $_
            baseSha256 = Get-HeaderHash -Path $baseHeaderPaths[$_]
            targetSha256 = Get-HeaderHash -Path $targetHeaderPaths[$_]
            hasDifference = [bool] $diffs[$_].hasDifference
        }
    })
    diffs = $diffs
} | ConvertTo-Json -Depth 8


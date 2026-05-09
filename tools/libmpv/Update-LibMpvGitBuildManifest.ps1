param(
    [string] $ManifestPath = 'docs/runtime/libmpv-git-builds.json',

    [Parameter(Mandatory = $true)]
    [string] $ShinchiroReleaseJson,

    [Parameter(Mandatory = $true)]
    [string] $ZhongflyReleaseJson,

    [string] $LastCheckedDate = (Get-Date -Format 'yyyy-MM-dd')
)

$ErrorActionPreference = 'Stop'

function Convert-ReleaseJson {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Json
    )

    if (Test-Path -LiteralPath $Json) {
        return Get-Content -LiteralPath $Json -Raw | ConvertFrom-Json
    }

    return $Json | ConvertFrom-Json
}

function New-ProviderEntry {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Release
    )

    [ordered]@{
        name = $Release.provider
        repository = $Release.repository
        releaseTag = $Release.releaseTag
        releaseUrl = $Release.releaseUrl
        publishedAt = $Release.publishedAt
        mpvCommit = $Release.mpvCommit
        sourceKind = 'git-build'
        cApiStatus = 'header-compatible'
        managedAction = 'Run Compare-LibMpvHeaders.ps1 and update managed API when public header shape changes.'
        notes = 'Generated from latest provider release metadata; confirm header diff before committing.'
    }
}

$shinchiro = Convert-ReleaseJson -Json $ShinchiroReleaseJson
$zhongfly = Convert-ReleaseJson -Json $ZhongflyReleaseJson
$manifest = [ordered]@{
    schemaVersion = 1
    lastCheckedDate = $LastCheckedDate
    stableBaseline = [ordered]@{
        mpvTag = 'v0.41.0'
        clientApiVersion = '2.5'
        headers = @('client.h', 'render.h', 'render_gl.h', 'stream_cb.h')
    }
    providers = @(
        New-ProviderEntry -Release $shinchiro
        New-ProviderEntry -Release $zhongfly
    )
}

$json = $manifest | ConvertTo-Json -Depth 6
$json = $json -replace "`r`n|`r|`n", "`n"
$json = $json -replace "`n", "`r`n"
$encoding = [System.Text.UTF8Encoding]::new($false)
$fullManifestPath = [System.IO.Path]::GetFullPath($ManifestPath)
$manifestDirectory = [System.IO.Path]::GetDirectoryName($fullManifestPath)
if (-not [string]::IsNullOrWhiteSpace($manifestDirectory)) {
    New-Item -ItemType Directory -Force -Path $manifestDirectory | Out-Null
}

[System.IO.File]::WriteAllText($fullManifestPath, $json, $encoding)


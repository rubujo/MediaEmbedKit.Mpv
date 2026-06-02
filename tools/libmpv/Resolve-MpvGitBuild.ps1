param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Shinchiro', 'Zhongfly')]
    [string] $Provider,

    [string] $ReleaseTag,

    [string] $UserAgent = 'MediaEmbedKit-Mpv-Agent/1.0'
)

$ErrorActionPreference = 'Stop'

function Get-RepositoryName {
    param(
        [Parameter(Mandatory = $true)]
        [string] $ProviderName
    )

    if ($ProviderName -eq 'Shinchiro') {
        return 'shinchiro/mpv-winbuild-cmake'
    }

    return 'zhongfly/mpv-winbuild'
}

function Get-MpvCommit {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Release
    )

    if ($Release.body -match 'mpv-player/mpv@`?([0-9a-fA-F]+)`?') {
        return $Matches[1].ToLowerInvariant()
    }

    foreach ($asset in $Release.assets) {
        if ($asset.name -match '^mpv(?:-dev|-debug)?-[^-]+(?:-v3)?-\d+-git-([0-9a-fA-F]+)\.7z$') {
            return $Matches[1].ToLowerInvariant()
        }
    }

    throw "無法從發行版 '$($Release.tag_name)' 解析 mpv commit。"
}

$repository = Get-RepositoryName -ProviderName $Provider
if ([string]::IsNullOrWhiteSpace($ReleaseTag)) {
    $uri = "https://api.github.com/repos/$repository/releases/latest"
}
else {
    $escapedTag = [uri]::EscapeDataString($ReleaseTag)
    $uri = "https://api.github.com/repos/$repository/releases/tags/$escapedTag"
}

$headers = @{
    'User-Agent' = $UserAgent
    'Accept' = 'application/vnd.github+json'
}

$release = Invoke-RestMethod -Uri $uri -Headers $headers
$mpvCommit = Get-MpvCommit -Release $release
$mpvAssets = @($release.assets | Where-Object { $_.name -like 'mpv-*.7z' -or $_.name -like 'mpv-dev-*.7z' } | ForEach-Object {
    [ordered]@{
        name = $_.name
        size = $_.size
        browserDownloadUrl = $_.browser_download_url
    }
})

[ordered]@{
    provider = $Provider
    repository = $repository
    releaseTag = $release.tag_name
    releaseUrl = $release.html_url
    publishedAt = $release.published_at
    mpvCommit = $mpvCommit
    assets = $mpvAssets
} | ConvertTo-Json -Depth 6


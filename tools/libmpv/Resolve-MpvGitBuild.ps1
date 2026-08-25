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
        if ($asset.name -match '^mpv(?:-dev|-debug|-dev-lgpl)?-[^-]+(?:-v3)?-\d+-git-([0-9a-fA-F]+)\.7z$') {
            return $Matches[1].ToLowerInvariant()
        }
    }

    throw "無法從發行版 '$($Release.tag_name)' 解析 mpv commit。"
}

$repository = Get-RepositoryName -ProviderName $Provider
$headers = @{
    'User-Agent' = $UserAgent
    'Accept' = 'application/vnd.github+json'
}

$token = if ($env:GITHUB_TOKEN) { $env:GITHUB_TOKEN } elseif ($env:GH_TOKEN) { $env:GH_TOKEN } else { $null }
if (-not [string]::IsNullOrWhiteSpace($token)) {
    $headers['Authorization'] = "Bearer $token"
}

$release = $null

try {
    if ([string]::IsNullOrWhiteSpace($ReleaseTag)) {
        $uri = "https://api.github.com/repos/$repository/releases/latest"
    }
    else {
        $escapedTag = [uri]::EscapeDataString($ReleaseTag)
        $uri = "https://api.github.com/repos/$repository/releases/tags/$escapedTag"
    }

    $release = Invoke-RestMethod -Uri $uri -Headers $headers
}
catch {
    $atomUri = "https://github.com/$repository/releases.atom"
    $atomContent = (Invoke-WebRequest -Uri $atomUri -Headers @{ 'User-Agent' = $UserAgent }).Content
    $atomXml = [xml]$atomContent

    $selectedEntry = $null
    if ([string]::IsNullOrWhiteSpace($ReleaseTag)) {
        $selectedEntry = $atomXml.feed.entry[0]
    }
    else {
        $selectedEntry = $atomXml.feed.entry | Where-Object { $_.id -like "*$ReleaseTag*" -or $_.link.href -like "*$ReleaseTag*" } | Select-Object -First 1
    }

    if (-not $selectedEntry) {
        throw "無法從 Atom feed 找到發行版 '$ReleaseTag'。"
    }

    $tag = if ($selectedEntry.id -match '/([^/]+)$') { $Matches[1] } else { $selectedEntry.title }
    $tagUrl = "https://github.com/$repository/releases/tag/$tag"
    $expandedUri = "https://github.com/$repository/releases/expanded_assets/$tag"

    $assets = @()
    try {
        $expandedContent = (Invoke-WebRequest -Uri $expandedUri -Headers @{ 'User-Agent' = $UserAgent }).Content
        $matches = [regex]::Matches($expandedContent, 'href="([^"]*/releases/download/[^"]+)"')
        foreach ($m in $matches) {
            $href = $m.Groups[1].Value
            $fileName = [System.IO.Path]::GetFileName($href)
            if ($fileName -like 'mpv-*.7z' -or $fileName -like 'mpv-dev-*.7z') {
                $assets += [ordered]@{
                    name = $fileName
                    size = 0
                    browserDownloadUrl = if ($href.StartsWith('http')) { $href } else { "https://github.com$href" }
                }
            }
        }
    }
    catch {
    }

    $release = [PSCustomObject]@{
        tag_name = $tag
        html_url = $tagUrl
        published_at = $selectedEntry.updated
        body = $selectedEntry.content.'#text'
        assets = $assets
    }
}

$mpvCommit = Get-MpvCommit -Release $release
$mpvAssets = @($release.assets | Where-Object { $_.name -like 'mpv-*.7z' -or $_.name -like 'mpv-dev-*.7z' } | ForEach-Object {
    [ordered]@{
        name = $_.name
        size = if ($_.size) { $_.size } else { 0 }
        browserDownloadUrl = if ($_.browser_download_url) { $_.browser_download_url } else { $_.browserDownloadUrl }
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


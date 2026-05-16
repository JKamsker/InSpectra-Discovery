param(
    [string]$InputPath = 'state/discovery/dotnet-tools.current.json',
    [string]$FilteredOutputPath = 'artifacts/index/dotnet-tools.clifx.json',
    [string]$PopularOutputPath = 'artifacts/index/dotnet-tools.clifx.popular.json',
    [int]$Top = 25,
    [int]$Concurrency = 12
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-GitHubRepository {
    param([AllowNull()][string]$Url)

    if ([string]::IsNullOrWhiteSpace($Url)) {
        return $null
    }

    $trimmed = $Url.Trim()
    if ($trimmed -notmatch '^https?://github\.com/(?<owner>[^/]+)/(?<repo>[^/#?]+)') {
        return $null
    }

    $owner = [string]$matches['owner']
    $repo = [string]$matches['repo']
    if ($repo.EndsWith('.git', [System.StringComparison]::OrdinalIgnoreCase)) {
        $repo = $repo.Substring(0, $repo.Length - 4)
    }

    if ([string]::IsNullOrWhiteSpace($owner) -or [string]::IsNullOrWhiteSpace($repo)) {
        return $null
    }

    return "$owner/$repo"
}

function Get-GitHubRepositoryMetadata {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Repository
    )

    $uri = "https://api.github.com/repos/$Repository"
    try {
        $response = Invoke-RestMethod -Uri $uri -Headers @{
            'User-Agent' = 'InSpectra-Discovery'
            'Accept' = 'application/vnd.github+json'
        }

        return [ordered]@{
            repository = $response.full_name
            stars = [int]$response.stargazers_count
            forks = [int]$response.forks_count
            watchers = [int]$response.subscribers_count
            htmlUrl = $response.html_url
        }
    }
    catch {
        return [ordered]@{
            repository = $Repository
            stars = $null
            forks = $null
            watchers = $null
            htmlUrl = "https://github.com/$Repository"
            error = $_.Exception.Message
        }
    }
}

$projectPath = Join-Path $PSScriptRoot '..\src\InSpectra.Discovery.Tool'
$resolvedInputPath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\$InputPath"))
$resolvedFilteredPath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\$FilteredOutputPath"))
$resolvedPopularPath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\$PopularOutputPath"))

dotnet run --project $projectPath -- catalog filter clifx --input $resolvedInputPath --output $resolvedFilteredPath --concurrency $Concurrency | Out-Null

$filtered = Get-Content -Raw $resolvedFilteredPath | ConvertFrom-Json
$packages = @($filtered.packages | Select-Object -First $Top)
$repoCache = @{}
$popularPackages = foreach ($package in $packages) {
    $repository = Resolve-GitHubRepository -Url $package.projectUrl
    if ($repository -and -not $repoCache.ContainsKey($repository)) {
        $repoCache[$repository] = Get-GitHubRepositoryMetadata -Repository $repository
    }

    [ordered]@{
        packageId = [string]$package.packageId
        latestVersion = [string]$package.latestVersion
        totalDownloads = $package.totalDownloads
        projectUrl = [string]$package.projectUrl
        description = [string]$package.description
        matchedDependencyIds = @($package.detection.matchedDependencyIds)
        toolCommandNames = @($package.detection.packageInspection.toolCommandNames)
        github = if ($repository) { $repoCache[$repository] } else { $null }
    }
}

$output = [ordered]@{
    generatedAt = [DateTimeOffset]::UtcNow.ToString('o')
    inputPath = $resolvedInputPath
    filteredOutputPath = $resolvedFilteredPath
    packageCount = @($filtered.packages).Count
    topCount = @($popularPackages).Count
    packages = @($popularPackages)
}

$directory = Split-Path -Parent $resolvedPopularPath
if (-not [string]::IsNullOrWhiteSpace($directory)) {
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
}

$json = $output | ConvertTo-Json -Depth 12
[System.IO.File]::WriteAllText($resolvedPopularPath, $json + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))

Write-Host "Filtered CliFx index: $resolvedFilteredPath"
Write-Host "Popular CliFx index:  $resolvedPopularPath"

[CmdletBinding()]
param(
    [string]$IndexPath = 'index/all.json',

    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$IndexFile = Resolve-Path $IndexPath
$Index = Get-Content $IndexFile -Raw | ConvertFrom-Json
$GeneratedAt = [DateTimeOffset]::UtcNow

function Write-JsonFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [object]$InputObject
    )

    $directory = Split-Path -Parent $Path
    if ($directory) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $json = $InputObject | ConvertTo-Json -Depth 100
    [System.IO.File]::WriteAllText($Path, $json + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
}

function Get-RelativeRepositoryPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    return ([System.IO.Path]::GetRelativePath($RepositoryRoot, $Path)) -replace '\\', '/'
}

function Invoke-JsonWithRetry {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Uri,

        [ValidateRange(1, 10)]
        [int]$MaxAttempts = 3
    )

    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        try {
            return Invoke-RestMethod -Uri $Uri
        }
        catch {
            if ($attempt -ge $MaxAttempts) {
                throw
            }

            Start-Sleep -Seconds ([Math]::Min(10, $attempt * 2))
        }
    }
}

function Get-RegistrationIndexUrl {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PackageId
    )

    $lowerId = $PackageId.ToLowerInvariant()
    return "https://api.nuget.org/v3/registration5-gz-semver2/$lowerId/index.json"
}

function Get-RegistrationLeaves {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PackageId
    )

    $indexDocument = Invoke-JsonWithRetry -Uri (Get-RegistrationIndexUrl -PackageId $PackageId)
    $leaves = [System.Collections.Generic.List[object]]::new()

    foreach ($page in @($indexDocument.items)) {
        $pageItems = if ($page.PSObject.Properties.Name -contains 'items' -and $null -ne $page.items) {
            @($page.items)
        }
        else {
            $pageDocument = Invoke-JsonWithRetry -Uri ([string]$page.'@id')
            @($pageDocument.items)
        }

        foreach ($leaf in $pageItems) {
            $leaves.Add($leaf)
        }
    }

    return @($leaves)
}

function Get-CatalogEntry {
    param([AllowNull()]$Leaf)

    if ($null -eq $Leaf) {
        return $null
    }

    $catalogEntry = $Leaf.catalogEntry
    if ($catalogEntry -is [string]) {
        return [ordered]@{
            id = $Leaf.id
            version = $Leaf.version
            listed = $null
            published = $null
            catalogEntryUrl = [string]$catalogEntry
        }
    }

    return [ordered]@{
        id = if ($catalogEntry.id) { [string]$catalogEntry.id } elseif ($Leaf.id) { [string]$Leaf.id } else { $null }
        version = if ($catalogEntry.version) { [string]$catalogEntry.version } elseif ($Leaf.version) { [string]$Leaf.version } else { $null }
        listed = if ($catalogEntry.PSObject.Properties.Name -contains 'listed') { $catalogEntry.listed } else { $null }
        published = if ($catalogEntry.PSObject.Properties.Name -contains 'published') { $catalogEntry.published } else { $null }
        catalogEntryUrl = if ($catalogEntry.PSObject.Properties.Name -contains '@id') { [string]$catalogEntry.'@id' } else { $null }
    }
}

$items = [System.Collections.Generic.List[object]]::new()
$skipped = [System.Collections.Generic.List[object]]::new()
$indexedVersionCount = 0

foreach ($package in @($Index.packages)) {
    $packageId = [string]$package.packageId
    $existingVersions = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

    foreach ($versionRecord in @($package.versions)) {
        if (-not [string]::IsNullOrWhiteSpace([string]$versionRecord.version)) {
            $null = $existingVersions.Add([string]$versionRecord.version)
            $indexedVersionCount++
        }
    }

    $leaves = Get-RegistrationLeaves -PackageId $packageId
    foreach ($leaf in $leaves) {
        $catalogEntry = Get-CatalogEntry -Leaf $leaf
        $version = [string]$catalogEntry.version

        if ([string]::IsNullOrWhiteSpace($version)) {
            $skipped.Add([ordered]@{
                packageId = $packageId
                reason = 'missing-version'
            })
            continue
        }

        if ($existingVersions.Contains($version)) {
            continue
        }

        $publishedAt = if ($catalogEntry.published) {
            try { ([DateTimeOffset]$catalogEntry.published).ToUniversalTime().ToString('o') } catch { $null }
        }
        else {
            $null
        }

        $items.Add([ordered]@{
            packageId = $packageId
            version = $version
            totalDownloads = $null
            packageUrl = "https://www.nuget.org/packages/$packageId/$version"
            packageContentUrl = if ($leaf.packageContent) { [string]$leaf.packageContent } else { $null }
            registrationLeafUrl = if ($leaf.'@id') { [string]$leaf.'@id' } else { $null }
            catalogEntryUrl = $catalogEntry.catalogEntryUrl
            publishedAt = $publishedAt
            listed = $catalogEntry.listed
            backfillKind = 'indexed-package-history'
        })
    }
}

$orderedItems = @(
    $items |
        Sort-Object `
            @{ Expression = { $_.packageId.ToLowerInvariant() } }, `
            @{ Expression = { if ($_.publishedAt) { [DateTimeOffset]$_.publishedAt } else { [DateTimeOffset]::MinValue } } }, `
            @{ Expression = { $_.version.ToLowerInvariant() } }
)

$queue = [ordered]@{
    schemaVersion = 1
    generatedAtUtc = $GeneratedAt.ToString('o')
    filter = 'indexed-package-history-backfill'
    sourceIndexPath = Get-RelativeRepositoryPath -Path $IndexFile.Path
    sourceGeneratedAtUtc = if ($Index.generatedAt) { ([DateTimeOffset]$Index.generatedAt).ToUniversalTime().ToString('o') } else { $null }
    sourceCurrentSnapshotPath = Get-RelativeRepositoryPath -Path $IndexFile.Path
    indexedPackageCount = @($Index.packages).Count
    indexedVersionCount = $indexedVersionCount
    itemCount = $orderedItems.Count
    batchPrefix = 'indexed-history-backfill'
    forceReanalyze = $false
    skippedCount = $skipped.Count
    skipped = @($skipped)
    items = $orderedItems
}

Write-JsonFile -Path $OutputPath -InputObject $queue

Write-Host "Indexed packages: $(@($Index.packages).Count)"
Write-Host "Already indexed versions: $indexedVersionCount"
Write-Host "Missing historical versions queued: $($orderedItems.Count)"
Write-Host "Skipped entries: $($skipped.Count)"
Write-Host "Queue path: $OutputPath"

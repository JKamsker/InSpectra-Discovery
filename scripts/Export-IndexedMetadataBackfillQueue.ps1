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

$items = [System.Collections.Generic.List[object]]::new()
$skipped = [System.Collections.Generic.List[object]]::new()

foreach ($package in @($Index.packages)) {
    foreach ($versionRecord in @($package.versions)) {
        $metadataPath = if ($versionRecord.paths -and $versionRecord.paths.metadataPath) {
            Join-Path $RepositoryRoot ([string]$versionRecord.paths.metadataPath -replace '/', [System.IO.Path]::DirectorySeparatorChar)
        }
        else {
            $null
        }

        if ([string]::IsNullOrWhiteSpace($metadataPath) -or -not (Test-Path $metadataPath)) {
            $skipped.Add([ordered]@{
                packageId = [string]$package.packageId
                version = [string]$versionRecord.version
                reason = 'missing-version-metadata'
            })
            continue
        }

        $metadata = Get-Content $metadataPath -Raw | ConvertFrom-Json
        $items.Add([ordered]@{
            packageId = [string]$metadata.packageId
            version = [string]$metadata.version
            totalDownloads = $null
            packageUrl = [string]$metadata.packageUrl
            packageContentUrl = [string]$metadata.packageContentUrl
            registrationLeafUrl = [string]$metadata.registrationLeafUrl
            catalogEntryUrl = [string]$metadata.catalogEntryUrl
            detection = $metadata.detection
            sourceMetadataPath = Get-RelativeRepositoryPath -Path $metadataPath
            backfillKind = 'indexed-version-metadata'
        })
    }
}

$orderedItems = @(
    $items |
        Sort-Object `
            @{ Expression = { $_.packageId.ToLowerInvariant() } }, `
            @{ Expression = { $_.version.ToLowerInvariant() } }
)

$queue = [ordered]@{
    schemaVersion = 1
    generatedAtUtc = $GeneratedAt.ToString('o')
    filter = 'indexed-package-version-metadata-backfill'
    sourceIndexPath = Get-RelativeRepositoryPath -Path $IndexFile.Path
    sourceGeneratedAtUtc = if ($Index.generatedAt) { ([DateTimeOffset]$Index.generatedAt).ToUniversalTime().ToString('o') } else { $null }
    sourceCurrentSnapshotPath = Get-RelativeRepositoryPath -Path $IndexFile.Path
    itemCount = $orderedItems.Count
    batchPrefix = 'indexed-version-backfill'
    forceReanalyze = $true
    skippedCount = $skipped.Count
    skipped = @($skipped)
    items = $orderedItems
}

Write-JsonFile -Path $OutputPath -InputObject $queue

Write-Host "Indexed package versions queued: $($orderedItems.Count)"
Write-Host "Skipped package versions: $($skipped.Count)"
Write-Host "Queue path: $OutputPath"

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BatchPath,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$BatchFile = Resolve-Path $BatchPath
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

    $json = $InputObject | ConvertTo-Json -Depth 50
    [System.IO.File]::WriteAllText($Path, $json + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
}

function Get-StatePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$LowerId,

        [Parameter(Mandatory = $true)]
        [string]$LowerVersion
    )

    return Join-Path $RepositoryRoot "state/packages/$LowerId/$LowerVersion.json"
}

function Get-ArtifactName {
    param(
        [Parameter(Mandatory = $true)]
        [string]$LowerId,

        [Parameter(Mandatory = $true)]
        [string]$LowerVersion
    )

    $raw = "analysis-$LowerId-$LowerVersion"
    return ([regex]::Replace($raw, '[^a-z0-9._-]+', '-')).Trim('-')
}

$batch = Get-Content $BatchFile -Raw | ConvertFrom-Json
$selectedItems = [System.Collections.Generic.List[object]]::new()
$skippedItems = [System.Collections.Generic.List[object]]::new()

foreach ($item in $batch.items) {
    $lowerId = $item.packageId.ToLowerInvariant()
    $lowerVersion = $item.version.ToLowerInvariant()
    $statePath = Get-StatePath -LowerId $lowerId -LowerVersion $lowerVersion
    $state = if (Test-Path $statePath) { Get-Content $statePath -Raw | ConvertFrom-Json } else { $null }

    if ($state) {
        $status = [string]$state.currentStatus
        $nextAttemptAt = if ($state.nextAttemptAt) { [DateTimeOffset]$state.nextAttemptAt } else { $null }

        if ($status -in @('success', 'terminal-negative', 'terminal-failure')) {
            $skippedItems.Add([ordered]@{
                packageId = $item.packageId
                version = $item.version
                reason = "existing-$status"
            })
            continue
        }

        if ($status -eq 'retryable-failure' -and $nextAttemptAt -and $nextAttemptAt -gt $GeneratedAt) {
            $skippedItems.Add([ordered]@{
                packageId = $item.packageId
                version = $item.version
                reason = 'backoff-active'
                nextAttemptAt = $nextAttemptAt.ToString('o')
            })
            continue
        }
    }

    $selectedItems.Add([ordered]@{
        packageId = $item.packageId
        version = $item.version
        totalDownloads = $item.totalDownloads
        packageUrl = $item.packageUrl
        packageContentUrl = $item.packageContentUrl
        catalogEntryUrl = $item.catalogEntryUrl
        attempt = if ($state) { [int]$state.attemptCount + 1 } else { 1 }
        artifactName = Get-ArtifactName -LowerId $lowerId -LowerVersion $lowerVersion
    })
}

$plan = [ordered]@{
    schemaVersion = 1
    batchId = $batch.batchId
    generatedAt = $GeneratedAt.ToString('o')
    sourceManifestPath = [System.IO.Path]::GetRelativePath($RepositoryRoot, $BatchFile.Path) -replace '\\', '/'
    sourceSnapshotPath = $batch.sourceSnapshotPath
    selectedCount = $selectedItems.Count
    skippedCount = $skippedItems.Count
    items = @($selectedItems)
    skipped = @($skippedItems)
}

Write-JsonFile -Path $OutputPath -InputObject $plan

Write-Host "Planned batch $($batch.batchId)"
Write-Host "Selected items: $($selectedItems.Count)"
Write-Host "Skipped items: $($skippedItems.Count)"

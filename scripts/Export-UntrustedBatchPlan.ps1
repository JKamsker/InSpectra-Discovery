[CmdletBinding(DefaultParameterSetName = 'Batch')]
param(
    [Parameter(Mandatory = $true, ParameterSetName = 'Batch')]
    [string]$BatchPath,

    [Parameter(Mandatory = $true, ParameterSetName = 'Queue')]
    [string]$QueuePath,

    [Parameter(Mandatory = $true, ParameterSetName = 'Queue')]
    [string]$BatchId,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath,

    [Parameter(ParameterSetName = 'Queue')]
    [ValidateRange(0, [int]::MaxValue)]
    [int]$Offset = 0,

    [Parameter(ParameterSetName = 'Queue')]
    [ValidateRange(1, [int]::MaxValue)]
    [int]$Take = [int]::MaxValue,

    [string]$TargetBranch = 'main'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
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

function Get-RelativeRepositoryPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    return ([System.IO.Path]::GetRelativePath($RepositoryRoot, $Path)) -replace '\\', '/'
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

function Get-BatchDefinition {
    if ($PSCmdlet.ParameterSetName -eq 'Batch') {
        $batchFile = Resolve-Path $BatchPath
        $batch = Get-Content $batchFile -Raw | ConvertFrom-Json
        return [ordered]@{
            batchId = [string]$batch.batchId
            items = @($batch.items)
            sourceManifestPath = Get-RelativeRepositoryPath -Path $batchFile.Path
            sourceSnapshotPath = [string]$batch.sourceSnapshotPath
            targetBranch = $TargetBranch
        }
    }

    $queueFile = Resolve-Path $QueuePath
    $queue = Get-Content $queueFile -Raw | ConvertFrom-Json
    $queueItems = @($queue.items)
    $selectedItems = if ($Offset -ge $queueItems.Count) {
        @()
    }
    else {
        @($queueItems | Select-Object -Skip $Offset -First $Take)
    }

    $sourceSnapshotPath = if ($queue.PSObject.Properties.Name -contains 'sourceCurrentSnapshotPath' -and $queue.sourceCurrentSnapshotPath) {
        [string]$queue.sourceCurrentSnapshotPath
    }
    elseif ($queue.PSObject.Properties.Name -contains 'inputDeltaPath' -and $queue.inputDeltaPath) {
        [string]$queue.inputDeltaPath
    }
    else {
        Get-RelativeRepositoryPath -Path $queueFile.Path
    }

    return [ordered]@{
        batchId = $BatchId
        items = $selectedItems
        sourceManifestPath = Get-RelativeRepositoryPath -Path $queueFile.Path
        sourceSnapshotPath = $sourceSnapshotPath
        targetBranch = $TargetBranch
    }
}

$batch = Get-BatchDefinition
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
    sourceManifestPath = $batch.sourceManifestPath
    sourceSnapshotPath = $batch.sourceSnapshotPath
    targetBranch = $batch.targetBranch
    selectedCount = $selectedItems.Count
    skippedCount = $skippedItems.Count
    items = @($selectedItems)
    skipped = @($skippedItems)
}

Write-JsonFile -Path $OutputPath -InputObject $plan

Write-Host "Planned batch $($batch.batchId)"
Write-Host "Target branch: $($batch.targetBranch)"
Write-Host "Selected items: $($selectedItems.Count)"
Write-Host "Skipped items: $($skippedItems.Count)"

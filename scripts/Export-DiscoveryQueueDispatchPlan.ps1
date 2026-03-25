[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$QueuePath,

    [Parameter(Mandatory = $true)]
    [string]$TargetBranch,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath,

    [ValidateRange(1, 250)]
    [int]$BatchSize = 250
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$QueueFile = Resolve-Path $QueuePath
$Queue = Get-Content $QueueFile -Raw | ConvertFrom-Json
$QueueItems = @($Queue.items)

function Write-JsonFile {
    param([string]$Path, [object]$InputObject)

    $directory = Split-Path -Parent $Path
    if ($directory) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $json = $InputObject | ConvertTo-Json -Depth 50
    [System.IO.File]::WriteAllText($Path, $json + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
}

function Get-RelativeRepositoryPath {
    param([string]$Path)
    return ([System.IO.Path]::GetRelativePath($RepositoryRoot, $Path)) -replace '\\', '/'
}

function Get-SanitizedBatchPrefix {
    param([string]$BranchName, [AllowNull()][string]$Timestamp)

    $normalizedBranch = ($BranchName.ToLowerInvariant() -replace '[^a-z0-9]+', '-').Trim('-')
    $normalizedTimestamp = if ([string]::IsNullOrWhiteSpace($Timestamp)) {
        [DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssZ').ToLowerInvariant()
    }
    else {
        $Timestamp.ToLowerInvariant() -replace '[^0-9tz]+', ''
    }

    return "discovery-queue-$normalizedBranch-$normalizedTimestamp"
}

$timestampSeed = if ($Queue.PSObject.Properties.Name -contains 'cursorEndUtc' -and $Queue.cursorEndUtc) {
    ([DateTimeOffset]$Queue.cursorEndUtc).ToUniversalTime().ToString('yyyyMMddTHHmmssZ')
}
else {
    [DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssZ')
}

$prefix = Get-SanitizedBatchPrefix -BranchName $TargetBranch -Timestamp $timestampSeed
$batches = [System.Collections.Generic.List[object]]::new()

for ($offset = 0; $offset -lt $QueueItems.Count; $offset += $BatchSize) {
    $take = [Math]::Min($BatchSize, $QueueItems.Count - $offset)
    $part = [int]($batches.Count + 1)

    $batches.Add([ordered]@{
        batchId = ('{0}-{1:000}' -f $prefix, $part)
        queuePath = Get-RelativeRepositoryPath -Path $QueueFile.Path
        offset = $offset
        take = $take
        targetBranch = $TargetBranch
        itemCount = $take
    })
}

$plan = [ordered]@{
    schemaVersion = 1
    generatedAt = [DateTimeOffset]::UtcNow.ToString('o')
    queuePath = Get-RelativeRepositoryPath -Path $QueueFile.Path
    targetBranch = $TargetBranch
    queueItemCount = $QueueItems.Count
    batchSize = $BatchSize
    batchCount = $batches.Count
    batches = @($batches)
}

Write-JsonFile -Path $OutputPath -InputObject $plan

Write-Host "Queue items: $($QueueItems.Count)"
Write-Host "Dispatch batches: $($batches.Count)"
Write-Host "Target branch: $TargetBranch"

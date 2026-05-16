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

    [switch]$ForceReanalyze,

    [string]$TargetBranch = 'main'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$GeneratedAt = [DateTimeOffset]::UtcNow

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

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

function Get-IsAllPrefixed {
    param(
        [AllowEmptyCollection()]
        [string[]]$Values,

        [Parameter(Mandatory = $true)]
        [string]$Prefix
    )

    if ($Values.Count -eq 0) {
        return $false
    }

    foreach ($value in $Values) {
        if (-not $value.StartsWith($Prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            return $false
        }
    }

    return $true
}

function Add-FrameworkName {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [System.Collections.Generic.HashSet[string]]$Frameworks,

        [AllowNull()]$Framework
    )

    if ($null -eq $Framework) {
        return
    }

    if ($Framework -is [string]) {
        if (-not [string]::IsNullOrWhiteSpace($Framework)) {
            $null = $Frameworks.Add($Framework.Trim())
        }

        return
    }

    if ($Framework.PSObject.Properties.Name -contains 'name' -and -not [string]::IsNullOrWhiteSpace([string]$Framework.name)) {
        $null = $Frameworks.Add(([string]$Framework.name).Trim())
    }
}

function Get-PackageRunnerSelection {
    param(
        [AllowNull()][string]$PackageContentUrl
    )

    $selection = [ordered]@{
        runsOn = 'ubuntu-latest'
        reason = 'default-ubuntu'
        requiredFrameworks = @()
        toolRids = @()
        runtimeRids = @()
        inspectionError = $null
    }

    if ([string]::IsNullOrWhiteSpace($PackageContentUrl)) {
        return $selection
    }

    $tempFile = Join-Path ([System.IO.Path]::GetTempPath()) ("inspectra-batch-" + [System.Guid]::NewGuid().ToString('n') + '.nupkg')
    $archive = $null

    try {
        Invoke-WebRequest -Uri $PackageContentUrl -OutFile $tempFile
        $archive = [System.IO.Compression.ZipFile]::OpenRead($tempFile)

        $frameworks = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
        $toolRids = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
        $runtimeRids = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

        foreach ($entry in $archive.Entries) {
            $entryPath = ($entry.FullName -replace '\\', '/').Trim('/')
            if ([string]::IsNullOrWhiteSpace($entryPath)) {
                continue
            }

            $segments = @($entryPath -split '/')

            if ($segments.Length -ge 4 -and $segments[0] -eq 'tools') {
                $rid = $segments[2]
                if (-not [string]::IsNullOrWhiteSpace($rid) -and $rid -ne 'any') {
                    $null = $toolRids.Add($rid)
                }
            }

            if ($segments.Length -ge 3 -and $segments[0] -eq 'runtimes') {
                $rid = $segments[1]
                if (-not [string]::IsNullOrWhiteSpace($rid)) {
                    $null = $runtimeRids.Add($rid)
                }
            }

            if ($entryPath.EndsWith('.runtimeconfig.json', [System.StringComparison]::OrdinalIgnoreCase)) {
                try {
                    $reader = New-Object System.IO.StreamReader($entry.Open())
                    try {
                        $runtimeConfig = $reader.ReadToEnd() | ConvertFrom-Json
                    }
                    finally {
                        $reader.Dispose()
                    }

                    $runtimeOptions = $runtimeConfig.runtimeOptions
                    if ($null -ne $runtimeOptions) {
                        if ($runtimeOptions.PSObject.Properties.Name -contains 'framework') {
                            Add-FrameworkName -Frameworks $frameworks -Framework $runtimeOptions.framework
                        }

                        if ($runtimeOptions.PSObject.Properties.Name -contains 'frameworks') {
                            foreach ($framework in @($runtimeOptions.frameworks)) {
                                Add-FrameworkName -Frameworks $frameworks -Framework $framework
                            }
                        }
                    }
                }
                catch {
                    $selection.inspectionError = $_.Exception.Message
                }
            }
        }

        $requiredFrameworks = @($frameworks | Sort-Object)
        $toolRidList = @($toolRids | Sort-Object)
        $runtimeRidList = @($runtimeRids | Sort-Object)

        $selection.requiredFrameworks = $requiredFrameworks
        $selection.toolRids = $toolRidList
        $selection.runtimeRids = $runtimeRidList

        if ($requiredFrameworks -contains 'Microsoft.WindowsDesktop.App') {
            $selection.runsOn = 'windows-latest'
            $selection.reason = 'framework-microsoft.windowsdesktop.app'
        }
        elseif (Get-IsAllPrefixed -Values $toolRidList -Prefix 'win') {
            $selection.runsOn = 'windows-latest'
            $selection.reason = 'tool-rids-windows-only'
        }
        elseif (Get-IsAllPrefixed -Values $runtimeRidList -Prefix 'win') {
            $selection.runsOn = 'windows-latest'
            $selection.reason = 'runtime-rids-windows-only'
        }
    }
    catch {
        $selection.reason = 'default-ubuntu-inspection-failed'
        $selection.inspectionError = $_.Exception.Message
    }
    finally {
        if ($null -ne $archive) {
            $archive.Dispose()
        }

        Remove-Item $tempFile -Force -ErrorAction SilentlyContinue
    }

    return $selection
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

    if ($state -and -not $ForceReanalyze) {
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

    $runnerSelection = Get-PackageRunnerSelection -PackageContentUrl $item.packageContentUrl

    $selectedItems.Add([ordered]@{
        packageId = $item.packageId
        version = $item.version
        totalDownloads = $item.totalDownloads
        packageUrl = $item.packageUrl
        packageContentUrl = $item.packageContentUrl
        catalogEntryUrl = $item.catalogEntryUrl
        attempt = if ($state) { [int]$state.attemptCount + 1 } else { 1 }
        artifactName = Get-ArtifactName -LowerId $lowerId -LowerVersion $lowerVersion
        runsOn = $runnerSelection.runsOn
        runnerReason = $runnerSelection.reason
        requiredFrameworks = $runnerSelection.requiredFrameworks
        toolRids = $runnerSelection.toolRids
        runtimeRids = $runnerSelection.runtimeRids
        inspectionError = $runnerSelection.inspectionError
    })
}

$plan = [ordered]@{
    schemaVersion = 1
    batchId = $batch.batchId
    generatedAt = $GeneratedAt.ToString('o')
    sourceManifestPath = $batch.sourceManifestPath
    sourceSnapshotPath = $batch.sourceSnapshotPath
    targetBranch = $batch.targetBranch
    forceReanalyze = $ForceReanalyze.IsPresent
    selectedCount = $selectedItems.Count
    skippedCount = $skippedItems.Count
    items = @($selectedItems)
    skipped = @($skippedItems)
}

Write-JsonFile -Path $OutputPath -InputObject $plan

Write-Host "Planned batch $($batch.batchId)"
Write-Host "Target branch: $($batch.targetBranch)"
Write-Host "Force reanalyze: $($ForceReanalyze.IsPresent)"
Write-Host "Selected items: $($selectedItems.Count)"
Write-Host "Skipped items: $($skippedItems.Count)"

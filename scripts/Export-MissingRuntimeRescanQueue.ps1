[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$GeneratedAt = [DateTimeOffset]::UtcNow

function Read-JsonFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    Get-Content -Path $Path -Raw | ConvertFrom-Json
}

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

function Get-OptionalPropertyValue {
    param(
        [AllowNull()][object]$InputObject,
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    if ($null -eq $InputObject) {
        return $null
    }

    if ($InputObject.PSObject.Properties.Name -contains $Name) {
        return $InputObject.$Name
    }

    return $null
}

function Convert-ToDateTimeOffsetOrNull {
    param([AllowNull()][object]$Value)

    if ($null -eq $Value -or [string]::IsNullOrWhiteSpace([string]$Value)) {
        return $null
    }

    return [DateTimeOffset]$Value
}

function Get-PackageIndexPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$LowerId
    )

    return Join-Path $RepositoryRoot "index/packages/$LowerId/index.json"
}

function Get-PackageIndexRecord {
    param(
        [Parameter(Mandatory = $true)]
        [string]$LowerId,

        [Parameter(Mandatory = $true)]
        [hashtable]$Cache
    )

    if ($Cache.ContainsKey($LowerId)) {
        return $Cache[$LowerId]
    }

    $path = Get-PackageIndexPath -LowerId $LowerId
    $value = if (Test-Path $path) { Read-JsonFile -Path $path } else { $null }
    $Cache[$LowerId] = $value
    return $value
}

function Get-IndexedVersionRecord {
    param(
        [AllowNull()][object]$PackageIndex,
        [Parameter(Mandatory = $true)]
        [string]$Version
    )

    if ($null -eq $PackageIndex) {
        return $null
    }

    foreach ($record in @($PackageIndex.versions)) {
        if ([string](Get-OptionalPropertyValue -InputObject $record -Name 'version') -eq $Version) {
            return $record
        }
    }

    return $null
}

function Get-LatestIndexedVersionRecord {
    param(
        [AllowNull()][object]$PackageIndex,
        [AllowNull()][string]$LatestVersion
    )

    if ($null -eq $PackageIndex) {
        return $null
    }

    if (-not [string]::IsNullOrWhiteSpace($LatestVersion)) {
        $matchingLatest = Get-IndexedVersionRecord -PackageIndex $PackageIndex -Version $LatestVersion
        if ($matchingLatest) {
            return $matchingLatest
        }
    }

    return @(
        @($PackageIndex.versions) |
            Sort-Object `
                @{ Expression = {
                    $publishedAt = Convert-ToDateTimeOffsetOrNull (Get-OptionalPropertyValue -InputObject $_ -Name 'publishedAt')
                    if ($null -ne $publishedAt) { $publishedAt } else { [DateTimeOffset]::MinValue }
                }; Descending = $true }, `
                @{ Expression = {
                    $evaluatedAt = Convert-ToDateTimeOffsetOrNull (Get-OptionalPropertyValue -InputObject $_ -Name 'evaluatedAt')
                    if ($null -ne $evaluatedAt) { $evaluatedAt } else { [DateTimeOffset]::MinValue }
                }; Descending = $true } |
            Select-Object -First 1
    )[0]
}

function Test-HasValidCurrentIndex {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Entry,

        [AllowNull()][object]$IndexedSummary,

        [AllowNull()][object]$PackageIndex
    )

    $knownVersion = [string]$Entry.latestVersion
    if ($IndexedSummary -and ([string]$IndexedSummary.latestVersion -eq $knownVersion)) {
        return $true
    }

    if ($null -eq $PackageIndex) {
        return $false
    }

    if (Get-IndexedVersionRecord -PackageIndex $PackageIndex -Version $knownVersion) {
        return $true
    }

    $knownPublishedAt = Convert-ToDateTimeOffsetOrNull (Get-OptionalPropertyValue -InputObject $Entry -Name 'publishedAtUtc')
    if ($null -eq $knownPublishedAt) {
        return $false
    }

    $latestIndexedRecord = Get-LatestIndexedVersionRecord -PackageIndex $PackageIndex -LatestVersion ([string](Get-OptionalPropertyValue -InputObject $IndexedSummary -Name 'latestVersion'))
    $indexedPublishedAt = if ($latestIndexedRecord) {
        Convert-ToDateTimeOffsetOrNull (Get-OptionalPropertyValue -InputObject $latestIndexedRecord -Name 'publishedAt')
    }
    else {
        $null
    }

    return $null -ne $indexedPublishedAt -and $indexedPublishedAt -ge $knownPublishedAt
}

function Get-StatePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$LowerId,

        [Parameter(Mandatory = $true)]
        [string]$LowerVersion
    )

    Join-Path $RepositoryRoot "state/packages/$LowerId/$LowerVersion.json"
}

function Test-IsMissingRuntimeFailure {
    param([AllowNull()][object]$StateRecord)

    if ($null -eq $StateRecord) {
        return $false
    }

    $signature = if ($StateRecord.PSObject.Properties.Name -contains 'lastFailureSignature') {
        [string]$StateRecord.lastFailureSignature
    }
    else {
        $null
    }

    if ($signature -match '\|environment-missing-runtime\|') {
        return $true
    }

    $message = if ($StateRecord.PSObject.Properties.Name -contains 'lastFailureMessage') {
        [string]$StateRecord.lastFailureMessage
    }
    else {
        $null
    }

    return $message -match '(?is)\byou must install or update \.net\b'
}

function Get-RequiredFrameworkVersion {
    param([AllowNull()][string]$Message)

    if ([string]::IsNullOrWhiteSpace($Message)) {
        return $null
    }

    $match = [regex]::Match($Message, "Framework:\s*'[^']+',\s*version\s*'([^']+)'")
    if ($match.Success) {
        return $match.Groups[1].Value
    }

    return $null
}

$base = Read-JsonFile -Path (Join-Path $RepositoryRoot 'artifacts/index/dotnet-tools.spectre-console-cli.json')
$delta = Read-JsonFile -Path (Join-Path $RepositoryRoot 'state/discovery/dotnet-tools.spectre-console-cli.delta.json')
$all = Read-JsonFile -Path (Join-Path $RepositoryRoot 'index/all.json')

$knownById = @{}
foreach ($package in $base.packages) {
    if (-not $package.packageId) {
        continue
    }

    $knownById[$package.packageId.ToLowerInvariant()] = $package
}

foreach ($package in $delta.packages) {
    if (-not $package.packageId) {
        continue
    }

    $lowerId = $package.packageId.ToLowerInvariant()
    if ($package.subsetChangeKind -eq 'left-subset') {
        $knownById.Remove($lowerId) | Out-Null
        continue
    }

    if ($package.current) {
        $knownById[$lowerId] = [pscustomobject]@{
            packageId = [string]$package.packageId
            latestVersion = [string](Get-OptionalPropertyValue -InputObject $package.current -Name 'latestVersion')
            totalDownloads = Get-OptionalPropertyValue -InputObject $package.current -Name 'totalDownloads'
            versionCount = Get-OptionalPropertyValue -InputObject $package.current -Name 'versionCount'
            listed = Get-OptionalPropertyValue -InputObject $package.current -Name 'listed'
            publishedAtUtc = Get-OptionalPropertyValue -InputObject $package.current -Name 'publishedAtUtc'
            commitTimestampUtc = Get-OptionalPropertyValue -InputObject $package.current -Name 'commitTimestampUtc'
            projectUrl = [string](Get-OptionalPropertyValue -InputObject $package.current -Name 'projectUrl')
            packageUrl = [string](Get-OptionalPropertyValue -InputObject $package.current -Name 'packageUrl')
            packageContentUrl = [string](Get-OptionalPropertyValue -InputObject $package.current -Name 'packageContentUrl')
            registrationUrl = [string](Get-OptionalPropertyValue -InputObject $package.current -Name 'registrationUrl')
            catalogEntryUrl = [string](Get-OptionalPropertyValue -InputObject $package.current -Name 'catalogEntryUrl')
            authors = [string](Get-OptionalPropertyValue -InputObject $package.current -Name 'authors')
            description = [string](Get-OptionalPropertyValue -InputObject $package.current -Name 'description')
            licenseExpression = [string](Get-OptionalPropertyValue -InputObject $package.current -Name 'licenseExpression')
            licenseUrl = [string](Get-OptionalPropertyValue -InputObject $package.current -Name 'licenseUrl')
            readmeUrl = [string](Get-OptionalPropertyValue -InputObject $package.current -Name 'readmeUrl')
            detection = Get-OptionalPropertyValue -InputObject $package.current -Name 'detection'
        }
        continue
    }

    $knownById[$lowerId] = [pscustomobject]@{
        packageId = [string]$package.packageId
        latestVersion = [string]$package.currentVersion
        totalDownloads = $null
        versionCount = $null
        listed = $null
        publishedAtUtc = $null
        commitTimestampUtc = $null
        projectUrl = $null
        packageUrl = $null
        packageContentUrl = $null
        registrationUrl = $null
        catalogEntryUrl = $null
        authors = $null
        description = $null
        licenseExpression = $null
        licenseUrl = $null
        readmeUrl = $null
        detection = $null
    }
}

$indexById = @{}
foreach ($package in $all.packages) {
    if (-not $package.packageId) {
        continue
    }

    $indexById[$package.packageId.ToLowerInvariant()] = $package
}

$items = New-Object System.Collections.Generic.List[object]
$frameworkCounts = @{}
$packageIndexCache = @{}

foreach ($entry in ($knownById.Values | Where-Object { $_ -and $_.packageId -and $_.latestVersion } | Sort-Object @{ Expression = { if ($_.totalDownloads) { [long]$_.totalDownloads } else { -1L } }; Descending = $true }, packageId)) {
    $lowerId = $entry.packageId.ToLowerInvariant()
    $indexed = if ($indexById.ContainsKey($lowerId)) { $indexById[$lowerId] } else { $null }
    $packageIndex = Get-PackageIndexRecord -LowerId $lowerId -Cache $packageIndexCache
    $hasValidCurrentIndex = Test-HasValidCurrentIndex -Entry $entry -IndexedSummary $indexed -PackageIndex $packageIndex
    if ($hasValidCurrentIndex) {
        continue
    }

    $statePath = Get-StatePath -LowerId $lowerId -LowerVersion $entry.latestVersion.ToLowerInvariant()
    if (-not (Test-Path $statePath)) {
        continue
    }

    $state = Read-JsonFile -Path $statePath
    if (-not (Test-IsMissingRuntimeFailure -StateRecord $state)) {
        continue
    }

    $frameworkVersion = Get-RequiredFrameworkVersion -Message ([string]$state.lastFailureMessage)
    if ($frameworkVersion) {
        if (-not $frameworkCounts.ContainsKey($frameworkVersion)) {
            $frameworkCounts[$frameworkVersion] = 0
        }

        $frameworkCounts[$frameworkVersion]++
    }

    $items.Add([ordered]@{
        packageId = [string]$entry.packageId
        version = [string]$entry.latestVersion
        broadChangeKind = 'runtime-rescan'
        subsetChangeKind = 'stayed-in-subset'
        totalDownloads = Get-OptionalPropertyValue -InputObject $entry -Name 'totalDownloads'
        packageUrl = [string](Get-OptionalPropertyValue -InputObject $entry -Name 'packageUrl')
        packageContentUrl = [string](Get-OptionalPropertyValue -InputObject $entry -Name 'packageContentUrl')
        registrationUrl = [string](Get-OptionalPropertyValue -InputObject $entry -Name 'registrationUrl')
        catalogEntryUrl = [string](Get-OptionalPropertyValue -InputObject $entry -Name 'catalogEntryUrl')
        detection = Get-OptionalPropertyValue -InputObject $entry -Name 'detection'
        lastKnownStatePath = (([System.IO.Path]::GetRelativePath($RepositoryRoot, $statePath)) -replace '\\', '/')
        requiredFrameworkVersion = $frameworkVersion
    }) | Out-Null
}

$orderedFrameworks = @(
    $frameworkCounts.GetEnumerator() |
        Sort-Object -Property @{ Expression = { $_.Value }; Descending = $true }, @{ Expression = { $_.Key }; Descending = $false } |
        ForEach-Object {
            [ordered]@{
                version = $_.Key
                count = $_.Value
            }
        }
)

$queue = [pscustomobject][ordered]@{
    generatedAtUtc = $GeneratedAt.ToString('o')
    filter = 'spectre-console-cli-runtime-rescan'
    sourceCurrentSnapshotPath = 'state/discovery/dotnet-tools.current.json'
    sourceSpectreSnapshotPath = 'artifacts/index/dotnet-tools.spectre-console-cli.json'
    sourceSpectreDeltaPath = 'state/discovery/dotnet-tools.spectre-console-cli.delta.json'
    sourceIndexPath = 'index/all.json'
    batchPrefix = 'runtime-rescan'
    itemCount = $items.Count
    requiredFrameworks = @($orderedFrameworks)
    items = $items.ToArray()
}

Write-JsonFile -Path $OutputPath -InputObject $queue

Write-Host "Runtime-missing rescan items queued: $($items.Count)"
if ($orderedFrameworks.Count -gt 0) {
    foreach ($framework in $orderedFrameworks) {
        Write-Host "  $($framework.version): $($framework.count)"
    }
}

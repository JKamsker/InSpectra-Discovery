param(
    [string]$Commit = 'HEAD',

    [ValidateSet('Combined', 'Recurrence', 'Depth', 'Nodes')]
    [string]$SortBy = 'Combined',

    [int]$Top = 100,

    [switch]$AddedOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-OpenCliOptionalPropertyValue {
    param(
        [AllowNull()][object]$InputObject,
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    if ($null -eq $InputObject) {
        return $null
    }

    $property = $InputObject.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $null
    }

    return $property.Value
}

function Get-OpenCliCollection {
    param([AllowNull()][object]$Value)

    if ($null -eq $Value) {
        return @()
    }

    return @($Value)
}

function Import-JsonDocument {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return $null
    }

    try {
        return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -Depth 100
    }
    catch {
        return $null
    }
}

function Measure-OpenCliNode {
    param(
        [AllowNull()][object]$Node,
        [string[]]$AncestorNames,
        [Parameter(Mandatory = $true)]
        [string]$NodePath,
        [Parameter(Mandatory = $true)]
        [int]$Depth,
        [Parameter(Mandatory = $true)]
        [hashtable]$State
    )

    if ($null -eq $Node) {
        return
    }

    $State.TotalNodes++
    if ($Depth -gt $State.MaxDepth) {
        $State.MaxDepth = $Depth
    }

    $name = [string](Get-OpenCliOptionalPropertyValue -InputObject $Node -Name 'name')
    $nextAncestors = $AncestorNames
    if (-not [string]::IsNullOrWhiteSpace($name)) {
        $normalizedName = $name.ToLowerInvariant()
        $recurrence = 1 + @($AncestorNames | Where-Object { $_ -eq $normalizedName }).Count
        if ($recurrence -gt $State.MaxRecurrence) {
            $State.MaxRecurrence = $recurrence
            $State.RepeatedName = $name
            $State.MaxRecurrencePath = $NodePath
        }

        $nextAncestors = @($AncestorNames + $normalizedName)
    }

    $childCommands = @(Get-OpenCliCollection -Value (Get-OpenCliOptionalPropertyValue -InputObject $Node -Name 'commands'))
    for ($index = 0; $index -lt $childCommands.Count; $index++) {
        Measure-OpenCliNode `
            -Node $childCommands[$index] `
            -AncestorNames $nextAncestors `
            -NodePath "$NodePath.commands[$index]" `
            -Depth ($Depth + 1) `
            -State $State
    }
}

function Measure-OpenCliArtifact {
    param([AllowNull()][object]$OpenCliDocument)

    $state = @{
        MaxRecurrence = 0
        RepeatedName = $null
        MaxRecurrencePath = $null
        TotalNodes = 0
        MaxDepth = 0
    }

    $rootCommands = @(Get-OpenCliCollection -Value (Get-OpenCliOptionalPropertyValue -InputObject $OpenCliDocument -Name 'commands'))
    for ($index = 0; $index -lt $rootCommands.Count; $index++) {
        Measure-OpenCliNode `
            -Node $rootCommands[$index] `
            -AncestorNames @() `
            -NodePath "$.commands[$index]" `
            -Depth 1 `
            -State $state
    }

    return [pscustomobject]@{
        MaxRecurrence = [int]$state.MaxRecurrence
        RepeatedName = [string]$state.RepeatedName
        MaxRecurrencePath = [string]$state.MaxRecurrencePath
        TotalNodes = [int]$state.TotalNodes
        MaxDepth = [int]$state.MaxDepth
    }
}

function Get-ChangedOpenCliArtifacts {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot,
        [Parameter(Mandatory = $true)]
        [string]$Commit,
        [switch]$AddedOnly
    )

    $diffLines = @(
        git -C $RepositoryRoot diff-tree --no-commit-id --name-status -r $Commit
    )

    foreach ($line in $diffLines) {
        if ($line -notmatch '^(?<Status>[AM])\s+(?<Path>index/packages/(?<PackageKey>[^/]+)/latest/opencli\.json)$') {
            continue
        }

        $status = $Matches.Status
        if ($AddedOnly.IsPresent -and $status -ne 'A') {
            continue
        }

        [pscustomobject]@{
            Status = $status
            PackageKey = $Matches.PackageKey
            OpenCliRelativePath = $Matches.Path
        }
    }
}

function Sort-MeasurementResults {
    param(
        [Parameter(Mandatory = $true)]
        [object[]]$Results,
        [Parameter(Mandatory = $true)]
        [string]$SortBy
    )

    switch ($SortBy) {
        'Recurrence' {
            return $Results | Sort-Object `
                @{ Expression = 'MaxRecurrence'; Descending = $true }, `
                @{ Expression = 'TotalNodes'; Descending = $true }, `
                @{ Expression = 'MaxDepth'; Descending = $true }, `
                @{ Expression = 'PackageId'; Descending = $false }
        }
        'Depth' {
            return $Results | Sort-Object `
                @{ Expression = 'MaxDepth'; Descending = $true }, `
                @{ Expression = 'TotalNodes'; Descending = $true }, `
                @{ Expression = 'MaxRecurrence'; Descending = $true }, `
                @{ Expression = 'PackageId'; Descending = $false }
        }
        'Nodes' {
            return $Results | Sort-Object `
                @{ Expression = 'TotalNodes'; Descending = $true }, `
                @{ Expression = 'MaxDepth'; Descending = $true }, `
                @{ Expression = 'MaxRecurrence'; Descending = $true }, `
                @{ Expression = 'PackageId'; Descending = $false }
        }
        default {
            return $Results | Sort-Object `
                @{ Expression = 'MaxRecurrence'; Descending = $true }, `
                @{ Expression = 'TotalNodes'; Descending = $true }, `
                @{ Expression = 'MaxDepth'; Descending = $true }, `
                @{ Expression = 'PackageId'; Descending = $false }
        }
    }
}

$repositoryRoot = (git rev-parse --show-toplevel).Trim()
$measurements = New-Object System.Collections.Generic.List[object]

foreach ($artifact in Get-ChangedOpenCliArtifacts -RepositoryRoot $repositoryRoot -Commit $Commit -AddedOnly:$AddedOnly.IsPresent) {
    $openCliPath = Join-Path $repositoryRoot ($artifact.OpenCliRelativePath -replace '/', [IO.Path]::DirectorySeparatorChar)
    $openCliDocument = Import-JsonDocument -Path $openCliPath
    if ($null -eq $openCliDocument) {
        continue
    }

    $packageRoot = Split-Path -Parent (Split-Path -Parent $openCliPath)
    $metadataPath = Join-Path (Join-Path $packageRoot 'latest') 'metadata.json'
    $metadataDocument = Import-JsonDocument -Path $metadataPath
    $measurement = Measure-OpenCliArtifact -OpenCliDocument $openCliDocument
    $inspectra = Get-OpenCliOptionalPropertyValue -InputObject $openCliDocument -Name 'x-inspectra'
    $packageId = [string](Get-OpenCliOptionalPropertyValue -InputObject $metadataDocument -Name 'packageId')
    if ([string]::IsNullOrWhiteSpace($packageId)) {
        $packageId = $artifact.PackageKey
    }

    $measurements.Add([pscustomobject]@{
            Status = $artifact.Status
            PackageId = $packageId
            Version = [string](Get-OpenCliOptionalPropertyValue -InputObject $metadataDocument -Name 'version')
            MaxRecurrence = $measurement.MaxRecurrence
            RepeatedName = $measurement.RepeatedName
            MaxRecurrencePath = $measurement.MaxRecurrencePath
            TotalNodes = $measurement.TotalNodes
            MaxDepth = $measurement.MaxDepth
            ArtifactSource = [string](Get-OpenCliOptionalPropertyValue -InputObject $inspectra -Name 'artifactSource')
            HelpDocumentCount = Get-OpenCliOptionalPropertyValue -InputObject $inspectra -Name 'helpDocumentCount'
            OpenCliPath = $artifact.OpenCliRelativePath
        })
}

$sortedMeasurements = @(Sort-MeasurementResults -Results $measurements.ToArray() -SortBy $SortBy)
if ($Top -gt 0) {
    $sortedMeasurements = @($sortedMeasurements | Select-Object -First $Top)
}

$sortedMeasurements

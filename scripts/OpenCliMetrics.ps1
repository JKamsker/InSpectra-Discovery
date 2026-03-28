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

function Test-OpenCliHasText {
    param([AllowNull()][object]$Value)

    if ($null -eq $Value) {
        return $false
    }

    return -not [string]::IsNullOrWhiteSpace([string]$Value)
}

function Test-OpenCliHidden {
    param([AllowNull()][object]$Node)

    $hiddenValue = Get-OpenCliOptionalPropertyValue -InputObject $Node -Name 'hidden'
    if ($null -eq $hiddenValue) {
        return $false
    }

    return [bool]$hiddenValue
}

function Get-OpenCliVisibleItems {
    param([AllowNull()][object]$Value)

    return @(
        Get-OpenCliCollection -Value $Value |
            Where-Object { -not (Test-OpenCliHidden -Node $_) }
    )
}

function New-OpenCliMetricsState {
    return [ordered]@{
        commandGroupCount = 0
        commandCount = 0
        describedCommandCount = 0
        visibleOptionCount = 0
        describedOptionCount = 0
        visibleArgumentCount = 0
        describedArgumentCount = 0
        visibleLeafCommandCount = 0
        leafCommandWithExampleCount = 0
    }
}

function Add-OpenCliOptionMetrics {
    param(
        [Parameter(Mandatory = $true)]
        [object]$State,

        [AllowNull()][object]$Options
    )

    foreach ($option in @(Get-OpenCliVisibleItems -Value $Options)) {
        $State.visibleOptionCount++
        if (Test-OpenCliHasText -Value (Get-OpenCliOptionalPropertyValue -InputObject $option -Name 'description')) {
            $State.describedOptionCount++
        }
    }
}

function Add-OpenCliArgumentMetrics {
    param(
        [Parameter(Mandatory = $true)]
        [object]$State,

        [AllowNull()][object]$Arguments
    )

    foreach ($argument in @(Get-OpenCliVisibleItems -Value $Arguments)) {
        $State.visibleArgumentCount++
        if (Test-OpenCliHasText -Value (Get-OpenCliOptionalPropertyValue -InputObject $argument -Name 'description')) {
            $State.describedArgumentCount++
        }
    }
}

function Add-OpenCliCommandMetrics {
    param(
        [Parameter(Mandatory = $true)]
        [object]$State,

        [AllowNull()][object]$Commands
    )

    foreach ($command in @(Get-OpenCliVisibleItems -Value $Commands)) {
        $State.commandCount++
        if (Test-OpenCliHasText -Value (Get-OpenCliOptionalPropertyValue -InputObject $command -Name 'description')) {
            $State.describedCommandCount++
        }

        Add-OpenCliOptionMetrics -State $State -Options (Get-OpenCliOptionalPropertyValue -InputObject $command -Name 'options')
        Add-OpenCliArgumentMetrics -State $State -Arguments (Get-OpenCliOptionalPropertyValue -InputObject $command -Name 'arguments')

        $childCommands = @(Get-OpenCliVisibleItems -Value (Get-OpenCliOptionalPropertyValue -InputObject $command -Name 'commands'))
        if ($childCommands.Count -gt 0) {
            $State.commandGroupCount++
        }
        else {
            $State.visibleLeafCommandCount++
            $examples = @(
                Get-OpenCliCollection -Value (Get-OpenCliOptionalPropertyValue -InputObject $command -Name 'examples') |
                    Where-Object { Test-OpenCliHasText -Value $_ }
            )

            if ($examples.Count -gt 0) {
                $State.leafCommandWithExampleCount++
            }
        }

        Add-OpenCliCommandMetrics -State $State -Commands $childCommands
    }
}

function Get-OpenCliMetricsFromDocument {
    param([AllowNull()][object]$OpenCliDocument)

    $state = New-OpenCliMetricsState
    if ($null -eq $OpenCliDocument) {
        return [pscustomobject]@{
            commandGroupCount = 0
            commandCount = 0
            documentationCoveragePercent = 0
            documentedItemCount = 0
            documentableItemCount = 0
        }
    }

    Add-OpenCliOptionMetrics -State $state -Options (Get-OpenCliOptionalPropertyValue -InputObject $OpenCliDocument -Name 'options')
    Add-OpenCliArgumentMetrics -State $state -Arguments (Get-OpenCliOptionalPropertyValue -InputObject $OpenCliDocument -Name 'arguments')
    Add-OpenCliCommandMetrics -State $state -Commands (Get-OpenCliOptionalPropertyValue -InputObject $OpenCliDocument -Name 'commands')

    $documentedItemCount = `
        $state.describedCommandCount + `
        $state.describedOptionCount + `
        $state.describedArgumentCount + `
        $state.leafCommandWithExampleCount
    $documentableItemCount = `
        $state.commandCount + `
        $state.visibleOptionCount + `
        $state.visibleArgumentCount + `
        $state.visibleLeafCommandCount
    $documentationCoveragePercent = if ($documentableItemCount -gt 0) {
        [math]::Round(($documentedItemCount * 100.0) / $documentableItemCount, 4)
    }
    else {
        0.0
    }

    return [pscustomobject]@{
        commandGroupCount = $state.commandGroupCount
        commandCount = $state.visibleLeafCommandCount
        documentationCoveragePercent = $documentationCoveragePercent
        documentedItemCount = $documentedItemCount
        documentableItemCount = $documentableItemCount
    }
}

function Get-OpenCliMetricsFromPath {
    param([AllowNull()][string]$OpenCliPath)

    if ([string]::IsNullOrWhiteSpace($OpenCliPath) -or -not (Test-Path -LiteralPath $OpenCliPath)) {
        return Get-OpenCliMetricsFromDocument -OpenCliDocument $null
    }

    try {
        $document = Get-Content -Path $OpenCliPath -Raw | ConvertFrom-Json -Depth 100
    }
    catch {
        return Get-OpenCliMetricsFromDocument -OpenCliDocument $null
    }

    return Get-OpenCliMetricsFromDocument -OpenCliDocument $document
}

function Import-OpenCliJsonDocument {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return $null
    }

    try {
        return Get-Content -Path $Path -Raw | ConvertFrom-Json -Depth 100
    }
    catch {
        return $null
    }
}

function Resolve-ExistingOpenCliPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot,

        [AllowNull()][object[]]$RelativePaths
    )

    foreach ($relativePath in @($RelativePaths)) {
        $candidate = [string]$relativePath
        if ([string]::IsNullOrWhiteSpace($candidate)) {
            continue
        }

        $fullPath = Join-Path $RepositoryRoot $candidate
        if (Test-Path -LiteralPath $fullPath) {
            return $fullPath
        }
    }

    return $null
}

function Resolve-OpenCliPathForSummary {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Summary,

        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot
    )

    $latestPaths = Get-OpenCliOptionalPropertyValue -InputObject $Summary -Name 'latestPaths'
    foreach ($relativePath in @(
        Get-OpenCliOptionalPropertyValue -InputObject $latestPaths -Name 'opencliPath',
        Get-OpenCliOptionalPropertyValue -InputObject (Get-OpenCliOptionalPropertyValue -InputObject ((Get-OpenCliCollection -Value (Get-OpenCliOptionalPropertyValue -InputObject $Summary -Name 'versions') | Select-Object -First 1)) -Name 'paths') -Name 'opencliPath'
    )) {
        $candidatePath = Resolve-ExistingOpenCliPath -RepositoryRoot $RepositoryRoot -RelativePaths @($relativePath)
        if (-not $candidatePath) {
            continue
        }

        if ($null -ne (Import-OpenCliJsonDocument -Path $candidatePath)) {
            return $candidatePath
        }
    }

    $metadataPath = Resolve-ExistingOpenCliPath -RepositoryRoot $RepositoryRoot -RelativePaths @(
        Get-OpenCliOptionalPropertyValue -InputObject $latestPaths -Name 'metadataPath'
    )
    if ($metadataPath) {
        $metadata = Import-OpenCliJsonDocument -Path $metadataPath
        if ($null -eq $metadata) {
            return $null
        }

        $metadataOpenCliPath = Resolve-ExistingOpenCliPath -RepositoryRoot $RepositoryRoot -RelativePaths @(
            Get-OpenCliOptionalPropertyValue -InputObject (Get-OpenCliOptionalPropertyValue -InputObject $metadata -Name 'artifacts') -Name 'opencliPath',
            Get-OpenCliOptionalPropertyValue -InputObject (Get-OpenCliOptionalPropertyValue -InputObject (Get-OpenCliOptionalPropertyValue -InputObject $metadata -Name 'steps') -Name 'opencli') -Name 'path',
            Get-OpenCliOptionalPropertyValue -InputObject (Get-OpenCliOptionalPropertyValue -InputObject ((Get-OpenCliCollection -Value (Get-OpenCliOptionalPropertyValue -InputObject $Summary -Name 'versions') | Select-Object -First 1)) -Name 'paths') -Name 'opencliPath'
        )
        if ($metadataOpenCliPath -and $null -ne (Import-OpenCliJsonDocument -Path $metadataOpenCliPath)) {
            return $metadataOpenCliPath
        }
    }

    return $null
}

function Add-OpenCliMetricsToPackageSummary {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Summary,

        [Parameter(Mandatory = $true)]
        [object]$Metrics
    )

    $result = [ordered]@{}
    foreach ($property in $Summary.PSObject.Properties) {
        $result[$property.Name] = $property.Value
        if ($property.Name -eq 'latestStatus') {
            $result['commandGroupCount'] = $Metrics.commandGroupCount
            $result['commandCount'] = $Metrics.commandCount
        }
    }

    if (-not $result.Contains('commandGroupCount')) {
        $result['commandGroupCount'] = $Metrics.commandGroupCount
    }

    if (-not $result.Contains('commandCount')) {
        $result['commandCount'] = $Metrics.commandCount
    }

    return $result
}

function Sort-PackageSummariesForAllIndex {
    param(
        [Parameter(Mandatory = $true)]
        [array]$PackageSummaries,

        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot
    )

    $decorated = foreach ($summary in $PackageSummaries) {
        $openCliFullPath = Resolve-OpenCliPathForSummary -Summary $summary -RepositoryRoot $RepositoryRoot
        $metrics = Get-OpenCliMetricsFromPath -OpenCliPath $openCliFullPath
        [pscustomobject]@{
            summary = Add-OpenCliMetricsToPackageSummary -Summary $summary -Metrics $metrics
            metrics = $metrics
        }
    }

    return @(
        $decorated |
            Sort-Object `
                @{ Expression = { $_.metrics.commandGroupCount }; Descending = $true }, `
                @{ Expression = { $_.metrics.commandCount }; Descending = $true }, `
                @{ Expression = { $_.metrics.documentationCoveragePercent }; Descending = $true }, `
                @{ Expression = { [string]$_.summary.packageId }; Descending = $false } |
            ForEach-Object { $_.summary }
    )
}

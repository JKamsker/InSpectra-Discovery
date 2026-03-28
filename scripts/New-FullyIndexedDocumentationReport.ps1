param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$ManifestPath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'index/all.json'),
    [string]$OutputPath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'docs/Reports/fully-indexed-documentation-report.md')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-OptionalPropertyValue {
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

function Get-Collection {
    param([AllowNull()][object]$Value)

    if ($null -eq $Value) {
        return @()
    }

    return @($Value)
}

function Test-HasText {
    param([AllowNull()][object]$Value)

    if ($null -eq $Value) {
        return $false
    }

    return -not [string]::IsNullOrWhiteSpace([string]$Value)
}

function Test-IsHidden {
    param([AllowNull()][object]$Node)

    $hiddenValue = Get-OptionalPropertyValue -InputObject $Node -Name 'hidden'
    if ($null -eq $hiddenValue) {
        return $false
    }

    return [bool]$hiddenValue
}

function Get-VisibleItems {
    param([AllowNull()][object]$Value)

    return @(
        Get-Collection -Value $Value |
            Where-Object { -not (Test-IsHidden -Node $_) }
    )
}

function New-StatsState {
    return [ordered]@{
        visibleCommands = 0
        describedCommands = 0
        visibleOptions = 0
        describedOptions = 0
        visibleArguments = 0
        describedArguments = 0
        visibleLeafCommands = 0
        leafCommandsWithExamples = 0
        missingCommandDescriptions = [System.Collections.Generic.List[string]]::new()
        missingOptionDescriptions = [System.Collections.Generic.List[string]]::new()
        missingArgumentDescriptions = [System.Collections.Generic.List[string]]::new()
        missingLeafExamples = [System.Collections.Generic.List[string]]::new()
    }
}

function Add-OptionStats {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Stats,

        [Parameter(Mandatory = $true)]
        [AllowEmptyString()][string]$Location,

        [AllowNull()][object]$Options
    )

    foreach ($option in Get-VisibleItems -Value $Options) {
        $Stats.visibleOptions++
        $optionName = [string](Get-OptionalPropertyValue -InputObject $option -Name 'name')
        $qualifiedName = if ([string]::IsNullOrWhiteSpace($Location)) {
            $optionName
        }
        else {
            "$Location $optionName"
        }

        if (Test-HasText -Value (Get-OptionalPropertyValue -InputObject $option -Name 'description')) {
            $Stats.describedOptions++
        }
        else {
            $Stats.missingOptionDescriptions.Add($qualifiedName)
        }
    }
}

function Add-ArgumentStats {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Stats,

        [Parameter(Mandatory = $true)]
        [AllowEmptyString()][string]$Location,

        [AllowNull()][object]$Arguments
    )

    foreach ($argument in Get-VisibleItems -Value $Arguments) {
        $Stats.visibleArguments++
        $argumentName = [string](Get-OptionalPropertyValue -InputObject $argument -Name 'name')
        $qualifiedName = if ([string]::IsNullOrWhiteSpace($Location)) {
            "<$argumentName>"
        }
        else {
            "$Location <$argumentName>"
        }

        if (Test-HasText -Value (Get-OptionalPropertyValue -InputObject $argument -Name 'description')) {
            $Stats.describedArguments++
        }
        else {
            $Stats.missingArgumentDescriptions.Add($qualifiedName)
        }
    }
}

function Add-CommandStats {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Stats,

        [Parameter(Mandatory = $true)]
        [AllowEmptyString()][string]$ParentPath,

        [AllowNull()][object]$Commands
    )

    foreach ($command in @(Get-VisibleItems -Value $Commands)) {
        $commandName = [string](Get-OptionalPropertyValue -InputObject $command -Name 'name')
        $commandPath = if ([string]::IsNullOrWhiteSpace($ParentPath)) {
            $commandName
        }
        else {
            "$ParentPath $commandName"
        }

        $Stats.visibleCommands++

        if (Test-HasText -Value (Get-OptionalPropertyValue -InputObject $command -Name 'description')) {
            $Stats.describedCommands++
        }
        else {
            $Stats.missingCommandDescriptions.Add($commandPath)
        }

        Add-OptionStats -Stats $Stats -Location $commandPath -Options (Get-OptionalPropertyValue -InputObject $command -Name 'options')
        Add-ArgumentStats -Stats $Stats -Location $commandPath -Arguments (Get-OptionalPropertyValue -InputObject $command -Name 'arguments')

        $childCommands = @(Get-VisibleItems -Value (Get-OptionalPropertyValue -InputObject $command -Name 'commands'))
        if ($childCommands.Count -eq 0) {
            $Stats.visibleLeafCommands++
            $examples = @(
                Get-Collection -Value (Get-OptionalPropertyValue -InputObject $command -Name 'examples') |
                    Where-Object { Test-HasText -Value $_ }
            )

            if ($examples.Count -gt 0) {
                $Stats.leafCommandsWithExamples++
            }
            else {
                $Stats.missingLeafExamples.Add($commandPath)
            }
        }

        Add-CommandStats -Stats $Stats -ParentPath $commandPath -Commands $childCommands
    }
}

function Format-Coverage {
    param(
        [int]$Documented,
        [int]$Visible
    )

    return "$Documented/$Visible"
}

function Test-IsComplete {
    param(
        [int]$Documented,
        [int]$Visible
    )

    return $Documented -eq $Visible
}

function Get-FirstNonEmptyText {
    param([AllowNull()][object[]]$Values)

    foreach ($value in @($Values)) {
        $text = [string]$value
        if (-not [string]::IsNullOrWhiteSpace($text)) {
            return $text
        }
    }

    return $null
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

function Test-IsReportableOpenCliClassification {
    param([AllowNull()][string]$Classification)

    return $Classification -in @('json-ready', 'json-ready-with-nonzero-exit')
}

function ConvertTo-AnchorSlug {
    param([Parameter(Mandatory = $true)][string]$Value)

    $normalized = $Value.ToLowerInvariant()
    $normalized = [regex]::Replace($normalized, '[^a-z0-9]+', '-')
    $normalized = $normalized.Trim('-')

    if ([string]::IsNullOrWhiteSpace($normalized)) {
        return 'package'
    }

    return $normalized
}

function Format-ListOrNone {
    param(
        [AllowNull()][object]$Items
    )

    $values = @($Items)
    if ($values.Count -eq 0) {
        return 'None'
    }

    return ($values | Sort-Object -Unique) -join ', '
}

$manifest = Get-Content -Path $ManifestPath -Raw | ConvertFrom-Json -Depth 100
$reportRows = [System.Collections.Generic.List[object]]::new()

foreach ($package in Get-Collection -Value (Get-OptionalPropertyValue -InputObject $manifest -Name 'packages')) {
    $latestPaths = Get-OptionalPropertyValue -InputObject $package -Name 'latestPaths'
    $metadataRelativePath = [string](Get-OptionalPropertyValue -InputObject $latestPaths -Name 'metadataPath')
    if ([string]::IsNullOrWhiteSpace($metadataRelativePath)) {
        continue
    }

    $metadataPath = Join-Path $RepositoryRoot $metadataRelativePath
    if (-not (Test-Path -LiteralPath $metadataPath)) {
        continue
    }

    $metadata = Get-Content -Path $metadataPath -Raw | ConvertFrom-Json -Depth 100
    $packageStatus = [string](Get-OptionalPropertyValue -InputObject $metadata -Name 'status')
    $openCliClassification = [string](Get-OptionalPropertyValue -InputObject (Get-OptionalPropertyValue -InputObject (Get-OptionalPropertyValue -InputObject $metadata -Name 'introspection') -Name 'opencli') -Name 'classification')
    if ($packageStatus -ne 'ok') {
        continue
    }

    if (-not (Test-IsReportableOpenCliClassification -Classification $openCliClassification)) {
        continue
    }

    $latestVersionRecord = @(Get-Collection -Value (Get-OptionalPropertyValue -InputObject $package -Name 'versions')) | Select-Object -First 1
    $openCliPath = Resolve-ExistingOpenCliPath -RepositoryRoot $RepositoryRoot -RelativePaths @(
        Get-OptionalPropertyValue -InputObject $latestPaths -Name 'opencliPath',
        Get-OptionalPropertyValue -InputObject (Get-OptionalPropertyValue -InputObject $metadata -Name 'artifacts') -Name 'opencliPath',
        Get-OptionalPropertyValue -InputObject (Get-OptionalPropertyValue -InputObject (Get-OptionalPropertyValue -InputObject $metadata -Name 'steps') -Name 'opencli') -Name 'path',
        Get-OptionalPropertyValue -InputObject (Get-OptionalPropertyValue -InputObject $latestVersionRecord -Name 'paths') -Name 'opencliPath'
    )
    if (-not $openCliPath) {
        continue
    }

    $openCli = Get-Content -Path $openCliPath -Raw | ConvertFrom-Json -Depth 100
    $artifactSource = Get-FirstNonEmptyText -Values @(
        Get-OptionalPropertyValue -InputObject (Get-OptionalPropertyValue -InputObject $openCli -Name 'x-inspectra') -Name 'artifactSource',
        Get-OptionalPropertyValue -InputObject (Get-OptionalPropertyValue -InputObject $metadata -Name 'artifacts') -Name 'opencliSource',
        Get-OptionalPropertyValue -InputObject (Get-OptionalPropertyValue -InputObject (Get-OptionalPropertyValue -InputObject $metadata -Name 'steps') -Name 'opencli') -Name 'artifactSource',
        Get-OptionalPropertyValue -InputObject (Get-OptionalPropertyValue -InputObject (Get-OptionalPropertyValue -InputObject $metadata -Name 'introspection') -Name 'opencli') -Name 'artifactSource'
    )
    if ($artifactSource -ne 'tool-output') {
        continue
    }

    $stats = New-StatsState

    Add-OptionStats -Stats $stats -Location '[root]' -Options (Get-OptionalPropertyValue -InputObject $openCli -Name 'options')
    Add-ArgumentStats -Stats $stats -Location '[root]' -Arguments (Get-OptionalPropertyValue -InputObject $openCli -Name 'arguments')
    Add-CommandStats -Stats $stats -ParentPath '' -Commands (Get-OptionalPropertyValue -InputObject $openCli -Name 'commands')

    $commandsComplete = Test-IsComplete -Documented $stats.describedCommands -Visible $stats.visibleCommands
    $optionsComplete = Test-IsComplete -Documented $stats.describedOptions -Visible $stats.visibleOptions
    $argumentsComplete = Test-IsComplete -Documented $stats.describedArguments -Visible $stats.visibleArguments
    $examplesComplete = Test-IsComplete -Documented $stats.leafCommandsWithExamples -Visible $stats.visibleLeafCommands
    $overallComplete = $commandsComplete -and $optionsComplete -and $argumentsComplete -and $examplesComplete

    $packageId = [string](Get-OptionalPropertyValue -InputObject $metadata -Name 'packageId')
    if ([string]::IsNullOrWhiteSpace($packageId)) {
        $packageId = [string](Get-OptionalPropertyValue -InputObject $package -Name 'packageId')
    }

    $version = [string](Get-OptionalPropertyValue -InputObject $metadata -Name 'version')
    $anchor = 'pkg-' + (ConvertTo-AnchorSlug -Value $packageId)
    $xmlDoc = Get-OptionalPropertyValue -InputObject (Get-OptionalPropertyValue -InputObject (Get-OptionalPropertyValue -InputObject $metadata -Name 'introspection') -Name 'xmldoc') -Name 'classification'

    $reportRows.Add([pscustomobject]@{
            PackageId = $packageId
            Version = $version
            PackageStatus = $packageStatus
            OpenCliClassification = $openCliClassification
            XmlDocClassification = if (Test-HasText -Value $xmlDoc) { [string]$xmlDoc } else { 'n/a' }
            CommandsCoverage = Format-Coverage -Documented $stats.describedCommands -Visible $stats.visibleCommands
            OptionsCoverage = Format-Coverage -Documented $stats.describedOptions -Visible $stats.visibleOptions
            ArgumentsCoverage = Format-Coverage -Documented $stats.describedArguments -Visible $stats.visibleArguments
            ExamplesCoverage = Format-Coverage -Documented $stats.leafCommandsWithExamples -Visible $stats.visibleLeafCommands
            OverallComplete = $overallComplete
            Anchor = $anchor
            MissingCommandDescriptions = Format-ListOrNone -Items $stats.missingCommandDescriptions
            MissingOptionDescriptions = Format-ListOrNone -Items $stats.missingOptionDescriptions
            MissingArgumentDescriptions = Format-ListOrNone -Items $stats.missingArgumentDescriptions
            MissingLeafExamples = Format-ListOrNone -Items $stats.missingLeafExamples
        })
}

$sortedRows = @(
    $reportRows |
        Sort-Object `
            @{ Expression = { if ($_.OverallComplete) { 1 } else { 0 } } }, `
            @{ Expression = { $_.PackageId.ToLowerInvariant() } }
)

$fullyDocumentedCount = @($sortedRows | Where-Object { $_.OverallComplete }).Count
$incompleteCount = $sortedRows.Count - $fullyDocumentedCount
$generatedAt = Get-Date -Format 'yyyy-MM-dd HH:mm:ssK'

$reportDirectory = Split-Path -Parent $OutputPath
if (-not (Test-Path -LiteralPath $reportDirectory)) {
    New-Item -ItemType Directory -Path $reportDirectory | Out-Null
}

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('# Fully Indexed Package Documentation Report')
$lines.Add('')
$lines.Add("Generated: $generatedAt")
$lines.Add('')
$lines.Add("Scope: latest package entries with status `ok`, whose OpenCLI classification is `json-ready` or `json-ready-with-nonzero-exit`, and whose resolved OpenCLI provenance is `tool-output`.")
$lines.Add('')
$lines.Add("Completeness rule: visible commands, options, and arguments must all have non-empty descriptions, and every visible leaf command must have at least one non-empty example.")
$lines.Add('')
$lines.Add("Hidden commands, options, and arguments are excluded from the score.")
$lines.Add('')
$lines.Add("Packages in scope: $($sortedRows.Count)")
$lines.Add('')
$lines.Add("Fully documented: $fullyDocumentedCount")
$lines.Add('')
$lines.Add("Incomplete: $incompleteCount")
$lines.Add('')
$lines.Add('| Package | Version | Status | XML | Cmd Docs | Opt Docs | Arg Docs | Leaf Examples | Overall |')
$lines.Add('| --- | --- | --- | --- | --- | --- | --- | --- | --- |')

foreach ($row in $sortedRows) {
    $overall = if ($row.OverallComplete) { 'PASS' } else { 'FAIL' }
    $lines.Add("| [$($row.PackageId)](#$($row.Anchor)) | $($row.Version) | $($row.PackageStatus) | $($row.XmlDocClassification) | $($row.CommandsCoverage) | $($row.OptionsCoverage) | $($row.ArgumentsCoverage) | $($row.ExamplesCoverage) | $overall |")
}

$lines.Add('')
$lines.Add('## Package Details')

foreach ($row in $sortedRows) {
    $overall = if ($row.OverallComplete) { 'PASS' } else { 'FAIL' }
    $lines.Add('')
    $lines.Add("<a id=""$($row.Anchor)""></a>")
    $lines.Add("### $($row.PackageId)")
    $lines.Add('')
    $lines.Add('- Version: `' + $row.Version + '`')
    $lines.Add('- Package status: `' + $row.PackageStatus + '`')
    $lines.Add('- OpenCLI classification: `' + $row.OpenCliClassification + '`')
    $lines.Add('- XMLDoc classification: `' + $row.XmlDocClassification + '`')
    $lines.Add('- Command documentation: `' + $row.CommandsCoverage + '`')
    $lines.Add('- Option documentation: `' + $row.OptionsCoverage + '`')
    $lines.Add('- Argument documentation: `' + $row.ArgumentsCoverage + '`')
    $lines.Add('- Leaf command examples: `' + $row.ExamplesCoverage + '`')
    $lines.Add('- Overall: `' + $overall + '`')
    $lines.Add("- Missing command descriptions: $($row.MissingCommandDescriptions)")
    $lines.Add("- Missing option descriptions: $($row.MissingOptionDescriptions)")
    $lines.Add("- Missing argument descriptions: $($row.MissingArgumentDescriptions)")
    $lines.Add("- Missing leaf command examples: $($row.MissingLeafExamples)")
}

[System.IO.File]::WriteAllLines($OutputPath, $lines)
Write-Host "Wrote report to $OutputPath"

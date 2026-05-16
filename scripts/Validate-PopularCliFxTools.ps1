param(
    [string]$InputPath = 'artifacts/index/dotnet-tools.clifx.popular.json',
    [string]$OutputPath = 'artifacts/analysis/clifx/popular-validation.json',
    [ValidateSet('downloads', 'stars')]
    [string]$SortBy = 'downloads',
    [int]$Top = 5,
    [int]$CommandTimeoutSeconds = 5,
    [int]$AnalysisTimeoutSeconds = 90
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-RepoPath {
    param([Parameter(Mandatory = $true)][string]$RelativePath)

    return [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\$RelativePath"))
}

function ConvertTo-Slug {
    param([Parameter(Mandatory = $true)][string]$Value)

    $lower = $Value.ToLowerInvariant()
    return [System.Text.RegularExpressions.Regex]::Replace($lower, '[^a-z0-9._-]+', '-')
}

function Measure-OpenCliCommandCount {
    param([AllowNull()]$Nodes)

    $count = 0
    foreach ($node in @($Nodes)) {
        if ($null -eq $node) {
            continue
        }

        $count++
        $children = if ($node.PSObject.Properties['commands']) { $node.commands } else { $null }
        $count += Measure-OpenCliCommandCount -Nodes $children
    }

    return $count
}

function Read-JsonFile {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path $Path)) {
        return $null
    }

    return Get-Content -Raw $Path | ConvertFrom-Json
}

function Get-GroupedCounts {
    param(
        [Parameter(Mandatory = $true)]$Items,
        [Parameter(Mandatory = $true)][string]$PropertyName
    )

    $result = [ordered]@{}
    foreach ($item in @($Items)) {
        if ($null -eq $item) {
            continue
        }

        $value = [string]$item.$PropertyName
        if ([string]::IsNullOrWhiteSpace($value)) {
            $value = 'unknown'
        }

        if ($result.Contains($value)) {
            $result[$value] = [int]$result[$value] + 1
        }
        else {
            $result[$value] = 1
        }
    }

    return $result
}

function Get-GroupedCountsFromSequence {
    param(
        [Parameter(Mandatory = $true)]$Items,
        [Parameter(Mandatory = $true)][string]$PropertyName
    )

    $result = [ordered]@{}
    foreach ($item in @($Items)) {
        if ($null -eq $item) {
            continue
        }

        foreach ($value in @($item.$PropertyName)) {
            $normalized = [string]$value
            if ([string]::IsNullOrWhiteSpace($normalized)) {
                $normalized = 'unknown'
            }

            if ($result.Contains($normalized)) {
                $result[$normalized] = [int]$result[$normalized] + 1
            }
            else {
                $result[$normalized] = 1
            }
        }
    }

    return $result
}

function New-ValidationSummary {
    param(
        [Parameter(Mandatory = $true)]$Package,
        [Parameter(Mandatory = $true)][string]$OutputRoot,
        [Parameter(Mandatory = $true)][int]$ExitCode
    )

    $resultPath = Join-Path $OutputRoot 'result.json'
    $crawlPath = Join-Path $OutputRoot 'crawl.json'
    $openCliPath = Join-Path $OutputRoot 'opencli.json'

    $result = Read-JsonFile -Path $resultPath
    $crawl = Read-JsonFile -Path $crawlPath
    $openCli = Read-JsonFile -Path $openCliPath

    $captures = if ($crawl -and $crawl.PSObject.Properties['commands']) { @($crawl.commands) } else { @() }
    $timedOutCaptures = @($captures | Where-Object { $_.result -and $_.result.timedOut })
    $unparsedCaptures = @($captures | Where-Object { -not $_.parsed })
    $coverage = if ($result -and $result.PSObject.Properties['coverage']) { $result.coverage } else { $null }
    $requiredFrameworks = if ($coverage -and $coverage.PSObject.Properties['requiredFrameworks']) {
        @($coverage.requiredFrameworks | ForEach-Object {
            if ($_.name -and $_.version) {
                "$($_.name)@$($_.version)"
            }
            elseif ($_.name) {
                [string]$_.name
            }
        } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    }
    else {
        @()
    }

    return [ordered]@{
        packageId = [string]$Package.packageId
        version = [string]$Package.latestVersion
        totalDownloads = $Package.totalDownloads
        githubStars = if ($Package.github) { $Package.github.stars } else { $null }
        toolCommandNames = @($Package.toolCommandNames)
        exitCode = $ExitCode
        disposition = if ($result) { [string]$result.disposition } else { 'missing-result' }
        failureMessage = if ($result) { $result.failureMessage } else { 'No result artifact was written.' }
        totalMs = if ($result) { $result.timings.totalMs } else { $null }
        installMs = if ($result) { $result.timings.installMs } else { $null }
        crawlMs = if ($result) { $result.timings.crawlMs } else { $null }
        helpCoverageMode = if ($coverage) { [string]$coverage.helpCoverageMode } else { 'unknown' }
        commandGraphMode = if ($coverage) { [string]$coverage.commandGraphMode } else { 'unknown' }
        runtimeCompatibilityMode = if ($coverage) { [string]$coverage.runtimeCompatibilityMode } else { 'unknown' }
        capturedCommandCount = if ($coverage) { [int]$coverage.capturedCommandCount } else { $captures.Count }
        helpDocumentCount = if ($coverage) { [int]$coverage.helpDocumentCount } elseif ($crawl -and $crawl.PSObject.Properties['commandCount']) { [int]$crawl.commandCount } else { 0 }
        parsedCommandCount = if ($coverage) { [int]$coverage.parsedCommandCount } else { @($captures | Where-Object { $_.parsed }).Count }
        unparsedCommandCount = if ($coverage) { [int]$coverage.unparsedCommandCount } else { $unparsedCaptures.Count }
        timedOutCommandCount = if ($coverage) { [int]$coverage.timedOutCommandCount } else { $timedOutCaptures.Count }
        runtimeBlockedCommandCount = if ($coverage) { [int]$coverage.runtimeBlockedCommandCount } else { 0 }
        timedOutCommands = if ($coverage -and $coverage.PSObject.Properties['timedOutCommands']) { @($coverage.timedOutCommands) } else { @($timedOutCaptures | ForEach-Object { [string]$_.command }) }
        unparsedCommands = if ($coverage -and $coverage.PSObject.Properties['unparsedCommands']) { @($coverage.unparsedCommands) } else { @($unparsedCaptures | ForEach-Object { [string]$_.command }) }
        runtimeBlockedCommands = if ($coverage -and $coverage.PSObject.Properties['runtimeBlockedCommands']) { @($coverage.runtimeBlockedCommands) } else { @() }
        requiredFrameworks = $requiredFrameworks
        openCliCommandCount = if ($openCli -and $openCli.PSObject.Properties['commands']) { Measure-OpenCliCommandCount -Nodes $openCli.commands } else { 0 }
        resultPath = $resultPath
        crawlPath = if (Test-Path $crawlPath) { $crawlPath } else { $null }
        openCliPath = if (Test-Path $openCliPath) { $openCliPath } else { $null }
    }
}

$resolvedInputPath = Resolve-RepoPath -RelativePath $InputPath
$resolvedOutputPath = Resolve-RepoPath -RelativePath $OutputPath
$projectPath = Resolve-RepoPath -RelativePath 'src/InSpectra.Discovery.Tool'
$validationRoot = Split-Path -Parent $resolvedOutputPath
$batchId = 'popular-clifx-validation-' + [DateTimeOffset]::UtcNow.ToString('yyyyMMddHHmmss')

$index = Get-Content -Raw $resolvedInputPath | ConvertFrom-Json
$packages = @($index.packages)
if ($SortBy -eq 'stars') {
    $packages = @($packages | Sort-Object `
        @{ Expression = { if ($_.github) { [int]$_.github.stars } else { 0 } }; Descending = $true }, `
        @{ Expression = { [long]$_.totalDownloads }; Descending = $true }, `
        @{ Expression = { [string]$_.packageId }; Descending = $false })
}
else {
    $packages = @($packages | Sort-Object `
        @{ Expression = { [long]$_.totalDownloads }; Descending = $true }, `
        @{ Expression = { if ($_.github) { [int]$_.github.stars } else { 0 } }; Descending = $true }, `
        @{ Expression = { [string]$_.packageId }; Descending = $false })
}

$selectedPackages = @($packages | Select-Object -First $Top)
$summaries = foreach ($package in $selectedPackages) {
    $packageSlug = ConvertTo-Slug -Value ([string]$package.packageId)
    $outputRoot = Join-Path $validationRoot (Join-Path $packageSlug ([string]$package.latestVersion))
    New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null

    & dotnet run --project $projectPath -- analysis run-clifx `
        --package-id ([string]$package.packageId) `
        --version ([string]$package.latestVersion) `
        --output-root $outputRoot `
        --batch-id $batchId `
        --command-timeout-seconds $CommandTimeoutSeconds `
        --analysis-timeout-seconds $AnalysisTimeoutSeconds *> $null

    New-ValidationSummary -Package $package -OutputRoot $outputRoot -ExitCode $LASTEXITCODE
}

$outputDirectory = Split-Path -Parent $resolvedOutputPath
if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
    New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
}

$report = [ordered]@{
    generatedAt = [DateTimeOffset]::UtcNow.ToString('o')
    batchId = $batchId
    inputPath = $resolvedInputPath
    sortBy = $SortBy
    topCount = @($selectedPackages).Count
    commandTimeoutSeconds = $CommandTimeoutSeconds
    analysisTimeoutSeconds = $AnalysisTimeoutSeconds
    successCount = @($summaries | Where-Object { $_.disposition -eq 'success' }).Count
    failureCount = @($summaries | Where-Object { $_.disposition -ne 'success' }).Count
    totalTimedOutCommands = (@($summaries | ForEach-Object { $_.timedOutCommandCount } | Measure-Object -Sum).Sum ?? 0)
    helpCoverageModeCounts = Get-GroupedCounts -Items $summaries -PropertyName 'helpCoverageMode'
    commandGraphModeCounts = Get-GroupedCounts -Items $summaries -PropertyName 'commandGraphMode'
    runtimeCompatibilityModeCounts = Get-GroupedCounts -Items $summaries -PropertyName 'runtimeCompatibilityMode'
    requiredFrameworkCounts = Get-GroupedCountsFromSequence -Items $summaries -PropertyName 'requiredFrameworks'
    packages = @($summaries)
}

$json = $report | ConvertTo-Json -Depth 12
[System.IO.File]::WriteAllText($resolvedOutputPath, $json + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))

Write-Host "Popular CliFx validation report: $resolvedOutputPath"

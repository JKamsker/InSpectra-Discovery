[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$DownloadRoot,

    [string]$SummaryOutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$IndexRoot = Join-Path $RepositoryRoot 'index'
$PackagesRoot = Join-Path $IndexRoot 'packages'
$StateRoot = Join-Path $RepositoryRoot 'state'
$Now = [DateTimeOffset]::UtcNow

. (Join-Path $PSScriptRoot 'OpenCliSynthesis.ps1')
. (Join-Path $PSScriptRoot 'OpenCliMetrics.ps1')

function Write-JsonFile {
    param([string]$Path, [object]$InputObject)
    $directory = Split-Path -Parent $Path
    if ($directory) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }
    $json = $InputObject | ConvertTo-Json -Depth 100
    [System.IO.File]::WriteAllText($Path, $json + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
}

function Write-TextFile {
    param([string]$Path, [string]$Content)
    $directory = Split-Path -Parent $Path
    if ($directory) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }
    [System.IO.File]::WriteAllText($Path, $Content, [System.Text.UTF8Encoding]::new($false))
}

function Get-RelativeRepositoryPath {
    param([string]$Path)
    return ([System.IO.Path]::GetRelativePath($RepositoryRoot, $Path)) -replace '\\', '/'
}

function Convert-ToIsoTimestamp {
    param([AllowNull()][object]$Value)
    if ($null -eq $Value -or [string]::IsNullOrWhiteSpace([string]$Value)) { return $null }
    return ([DateTimeOffset]$Value).ToUniversalTime().ToString('o')
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

function Copy-ObjectProperties {
    param([AllowNull()][object]$InputObject)

    if ($null -eq $InputObject) {
        return $null
    }

    $clone = [ordered]@{}
    foreach ($property in $InputObject.PSObject.Properties) {
        $clone[$property.Name] = $property.Value
    }

    return $clone
}

function Get-ResultAttempt {
    param([AllowNull()][object]$Result)

    if ($null -eq $Result -or $null -eq $Result.attempt) {
        return 0
    }

    return [int]$Result.attempt
}

function Get-PackageVersionKey {
    param(
        [Parameter(Mandatory = $true)][string]$PackageId,
        [Parameter(Mandatory = $true)][string]$Version
    )

    return "$($PackageId.ToLowerInvariant())|$($Version.ToLowerInvariant())"
}

function Get-CompositeResultKey {
    param(
        [Parameter(Mandatory = $true)][string]$PackageId,
        [Parameter(Mandatory = $true)][string]$Version,
        [AllowNull()][string]$ArtifactName,
        [AllowNull()][string]$CommandName
    )

    $discriminator = if (-not [string]::IsNullOrWhiteSpace($ArtifactName)) {
        "artifact:$ArtifactName"
    }
    elseif (-not [string]::IsNullOrWhiteSpace($CommandName)) {
        "command:$CommandName"
    }
    else {
        ''
    }

    return "$(Get-PackageVersionKey -PackageId $PackageId -Version $Version)|$discriminator"
}

function Get-PlanItemKey {
    param([Parameter(Mandatory = $true)][object]$Item)

    return Get-CompositeResultKey `
        -PackageId ([string]$Item.packageId) `
        -Version ([string]$Item.version) `
        -ArtifactName (if ($Item.PSObject.Properties.Name -contains 'artifactName') { [string]$Item.artifactName } else { $null }) `
        -CommandName (if ($Item.PSObject.Properties.Name -contains 'command') { [string]$Item.command } else { $null })
}

function Get-ResultKeys {
    param([Parameter(Mandatory = $true)][object]$Result)

    $keys = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $packageId = [string]$Result.packageId
    $version = [string]$Result.version

    if ($Result.PSObject.Properties.Name -contains 'artifactName' -and -not [string]::IsNullOrWhiteSpace([string]$Result.artifactName)) {
        [void]$keys.Add((Get-CompositeResultKey -PackageId $packageId -Version $version -ArtifactName ([string]$Result.artifactName) -CommandName $null))
    }

    if ($Result.PSObject.Properties.Name -contains 'artifactDirectoryFullPath' -and -not [string]::IsNullOrWhiteSpace([string]$Result.artifactDirectoryFullPath)) {
        $artifactDirectoryName = Split-Path -Leaf ([string]$Result.artifactDirectoryFullPath)
        if (-not [string]::IsNullOrWhiteSpace($artifactDirectoryName)) {
            [void]$keys.Add((Get-CompositeResultKey -PackageId $packageId -Version $version -ArtifactName $artifactDirectoryName -CommandName $null))
        }
    }

    if ($Result.PSObject.Properties.Name -contains 'command' -and -not [string]::IsNullOrWhiteSpace([string]$Result.command)) {
        [void]$keys.Add((Get-CompositeResultKey -PackageId $packageId -Version $version -ArtifactName $null -CommandName ([string]$Result.command)))
    }

    return @($keys)
}

function Get-InferredAnalysisMode {
    param(
        [AllowNull()][object]$Result,
        [AllowNull()][object]$Item,
        [bool]$HasCrawlArtifact,
        [AllowNull()][string]$ArtifactDirectory
    )

    $explicitMode = Get-FirstNonEmptyText -Values @(
        if ($Result) { $Result.analysisMode } else { $null },
        if ($Item) { $Item.analysisMode } else { $null }
    )
    if ($explicitMode) {
        return $explicitMode
    }

    if (-not $HasCrawlArtifact -or [string]::IsNullOrWhiteSpace($ArtifactDirectory) -or -not $Result -or -not $Result.artifacts.crawlArtifact) {
        return $null
    }

    $crawlPath = Join-Path $ArtifactDirectory $Result.artifacts.crawlArtifact
    if (-not (Test-Path -LiteralPath $crawlPath)) {
        return $null
    }

    try {
        $crawl = Get-Content -Path $crawlPath -Raw | ConvertFrom-Json -Depth 100
        if ($crawl -and $crawl.PSObject.Properties.Name -contains 'staticCommands') {
            return 'clifx'
        }
    }
    catch {
        return 'help'
    }

    return 'help'
}

function Get-InferredOpenCliArtifactSource {
    param([AllowNull()][string]$AnalysisMode)

    switch ($AnalysisMode) {
        'native' { return 'tool-output' }
        'help' { return 'crawled-from-help' }
        'clifx' { return 'crawled-from-clifx-help' }
        'xmldoc' { return 'synthesized-from-xmldoc' }
        default { return $null }
    }
}

function Get-InferredOpenCliClassification {
    param(
        [AllowNull()][string]$ArtifactSource,
        [AllowNull()][object[]]$ExistingClassifications
    )

    if ($ArtifactSource -eq 'tool-output') {
        foreach ($existing in @($ExistingClassifications)) {
            if ([string]$existing -eq 'json-ready-with-nonzero-exit') {
                return 'json-ready-with-nonzero-exit'
            }
        }
    }

    switch ($ArtifactSource) {
        'tool-output' { return 'json-ready' }
        'crawled-from-help' { return 'help-crawl' }
        'crawled-from-clifx-help' { return 'clifx-crawl' }
        'synthesized-from-xmldoc' { return 'xmldoc-synthesized' }
        default { return $null }
    }
}

function Sync-LatestDirectory {
    param([string]$VersionDirectory, [string]$LatestDirectory)
    New-Item -ItemType Directory -Path $LatestDirectory -Force | Out-Null
    foreach ($artifactName in @('metadata.json', 'opencli.json', 'xmldoc.xml', 'crawl.json')) {
        $sourcePath = Join-Path $VersionDirectory $artifactName
        $targetPath = Join-Path $LatestDirectory $artifactName
        if (Test-Path $sourcePath) { Copy-Item -Path $sourcePath -Destination $targetPath -Force } else { Remove-Item -Path $targetPath -Force -ErrorAction SilentlyContinue }
    }
}

function Get-PackageSummary {
    param([array]$Records)
    $ordered = $Records | Sort-Object @{ Expression = { if ($_.publishedAt) { [DateTimeOffset]$_.publishedAt } else { [DateTimeOffset]::MinValue } }; Descending = $true }, @{ Expression = { [DateTimeOffset]$_.evaluatedAt }; Descending = $true }
    $latest = $ordered[0]
    $lowerId = $latest.packageId.ToLowerInvariant()
    [ordered]@{
        schemaVersion = 1
        packageId = $latest.packageId
        trusted = [bool]$latest.trusted
        latestVersion = $latest.version
        latestStatus = $latest.status
        latestPaths = [ordered]@{
            metadataPath = "index/packages/$lowerId/latest/metadata.json"
            opencliPath = if ($latest.artifacts.opencliPath) { "index/packages/$lowerId/latest/opencli.json" } else { $null }
            crawlPath = if ($latest.artifacts.crawlPath) { "index/packages/$lowerId/latest/crawl.json" } else { $null }
            xmldocPath = if ($latest.artifacts.xmldocPath) { "index/packages/$lowerId/latest/xmldoc.xml" } else { $null }
        }
        versions = @(
            $ordered | ForEach-Object {
                [ordered]@{
                    version = $_.version
                    publishedAt = Convert-ToIsoTimestamp $_.publishedAt
                    evaluatedAt = Convert-ToIsoTimestamp $_.evaluatedAt
                    status = $_.status
                    command = $_.command
                    timings = $_.timings
                    paths = $_.artifacts
                }
            }
        )
    }
}

function Get-StatePath {
    param([string]$LowerId, [string]$LowerVersion)
    return Join-Path $StateRoot "packages/$LowerId/$LowerVersion.json"
}

function Get-PackageIndexPath {
    param([string]$LowerId)
    return Join-Path $PackagesRoot "$LowerId/index.json"
}

function Get-BackoffHours {
    param([int]$Attempt)
    if ($Attempt -le 1) { return 1 }
    if ($Attempt -eq 2) { return 6 }
    return 24
}

function Get-DefaultReasonMessage {
    param(
        [AllowNull()][string]$Status,
        [AllowNull()][string]$Classification
    )

    switch ($Classification) {
        'spectre-cli-missing' { return 'No Spectre.Console.Cli evidence was found in the published package.' }
        'missing-result-artifact' { return 'No result artifact was uploaded for this matrix item.' }
        'missing-success-artifact' { return 'The analyzer reported success, but the expected success artifact was missing.' }
        'missing-result' { return 'No result was recorded for this matrix item.' }
        'environment-missing-runtime' { return 'The runner did not have the .NET runtime required by this tool.' }
        'environment-missing-dependency' { return 'The tool required a native dependency that is not available on the runner.' }
        'requires-interactive-input' { return 'The tool attempted to prompt for interactive input, which is not available in batch mode.' }
        'requires-interactive-authentication' { return 'The tool attempted an interactive authentication flow.' }
        'unsupported-platform' { return 'The tool does not support the runner operating system.' }
        'unsupported-command' { return 'The tool does not implement the expected introspection command.' }
        'invalid-json' { return 'The tool exited, but its JSON output could not be parsed.' }
        default {
            if ($Status -eq 'terminal-negative') {
                return 'The package did not satisfy the Spectre.Console.Cli prefilter.'
            }

            return 'No explicit reason was recorded.'
        }
    }
}

function Get-NonSuccessReason {
    param(
        [object]$Result,
        [object]$StateRecord
    )

    $message = if ($Result.failureMessage) {
        [string]$Result.failureMessage
    }
    elseif ($StateRecord.lastFailureMessage) {
        [string]$StateRecord.lastFailureMessage
    }
    else {
        Get-DefaultReasonMessage -Status $StateRecord.currentStatus -Classification $Result.classification
    }

    return $message.Trim()
}

function New-SyntheticFailureResult {
    param([object]$Item, [int]$Attempt, [string]$Classification, [string]$Message)
    [ordered]@{
        schemaVersion = 1
        packageId = $Item.packageId
        version = $Item.version
        batchId = $Plan.batchId
        attempt = $Attempt
        trusted = $false
        source = 'workflow_run'
        analyzedAt = $Now.ToString('o')
        disposition = 'retryable-failure'
        retryEligible = $true
        phase = 'infra'
        classification = $Classification
        failureMessage = $Message
        failureSignature = "infra|$Classification|$Message"
        packageUrl = $Item.packageUrl
        packageContentUrl = $Item.packageContentUrl
        registrationLeafUrl = $null
        catalogEntryUrl = $Item.catalogEntryUrl
        command = $null
        entryPoint = $null
        runner = $null
        toolSettingsPath = $null
        publishedAt = $null
        detection = [ordered]@{ hasSpectreConsole = $false; hasSpectreConsoleCli = $false; matchedPackageEntries = @(); matchedDependencyIds = @() }
        introspection = [ordered]@{ opencli = $null; xmldoc = $null }
        timings = [ordered]@{ totalMs = $null; installMs = $null; opencliMs = $null; xmldocMs = $null }
        steps = [ordered]@{ install = $null; opencli = $null; xmldoc = $null }
        artifacts = [ordered]@{ opencliArtifact = $null; xmldocArtifact = $null }
    }
}

function Update-StateRecord {
    param([AllowNull()][object]$ExistingState, [object]$Result, [AllowNull()][object]$IndexedPaths)

    $sameSignature = $ExistingState -and $ExistingState.lastFailureSignature -and $ExistingState.lastFailureSignature -eq $Result.failureSignature
    $consecutiveFailures = if ($Result.disposition -eq 'retryable-failure') { if ($sameSignature) { [int]$ExistingState.consecutiveFailureCount + 1 } else { 1 } } else { 0 }
    $attemptCount = [int]$Result.attempt
    $allowTerminalEscalation = -not (
        $Result.disposition -eq 'retryable-failure' -and
        $Result.classification -eq 'environment-missing-runtime'
    )

    $status = switch ($Result.disposition) {
        'success' { 'success'; break }
        'terminal-negative' { 'terminal-negative'; break }
        'terminal-failure' { 'terminal-failure'; break }
        default {
            if ($allowTerminalEscalation -and $consecutiveFailures -ge 3) { 'terminal-failure' } else { 'retryable-failure' }
        }
    }

    [ordered]@{
        schemaVersion = 1
        packageId = $Result.packageId
        version = $Result.version
        trusted = $false
        currentStatus = $status
        lastDisposition = $Result.disposition
        attemptCount = $attemptCount
        consecutiveFailureCount = $consecutiveFailures
        lastFailureSignature = if ($status -like '*failure') { $Result.failureSignature } else { $null }
        lastFailurePhase = if ($status -like '*failure') { $Result.phase } else { $null }
        lastFailureMessage = if ($status -like '*failure') { $Result.failureMessage } else { $null }
        firstEvaluatedAt = if ($ExistingState -and $ExistingState.firstEvaluatedAt) { $ExistingState.firstEvaluatedAt } else { $Result.analyzedAt }
        lastEvaluatedAt = $Result.analyzedAt
        lastBatchId = $Result.batchId
        retryEligible = $status -eq 'retryable-failure'
        nextAttemptAt = if ($status -eq 'retryable-failure') { $Now.AddHours((Get-BackoffHours -Attempt $attemptCount)).ToString('o') } else { $null }
        lastSuccessfulAt = if ($status -eq 'success') { $Result.analyzedAt } elseif ($ExistingState) { $ExistingState.lastSuccessfulAt } else { $null }
        indexedPaths = $IndexedPaths
    }
}

function Write-SuccessArtifacts {
    param([object]$Result, [string]$ArtifactDirectory)

    $lowerId = $Result.packageId.ToLowerInvariant()
    $lowerVersion = $Result.version.ToLowerInvariant()
    $versionRoot = Join-Path $PackagesRoot "$lowerId/$lowerVersion"
    $metadataPath = Join-Path $versionRoot 'metadata.json'
    $openCliPath = Join-Path $versionRoot 'opencli.json'
    $crawlPath = Join-Path $versionRoot 'crawl.json'
    $xmlDocPath = Join-Path $versionRoot 'xmldoc.xml'

    $hasOpenCliArtifact = $ArtifactDirectory -and $Result.artifacts.opencliArtifact -and (Test-Path (Join-Path $ArtifactDirectory $Result.artifacts.opencliArtifact))
    $hasCrawlArtifact = $ArtifactDirectory -and $Result.artifacts.crawlArtifact -and (Test-Path (Join-Path $ArtifactDirectory $Result.artifacts.crawlArtifact))
    $hasXmlDocArtifact = $ArtifactDirectory -and $Result.artifacts.xmldocArtifact -and (Test-Path (Join-Path $ArtifactDirectory $Result.artifacts.xmldocArtifact))
    $analysisMode = Get-InferredAnalysisMode -Result $Result -Item $null -HasCrawlArtifact:$hasCrawlArtifact -ArtifactDirectory $ArtifactDirectory
    $openCliSource = $null
    $openCliDocument = $null
    $xmlDocContent = $null

    if ($hasOpenCliArtifact) {
        $openCliDocument = Get-Content (Join-Path $ArtifactDirectory $Result.artifacts.opencliArtifact) -Raw | ConvertFrom-Json
        $openCliSource = Get-FirstNonEmptyText -Values @(
            if ($openCliDocument.PSObject.Properties.Name -contains 'x-inspectra' -and $openCliDocument.'x-inspectra') { $openCliDocument.'x-inspectra'.artifactSource } else { $null },
            $Result.artifacts.opencliSource,
            if ($Result.steps) { $Result.steps.opencli.artifactSource } else { $null },
            if ($Result.introspection) { $Result.introspection.opencli.artifactSource } else { $null },
            Get-InferredOpenCliArtifactSource -AnalysisMode $analysisMode,
            'tool-output'
        )
    }

    if ($hasXmlDocArtifact) {
        $xmlDocContent = Get-Content (Join-Path $ArtifactDirectory $Result.artifacts.xmldocArtifact) -Raw
    }

    if ($null -eq $openCliDocument -and $hasXmlDocArtifact) {
        [xml]$xmlDocument = $xmlDocContent
        $openCliTitle = if ($Result.command) { [string]$Result.command } else { [string]$Result.packageId }
        $openCliDocument = Convert-XmldocToOpenCliDocument `
            -XmlDocument $xmlDocument `
            -Title $openCliTitle `
            -Version ([string]$Result.version)
        $openCliSource = 'synthesized-from-xmldoc'
    }

    $hasOpenCliOutput = $null -ne $openCliDocument
    $openCliClassification = Get-InferredOpenCliClassification -ArtifactSource $openCliSource -ExistingClassifications @(
        if ($Result.steps) { $Result.steps.opencli.classification } else { $null },
        if ($Result.introspection) { $Result.introspection.opencli.classification } else { $null }
    )

    if ($hasOpenCliOutput) {
        if (-not ($openCliDocument.PSObject.Properties.Name -contains 'x-inspectra') -or -not $openCliDocument.'x-inspectra') {
            $openCliDocument | Add-Member -NotePropertyName 'x-inspectra' -NotePropertyValue ([ordered]@{}) -Force
        }
        $openCliDocument.'x-inspectra'.artifactSource = $openCliSource
    }

    if ($hasOpenCliOutput) {
        Write-JsonFile -Path $openCliPath -InputObject $openCliDocument
    }
    else {
        Remove-Item -Path $openCliPath -Force -ErrorAction SilentlyContinue
    }

    if ($hasCrawlArtifact) {
        Copy-Item -Path (Join-Path $ArtifactDirectory $Result.artifacts.crawlArtifact) -Destination $crawlPath -Force
    }
    else {
        Remove-Item -Path $crawlPath -Force -ErrorAction SilentlyContinue
    }

    if ($hasXmlDocArtifact) {
        Write-TextFile -Path $xmlDocPath -Content $xmlDocContent
    }
    else {
        Remove-Item -Path $xmlDocPath -Force -ErrorAction SilentlyContinue
    }

    $steps = [ordered]@{
        install = if ($Result.steps) { $Result.steps.install } else { $null }
        opencli = $null
        xmldoc = $null
    }

    $openCliStep = if ($Result.steps -and $Result.steps.opencli) {
        Copy-ObjectProperties -InputObject $Result.steps.opencli
    }
    elseif ($hasOpenCliOutput) {
        [ordered]@{ status = 'ok' }
    }
    else { $null }
    if ($openCliStep) {
        if ($hasOpenCliOutput) {
            $openCliStep.status = 'ok'
            $openCliStep.path = Get-RelativeRepositoryPath -Path $openCliPath
            $openCliStep.Remove('message')
            if ($openCliSource) { $openCliStep.artifactSource = $openCliSource }
            if ($openCliClassification) { $openCliStep.classification = $openCliClassification }
        }
        else {
            $openCliStep.Remove('path')
        }
    }
    $steps.opencli = $openCliStep

    $xmlDocStep = if ($Result.steps -and $Result.steps.xmldoc) {
        Copy-ObjectProperties -InputObject $Result.steps.xmldoc
    }
    else { $null }
    if ($xmlDocStep) {
        if ($hasXmlDocArtifact) {
            $xmlDocStep.path = Get-RelativeRepositoryPath -Path $xmlDocPath
        }
        else {
            $xmlDocStep.Remove('path')
        }
    }
    $steps.xmldoc = $xmlDocStep

    $introspection = if ($Result.PSObject.Properties.Name -contains 'introspection' -and $Result.introspection) {
        Copy-ObjectProperties -InputObject $Result.introspection
    } else { [ordered]@{} }

    if ($hasOpenCliOutput) {
        if (-not ($introspection.Contains('opencli')) -or $null -eq $introspection.opencli) {
            $introspection.opencli = [ordered]@{}
        }

        $introspection.opencli.status = 'ok'
        $introspection.opencli.artifactSource = $openCliSource
        if ($openCliClassification) {
            $introspection.opencli.classification = $openCliClassification
        }
        $introspection.opencli.Remove('message')
        if ($openCliSource -eq 'synthesized-from-xmldoc') {
            $introspection.opencli.synthesizedArtifact = $true
        }
        else {
            $introspection.opencli.Remove('synthesizedArtifact')
        }
    }

    $analysisSelection = if ($Result.PSObject.Properties.Name -contains 'analysisSelection' -and $Result.analysisSelection) {
        Copy-ObjectProperties -InputObject $Result.analysisSelection
    }
    elseif ($analysisMode) {
        [ordered]@{}
    }
    else { $null }
    if ($analysisSelection) {
        if (-not ($analysisSelection.Contains('selectedMode')) -or $null -eq $analysisSelection.selectedMode) {
            $analysisSelection.selectedMode = $analysisMode
        }
        if (-not ($analysisSelection.Contains('preferredMode')) -or $null -eq $analysisSelection.preferredMode) {
            $analysisSelection.preferredMode = $analysisMode
        }
    }

    $metadata = [ordered]@{
        schemaVersion = 1
        packageId = $Result.packageId
        version = $Result.version
        trusted = $false
        analysisMode = $analysisMode
        analysisSelection = $analysisSelection
        fallback = if ($Result.PSObject.Properties.Name -contains 'fallback' -and $Result.fallback) { Copy-ObjectProperties -InputObject $Result.fallback } else { $null }
        cliFramework = $Result.cliFramework
        source = $Result.source
        batchId = $Result.batchId
        attempt = $Result.attempt
        status = if ($hasOpenCliOutput) { 'ok' } else { 'partial' }
        evaluatedAt = $Result.analyzedAt
        publishedAt = Convert-ToIsoTimestamp $Result.publishedAt
        packageUrl = $Result.packageUrl
        projectUrl = $Result.projectUrl
        sourceRepositoryUrl = $Result.sourceRepositoryUrl
        packageContentUrl = $Result.packageContentUrl
        registrationLeafUrl = $Result.registrationLeafUrl
        catalogEntryUrl = $Result.catalogEntryUrl
        command = $Result.command
        entryPoint = $Result.entryPoint
        runner = $Result.runner
        toolSettingsPath = $Result.toolSettingsPath
        detection = $Result.detection
        introspection = $introspection
        coverage = if ($Result.PSObject.Properties.Name -contains 'coverage' -and $Result.coverage) { Copy-ObjectProperties -InputObject $Result.coverage } else { $null }
        timings = $Result.timings
        steps = $steps
        artifacts = [ordered]@{
            metadataPath = Get-RelativeRepositoryPath -Path $metadataPath
            opencliPath = if ($hasOpenCliOutput) { Get-RelativeRepositoryPath -Path $openCliPath } else { $null }
            opencliSource = if ($hasOpenCliOutput) { $openCliSource } else { $null }
            crawlPath = if ($hasCrawlArtifact) { Get-RelativeRepositoryPath -Path $crawlPath } else { $null }
            xmldocPath = if ($hasXmlDocArtifact) { Get-RelativeRepositoryPath -Path $xmlDocPath } else { $null }
        }
    }

    Write-JsonFile -Path $metadataPath -InputObject $metadata
    return $metadata.artifacts
}

function Rebuild-Indexes {
    $versionRecords = @(
        Get-ChildItem -Path $PackagesRoot -Filter 'metadata.json' -Recurse -ErrorAction SilentlyContinue |
            Where-Object { (Split-Path -Leaf (Split-Path -Parent $_.FullName)) -ne 'latest' } |
            ForEach-Object {
                $data = Get-Content $_.FullName -Raw | ConvertFrom-Json
                $data | Add-Member -NotePropertyName metadataFileFullPath -NotePropertyValue $_.FullName -Force
                $data | Add-Member -NotePropertyName versionDirectoryFullPath -NotePropertyValue (Split-Path -Parent $_.FullName) -Force
                $data
            }
    )

    $unsortedPackageSummaries = @(
        $versionRecords |
            Group-Object packageId |
            ForEach-Object {
                $summary = Get-PackageSummary -Records $_.Group
                $summaryPath = Join-Path $PackagesRoot ("{0}/index.json" -f $_.Name.ToLowerInvariant())
                $latestRecord = ($_.Group | Sort-Object @{ Expression = { if ($_.publishedAt) { [DateTimeOffset]$_.publishedAt } else { [DateTimeOffset]::MinValue } }; Descending = $true }, @{ Expression = { [DateTimeOffset]$_.evaluatedAt }; Descending = $true })[0]
                Sync-LatestDirectory -VersionDirectory $latestRecord.versionDirectoryFullPath -LatestDirectory (Join-Path $PackagesRoot ("{0}/latest" -f $_.Name.ToLowerInvariant()))
                Write-JsonFile -Path $summaryPath -InputObject $summary
                $summary
            }
    )
    $packageSummaries = Sort-PackageSummariesForAllIndex -PackageSummaries $unsortedPackageSummaries -RepositoryRoot $RepositoryRoot

    Write-JsonFile -Path (Join-Path $IndexRoot 'all.json') -InputObject ([ordered]@{
        schemaVersion = 1
        generatedAt = [DateTimeOffset]::UtcNow.ToString('o')
        packageCount = $packageSummaries.Count
        packages = $packageSummaries
    })

    & (Join-Path $PSScriptRoot 'New-BrowserIndex.ps1') `
        -AllIndexPath (Join-Path $IndexRoot 'all.json') `
        -OutputPath (Join-Path $IndexRoot 'index.json')
}

$DownloadDirectory = Resolve-Path $DownloadRoot
$expectedFile = Get-ChildItem -Path $DownloadDirectory -Filter 'expected.json' -Recurse | Select-Object -First 1 -ExpandProperty FullName
if (-not $expectedFile) { throw "expected.json was not found under '$DownloadRoot'." }

$Plan = Get-Content $expectedFile -Raw | ConvertFrom-Json
$resultLookup = @{}
$legacyResultLookup = @{}
Get-ChildItem -Path $DownloadDirectory -Filter 'result.json' -Recurse | ForEach-Object {
    $record = Get-Content $_.FullName -Raw | ConvertFrom-Json
    $record | Add-Member -NotePropertyName artifactDirectoryFullPath -NotePropertyValue (Split-Path -Parent $_.FullName) -Force
    foreach ($resultKey in @(Get-ResultKeys -Result $record)) {
        if (-not $resultLookup.ContainsKey($resultKey) -or (Get-ResultAttempt -Result $record) -ge (Get-ResultAttempt -Result $resultLookup[$resultKey])) {
            $resultLookup[$resultKey] = $record
        }
    }

    $legacyKey = Get-PackageVersionKey -PackageId ([string]$record.packageId) -Version ([string]$record.version)
    if (-not $legacyResultLookup.ContainsKey($legacyKey) -or (Get-ResultAttempt -Result $record) -ge (Get-ResultAttempt -Result $legacyResultLookup[$legacyKey])) {
        $legacyResultLookup[$legacyKey] = $record
    }
}

$summary = [ordered]@{
    schemaVersion = 1
    batchId = $Plan.batchId
    targetBranch = if ($Plan.PSObject.Properties.Name -contains 'targetBranch' -and $Plan.targetBranch) { [string]$Plan.targetBranch } else { 'main' }
    promotedAt = $Now.ToString('o')
    expectedCount = $Plan.items.Count
    successCount = 0
    terminalNegativeCount = 0
    retryableFailureCount = 0
    terminalFailureCount = 0
    missingCount = 0
    createdPackages = @()
    updatedPackages = @()
    nonSuccessItems = @()
}

foreach ($item in $Plan.items) {
    $key = Get-PlanItemKey -Item $item
    $hasExplicitLookup = $resultLookup.ContainsKey($key)
    $result = if ($hasExplicitLookup) {
        $resultLookup[$key]
    }
    elseif ((-not ($item.PSObject.Properties.Name -contains 'artifactName') -or [string]::IsNullOrWhiteSpace([string]$item.artifactName))
        -and (-not ($item.PSObject.Properties.Name -contains 'command') -or [string]::IsNullOrWhiteSpace([string]$item.command))
        -and $legacyResultLookup.ContainsKey((Get-PackageVersionKey -PackageId ([string]$item.packageId) -Version ([string]$item.version)))) {
        $legacyResultLookup[(Get-PackageVersionKey -PackageId ([string]$item.packageId) -Version ([string]$item.version))]
    }
    else {
        $summary.missingCount++
        New-SyntheticFailureResult -Item $item -Attempt $item.attempt -Classification 'missing-result-artifact' -Message 'No result artifact was uploaded for this matrix item.'
    }
    $artifactDir = if ($result.PSObject.Properties.Name -contains 'artifactDirectoryFullPath') { $result.artifactDirectoryFullPath } else { $null }

    if ($result.disposition -eq 'success') {
        $openCliExists = $artifactDir -and $result.artifacts.opencliArtifact -and (Test-Path (Join-Path $artifactDir $result.artifacts.opencliArtifact))
        $crawlExists = $artifactDir -and $result.artifacts.crawlArtifact -and (Test-Path (Join-Path $artifactDir $result.artifacts.crawlArtifact))
        $xmlDocExists = $artifactDir -and $result.artifacts.xmldocArtifact -and (Test-Path (Join-Path $artifactDir $result.artifacts.xmldocArtifact))
        $analysisMode = Get-InferredAnalysisMode -Result $result -Item $item -HasCrawlArtifact:$crawlExists -ArtifactDirectory $artifactDir
        $requiresCrawlArtifact = $analysisMode -in @('help', 'clifx')
        $declaredMissing = @()
        if ($result.artifacts.opencliArtifact -and -not $openCliExists) { $declaredMissing += $result.artifacts.opencliArtifact }
        if ($result.artifacts.crawlArtifact -and -not $crawlExists) { $declaredMissing += $result.artifacts.crawlArtifact }
        if ($result.artifacts.xmldocArtifact -and -not $xmlDocExists) { $declaredMissing += $result.artifacts.xmldocArtifact }

        if ($declaredMissing.Count -gt 0 -or -not ($openCliExists -or $xmlDocExists) -or ($requiresCrawlArtifact -and -not $crawlExists)) {
            $message = if ($declaredMissing.Count -gt 0) {
                'Success result declared artifact(s) that were not uploaded: ' + ($declaredMissing -join ', ')
            } elseif ($requiresCrawlArtifact -and -not $crawlExists) {
                'Success result did not include crawl.json.'
            } else {
                'Success result did not include either opencli.json or xmldoc.xml.'
            }

            $result = New-SyntheticFailureResult -Item $item -Attempt $result.attempt -Classification 'missing-success-artifact' -Message $message
            $artifactDir = $null
        }
    }

    $existingStatePath = Get-StatePath -LowerId $item.packageId.ToLowerInvariant() -LowerVersion $item.version.ToLowerInvariant()
    $existingState = if (Test-Path $existingStatePath) { Get-Content $existingStatePath -Raw | ConvertFrom-Json } else { $null }
    $existingPackageIndexPath = Get-PackageIndexPath -LowerId $item.packageId.ToLowerInvariant()
    $existingPackageIndex = if (Test-Path $existingPackageIndexPath) { Get-Content $existingPackageIndexPath -Raw | ConvertFrom-Json } else { $null }
    $indexedPaths = if ($result.disposition -eq 'success') { Write-SuccessArtifacts -Result $result -ArtifactDirectory $artifactDir } else { $null }
    $stateRecord = Update-StateRecord -ExistingState $existingState -Result $result -IndexedPaths $indexedPaths
    Write-JsonFile -Path $existingStatePath -InputObject $stateRecord

    switch ($stateRecord.currentStatus) {
        'success' { $summary.successCount++ }
        'terminal-negative' { $summary.terminalNegativeCount++ }
        'retryable-failure' { $summary.retryableFailureCount++ }
        'terminal-failure' { $summary.terminalFailureCount++ }
    }

    if ($stateRecord.currentStatus -eq 'success') {
        if (-not $existingPackageIndex) {
            $summary.createdPackages += [ordered]@{
                packageId = $result.packageId
                version = $result.version
            }
        }
        elseif (-not [string]::Equals([string]$existingPackageIndex.latestVersion, [string]$result.version, [System.StringComparison]::OrdinalIgnoreCase)) {
            $summary.updatedPackages += [ordered]@{
                packageId = $result.packageId
                previousVersion = [string]$existingPackageIndex.latestVersion
                version = $result.version
            }
        }
    }

    if ($stateRecord.currentStatus -ne 'success') {
        $summary.nonSuccessItems += [ordered]@{
            packageId = $result.packageId
            version = $result.version
            status = $stateRecord.currentStatus
            disposition = $result.disposition
            phase = $result.phase
            classification = $result.classification
            reason = Get-NonSuccessReason -Result $result -StateRecord $stateRecord
        }
    }
}

Rebuild-Indexes

if ($SummaryOutputPath) {
    Write-JsonFile -Path $SummaryOutputPath -InputObject $summary
}

Write-Host "Promoted batch $($Plan.batchId)"
Write-Host "Success: $($summary.successCount)"
Write-Host "Terminal negative: $($summary.terminalNegativeCount)"
Write-Host "Retryable failure: $($summary.retryableFailureCount)"
Write-Host "Terminal failure: $($summary.terminalFailureCount)"

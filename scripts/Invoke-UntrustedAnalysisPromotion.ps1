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

function Sync-LatestDirectory {
    param([string]$VersionDirectory, [string]$LatestDirectory)
    New-Item -ItemType Directory -Path $LatestDirectory -Force | Out-Null
    foreach ($artifactName in @('metadata.json', 'opencli.json', 'xmldoc.xml')) {
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

    $status = switch ($Result.disposition) {
        'success' { 'success'; break }
        'terminal-negative' { 'terminal-negative'; break }
        'terminal-failure' { 'terminal-failure'; break }
        default {
            if ($consecutiveFailures -ge 3) { 'terminal-failure' } else { 'retryable-failure' }
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
    $xmlDocPath = Join-Path $versionRoot 'xmldoc.xml'

    $hasOpenCliArtifact = $ArtifactDirectory -and $Result.artifacts.opencliArtifact -and (Test-Path (Join-Path $ArtifactDirectory $Result.artifacts.opencliArtifact))
    $hasXmlDocArtifact = $ArtifactDirectory -and $Result.artifacts.xmldocArtifact -and (Test-Path (Join-Path $ArtifactDirectory $Result.artifacts.xmldocArtifact))
    $openCliSource = $null
    $openCliDocument = $null
    $xmlDocContent = $null

    if ($hasOpenCliArtifact) {
        $openCliDocument = Get-Content (Join-Path $ArtifactDirectory $Result.artifacts.opencliArtifact) -Raw | ConvertFrom-Json
        $openCliSource = 'tool-output'
    }

    if ($hasXmlDocArtifact) {
        $xmlDocContent = Get-Content (Join-Path $ArtifactDirectory $Result.artifacts.xmldocArtifact) -Raw
    }

    if ($null -eq $openCliDocument -and $hasXmlDocArtifact) {
        [xml]$xmlDocument = $xmlDocContent
        $openCliTitle = if ($Result.command) { [string]$Result.command } else { [string]$Result.packageId }
        $openCliDocument = Convert-XmldocToOpenCliDocument `
            -XmlDocument $xmlDocument `
            -Title $openCliTitle
        $openCliSource = 'synthesized-from-xmldoc'
    }

    $hasOpenCliOutput = $null -ne $openCliDocument

    if ($hasOpenCliOutput) {
        Write-JsonFile -Path $openCliPath -InputObject $openCliDocument
    }
    else {
        Remove-Item -Path $openCliPath -Force -ErrorAction SilentlyContinue
    }

    if ($hasXmlDocArtifact) {
        Write-TextFile -Path $xmlDocPath -Content $xmlDocContent
    }
    else {
        Remove-Item -Path $xmlDocPath -Force -ErrorAction SilentlyContinue
    }

    $steps = [ordered]@{
        install = $Result.steps.install
        opencli = if ($Result.steps.opencli) {
            $clone = [ordered]@{}
            foreach ($property in $Result.steps.opencli.PSObject.Properties) {
                $clone[$property.Name] = $property.Value
            }
            if ($hasOpenCliOutput) {
                $clone.path = Get-RelativeRepositoryPath -Path $openCliPath
            }
            else {
                $clone.Remove('path')
            }
            if ($openCliSource) {
                $clone.artifactSource = $openCliSource
            }
            $clone
        } else { $null }
        xmldoc = if ($Result.steps.xmldoc) {
            $clone = [ordered]@{}
            foreach ($property in $Result.steps.xmldoc.PSObject.Properties) {
                $clone[$property.Name] = $property.Value
            }
            if ($hasXmlDocArtifact) {
                $clone.path = Get-RelativeRepositoryPath -Path $xmlDocPath
            }
            else {
                $clone.Remove('path')
            }
            $clone
        } else { $null }
    }

    $metadata = [ordered]@{
        schemaVersion = 1
        packageId = $Result.packageId
        version = $Result.version
        trusted = $false
        source = $Result.source
        batchId = $Result.batchId
        attempt = $Result.attempt
        status = if ($hasOpenCliArtifact -and $hasXmlDocArtifact) { 'ok' } else { 'partial' }
        evaluatedAt = $Result.analyzedAt
        publishedAt = Convert-ToIsoTimestamp $Result.publishedAt
        packageUrl = $Result.packageUrl
        packageContentUrl = $Result.packageContentUrl
        registrationLeafUrl = $Result.registrationLeafUrl
        catalogEntryUrl = $Result.catalogEntryUrl
        command = $Result.command
        entryPoint = $Result.entryPoint
        runner = $Result.runner
        toolSettingsPath = $Result.toolSettingsPath
        detection = $Result.detection
        introspection = if ($Result.PSObject.Properties.Name -contains 'introspection') { $Result.introspection } else { $null }
        timings = $Result.timings
        steps = $steps
        artifacts = [ordered]@{
            metadataPath = Get-RelativeRepositoryPath -Path $metadataPath
            opencliPath = if ($hasOpenCliOutput) { Get-RelativeRepositoryPath -Path $openCliPath } else { $null }
            opencliSource = if ($hasOpenCliOutput) { $openCliSource } else { $null }
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

    $packageSummaries = @(
        $versionRecords |
            Group-Object packageId |
            ForEach-Object {
                $summary = Get-PackageSummary -Records $_.Group
                $summaryPath = Join-Path $PackagesRoot ("{0}/index.json" -f $_.Name.ToLowerInvariant())
                $latestRecord = ($_.Group | Sort-Object @{ Expression = { if ($_.publishedAt) { [DateTimeOffset]$_.publishedAt } else { [DateTimeOffset]::MinValue } }; Descending = $true }, @{ Expression = { [DateTimeOffset]$_.evaluatedAt }; Descending = $true })[0]
                Sync-LatestDirectory -VersionDirectory $latestRecord.versionDirectoryFullPath -LatestDirectory (Join-Path $PackagesRoot ("{0}/latest" -f $_.Name.ToLowerInvariant()))
                Write-JsonFile -Path $summaryPath -InputObject $summary
                $summary
            } |
            Sort-Object packageId
    )

    Write-JsonFile -Path (Join-Path $IndexRoot 'all.json') -InputObject ([ordered]@{
        schemaVersion = 1
        generatedAt = [DateTimeOffset]::UtcNow.ToString('o')
        packageCount = $packageSummaries.Count
        packages = $packageSummaries
    })
}

$DownloadDirectory = Resolve-Path $DownloadRoot
$expectedFile = Get-ChildItem -Path $DownloadDirectory -Filter 'expected.json' -Recurse | Select-Object -First 1 -ExpandProperty FullName
if (-not $expectedFile) { throw "expected.json was not found under '$DownloadRoot'." }

$Plan = Get-Content $expectedFile -Raw | ConvertFrom-Json
$resultLookup = @{}
Get-ChildItem -Path $DownloadDirectory -Filter 'result.json' -Recurse | ForEach-Object {
    $record = Get-Content $_.FullName -Raw | ConvertFrom-Json
    $record | Add-Member -NotePropertyName artifactDirectoryFullPath -NotePropertyValue (Split-Path -Parent $_.FullName) -Force
    $resultLookup["$($record.packageId.ToLowerInvariant())|$($record.version.ToLowerInvariant())"] = $record
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
    nonSuccessItems = @()
}

foreach ($item in $Plan.items) {
    $key = "$($item.packageId.ToLowerInvariant())|$($item.version.ToLowerInvariant())"
    $result = if ($resultLookup.ContainsKey($key)) { $resultLookup[$key] } else { $summary.missingCount++; New-SyntheticFailureResult -Item $item -Attempt $item.attempt -Classification 'missing-result-artifact' -Message 'No result artifact was uploaded for this matrix item.' }
    $artifactDir = if ($result.PSObject.Properties.Name -contains 'artifactDirectoryFullPath') { $result.artifactDirectoryFullPath } else { $null }

    if ($result.disposition -eq 'success') {
        $openCliExists = $artifactDir -and $result.artifacts.opencliArtifact -and (Test-Path (Join-Path $artifactDir $result.artifacts.opencliArtifact))
        $xmlDocExists = $artifactDir -and $result.artifacts.xmldocArtifact -and (Test-Path (Join-Path $artifactDir $result.artifacts.xmldocArtifact))
        $declaredMissing = @()
        if ($result.artifacts.opencliArtifact -and -not $openCliExists) { $declaredMissing += $result.artifacts.opencliArtifact }
        if ($result.artifacts.xmldocArtifact -and -not $xmlDocExists) { $declaredMissing += $result.artifacts.xmldocArtifact }

        if ($declaredMissing.Count -gt 0 -or -not ($openCliExists -or $xmlDocExists)) {
            $message = if ($declaredMissing.Count -gt 0) {
                'Success result declared artifact(s) that were not uploaded: ' + ($declaredMissing -join ', ')
            } else {
                'Success result did not include either opencli.json or xmldoc.xml.'
            }

            $result = New-SyntheticFailureResult -Item $item -Attempt $result.attempt -Classification 'missing-success-artifact' -Message $message
            $artifactDir = $null
        }
    }

    $existingStatePath = Get-StatePath -LowerId $item.packageId.ToLowerInvariant() -LowerVersion $item.version.ToLowerInvariant()
    $existingState = if (Test-Path $existingStatePath) { Get-Content $existingStatePath -Raw | ConvertFrom-Json } else { $null }
    $indexedPaths = if ($result.disposition -eq 'success') { Write-SuccessArtifacts -Result $result -ArtifactDirectory $artifactDir } else { $null }
    $stateRecord = Update-StateRecord -ExistingState $existingState -Result $result -IndexedPaths $indexedPaths
    Write-JsonFile -Path $existingStatePath -InputObject $stateRecord

    switch ($stateRecord.currentStatus) {
        'success' { $summary.successCount++ }
        'terminal-negative' { $summary.terminalNegativeCount++ }
        'retryable-failure' { $summary.retryableFailureCount++ }
        'terminal-failure' { $summary.terminalFailureCount++ }
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

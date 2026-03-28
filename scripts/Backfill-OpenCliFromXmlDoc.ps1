[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$IndexRoot = Join-Path $RepositoryRoot 'index'
$PackagesRoot = Join-Path $IndexRoot 'packages'

. (Join-Path $PSScriptRoot 'OpenCliSynthesis.ps1')
. (Join-Path $PSScriptRoot 'OpenCliMetrics.ps1')

function Write-JsonFile {
    param([string]$Path, [object]$InputObject)
    $directory = Split-Path -Parent $Path
    if ($directory) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $json = $InputObject | ConvertTo-Json -Depth 100
    [System.IO.File]::WriteAllText($Path, $json + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
}

function Get-RelativeRepositoryPath {
    param([string]$Path)
    return ([System.IO.Path]::GetRelativePath($RepositoryRoot, $Path)) -replace '\\', '/'
}

function Convert-ToIsoTimestamp {
    param([AllowNull()][object]$Value)
    if ($null -eq $Value -or [string]::IsNullOrWhiteSpace([string]$Value)) {
        return $null
    }

    return ([DateTimeOffset]$Value).ToUniversalTime().ToString('o')
}

function Sync-LatestDirectory {
    param([string]$VersionDirectory, [string]$LatestDirectory)
    New-Item -ItemType Directory -Path $LatestDirectory -Force | Out-Null
    foreach ($artifactName in @('metadata.json', 'opencli.json', 'xmldoc.xml', 'crawl.json')) {
        $sourcePath = Join-Path $VersionDirectory $artifactName
        $targetPath = Join-Path $LatestDirectory $artifactName
        if (Test-Path $sourcePath) {
            Copy-Item -Path $sourcePath -Destination $targetPath -Force
        }
        else {
            Remove-Item -Path $targetPath -Force -ErrorAction SilentlyContinue
        }
    }
}

function Get-PackageSummary {
    param([array]$Records)

    $ordered = $Records | Sort-Object `
        @{ Expression = { if ($_.publishedAt) { [DateTimeOffset]$_.publishedAt } else { [DateTimeOffset]::MinValue } }; Descending = $true }, `
        @{ Expression = { [DateTimeOffset]$_.evaluatedAt }; Descending = $true }
    $latest = $ordered[0]
    $lowerId = $latest.packageId.ToLowerInvariant()

    return [ordered]@{
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
                $latestRecord = ($_.Group | Sort-Object `
                    @{ Expression = { if ($_.publishedAt) { [DateTimeOffset]$_.publishedAt } else { [DateTimeOffset]::MinValue } }; Descending = $true }, `
                    @{ Expression = { [DateTimeOffset]$_.evaluatedAt }; Descending = $true })[0]
                $latestDirectory = Join-Path $PackagesRoot ("{0}/latest" -f $_.Name.ToLowerInvariant())

                Sync-LatestDirectory -VersionDirectory $latestRecord.versionDirectoryFullPath -LatestDirectory $latestDirectory
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

$updated = [System.Collections.Generic.List[object]]::new()
$metadataFiles = @(
    Get-ChildItem -Path $PackagesRoot -Filter 'metadata.json' -Recurse -ErrorAction SilentlyContinue |
        Where-Object { (Split-Path -Leaf (Split-Path -Parent $_.FullName)) -ne 'latest' }
)

foreach ($metadataFile in $metadataFiles) {
    $metadata = Get-Content $metadataFile.FullName -Raw | ConvertFrom-Json
    $openCliSource = if ($metadata.artifacts.PSObject.Properties.Name -contains 'opencliSource') {
        [string]$metadata.artifacts.opencliSource
    }
    else {
        $null
    }
    $shouldBackfillMissingOpenCli = $metadata.artifacts.xmldocPath -and -not $metadata.artifacts.opencliPath
    $shouldRefreshSynthesizedOpenCli = `
        $metadata.artifacts.xmldocPath -and `
        $metadata.artifacts.opencliPath -and `
        $openCliSource -eq 'synthesized-from-xmldoc'

    if (-not $shouldBackfillMissingOpenCli -and -not $shouldRefreshSynthesizedOpenCli) {
        continue
    }

    $xmlDocPath = Join-Path $RepositoryRoot $metadata.artifacts.xmldocPath
    if (-not (Test-Path $xmlDocPath)) {
        continue
    }

    [xml]$xmlDocument = Get-Content $xmlDocPath -Raw
    $openCliTitle = if ($metadata.command) { [string]$metadata.command } else { [string]$metadata.packageId }
    $openCliDocument = Convert-XmldocToOpenCliDocument `
        -XmlDocument $xmlDocument `
        -Title $openCliTitle `
        -Version ([string]$metadata.version)

    $versionDirectory = Split-Path -Parent $metadataFile.FullName
    $openCliPath = Join-Path $versionDirectory 'opencli.json'
    Write-JsonFile -Path $openCliPath -InputObject $openCliDocument

    $relativeOpenCliPath = Get-RelativeRepositoryPath -Path $openCliPath
    $metadata.artifacts.opencliPath = $relativeOpenCliPath
    if ($metadata.artifacts.PSObject.Properties.Name -contains 'opencliSource') {
        $metadata.artifacts.opencliSource = 'synthesized-from-xmldoc'
    }
    else {
        $metadata.artifacts | Add-Member -NotePropertyName opencliSource -NotePropertyValue 'synthesized-from-xmldoc'
    }

    if (-not ($metadata.PSObject.Properties.Name -contains 'steps') -or -not $metadata.steps) {
        $metadata | Add-Member -NotePropertyName steps -NotePropertyValue ([ordered]@{}) -Force
    }

    if (-not ($metadata.steps.PSObject.Properties.Name -contains 'opencli') -or -not $metadata.steps.opencli) {
        $metadata.steps | Add-Member -NotePropertyName opencli -NotePropertyValue ([ordered]@{}) -Force
    }

    $metadata.steps.opencli | Add-Member -NotePropertyName status -NotePropertyValue 'ok' -Force
    $metadata.steps.opencli | Add-Member -NotePropertyName path -NotePropertyValue $relativeOpenCliPath -Force
    $metadata.steps.opencli | Add-Member -NotePropertyName artifactSource -NotePropertyValue 'synthesized-from-xmldoc' -Force
    $metadata.steps.opencli | Add-Member -NotePropertyName classification -NotePropertyValue 'xmldoc-synthesized' -Force

    if (-not ($metadata.PSObject.Properties.Name -contains 'introspection') -or -not $metadata.introspection) {
        $metadata | Add-Member -NotePropertyName introspection -NotePropertyValue ([ordered]@{}) -Force
    }

    if (-not ($metadata.introspection.PSObject.Properties.Name -contains 'opencli') -or -not $metadata.introspection.opencli) {
        $metadata.introspection | Add-Member -NotePropertyName opencli -NotePropertyValue ([ordered]@{}) -Force
    }

    $metadata.introspection.opencli | Add-Member -NotePropertyName status -NotePropertyValue 'ok' -Force
    $metadata.introspection.opencli | Add-Member -NotePropertyName synthesizedArtifact -NotePropertyValue $true -Force
    $metadata.introspection.opencli | Add-Member -NotePropertyName artifactSource -NotePropertyValue 'synthesized-from-xmldoc' -Force
    $metadata.introspection.opencli | Add-Member -NotePropertyName classification -NotePropertyValue 'xmldoc-synthesized' -Force

    $metadata.status = 'ok'

    Write-JsonFile -Path $metadataFile.FullName -InputObject $metadata
    $updated.Add([ordered]@{
        packageId = $metadata.packageId
        version = $metadata.version
        opencliPath = $relativeOpenCliPath
        artifactSource = 'synthesized-from-xmldoc'
    })
}

Rebuild-Indexes

[pscustomobject]@{
    updatedCount = $updated.Count
    updated = @($updated)
} | ConvertTo-Json -Depth 10

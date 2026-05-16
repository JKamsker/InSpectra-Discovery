[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$DownloadRoot,

    [Parameter(Mandatory = $true)]
    [string]$IncludeListPath,

    [Parameter(Mandatory = $true)]
    [string]$BatchId,

    [string]$TargetBranch = 'main',

    [string]$PlanOutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-JsonFile {
    param([string]$Path, [object]$InputObject)

    $directory = Split-Path -Parent $Path
    if ($directory) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $json = $InputObject | ConvertTo-Json -Depth 100
    [System.IO.File]::WriteAllText($Path, $json + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
}

function Write-TextFile {
    param([string]$Path, [string]$Content)

    $directory = Split-Path -Parent $Path
    if ($directory) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    [System.IO.File]::WriteAllText($Path, $Content, [System.Text.UTF8Encoding]::new($false))
}

function Set-ObjectPropertyValue {
    param(
        [Parameter(Mandatory = $true)]
        [object]$InputObject,

        [Parameter(Mandatory = $true)]
        [string]$PropertyName,

        [AllowNull()]
        [object]$Value
    )

    if ($InputObject.PSObject.Properties.Name -contains $PropertyName) {
        $InputObject.$PropertyName = $Value
    }
    else {
        $InputObject | Add-Member -NotePropertyName $PropertyName -NotePropertyValue $Value
    }
}

function Get-ItemKey {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PackageId,

        [Parameter(Mandatory = $true)]
        [string]$Version
    )

    return ('{0}|{1}' -f $PackageId.ToLowerInvariant(), $Version.ToLowerInvariant())
}

function Get-RequiredPropertyString {
    param(
        [Parameter(Mandatory = $true)]
        [object]$InputObject,

        [Parameter(Mandatory = $true)]
        [string]$PropertyName
    )

    if (-not ($InputObject.PSObject.Properties.Name -contains $PropertyName)) {
        throw "Include list entry is missing required property '$PropertyName'."
    }

    $value = [string]$InputObject.$PropertyName
    if ([string]::IsNullOrWhiteSpace($value)) {
        throw "Include list entry property '$PropertyName' cannot be empty."
    }

    return $value
}

function Get-XmldocTextFromResult {
    param([Parameter(Mandatory = $true)][object]$Result)

    $candidates = [System.Collections.Generic.List[string]]::new()

    if (
        $Result.PSObject.Properties.Name -contains 'steps' -and
        $Result.steps -and
        $Result.steps.PSObject.Properties.Name -contains 'xmldoc' -and
        $Result.steps.xmldoc -and
        $Result.steps.xmldoc.PSObject.Properties.Name -contains 'stdout' -and
        -not [string]::IsNullOrWhiteSpace([string]$Result.steps.xmldoc.stdout)
    ) {
        $candidates.Add([string]$Result.steps.xmldoc.stdout)
    }

    if ($Result.PSObject.Properties.Name -contains 'failureMessage' -and -not [string]::IsNullOrWhiteSpace([string]$Result.failureMessage)) {
        $candidates.Add([string]$Result.failureMessage)
    }

    if (
        $Result.PSObject.Properties.Name -contains 'introspection' -and
        $Result.introspection -and
        $Result.introspection.PSObject.Properties.Name -contains 'xmldoc' -and
        $Result.introspection.xmldoc -and
        $Result.introspection.xmldoc.PSObject.Properties.Name -contains 'message' -and
        -not [string]::IsNullOrWhiteSpace([string]$Result.introspection.xmldoc.message)
    ) {
        $candidates.Add([string]$Result.introspection.xmldoc.message)
    }

    foreach ($candidate in $candidates) {
        $xmlStart = $candidate.IndexOf('<?xml', [System.StringComparison]::Ordinal)
        if ($xmlStart -lt 0) {
            continue
        }

        $xmlEndStart = $candidate.LastIndexOf('</Model>', [System.StringComparison]::Ordinal)
        if ($xmlEndStart -lt $xmlStart) {
            continue
        }

        $xmlEnd = $xmlEndStart + '</Model>'.Length
        $xmlText = $candidate.Substring($xmlStart, $xmlEnd - $xmlStart).Trim()

        try {
            [xml]$null = $xmlText
            return $xmlText
        }
        catch {
            continue
        }
    }

    return $null
}

function Convert-ToSalvagedSuccessResult {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Result,

        [Parameter(Mandatory = $true)]
        [string]$XmlDocFileName
    )

    $xmlText = Get-XmldocTextFromResult -Result $Result
    if (-not $xmlText) {
        throw "No salvageable XML document was found in recorded stdout for $($Result.packageId) $($Result.version)."
    }

    $resultJson = $Result | ConvertTo-Json -Depth 100
    $clone = $resultJson | ConvertFrom-Json

    $clone.batchId = $BatchId
    $clone.disposition = 'success'
    $clone.retryEligible = $false

    if ($clone.PSObject.Properties.Name -contains 'phase') {
        $clone.phase = 'xmldoc'
    }

    if ($clone.PSObject.Properties.Name -contains 'classification') {
        $clone.classification = 'salvaged-from-stdout'
    }

    if ($clone.PSObject.Properties.Name -contains 'failureMessage') {
        $clone.failureMessage = $null
    }

    if ($clone.PSObject.Properties.Name -contains 'failureSignature') {
        $clone.failureSignature = $null
    }

    if (
        $clone.PSObject.Properties.Name -contains 'steps' -and
        $clone.steps -and
        $clone.steps.PSObject.Properties.Name -contains 'xmldoc' -and
        $clone.steps.xmldoc
    ) {
        Set-ObjectPropertyValue -InputObject $clone.steps.xmldoc -PropertyName 'status' -Value 'ok'
        Set-ObjectPropertyValue -InputObject $clone.steps.xmldoc -PropertyName 'timedOut' -Value $false
        Set-ObjectPropertyValue -InputObject $clone.steps.xmldoc -PropertyName 'exitCode' -Value 0
        Set-ObjectPropertyValue -InputObject $clone.steps.xmldoc -PropertyName 'stdout' -Value $xmlText
        Set-ObjectPropertyValue -InputObject $clone.steps.xmldoc -PropertyName 'stdoutLength' -Value $xmlText.Length
        Set-ObjectPropertyValue -InputObject $clone.steps.xmldoc -PropertyName 'stderr' -Value $null
        Set-ObjectPropertyValue -InputObject $clone.steps.xmldoc -PropertyName 'stderrLength' -Value 0
        Set-ObjectPropertyValue -InputObject $clone.steps.xmldoc -PropertyName 'outcomeStatus' -Value 'ok'
        Set-ObjectPropertyValue -InputObject $clone.steps.xmldoc -PropertyName 'classification' -Value 'salvaged-from-stdout'
        Set-ObjectPropertyValue -InputObject $clone.steps.xmldoc -PropertyName 'message' -Value 'Recovered XML document from recorded stdout despite the original nonzero exit.'
    }

    if (
        $clone.PSObject.Properties.Name -contains 'introspection' -and
        $clone.introspection -and
        $clone.introspection.PSObject.Properties.Name -contains 'xmldoc' -and
        $clone.introspection.xmldoc
    ) {
        Set-ObjectPropertyValue -InputObject $clone.introspection.xmldoc -PropertyName 'status' -Value 'ok'
        Set-ObjectPropertyValue -InputObject $clone.introspection.xmldoc -PropertyName 'classification' -Value 'salvaged-from-stdout'
        Set-ObjectPropertyValue -InputObject $clone.introspection.xmldoc -PropertyName 'message' -Value 'Recovered XML document from recorded stdout despite the original nonzero exit.'
    }

    if (-not ($clone.PSObject.Properties.Name -contains 'artifacts') -or -not $clone.artifacts) {
        $clone | Add-Member -NotePropertyName artifacts -NotePropertyValue ([ordered]@{
            opencliArtifact = $null
            xmldocArtifact = $XmlDocFileName
        }) -Force
    }
    else {
        if (-not ($clone.artifacts.PSObject.Properties.Name -contains 'opencliArtifact')) {
            $clone.artifacts | Add-Member -NotePropertyName opencliArtifact -NotePropertyValue $null
        }

        if ($clone.artifacts.PSObject.Properties.Name -contains 'xmldocArtifact') {
            $clone.artifacts.xmldocArtifact = $XmlDocFileName
        }
        else {
            $clone.artifacts | Add-Member -NotePropertyName xmldocArtifact -NotePropertyValue $XmlDocFileName
        }
    }

    return [pscustomobject]@{
        Result = $clone
        XmlText = $xmlText
    }
}

$downloadDirectory = (Resolve-Path $DownloadRoot).Path
$includeEntries = Get-Content $IncludeListPath -Raw | ConvertFrom-Json
$includeLookup = @{}

foreach ($entry in @($includeEntries)) {
    $packageId = Get-RequiredPropertyString -InputObject $entry -PropertyName 'packageId'
    $version = Get-RequiredPropertyString -InputObject $entry -PropertyName 'version'
    $includeLookup[(Get-ItemKey -PackageId $packageId -Version $version)] = $true
}

$selected = [System.Collections.Generic.List[object]]::new()
$resultFiles = Get-ChildItem -Path $downloadDirectory -Filter 'result.json' -Recurse

foreach ($resultFile in $resultFiles) {
    $result = Get-Content $resultFile.FullName -Raw | ConvertFrom-Json
    $key = Get-ItemKey -PackageId ([string]$result.packageId) -Version ([string]$result.version)
    if (-not $includeLookup.ContainsKey($key)) {
        continue
    }

    $artifactDirectory = Split-Path -Parent $resultFile.FullName
    $xmlDocFileName = 'xmldoc.xml'
    $salvaged = Convert-ToSalvagedSuccessResult -Result $result -XmlDocFileName $xmlDocFileName

    Write-TextFile -Path (Join-Path $artifactDirectory $xmlDocFileName) -Content $salvaged.XmlText
    Write-JsonFile -Path $resultFile.FullName -InputObject $salvaged.Result

    $selected.Add([ordered]@{
        packageId = [string]$salvaged.Result.packageId
        version = [string]$salvaged.Result.version
        attempt = [int]$salvaged.Result.attempt
        packageUrl = [string]$salvaged.Result.packageUrl
        packageContentUrl = [string]$salvaged.Result.packageContentUrl
        catalogEntryUrl = [string]$salvaged.Result.catalogEntryUrl
    })
}

if ($selected.Count -ne $includeLookup.Count) {
    $selectedLookup = @{}
    foreach ($item in $selected) {
        $selectedLookup[(Get-ItemKey -PackageId $item.packageId -Version $item.version)] = $true
    }

    $missing = foreach ($key in $includeLookup.Keys) {
        if (-not $selectedLookup.ContainsKey($key)) {
            $key
        }
    }

    throw "Failed to prepare all requested salvage items. Missing prepared items: $($missing -join ', ')"
}

$plan = [ordered]@{
    schemaVersion = 1
    batchId = $BatchId
    targetBranch = $TargetBranch
    items = @($selected | Sort-Object packageId, version)
}

$resolvedPlanOutputPath = if ($PlanOutputPath) {
    $PlanOutputPath
}
else {
    Join-Path $downloadDirectory 'expected.json'
}

Write-JsonFile -Path $resolvedPlanOutputPath -InputObject $plan

[pscustomobject]@{
    batchId = $BatchId
    targetBranch = $TargetBranch
    preparedCount = $selected.Count
    planPath = $resolvedPlanOutputPath
    preparedItems = @($selected | Sort-Object packageId, version)
} | ConvertTo-Json -Depth 10

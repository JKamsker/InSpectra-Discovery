[CmdletBinding()]
param(
    [string]$AllIndexPath,

    [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if ([string]::IsNullOrWhiteSpace($AllIndexPath)) {
    $AllIndexPath = Join-Path $RepositoryRoot 'index/all.json'
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $RepositoryRoot 'index/index.json'
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

function Get-PackageCompletenessLabel {
    param([AllowNull()][string]$LatestStatus)

    switch ($LatestStatus) {
        'ok' { return 'full' }
        'partial' { return 'partial' }
        default { return [string]$LatestStatus }
    }
}

function Get-OptionalIntPropertyValue {
    param(
        [AllowNull()][object]$InputObject,
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    if ($null -eq $InputObject) {
        return 0
    }

    $property = $InputObject.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value) {
        return 0
    }

    return [int]$property.Value
}

function Get-NuGetPackageIconUrl {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PackageId,

        [Parameter(Mandatory = $true)]
        [string]$Version
    )

    if ([string]::IsNullOrWhiteSpace($PackageId) -or [string]::IsNullOrWhiteSpace($Version)) {
        return $null
    }

    return "https://api.nuget.org/v3-flatcontainer/$($PackageId.ToLowerInvariant())/$($Version.ToLowerInvariant())/icon"
}

$allIndex = Get-Content -Path $AllIndexPath -Raw | ConvertFrom-Json -Depth 100
$packages = @(
    @($allIndex.packages) | ForEach-Object {
        $latestVersionRecord = @($_.versions) | Select-Object -First 1

        [ordered]@{
            packageId = [string]$_.packageId
            commandName = if ($null -ne $latestVersionRecord) { [string]$latestVersionRecord.command } else { $null }
            versionCount = @($_.versions).Count
            latestVersion = [string]$_.latestVersion
            completeness = Get-PackageCompletenessLabel -LatestStatus ([string]$_.latestStatus)
            packageIconUrl = Get-NuGetPackageIconUrl -PackageId ([string]$_.packageId) -Version ([string]$_.latestVersion)
            commandCount = Get-OptionalIntPropertyValue -InputObject $_ -Name 'commandCount'
            commandGroupCount = Get-OptionalIntPropertyValue -InputObject $_ -Name 'commandGroupCount'
        }
    }
)

$browserIndex = [ordered]@{
    schemaVersion = 1
    generatedAt = [DateTimeOffset]::UtcNow.ToString('o')
    packageCount = $packages.Count
    packages = $packages
}

Write-JsonFile -Path $OutputPath -InputObject $browserIndex

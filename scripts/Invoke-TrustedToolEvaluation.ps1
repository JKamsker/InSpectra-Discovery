[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageId,

    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$Source = 'workflow-dispatch',

    [switch]$Trusted
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$GeneratedRoot = Join-Path $RepositoryRoot 'generated'
$QueueRoot = Join-Path $RepositoryRoot 'queue'
$IndexRoot = Join-Path $RepositoryRoot 'index'
$TempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("inspectra-$($PackageId.ToLowerInvariant())-$($Version.ToLowerInvariant())")
$GeneratedAt = [DateTimeOffset]::UtcNow

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

function Invoke-ProcessCapture {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string[]]$ArgumentList,

        [Parameter(Mandatory = $true)]
        [string]$WorkingDirectory
    )

    $stdoutPath = Join-Path $TempRoot ([System.Guid]::NewGuid().ToString('n') + '.stdout.txt')
    $stderrPath = Join-Path $TempRoot ([System.Guid]::NewGuid().ToString('n') + '.stderr.txt')
    $timer = [System.Diagnostics.Stopwatch]::StartNew()

    $process = Start-Process `
        -FilePath $FilePath `
        -ArgumentList $ArgumentList `
        -WorkingDirectory $WorkingDirectory `
        -Wait `
        -PassThru `
        -NoNewWindow `
        -RedirectStandardOutput $stdoutPath `
        -RedirectStandardError $stderrPath

    $timer.Stop()

    [ordered]@{
        status = if ($process.ExitCode -eq 0) { 'ok' } else { 'failed' }
        exitCode = $process.ExitCode
        durationMs = [int][Math]::Round($timer.Elapsed.TotalMilliseconds)
        stdout = [System.IO.File]::ReadAllText($stdoutPath)
        stderr = [System.IO.File]::ReadAllText($stderrPath)
    }
}

function Resolve-CommandPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ToolDirectory,

        [Parameter(Mandatory = $true)]
        [string]$CommandName
    )

    $candidates = @(
        (Join-Path $ToolDirectory $CommandName),
        (Join-Path $ToolDirectory ($CommandName + '.exe'))
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    throw "Could not locate installed command '$CommandName' under '$ToolDirectory'."
}

function Get-RelativeRepositoryPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $relative = [System.IO.Path]::GetRelativePath($RepositoryRoot, $Path)
    return $relative -replace '\\', '/'
}

function Convert-ToIsoTimestamp {
    param(
        [Parameter(Mandatory = $false)]
        [AllowNull()]
        [object]$Value
    )

    if ($null -eq $Value -or [string]::IsNullOrWhiteSpace([string]$Value)) {
        return $null
    }

    return ([DateTimeOffset]$Value).ToUniversalTime().ToString('o')
}

function Get-LatestPackageSummary {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PackageId,

        [Parameter(Mandatory = $true)]
        [array]$Results
    )

    $ordered = $Results |
        Sort-Object `
            @{ Expression = { if ($_.publishedAt) { [DateTimeOffset]$_.publishedAt } else { [DateTimeOffset]::MinValue } }; Descending = $true }, `
            @{ Expression = { [DateTimeOffset]$_.evaluatedAt }; Descending = $true }

    $latest = $ordered[0]
    $versions = @(
        $ordered | ForEach-Object {
            [ordered]@{
                version = $_.version
                publishedAt = Convert-ToIsoTimestamp $_.publishedAt
                evaluatedAt = Convert-ToIsoTimestamp $_.evaluatedAt
                status = $_.status
                command = $_.command
                generatedPath = $_.generatedPath
                opencliStatus = if ($_.opencli) { $_.opencli.status } else { $null }
                xmldocStatus = if ($_.xmldoc) { $_.xmldoc.status } else { $null }
            }
        }
    )

    [ordered]@{
        packageId = $PackageId
        trusted = [bool]$latest.trusted
        latestVersion = $latest.version
        latestStatus = $latest.status
        latestGeneratedPath = $latest.generatedPath
        versions = $versions
    }
}

Remove-Item $TempRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $TempRoot -Force | Out-Null

$lowerId = $PackageId.ToLowerInvariant()
$lowerVersion = $Version.ToLowerInvariant()
$leafUrl = "https://api.nuget.org/v3/registration5-gz-semver2/$lowerId/$lowerVersion.json"
$leaf = Invoke-RestMethod -Uri $leafUrl
$catalogEntryUrl = [string]$leaf.catalogEntry
$catalogLeaf = Invoke-RestMethod -Uri $catalogEntryUrl
$packageContentUrl = [string]$leaf.packageContent
$packageUrl = "https://www.nuget.org/packages/$PackageId/$Version"

$packageDir = Join-Path $TempRoot 'package'
$packageFile = Join-Path $TempRoot "$lowerId.$lowerVersion.nupkg"
$packageZip = Join-Path $TempRoot "$lowerId.$lowerVersion.zip"
Invoke-WebRequest -Uri $packageContentUrl -OutFile $packageFile
Copy-Item $packageFile $packageZip -Force
Expand-Archive -Path $packageZip -DestinationPath $packageDir -Force

$toolSettingsFile = Get-ChildItem -Path $packageDir -Filter 'DotnetToolSettings.xml' -Recurse | Select-Object -First 1 -ExpandProperty FullName
if (-not $toolSettingsFile) {
    throw "DotnetToolSettings.xml was not found in package '$PackageId' version '$Version'."
}
$packageRelativeToolSettingsPath = ([System.IO.Path]::GetRelativePath($packageDir, $toolSettingsFile)) -replace '\\', '/'

[xml]$toolSettings = Get-Content $toolSettingsFile
$commandName = [string]$toolSettings.DotNetCliTool.Commands.Command.Name
$entryPoint = [string]$toolSettings.DotNetCliTool.Commands.Command.EntryPoint
$runner = [string]$toolSettings.DotNetCliTool.Commands.Command.Runner

$installDirectory = Join-Path $TempRoot 'tool'
$installResult = Invoke-ProcessCapture -FilePath 'dotnet' -ArgumentList @('tool', 'install', $PackageId, '--version', $Version, '--tool-path', $installDirectory) -WorkingDirectory $RepositoryRoot

$commandPath = $null
$openCliResult = $null
$xmlDocResult = $null

if ($installResult.exitCode -eq 0) {
    $commandPath = Resolve-CommandPath -ToolDirectory $installDirectory -CommandName $commandName
    $openCliResult = Invoke-ProcessCapture -FilePath $commandPath -ArgumentList @('cli', 'opencli') -WorkingDirectory $RepositoryRoot
    $xmlDocResult = Invoke-ProcessCapture -FilePath $commandPath -ArgumentList @('cli', 'xmldoc') -WorkingDirectory $RepositoryRoot
}

$status = if (
    $installResult.exitCode -eq 0 -and
    $openCliResult -and $openCliResult.exitCode -eq 0 -and
    $xmlDocResult -and $xmlDocResult.exitCode -eq 0
) {
    'ok'
}
else {
    'failed'
}

$detectionEntries = @(
    @($catalogLeaf.packageEntries) |
        Where-Object { $_.name -in @('Spectre.Console.dll', 'Spectre.Console.Cli.dll') } |
        Select-Object -ExpandProperty fullName
)
$hasSpectreConsole = @($detectionEntries | Where-Object { $_ -like '*Spectre.Console.dll' }).Count -gt 0
$hasSpectreConsoleCli = @($detectionEntries | Where-Object { $_ -like '*Spectre.Console.Cli.dll' }).Count -gt 0

$result = [ordered]@{
    packageId = $PackageId
    version = $Version
    trusted = [bool]$Trusted
    source = $Source
    status = $status
    evaluatedAt = $GeneratedAt.ToString('o')
    publishedAt = ([DateTimeOffset]$catalogLeaf.published).ToUniversalTime().ToString('o')
    packageUrl = $packageUrl
    packageContentUrl = $packageContentUrl
    registrationLeafUrl = $leafUrl
    catalogEntryUrl = $catalogEntryUrl
    command = $commandName
    entryPoint = $entryPoint
    runner = $runner
    toolSettingsPath = $packageRelativeToolSettingsPath
    detection = [ordered]@{
        hasSpectreConsole = $hasSpectreConsole
        hasSpectreConsoleCli = $hasSpectreConsoleCli
        matchedPackageEntries = $detectionEntries
    }
    install = $installResult
    opencli = $openCliResult
    xmldoc = $xmlDocResult
}

$queueFile = Join-Path $QueueRoot "$lowerId/$lowerVersion.json"
$generatedFile = Join-Path $GeneratedRoot "$lowerId/$lowerVersion.json"
$queueRecord = [ordered]@{
    packageId = $PackageId
    version = $Version
    discoveredAt = $GeneratedAt.ToString('o')
    source = $Source
    trusted = [bool]$Trusted
    status = 'analyzed'
}

Write-JsonFile -Path $queueFile -InputObject $queueRecord
Write-JsonFile -Path $generatedFile -InputObject $result

$allResults = @(
    Get-ChildItem -Path $GeneratedRoot -Filter '*.json' -Recurse | ForEach-Object {
        $data = Get-Content $_.FullName -Raw | ConvertFrom-Json
        $data | Add-Member -NotePropertyName generatedPath -NotePropertyValue (Get-RelativeRepositoryPath -Path $_.FullName) -Force
        $data
    }
)

$packageSummaries = @(
    $allResults |
        Group-Object packageId |
        ForEach-Object { Get-LatestPackageSummary -PackageId $_.Name -Results $_.Group } |
        Sort-Object packageId
)

foreach ($summary in $packageSummaries) {
    $packageIndexPath = Join-Path $IndexRoot ("packages/{0}.json" -f $summary.packageId.ToLowerInvariant())
    Write-JsonFile -Path $packageIndexPath -InputObject $summary
}

$allIndex = [ordered]@{
    generatedAt = [DateTimeOffset]::UtcNow.ToString('o')
    packageCount = $packageSummaries.Count
    packages = $packageSummaries
}

Write-JsonFile -Path (Join-Path $IndexRoot 'all.json') -InputObject $allIndex

Write-Host "Evaluated $PackageId $Version"
Write-Host "Generated result: $(Get-RelativeRepositoryPath -Path $generatedFile)"
Write-Host "Package index: index/packages/$lowerId.json"
Write-Host "Aggregate index: index/all.json"

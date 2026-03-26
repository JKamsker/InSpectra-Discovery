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
$IndexRoot = Join-Path $RepositoryRoot 'index'
$PackagesRoot = Join-Path $IndexRoot 'packages'
$TempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("inspectra-$($PackageId.ToLowerInvariant())-$($Version.ToLowerInvariant())")
$GeneratedAt = [DateTimeOffset]::UtcNow
$EvaluationTimer = [System.Diagnostics.Stopwatch]::StartNew()

. (Join-Path $PSScriptRoot 'OpenCliSynthesis.ps1')

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

function Write-TextFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Content
    )

    $directory = Split-Path -Parent $Path
    if ($directory) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    [System.IO.File]::WriteAllText($Path, $Content, [System.Text.UTF8Encoding]::new($false))
}

function Get-NuGetRegistrationLeafVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Version
    )

    $normalized = $Version.Trim().ToLowerInvariant()
    $buildMetadataIndex = $normalized.IndexOf('+')
    if ($buildMetadataIndex -ge 0) {
        $normalized = $normalized.Substring(0, $buildMetadataIndex)
    }

    return $normalized
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

    foreach ($candidate in @(
        (Join-Path $ToolDirectory $CommandName),
        (Join-Path $ToolDirectory ($CommandName + '.exe'))
    )) {
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

function Get-TextLength {
    param(
        [Parameter(Mandatory = $false)]
        [AllowNull()]
        [string]$Value
    )

    if ($null -eq $Value) {
        return 0
    }

    return [System.Text.Encoding]::UTF8.GetByteCount($Value)
}

function Get-StepMetadata {
    param(
        [Parameter(Mandatory = $false)]
        [AllowNull()]
        [object]$Result,

        [Parameter(Mandatory = $false)]
        [AllowNull()]
        [string]$ArtifactPath,

        [switch]$IncludeStdout,

        [switch]$IncludeStderr
    )

    if ($null -eq $Result) {
        return $null
    }

    $metadata = [ordered]@{
        status = $Result.status
        exitCode = $Result.exitCode
        durationMs = $Result.durationMs
        stdoutLength = Get-TextLength $Result.stdout
        stderrLength = Get-TextLength $Result.stderr
    }

    if ($ArtifactPath) {
        $metadata.path = $ArtifactPath
    }

    if ($IncludeStdout) {
        $metadata.stdout = $Result.stdout
    }

    if ($IncludeStderr -or -not [string]::IsNullOrEmpty($Result.stderr)) {
        $metadata.stderr = $Result.stderr
    }

    return $metadata
}

function Sync-LatestDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$VersionDirectory,

        [Parameter(Mandatory = $true)]
        [string]$LatestDirectory
    )

    New-Item -ItemType Directory -Path $LatestDirectory -Force | Out-Null

    foreach ($artifactName in @('metadata.json', 'opencli.json', 'xmldoc.xml')) {
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
    param(
        [Parameter(Mandatory = $true)]
        [array]$Records
    )

    $ordered = $Records |
        Sort-Object `
            @{ Expression = { if ($_.publishedAt) { [DateTimeOffset]$_.publishedAt } else { [DateTimeOffset]::MinValue } }; Descending = $true }, `
            @{ Expression = { [DateTimeOffset]$_.evaluatedAt }; Descending = $true }

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

Remove-Item $TempRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $TempRoot -Force | Out-Null

try {
    $lowerId = $PackageId.ToLowerInvariant()
    $lowerVersion = $Version.ToLowerInvariant()
    $env:HOME = Join-Path $TempRoot 'home'
    $env:DOTNET_CLI_HOME = Join-Path $TempRoot 'dotnet-home'
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    $env:DOTNET_NOLOGO = '1'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:NUGET_PACKAGES = Join-Path $TempRoot 'nuget-packages'
    $env:NUGET_HTTP_CACHE_PATH = Join-Path $TempRoot 'nuget-http-cache'
    $env:XDG_CONFIG_HOME = Join-Path $TempRoot 'xdg-config'
    $env:XDG_CACHE_HOME = Join-Path $TempRoot 'xdg-cache'
    $env:XDG_DATA_HOME = Join-Path $TempRoot 'xdg-data'

    foreach ($directory in @(
        $env:HOME,
        $env:DOTNET_CLI_HOME,
        $env:NUGET_PACKAGES,
        $env:NUGET_HTTP_CACHE_PATH,
        $env:XDG_CONFIG_HOME,
        $env:XDG_CACHE_HOME,
        $env:XDG_DATA_HOME
    )) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $registrationLeafVersion = Get-NuGetRegistrationLeafVersion -Version $Version
    $leafUrl = "https://api.nuget.org/v3/registration5-gz-semver2/$lowerId/$registrationLeafVersion.json"
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

    $toolSettingsFile = Get-ChildItem -Path $packageDir -Filter 'DotnetToolSettings.xml' -Recurse |
        Select-Object -First 1 -ExpandProperty FullName
    if (-not $toolSettingsFile) {
        throw "DotnetToolSettings.xml was not found in package '$PackageId' version '$Version'."
    }

    $packageRelativeToolSettingsPath = ([System.IO.Path]::GetRelativePath($packageDir, $toolSettingsFile)) -replace '\\', '/'

    [xml]$toolSettings = Get-Content $toolSettingsFile
    $commandName = [string]$toolSettings.DotNetCliTool.Commands.Command.Name
    $entryPoint = [string]$toolSettings.DotNetCliTool.Commands.Command.EntryPoint
    $runner = [string]$toolSettings.DotNetCliTool.Commands.Command.Runner

    $installDirectory = Join-Path $TempRoot 'tool'
    $installResult = Invoke-ProcessCapture `
        -FilePath 'dotnet' `
        -ArgumentList @('tool', 'install', $PackageId, '--version', $Version, '--tool-path', $installDirectory) `
        -WorkingDirectory $RepositoryRoot

    $commandPath = $null
    $openCliResult = $null
    $xmlDocResult = $null
    $openCliDocument = $null
    $validatedXmlDoc = $null
    $openCliSource = $null

    if ($installResult.exitCode -eq 0) {
        $commandPath = Resolve-CommandPath -ToolDirectory $installDirectory -CommandName $commandName
        $openCliResult = Invoke-ProcessCapture -FilePath $commandPath -ArgumentList @('cli', 'opencli') -WorkingDirectory $RepositoryRoot
        $xmlDocResult = Invoke-ProcessCapture -FilePath $commandPath -ArgumentList @('cli', 'xmldoc') -WorkingDirectory $RepositoryRoot
    }

    if ($openCliResult -and $openCliResult.exitCode -eq 0) {
        $openCliDocument = $openCliResult.stdout | ConvertFrom-Json
        $openCliSource = 'tool-output'
    }

    if ($xmlDocResult -and $xmlDocResult.exitCode -eq 0) {
        $validatedXmlDoc = [xml]$xmlDocResult.stdout
    }

    if ($null -eq $openCliDocument -and $null -ne $validatedXmlDoc) {
        $openCliDocument = Convert-XmldocToOpenCliDocument `
            -XmlDocument $validatedXmlDoc `
            -Title $commandName
        $openCliSource = 'synthesized-from-xmldoc'
    }

    $status = if (
        $installResult.exitCode -eq 0 -and
        $xmlDocResult -and $xmlDocResult.exitCode -eq 0 -and
        $null -ne $openCliDocument -and
        $null -ne $validatedXmlDoc -and
        $openCliSource -eq 'tool-output'
    ) {
        'ok'
    }
    elseif (
        $installResult.exitCode -eq 0 -and
        ($null -ne $openCliDocument -or $null -ne $validatedXmlDoc)
    ) {
        'partial'
    }
    else {
        'failed'
    }

    $detectionEntries = @(
        @($catalogLeaf.packageEntries) |
            Where-Object { $_.name -in @('Spectre.Console.dll', 'Spectre.Console.Cli.dll') } |
            Select-Object -ExpandProperty fullName
    )

    $packageIndexRoot = Join-Path $PackagesRoot $lowerId
    $versionRoot = Join-Path $packageIndexRoot $lowerVersion
    $metadataPath = Join-Path $versionRoot 'metadata.json'
    $openCliPath = if ($null -ne $openCliDocument) { Join-Path $versionRoot 'opencli.json' } else { $null }
    $xmlDocPath = if ($null -ne $validatedXmlDoc) { Join-Path $versionRoot 'xmldoc.xml' } else { $null }
    $openCliArtifactPath = if ($openCliPath) { Get-RelativeRepositoryPath -Path $openCliPath } else { $null }
    $xmlDocArtifactPath = if ($xmlDocPath) { Get-RelativeRepositoryPath -Path $xmlDocPath } else { $null }

    if ($openCliPath) {
        Write-JsonFile -Path $openCliPath -InputObject $openCliDocument
    }

    if ($xmlDocPath) {
        Write-TextFile -Path $xmlDocPath -Content $xmlDocResult.stdout
    }

    $EvaluationTimer.Stop()

    $metadata = [ordered]@{
        schemaVersion = 1
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
            hasSpectreConsole = @($detectionEntries | Where-Object { $_ -like '*Spectre.Console.dll' }).Count -gt 0
            hasSpectreConsoleCli = @($detectionEntries | Where-Object { $_ -like '*Spectre.Console.Cli.dll' }).Count -gt 0
            matchedPackageEntries = $detectionEntries
        }
        timings = [ordered]@{
            totalMs = [int][Math]::Round($EvaluationTimer.Elapsed.TotalMilliseconds)
            installMs = if ($installResult) { $installResult.durationMs } else { $null }
            opencliMs = if ($openCliResult) { $openCliResult.durationMs } else { $null }
            xmldocMs = if ($xmlDocResult) { $xmlDocResult.durationMs } else { $null }
        }
        steps = [ordered]@{
            install = Get-StepMetadata -Result $installResult -IncludeStdout -IncludeStderr
            opencli = Get-StepMetadata `
                -Result $openCliResult `
                -ArtifactPath $openCliArtifactPath `
                -IncludeStderr
            xmldoc = Get-StepMetadata `
                -Result $xmlDocResult `
                -ArtifactPath $xmlDocArtifactPath `
                -IncludeStderr
        }
        artifacts = [ordered]@{
            metadataPath = Get-RelativeRepositoryPath -Path $metadataPath
            opencliPath = $openCliArtifactPath
            opencliSource = if ($openCliArtifactPath) { $openCliSource } else { $null }
            xmldocPath = $xmlDocArtifactPath
        }
    }

    Write-JsonFile -Path $metadataPath -InputObject $metadata

    $versionRecords = @(
        Get-ChildItem -Path $PackagesRoot -Filter 'metadata.json' -Recurse |
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
                $latestRecord = ($_.Group |
                    Sort-Object `
                        @{ Expression = { if ($_.publishedAt) { [DateTimeOffset]$_.publishedAt } else { [DateTimeOffset]::MinValue } }; Descending = $true }, `
                        @{ Expression = { [DateTimeOffset]$_.evaluatedAt }; Descending = $true })[0]
                $latestDirectory = Join-Path $PackagesRoot ("{0}/latest" -f $_.Name.ToLowerInvariant())

                Sync-LatestDirectory -VersionDirectory $latestRecord.versionDirectoryFullPath -LatestDirectory $latestDirectory
                Write-JsonFile -Path $summaryPath -InputObject $summary
                $summary
            } |
            Sort-Object packageId
    )

    $allIndex = [ordered]@{
        schemaVersion = 1
        generatedAt = [DateTimeOffset]::UtcNow.ToString('o')
        packageCount = $packageSummaries.Count
        packages = $packageSummaries
    }

    Write-JsonFile -Path (Join-Path $IndexRoot 'all.json') -InputObject $allIndex

    Write-Host "Evaluated $PackageId $Version"
    Write-Host "Version metadata: $(Get-RelativeRepositoryPath -Path $metadataPath)"
    if ($openCliPath) {
        Write-Host "OpenCLI JSON: $(Get-RelativeRepositoryPath -Path $openCliPath)"
    }
    if ($xmlDocPath) {
        Write-Host "XML doc: $(Get-RelativeRepositoryPath -Path $xmlDocPath)"
    }
    Write-Host "Latest alias: index/packages/$lowerId/latest/"
    Write-Host "Package index: index/packages/$lowerId/index.json"
    Write-Host "Aggregate index: index/all.json"
}
finally {
    Remove-Item $TempRoot -Recurse -Force -ErrorAction SilentlyContinue
}

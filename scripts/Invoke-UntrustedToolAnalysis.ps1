[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageId,

    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$OutputRoot,

    [Parameter(Mandatory = $true)]
    [string]$BatchId,

    [int]$Attempt = 1,

    [string]$Source = 'untrusted-batch'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$TempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("inspectra-untrusted-$($PackageId.ToLowerInvariant())-$($Version.ToLowerInvariant())-$([System.Guid]::NewGuid().ToString('n'))")
$GeneratedAt = [DateTimeOffset]::UtcNow
$Timer = [System.Diagnostics.Stopwatch]::StartNew()
$ResultPath = Join-Path $OutputRoot 'result.json'

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

function Get-TextLength {
    param([AllowNull()][string]$Value)
    if ($null -eq $Value) { return 0 }
    return [System.Text.Encoding]::UTF8.GetByteCount($Value)
}

function Invoke-ProcessCapture {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string[]]$ArgumentList,

        [Parameter(Mandatory = $true)]
        [string]$WorkingDirectory,

        [int]$TimeoutSeconds = 300
    )

    $stdoutPath = Join-Path $TempRoot ([System.Guid]::NewGuid().ToString('n') + '.stdout.txt')
    $stderrPath = Join-Path $TempRoot ([System.Guid]::NewGuid().ToString('n') + '.stderr.txt')
    $timer = [System.Diagnostics.Stopwatch]::StartNew()
    $process = Start-Process `
        -FilePath $FilePath `
        -ArgumentList $ArgumentList `
        -WorkingDirectory $WorkingDirectory `
        -PassThru `
        -NoNewWindow `
        -RedirectStandardOutput $stdoutPath `
        -RedirectStandardError $stderrPath

    $completed = $process.WaitForExit($TimeoutSeconds * 1000)
    if (-not $completed) {
        try { $process.Kill($true) } catch { }
        $process.WaitForExit()
    }

    $timer.Stop()

    return [ordered]@{
        status = if ($completed -and $process.ExitCode -eq 0) { 'ok' } elseif ($completed) { 'failed' } else { 'timed-out' }
        timedOut = -not $completed
        exitCode = if ($completed) { $process.ExitCode } else { $null }
        durationMs = [int][Math]::Round($timer.Elapsed.TotalMilliseconds)
        stdout = [System.IO.File]::ReadAllText($stdoutPath)
        stderr = [System.IO.File]::ReadAllText($stderrPath)
    }
}

function Get-StepMetadata {
    param(
        [AllowNull()][object]$Result,
        [AllowNull()][string]$ArtifactPath,
        [switch]$IncludeStdout
    )

    if ($null -eq $Result) {
        return $null
    }

    $metadata = [ordered]@{
        status = $Result.status
        timedOut = [bool]$Result.timedOut
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

    if (-not [string]::IsNullOrEmpty($Result.stderr)) {
        $metadata.stderr = $Result.stderr
    }

    return $metadata
}

function Get-FailureSignature {
    param(
        [string]$Phase,
        [string]$Classification,
        [string]$Message
    )

    $normalized = if ($Message) { ($Message -replace '\s+', ' ').Trim() } else { '' }
    return "$Phase|$Classification|$normalized"
}

$result = [ordered]@{
    schemaVersion = 1
    packageId = $PackageId
    version = $Version
    batchId = $BatchId
    attempt = $Attempt
    trusted = $false
    source = $Source
    analyzedAt = $GeneratedAt.ToString('o')
    disposition = 'retryable-failure'
    retryEligible = $true
    phase = 'bootstrap'
    classification = 'uninitialized'
    failureMessage = $null
    failureSignature = $null
    packageUrl = "https://www.nuget.org/packages/$PackageId/$Version"
    packageContentUrl = $null
    registrationLeafUrl = $null
    catalogEntryUrl = $null
    publishedAt = $null
    command = $null
    entryPoint = $null
    runner = $null
    toolSettingsPath = $null
    detection = [ordered]@{
        hasSpectreConsole = $false
        hasSpectreConsoleCli = $false
        matchedPackageEntries = @()
        matchedDependencyIds = @()
    }
    timings = [ordered]@{
        totalMs = $null
        installMs = $null
        opencliMs = $null
        xmldocMs = $null
    }
    steps = [ordered]@{
        install = $null
        opencli = $null
        xmldoc = $null
    }
    artifacts = [ordered]@{
        opencliArtifact = $null
        xmldocArtifact = $null
    }
}

New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null
Remove-Item $TempRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $TempRoot -Force | Out-Null

try {
    $env:HOME = Join-Path $TempRoot 'home'
    $env:DOTNET_CLI_HOME = Join-Path $TempRoot 'dotnet-home'
    $env:NUGET_PACKAGES = Join-Path $TempRoot 'nuget-packages'
    $env:NUGET_HTTP_CACHE_PATH = Join-Path $TempRoot 'nuget-http-cache'

    $lowerId = $PackageId.ToLowerInvariant()
    $lowerVersion = $Version.ToLowerInvariant()
    $leafUrl = "https://api.nuget.org/v3/registration5-gz-semver2/$lowerId/$lowerVersion.json"
    $leaf = Invoke-RestMethod -Uri $leafUrl
    $catalogLeaf = Invoke-RestMethod -Uri ([string]$leaf.catalogEntry)

    $result.registrationLeafUrl = $leafUrl
    $result.catalogEntryUrl = [string]$leaf.catalogEntry
    $result.packageContentUrl = [string]$leaf.packageContent
    $result.publishedAt = ([DateTimeOffset]$catalogLeaf.published).ToUniversalTime().ToString('o')

    $detectionEntries = @(
        @($catalogLeaf.packageEntries) |
            Where-Object { $_.name -in @('Spectre.Console.dll', 'Spectre.Console.Cli.dll') } |
            Select-Object -ExpandProperty fullName
    )

    $dependencyIds = @(
        @($catalogLeaf.dependencyGroups) |
            ForEach-Object { @($_.dependencies) } |
            Where-Object { $_.id -like 'Spectre.Console*' } |
            Select-Object -ExpandProperty id -Unique
    )

    $result.detection.hasSpectreConsole = @($detectionEntries | Where-Object { $_ -like '*Spectre.Console.dll' }).Count -gt 0
    $result.detection.hasSpectreConsoleCli = @($detectionEntries | Where-Object { $_ -like '*Spectre.Console.Cli.dll' }).Count -gt 0 -or @($dependencyIds | Where-Object { $_ -eq 'Spectre.Console.Cli' }).Count -gt 0
    $result.detection.matchedPackageEntries = $detectionEntries
    $result.detection.matchedDependencyIds = $dependencyIds

    if (-not $result.detection.hasSpectreConsoleCli) {
        $result.disposition = 'terminal-negative'
        $result.retryEligible = $false
        $result.phase = 'prefilter'
        $result.classification = 'spectre-cli-missing'
    }
    else {
        $packageDir = Join-Path $TempRoot 'package'
        $packageFile = Join-Path $TempRoot "$lowerId.$lowerVersion.nupkg"
        $packageZip = Join-Path $TempRoot "$lowerId.$lowerVersion.zip"
        Invoke-WebRequest -Uri $result.packageContentUrl -OutFile $packageFile
        Copy-Item $packageFile $packageZip -Force
        Expand-Archive -Path $packageZip -DestinationPath $packageDir -Force

        $toolSettingsFile = Get-ChildItem -Path $packageDir -Filter 'DotnetToolSettings.xml' -Recurse |
            Select-Object -First 1 -ExpandProperty FullName
        if (-not $toolSettingsFile) {
            throw "DotnetToolSettings.xml was not found in package '$PackageId' version '$Version'."
        }

        $result.toolSettingsPath = ([System.IO.Path]::GetRelativePath($packageDir, $toolSettingsFile)) -replace '\\', '/'
        [xml]$toolSettings = Get-Content $toolSettingsFile
        $result.command = [string]$toolSettings.DotNetCliTool.Commands.Command.Name
        $result.entryPoint = [string]$toolSettings.DotNetCliTool.Commands.Command.EntryPoint
        $result.runner = [string]$toolSettings.DotNetCliTool.Commands.Command.Runner

        $installDirectory = Join-Path $TempRoot 'tool'
        $installResult = Invoke-ProcessCapture -FilePath 'dotnet' -ArgumentList @('tool', 'install', $PackageId, '--version', $Version, '--tool-path', $installDirectory) -WorkingDirectory $TempRoot -TimeoutSeconds 300
        $result.steps.install = Get-StepMetadata -Result $installResult -IncludeStdout
        $result.timings.installMs = $installResult.durationMs

        if ($installResult.timedOut -or $installResult.exitCode -ne 0) {
            $result.phase = 'install'
            $result.classification = if ($installResult.timedOut) { 'install-timeout' } else { 'install-failed' }
            $result.failureMessage = if ($installResult.stderr) { $installResult.stderr.Trim() } else { $installResult.stdout.Trim() }
        }
        else {
            $commandPath = foreach ($candidate in @(
                (Join-Path $installDirectory $result.command),
                (Join-Path $installDirectory ($result.command + '.exe'))
            )) { if (Test-Path $candidate) { $candidate; break } }

            if (-not $commandPath) {
                $result.phase = 'install'
                $result.classification = 'installed-command-missing'
                $result.failureMessage = "Installed tool command '$($result.command)' was not found."
            }
            else {
                $openCliResult = Invoke-ProcessCapture -FilePath $commandPath -ArgumentList @('cli', 'opencli') -WorkingDirectory $TempRoot -TimeoutSeconds 300
                $result.timings.opencliMs = $openCliResult.durationMs
                if ($openCliResult.timedOut -or $openCliResult.exitCode -ne 0) {
                    $result.steps.opencli = Get-StepMetadata -Result $openCliResult
                    $result.phase = 'opencli'
                    $result.classification = if ($openCliResult.timedOut) { 'opencli-timeout' } else { 'opencli-failed' }
                    $result.failureMessage = if ($openCliResult.stderr) { $openCliResult.stderr.Trim() } else { $openCliResult.stdout.Trim() }
                }
                else {
                    $openCliDocument = $openCliResult.stdout | ConvertFrom-Json
                    Write-JsonFile -Path (Join-Path $OutputRoot 'opencli.json') -InputObject $openCliDocument
                    $result.artifacts.opencliArtifact = 'opencli.json'
                    $result.steps.opencli = Get-StepMetadata -Result $openCliResult -ArtifactPath 'opencli.json'

                    $xmlDocResult = Invoke-ProcessCapture -FilePath $commandPath -ArgumentList @('cli', 'xmldoc') -WorkingDirectory $TempRoot -TimeoutSeconds 300
                    $result.timings.xmldocMs = $xmlDocResult.durationMs
                    if ($xmlDocResult.timedOut -or $xmlDocResult.exitCode -ne 0) {
                        $result.steps.xmldoc = Get-StepMetadata -Result $xmlDocResult
                        $result.phase = 'xmldoc'
                        $result.classification = if ($xmlDocResult.timedOut) { 'xmldoc-timeout' } else { 'xmldoc-failed' }
                        $result.failureMessage = if ($xmlDocResult.stderr) { $xmlDocResult.stderr.Trim() } else { $xmlDocResult.stdout.Trim() }
                    }
                    else {
                        [xml]$null = $xmlDocResult.stdout
                        Write-TextFile -Path (Join-Path $OutputRoot 'xmldoc.xml') -Content $xmlDocResult.stdout
                        $result.artifacts.xmldocArtifact = 'xmldoc.xml'
                        $result.steps.xmldoc = Get-StepMetadata -Result $xmlDocResult -ArtifactPath 'xmldoc.xml'
                        $result.disposition = 'success'
                        $result.retryEligible = $false
                        $result.phase = 'complete'
                        $result.classification = 'spectre-cli-confirmed'
                    }
                }
            }
        }
    }
}
catch {
    $result.disposition = 'retryable-failure'
    $result.retryEligible = $true
    if ($result.phase -eq 'bootstrap') {
        $result.phase = 'bootstrap'
        $result.classification = 'unexpected-exception'
    }
    $result.failureMessage = $_.Exception.Message
}
finally {
    $Timer.Stop()
    $result.timings.totalMs = [int][Math]::Round($Timer.Elapsed.TotalMilliseconds)
    if ($result.disposition -eq 'retryable-failure') {
        $result.failureSignature = Get-FailureSignature -Phase $result.phase -Classification $result.classification -Message $result.failureMessage
    }

    Write-JsonFile -Path $ResultPath -InputObject $result
    Remove-Item $TempRoot -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "Analyzed $PackageId $Version"
Write-Host "Disposition: $($result.disposition)"
Write-Host "Result artifact: result.json"

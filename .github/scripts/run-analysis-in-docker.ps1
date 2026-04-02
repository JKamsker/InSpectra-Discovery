param(
    [Parameter(Mandatory = $true)]
    [string]$ToolRoot,

    [Parameter(Mandatory = $true)]
    [string]$OutputRoot,

    [Parameter(Mandatory = $true)]
    [string]$PackageId,

    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$BatchId,

    [Parameter(Mandatory = $true)]
    [int]$Attempt,

    [string]$Source = 'analyze-untrusted-batch',
    [string]$Image = 'mcr.microsoft.com/dotnet/sdk:10.0',
    [int]$InstallTimeoutSeconds = 300,
    [int]$AnalysisTimeoutSeconds = 600,
    [int]$CommandTimeoutSeconds = 60,
    [switch]$Json
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$resolvedToolRoot = (Resolve-Path -LiteralPath $ToolRoot).Path
if (-not (Test-Path -LiteralPath $OutputRoot)) {
    New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null
}

$resolvedOutputRoot = (Resolve-Path -LiteralPath $OutputRoot).Path
$containerToolRoot = '/opt/inspectra-discovery'
$containerOutputRoot = '/work/output'
$containerDotnetRoot = '/usr/share/dotnet'
$toolExecutablePath = Join-Path $resolvedToolRoot 'inspectra-discovery'
$toolAssemblyPath = Join-Path $resolvedToolRoot 'InSpectra.Discovery.Tool.dll'

$dockerArgs = @(
    'run',
    '--rm',
    '--env', 'HOME=/tmp',
    '--env', 'DOTNET_CLI_HOME=/tmp/.dotnet',
    '--env', 'NUGET_PACKAGES=/tmp/.nuget/packages'
)

if ($IsLinux -and -not [string]::IsNullOrWhiteSpace($env:DOTNET_ROOT) -and (Test-Path -LiteralPath $env:DOTNET_ROOT)) {
    $dockerArgs += @(
        '--volume', "$($env:DOTNET_ROOT):${containerDotnetRoot}:ro",
        '--env', "DOTNET_ROOT=${containerDotnetRoot}",
        '--env', "DOTNET_ROOT_X64=${containerDotnetRoot}"
    )
}

if ($IsLinux) {
    $userId = (& id -u).Trim()
    $groupId = (& id -g).Trim()
    if (-not [string]::IsNullOrWhiteSpace($userId) -and -not [string]::IsNullOrWhiteSpace($groupId)) {
        $dockerArgs += @('--user', "${userId}:${groupId}")
    }
}

$dockerArgs += @(
    '--volume', "${resolvedToolRoot}:${containerToolRoot}:ro",
    '--volume', "${resolvedOutputRoot}:${containerOutputRoot}"
)

$toolCommand = if (Test-Path -LiteralPath $toolExecutablePath) {
    @("${containerToolRoot}/inspectra-discovery")
}
elseif (Test-Path -LiteralPath $toolAssemblyPath) {
    @('dotnet', "${containerToolRoot}/InSpectra.Discovery.Tool.dll")
}
else {
    throw "Tool root '$resolvedToolRoot' did not contain inspectra-discovery or InSpectra.Discovery.Tool.dll."
}

$dockerArgs += @(
    $Image
)
$dockerArgs += $toolCommand
$dockerArgs += @(
    'analysis',
    'run-auto',
    '--package-id', $PackageId,
    '--version', $Version,
    '--output-root', $containerOutputRoot,
    '--batch-id', $BatchId,
    '--attempt', $Attempt.ToString([System.Globalization.CultureInfo]::InvariantCulture),
    '--source', $Source,
    '--install-timeout-seconds', $InstallTimeoutSeconds.ToString([System.Globalization.CultureInfo]::InvariantCulture),
    '--analysis-timeout-seconds', $AnalysisTimeoutSeconds.ToString([System.Globalization.CultureInfo]::InvariantCulture),
    '--command-timeout-seconds', $CommandTimeoutSeconds.ToString([System.Globalization.CultureInfo]::InvariantCulture)
)

if ($Json) {
    $dockerArgs += '--json'
}

Write-Host "Running analysis for ${PackageId} ${Version} in Docker image ${Image}."
& docker @dockerArgs
if ($LASTEXITCODE -ne 0) {
    throw "Dockerized analysis failed with exit code $LASTEXITCODE."
}

[CmdletBinding()]
param(
    [string]$PackageId,

    [string]$Version,

    [string]$AnalysisMode,

    [string]$Classification,

    [string]$MessageContains,

    [int]$Limit,

    [string]$BatchId,

    [string]$WorkingRoot,

    [switch]$KeepWorkingRoot,

    [string]$Source = 'docker-latest-partial-reanalysis',

    [string]$Image = 'mcr.microsoft.com/dotnet/sdk:10.0',

    [string]$ToolRoot,

    [int]$InstallTimeoutSeconds = 120,

    [int]$AnalysisTimeoutSeconds = 180,

    [int]$CommandTimeoutSeconds = 30
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$resolvedBatchId = if ([string]::IsNullOrWhiteSpace($BatchId)) {
    "docker-latest-partials-$([DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssfffZ'))"
}
else {
    $BatchId
}

$resolvedWorkingRoot = if ([string]::IsNullOrWhiteSpace($WorkingRoot)) {
    Join-Path ([System.IO.Path]::GetTempPath()) "inspectra-docker-latest-partials-$([Guid]::NewGuid().ToString('N'))"
}
else {
    [System.IO.Path]::GetFullPath($WorkingRoot)
}

$preserveWorkingRoot = $KeepWorkingRoot -or -not [string]::IsNullOrWhiteSpace($WorkingRoot)
$expectedPath = Join-Path $resolvedWorkingRoot 'plan\expected.json'
$summaryPath = Join-Path $resolvedWorkingRoot 'promotion-summary.json'
$dockerRunnerPath = Join-Path $repositoryRoot '.github\scripts\run-analysis-in-docker.ps1'
$resolvedToolRoot = $null
$discoveryInvocation = $null

function Normalize-Segment {
    param([string]$Value)

    $normalized = [System.Text.RegularExpressions.Regex]::Replace($Value, '[^A-Za-z0-9._-]+', '-').Trim('-')
    while ($normalized.Contains('--', [System.StringComparison]::Ordinal)) {
        $normalized = $normalized.Replace('--', '-', [System.StringComparison]::Ordinal)
    }

    if ([string]::IsNullOrWhiteSpace($normalized)) {
        return 'item'
    }

    return $normalized
}

function Resolve-ToolRoot {
    param(
        [string]$ToolRootPath
    )

    if ([string]::IsNullOrWhiteSpace($ToolRootPath)) {
        throw "ToolRoot is required because the discovery tool source now lives in the InSpectra repository. Provide a tool root containing inspectra-discovery or InSpectra.Discovery.Tool.dll."
    }

    $resolvedPath = (Resolve-Path -LiteralPath $ToolRootPath).Path
    $toolExecutablePath = Join-Path $resolvedPath 'inspectra-discovery'
    $toolAssemblyPath = Join-Path $resolvedPath 'InSpectra.Discovery.Tool.dll'
    if (
        -not (Test-Path -LiteralPath $toolExecutablePath) -and
        -not (Test-Path -LiteralPath $toolAssemblyPath)
    ) {
        throw "Tool root '$resolvedPath' did not contain inspectra-discovery or InSpectra.Discovery.Tool.dll."
    }

    return $resolvedPath
}

function Resolve-DiscoveryInvocation {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ToolRootPath
    )

    $pathCommand = Get-Command inspectra-discovery -ErrorAction SilentlyContinue
    if ($pathCommand) {
        return [pscustomobject]@{
            Command = $pathCommand.Source
            Prefix = @()
        }
    }

    $toolAssemblyPath = Join-Path $ToolRootPath 'InSpectra.Discovery.Tool.dll'
    if (Test-Path -LiteralPath $toolAssemblyPath) {
        return [pscustomobject]@{
            Command = 'dotnet'
            Prefix = @($toolAssemblyPath)
        }
    }

    $toolExecutablePath = Join-Path $ToolRootPath 'inspectra-discovery'
    return [pscustomobject]@{
        Command = $toolExecutablePath
        Prefix = @()
    }
}

function Invoke-DiscoveryTool {
    param(
        [Parameter(Mandatory = $true)]
        $Invocation,

        [Parameter(Mandatory = $true)]
        [string[]]$ArgumentList
    )

    $resolvedArguments = @($Invocation.Prefix) + $ArgumentList
    & $Invocation.Command @resolvedArguments | Out-Host

    return $LASTEXITCODE
}

try {
    $resolvedToolRoot = Resolve-ToolRoot `
        -ToolRootPath $ToolRoot
    $discoveryInvocation = Resolve-DiscoveryInvocation -ToolRootPath $resolvedToolRoot

    New-Item -ItemType Directory -Path (Split-Path -Parent $expectedPath) -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $resolvedWorkingRoot 'results') -Force | Out-Null

    $exportArgs = @(
        'docs',
        'export-latest-partials-plan',
        '--output', $expectedPath,
        '--batch-id', $resolvedBatchId,
        '--target-branch', 'main',
        '--json'
    )

    if (-not [string]::IsNullOrWhiteSpace($PackageId)) {
        $exportArgs += @('--package-id', $PackageId)
    }

    if (-not [string]::IsNullOrWhiteSpace($Version)) {
        $exportArgs += @('--version', $Version)
    }

    if (-not [string]::IsNullOrWhiteSpace($AnalysisMode)) {
        $exportArgs += @('--analysis-mode', $AnalysisMode)
    }

    if (-not [string]::IsNullOrWhiteSpace($Classification)) {
        $exportArgs += @('--classification', $Classification)
    }

    if (-not [string]::IsNullOrWhiteSpace($MessageContains)) {
        $exportArgs += @('--message-contains', $MessageContains)
    }

    if ($Limit -gt 0) {
        $exportArgs += @('--limit', $Limit.ToString([System.Globalization.CultureInfo]::InvariantCulture))
    }

    $exportExitCode = Invoke-DiscoveryTool `
        -Invocation $discoveryInvocation `
        -ArgumentList $exportArgs
    if ($exportExitCode -ne 0) {
        throw "Failed to export latest partial plan."
    }

    $expected = Get-Content -LiteralPath $expectedPath -Raw | ConvertFrom-Json -Depth 100
    $items = @($expected.items)
    if ($items.Count -eq 0) {
        Write-Host "No matching latest partials were selected."
        return
    }

    for ($i = 0; $i -lt $items.Count; $i++) {
        $item = $items[$i]
        $itemOutputRoot = Join-Path $resolvedWorkingRoot ('results\{0:D4}-{1}-{2}' -f $i, (Normalize-Segment $item.packageId), (Normalize-Segment $item.version))
        $resultPath = Join-Path $itemOutputRoot 'result.json'
        if (Test-Path -LiteralPath $resultPath) {
            Write-Host "Skipping existing result for $($item.packageId) $($item.version)."
            continue
        }

        & $dockerRunnerPath `
            -ToolRoot $resolvedToolRoot `
            -OutputRoot $itemOutputRoot `
            -PackageId ([string]$item.packageId) `
            -Version ([string]$item.version) `
            -BatchId $resolvedBatchId `
            -Attempt ([int]$item.attempt) `
            -Source $Source `
            -Image $Image `
            -InstallTimeoutSeconds $InstallTimeoutSeconds `
            -AnalysisTimeoutSeconds $AnalysisTimeoutSeconds `
            -CommandTimeoutSeconds $CommandTimeoutSeconds `
            -Json
    }

    $applyArgs = @(
        'promotion',
        'apply-untrusted',
        '--download-root', $resolvedWorkingRoot,
        '--summary-output', $summaryPath,
        '--json'
    )
    $applyExitCode = Invoke-DiscoveryTool `
        -Invocation $discoveryInvocation `
        -ArgumentList $applyArgs
    if ($applyExitCode -ne 0) {
        throw "Failed to apply untrusted promotion output from '$resolvedWorkingRoot'."
    }

    Write-Host "Reanalysis complete. Working root: $resolvedWorkingRoot"
}
finally {
    if (-not $preserveWorkingRoot -and (Test-Path -LiteralPath $resolvedWorkingRoot)) {
        Remove-Item -LiteralPath $resolvedWorkingRoot -Recurse -Force
    }
}

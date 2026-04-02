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

    [string]$Configuration = 'Debug',

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
$toolProject = Join-Path $repositoryRoot 'src\InSpectra.Discovery.Tool\InSpectra.Discovery.Tool.csproj'
$dockerRunnerPath = Join-Path $repositoryRoot '.github\scripts\run-analysis-in-docker.ps1'
$resolvedToolRoot = $null

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
        [string]$RepositoryRoot,
        [string]$ProjectPath,
        [string]$ConfigurationName,
        [string]$ToolRootPath
    )

    if (-not [string]::IsNullOrWhiteSpace($ToolRootPath)) {
        return (Resolve-Path -LiteralPath $ToolRootPath).Path
    }

    $defaultToolRoot = Join-Path $RepositoryRoot "src\InSpectra.Discovery.Tool\bin\$ConfigurationName\net10.0"
    $toolAssemblyPath = Join-Path $defaultToolRoot 'InSpectra.Discovery.Tool.dll'
    if (Test-Path -LiteralPath $toolAssemblyPath) {
        return $defaultToolRoot
    }

    Write-Host "Building discovery tool in $ConfigurationName so Docker analysis can reuse the host output."
    dotnet build $ProjectPath -c $ConfigurationName --nologo | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed with exit code $LASTEXITCODE."
    }

    if (-not (Test-Path -LiteralPath $toolAssemblyPath)) {
        throw "Tool build output '$toolAssemblyPath' was not created."
    }

    return $defaultToolRoot
}

try {
    $resolvedToolRoot = Resolve-ToolRoot `
        -RepositoryRoot $repositoryRoot `
        -ProjectPath $toolProject `
        -ConfigurationName $Configuration `
        -ToolRootPath $ToolRoot

    New-Item -ItemType Directory -Path (Split-Path -Parent $expectedPath) -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $resolvedWorkingRoot 'results') -Force | Out-Null

    $exportArgs = @(
        'run',
        '--project', $toolProject,
        '--',
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

    dotnet @exportArgs | Out-Host
    if ($LASTEXITCODE -ne 0) {
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

    dotnet run --project $toolProject -- promotion apply-untrusted --download-root $resolvedWorkingRoot --summary-output $summaryPath --json | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to apply untrusted promotion output from '$resolvedWorkingRoot'."
    }

    Write-Host "Reanalysis complete. Working root: $resolvedWorkingRoot"
}
finally {
    if (-not $preserveWorkingRoot -and (Test-Path -LiteralPath $resolvedWorkingRoot)) {
        Remove-Item -LiteralPath $resolvedWorkingRoot -Recurse -Force
    }
}

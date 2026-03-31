param(
    [Parameter(Mandatory = $true)]
    [string]$RuntimeRequestsJson,

    [string]$InstallDir = $env:DOTNET_ROOT
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RuntimeRequestsJson) -or $RuntimeRequestsJson -eq '[]')
{
    Write-Host 'No additional shared runtimes are required.'
    exit 0
}

if ([string]::IsNullOrWhiteSpace($InstallDir))
{
    throw 'DOTNET_ROOT is not set.'
}

$requests = @($RuntimeRequestsJson | ConvertFrom-Json)
if ($requests.Count -eq 0)
{
    Write-Host 'No additional shared runtimes are required.'
    exit 0
}

$installerDirectory = Join-Path $env:RUNNER_TEMP 'dotnet-install'
New-Item -ItemType Directory -Force -Path $installerDirectory | Out-Null

if ($IsWindows)
{
    $installerPath = Join-Path $installerDirectory 'dotnet-install.ps1'
    if (-not (Test-Path $installerPath))
    {
        Invoke-WebRequest 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installerPath
    }

    foreach ($request in $requests)
    {
        Write-Host "Installing shared runtime '$($request.runtime)' on channel '$($request.channel)' for '$($request.name)'."
        & $installerPath `
            -Runtime $request.runtime `
            -Channel $request.channel `
            -InstallDir $InstallDir `
            -SkipNonVersionedFiles `
            -NoPath
    }

    exit 0
}

$installerPath = Join-Path $installerDirectory 'dotnet-install.sh'
if (-not (Test-Path $installerPath))
{
    Invoke-WebRequest 'https://dot.net/v1/dotnet-install.sh' -OutFile $installerPath
}

& chmod +x $installerPath

foreach ($request in $requests)
{
    Write-Host "Installing shared runtime '$($request.runtime)' on channel '$($request.channel)' for '$($request.name)'."
    & bash $installerPath `
        --runtime $request.runtime `
        --channel $request.channel `
        --install-dir $InstallDir `
        --skip-non-versioned-files `
        --no-path
}

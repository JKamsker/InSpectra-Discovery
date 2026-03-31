param(
    [Parameter(Mandatory = $true)]
    [string]$RuntimeRequestsJson,

    [string]$InstallDir = $env:DOTNET_ROOT
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-RequestFieldValue
{
    param(
        [Parameter(Mandatory = $true)]
        [AllowNull()]
        $Request,

        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    if ($Request -is [System.Collections.IDictionary])
    {
        return [string]$Request[$Name]
    }

    $property = $Request.PSObject.Properties[$Name]
    if ($null -eq $property)
    {
        return $null
    }

    return [string]$property.Value
}

function ConvertFrom-StringifiedHashtable
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    $trimmed = $Value.Trim()
    if ((-not $trimmed.StartsWith('@{', [StringComparison]::Ordinal)) -or
        (-not $trimmed.EndsWith('}', [StringComparison]::Ordinal)))
    {
        throw "Unsupported runtime request string '$Value'."
    }

    $pairs = [ordered]@{}
    $content = $trimmed.Substring(2, $trimmed.Length - 3)
    $splitOptions = [StringSplitOptions]::RemoveEmptyEntries -bor [StringSplitOptions]::TrimEntries
    foreach ($segment in $content.Split(';', $splitOptions))
    {
        $parts = $segment.Split('=', 2, [StringSplitOptions]::TrimEntries)
        if ($parts.Length -ne 2)
        {
            throw "Could not parse runtime request segment '$segment'."
        }

        $pairs[$parts[0]] = $parts[1]
    }

    return [pscustomobject]$pairs
}

function ConvertTo-RuntimeRequest
{
    param(
        [Parameter(Mandatory = $true)]
        [AllowNull()]
        $Request
    )

    $normalized = $Request
    if ($Request -is [string])
    {
        $trimmed = $Request.Trim()
        if ($trimmed.StartsWith('{', [StringComparison]::Ordinal))
        {
            $normalized = $trimmed | ConvertFrom-Json
        }
        else
        {
            $normalized = ConvertFrom-StringifiedHashtable -Value $trimmed
        }
    }

    $runtime = Get-RequestFieldValue -Request $normalized -Name 'runtime'
    $channel = Get-RequestFieldValue -Request $normalized -Name 'channel'
    $name = Get-RequestFieldValue -Request $normalized -Name 'name'
    $version = Get-RequestFieldValue -Request $normalized -Name 'version'

    if ([string]::IsNullOrWhiteSpace($runtime) -or [string]::IsNullOrWhiteSpace($channel))
    {
        throw "Runtime request is missing required values: '$($Request | Out-String)'."
    }

    return [pscustomobject]@{
        name = $name
        version = $version
        channel = $channel
        runtime = $runtime
    }
}

if ([string]::IsNullOrWhiteSpace($RuntimeRequestsJson) -or $RuntimeRequestsJson -eq '[]')
{
    Write-Host 'No additional shared runtimes are required.'
    exit 0
}

if ([string]::IsNullOrWhiteSpace($InstallDir))
{
    throw 'DOTNET_ROOT is not set.'
}

$rawRequests = @($RuntimeRequestsJson | ConvertFrom-Json)
$requests = @($rawRequests | ForEach-Object { ConvertTo-RuntimeRequest -Request $_ }) |
    Sort-Object runtime, channel -Unique
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

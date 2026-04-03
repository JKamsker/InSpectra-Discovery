param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [switch]$ResetLocalAuthorToGlobal
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-GitConfigValue {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Scope,

        [Parameter(Mandatory = $true)]
        [string]$Key
    )

    $command = @('config')
    if ($Scope -in @('local', 'global')) {
        $command += "--$Scope"
    }

    $command += @('--get', $Key)
    $output = git -C $RepositoryRoot @command 2>$null
    if ($LASTEXITCODE -ne 0) {
        return $null
    }

    return ($output | Select-Object -First 1).Trim()
}

git -C $RepositoryRoot config core.hooksPath .githooks

if ($ResetLocalAuthorToGlobal) {
    $globalName = Get-GitConfigValue -Scope 'global' -Key 'user.name'
    $globalEmail = Get-GitConfigValue -Scope 'global' -Key 'user.email'

    if ([string]::IsNullOrWhiteSpace($globalName) -or [string]::IsNullOrWhiteSpace($globalEmail)) {
        throw 'Global git user.name/user.email must be set before resetting the repo-local author.'
    }

    git -C $RepositoryRoot config --unset-all --local user.name 2>$null
    git -C $RepositoryRoot config --unset-all --local user.email 2>$null
}

$effectiveName = Get-GitConfigValue -Scope 'get' -Key 'user.name'
$effectiveEmail = Get-GitConfigValue -Scope 'get' -Key 'user.email'

Write-Host "Configured core.hooksPath=.githooks for '$RepositoryRoot'."
Write-Host "Effective git identity: $effectiveName <$effectiveEmail>"

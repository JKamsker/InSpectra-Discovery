param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [switch]$CheckOutgoingCommits
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$botName = 'github-actions[bot]'
$botEmail = '41898282+github-actions[bot]@users.noreply.github.com'

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

function Test-IsBotIdentity {
    param(
        [AllowNull()][string]$Name,
        [AllowNull()][string]$Email
    )

    return [string]::Equals($Name, $botName, [System.StringComparison]::Ordinal) -or
        [string]::Equals($Email, $botEmail, [System.StringComparison]::OrdinalIgnoreCase)
}

function Test-IsCiContext {
    if ($env:GITHUB_ACTIONS -eq 'true') {
        return $true
    }

    if ([string]::IsNullOrWhiteSpace($env:CI)) {
        return $false
    }

    return $env:CI -ne 'false'
}

function Get-UpstreamReference {
    $output = git -C $RepositoryRoot rev-parse --abbrev-ref --symbolic-full-name '@{upstream}' 2>$null
    if ($LASTEXITCODE -ne 0) {
        return $null
    }

    return ($output | Select-Object -First 1).Trim()
}

if (Test-IsCiContext) {
    exit 0
}

$localName = Get-GitConfigValue -Scope 'local' -Key 'user.name'
$localEmail = Get-GitConfigValue -Scope 'local' -Key 'user.email'
$effectiveName = Get-GitConfigValue -Scope 'get' -Key 'user.name'
$effectiveEmail = Get-GitConfigValue -Scope 'get' -Key 'user.email'

$violations = [System.Collections.Generic.List[string]]::new()

if (Test-IsBotIdentity -Name $localName -Email $localEmail) {
    $violations.Add("Repo-local git identity is set to the GitHub Actions bot: '$localName <$localEmail>'.")
}

if (Test-IsBotIdentity -Name $effectiveName -Email $effectiveEmail) {
    $violations.Add("Effective git identity resolves to the GitHub Actions bot: '$effectiveName <$effectiveEmail>'.")
}

if (Test-IsBotIdentity -Name $env:GIT_AUTHOR_NAME -Email $env:GIT_AUTHOR_EMAIL) {
    $violations.Add("Environment override GIT_AUTHOR_* resolves to the GitHub Actions bot.")
}

if (Test-IsBotIdentity -Name $env:GIT_COMMITTER_NAME -Email $env:GIT_COMMITTER_EMAIL) {
    $violations.Add("Environment override GIT_COMMITTER_* resolves to the GitHub Actions bot.")
}

if ($CheckOutgoingCommits) {
    $upstream = Get-UpstreamReference
    if (-not [string]::IsNullOrWhiteSpace($upstream)) {
        $records = git -C $RepositoryRoot log --format='%H%x1f%an%x1f%ae%x1f%cn%x1f%ce' "$upstream..HEAD" 2>$null
        if ($LASTEXITCODE -eq 0) {
            foreach ($record in @($records)) {
                if ([string]::IsNullOrWhiteSpace($record)) {
                    continue
                }

                $parts = $record -split [char]31
                if ($parts.Count -lt 5) {
                    continue
                }

                $commitSha = $parts[0]
                $authorName = $parts[1]
                $authorEmail = $parts[2]
                $committerName = $parts[3]
                $committerEmail = $parts[4]

                if (Test-IsBotIdentity -Name $authorName -Email $authorEmail) {
                    $violations.Add("Outgoing commit $commitSha is authored by the GitHub Actions bot.")
                }

                if (Test-IsBotIdentity -Name $committerName -Email $committerEmail) {
                    $violations.Add("Outgoing commit $commitSha is committed by the GitHub Actions bot.")
                }
            }
        }
    }
}

if ($violations.Count -eq 0) {
    exit 0
}

$message = @(
    'Refusing local git operation because this repository is configured to use the GitHub Actions bot identity.'
    ''
    'Detected problems:'
    ($violations | ForEach-Object { "- $_" })
    ''
    'Fix:'
    '- Run `pwsh ./eng/Enable-RepositoryGuards.ps1 -ResetLocalAuthorToGlobal` to restore your normal local author.'
    '- Or unset the repo-local override manually with `git config --unset-all --local user.name` and `git config --unset-all --local user.email`.'
)

throw ($message -join [Environment]::NewLine)

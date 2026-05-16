[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Repository,

    [Parameter(Mandatory = $true)]
    [string]$Branch,

    [Parameter(Mandatory = $true)]
    [string]$BaseBranch,

    [Parameter(Mandatory = $true)]
    [string]$Title,

    [Parameter(Mandatory = $true)]
    [string]$BodyPath,

    [string]$ExistingPullRequestNumber,

    [string]$ExistingPullRequestUrl,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-JsonFile {
    param([string]$Path, [object]$InputObject)

    $directory = Split-Path -Parent $Path
    if ($directory) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $json = $InputObject | ConvertTo-Json -Depth 20
    [System.IO.File]::WriteAllText($Path, $json + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
}

function Get-ExistingPullRequest {
    param(
        [string]$Repository,
        [string]$Branch
    )

    $prs = @(gh pr list --repo $Repository --state open --head $Branch --json number,url,title | ConvertFrom-Json)
    return $prs | Select-Object -First 1
}

function Get-PullRequestByNumber {
    param(
        [string]$Repository,
        [string]$Number
    )

    if ([string]::IsNullOrWhiteSpace($Number)) {
        return $null
    }

    try {
        return gh pr view $Number --repo $Repository --json number,url,title,state | ConvertFrom-Json
    }
    catch {
        return $null
    }
}

function Set-PullRequestBody {
    param(
        [string]$Repository,
        [string]$Number,
        [string]$Title,
        [string]$BodyPath
    )

    gh pr edit $Number `
        --repo $Repository `
        --title $Title `
        --body-file $BodyPath | Out-Null
}

$resolved = $null
$operation = $null

if (-not [string]::IsNullOrWhiteSpace($ExistingPullRequestUrl)) {
    $resolved = Get-PullRequestByNumber -Repository $Repository -Number $ExistingPullRequestNumber
    if ($resolved -and $resolved.state -eq 'OPEN') {
        Set-PullRequestBody -Repository $Repository -Number $resolved.number -Title $Title -BodyPath $BodyPath
        $operation = 'updated'
    }
}

if (-not $resolved) {
    $resolved = Get-ExistingPullRequest -Repository $Repository -Branch $Branch
    if ($resolved) {
        Set-PullRequestBody -Repository $Repository -Number $resolved.number -Title $Title -BodyPath $BodyPath
        $operation = 'updated'
    }
}

if (-not $resolved) {
    $maxAttempts = 10
    for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
        try {
            $createdUrl = gh pr create `
                --repo $Repository `
                --base $BaseBranch `
                --head $Branch `
                --title $Title `
                --body-file $BodyPath

            $resolved = gh pr view $createdUrl.Trim() --repo $Repository --json number,url,title | ConvertFrom-Json
            $operation = 'created'
            break
        }
        catch {
            $message = $_.Exception.Message
            $resolved = Get-ExistingPullRequest -Repository $Repository -Branch $Branch
            if ($resolved) {
                Set-PullRequestBody -Repository $Repository -Number $resolved.number -Title $Title -BodyPath $BodyPath
                $operation = 'updated'
                break
            }

            $shouldRetry = $message -match 'field":"head"' -or
                $message -match 'field ''head''' -or
                $message -match 'Head sha can''t be blank' -or
                $message -match 'No commits between' -or
                $message -match 'GraphQL:'

            if (-not $shouldRetry -or $attempt -eq $maxAttempts) {
                throw
            }

            Start-Sleep -Seconds 15
        }
    }
}

if (-not $resolved) {
    throw "Failed to resolve a pull request for branch '$Branch'."
}

Write-JsonFile -Path $OutputPath -InputObject ([ordered]@{
    number = [string]$resolved.number
    url = [string]$resolved.url
    title = [string]$resolved.title
    operation = $operation
})

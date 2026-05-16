[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$Repository
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$ReleasePrefix = 'Promote untrusted analysis '
$ReleaseMarker = ' - Promote untrusted analysis '
$BotName = 'github-actions[bot]'
$BotEmail = '41898282+github-actions[bot]@users.noreply.github.com'

function Invoke-GhJson {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    return (& gh @Arguments | ConvertFrom-Json)
}

function Resolve-RepositoryName {
    param(
        [AllowNull()][string]$ExplicitRepository
    )

    if (-not [string]::IsNullOrWhiteSpace($ExplicitRepository)) {
        return $ExplicitRepository.Trim()
    }

    return (& gh repo view --json nameWithOwner --jq '.nameWithOwner').Trim()
}

function Get-BatchIdFromReleaseName {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$ReleaseName
    )

    if ($ReleaseName.StartsWith($ReleasePrefix, [System.StringComparison]::Ordinal)) {
        return $ReleaseName.Substring($ReleasePrefix.Length)
    }

    $markerIndex = $ReleaseName.IndexOf($ReleaseMarker, [System.StringComparison]::Ordinal)
    if ($markerIndex -ge 0) {
        return $ReleaseName.Substring($markerIndex + $ReleaseMarker.Length)
    }

    throw "Release title '$ReleaseName' is not in a supported format."
}

function Get-RunIdFromTag {
    param(
        [Parameter(Mandatory = $true)]
        [string]$TagName
    )

    $match = [regex]::Match($TagName, '(?:-run-|-)(?<run>\d+)$')
    if (-not $match.Success) {
        throw "Unable to determine the analysis run id from tag '$TagName'."
    }

    return $match.Groups['run'].Value
}

function Get-LocalTagState {
    param(
        [Parameter(Mandatory = $true)]
        [string]$TagName
    )

    $objectTypeLines = @(git for-each-ref "refs/tags/$TagName" --format='%(objecttype)')
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to inspect tag '$TagName'."
    }

    $objectType = if ($objectTypeLines.Count -gt 0) { [string]$objectTypeLines[0] } else { '' }
    if ([string]::IsNullOrWhiteSpace($objectType)) {
        return $null
    }

    $target = (git rev-list -n 1 $TagName).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to resolve the commit for tag '$TagName'."
    }

    return [ordered]@{
        objectType = $objectType.Trim()
        target = $target
    }
}

function Ensure-AnnotatedTag {
    param(
        [Parameter(Mandatory = $true)]
        [string]$TagName,

        [Parameter(Mandatory = $true)]
        [string]$TargetCommit,

        [Parameter(Mandatory = $true)]
        [string]$Annotation,

        [Parameter(Mandatory = $true)]
        [string]$TagTimestampIso
    )

    $existing = Get-LocalTagState -TagName $TagName
    if ($null -ne $existing) {
        if ($existing.objectType -eq 'tag' -and $existing.target -eq $TargetCommit) {
            return
        }

        throw "Refusing to overwrite existing tag '$TagName' that points to '$($existing.target)' as a '$($existing.objectType)' object."
    }

    $previousAuthorName = $env:GIT_AUTHOR_NAME
    $previousAuthorEmail = $env:GIT_AUTHOR_EMAIL
    $previousAuthorDate = $env:GIT_AUTHOR_DATE
    $previousCommitterName = $env:GIT_COMMITTER_NAME
    $previousCommitterEmail = $env:GIT_COMMITTER_EMAIL
    $previousCommitterDate = $env:GIT_COMMITTER_DATE

    try {
        $env:GIT_AUTHOR_NAME = $BotName
        $env:GIT_AUTHOR_EMAIL = $BotEmail
        $env:GIT_AUTHOR_DATE = $TagTimestampIso
        $env:GIT_COMMITTER_NAME = $BotName
        $env:GIT_COMMITTER_EMAIL = $BotEmail
        $env:GIT_COMMITTER_DATE = $TagTimestampIso

        git tag -a $TagName $TargetCommit -m $Annotation | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to create annotated tag '$TagName'."
        }
    }
    finally {
        $env:GIT_AUTHOR_NAME = $previousAuthorName
        $env:GIT_AUTHOR_EMAIL = $previousAuthorEmail
        $env:GIT_AUTHOR_DATE = $previousAuthorDate
        $env:GIT_COMMITTER_NAME = $previousCommitterName
        $env:GIT_COMMITTER_EMAIL = $previousCommitterEmail
        $env:GIT_COMMITTER_DATE = $previousCommitterDate
    }

    git push origin "refs/tags/$TagName" | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to push tag '$TagName' to origin."
    }
}

function Remove-Tag {
    param(
        [Parameter(Mandatory = $true)]
        [string]$TagName
    )

    $existing = Get-LocalTagState -TagName $TagName
    if ($null -eq $existing) {
        return
    }

    git push origin ":refs/tags/$TagName" | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to delete remote tag '$TagName'."
    }

    git tag -d $TagName | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to delete local tag '$TagName'."
    }
}

Push-Location $RepositoryRoot
try {
    $resolvedRepository = Resolve-RepositoryName -ExplicitRepository $Repository

    git fetch --force --tags origin | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw 'Failed to fetch tags from origin.'
    }

    $releasePages = @(Invoke-GhJson -Arguments @(
            'api',
            "repos/$resolvedRepository/releases",
            '--paginate',
            '--slurp'
        ))
    $releases = @($releasePages | ForEach-Object { @($_) })

    $entries = [System.Collections.Generic.List[object]]::new()

    foreach ($release in $releases) {
        if (-not ([string]$release.tag_name).StartsWith('untrusted-analysis-', [System.StringComparison]::Ordinal)) {
            continue
        }

        $batchId = Get-BatchIdFromReleaseName -ReleaseName ([string]$release.name)
        $runId = Get-RunIdFromTag -TagName ([string]$release.tag_name)
        $metadata = & (Join-Path $RepositoryRoot 'eng/Get-UntrustedAnalysisReleaseMetadata.ps1') `
            -BatchId $batchId `
            -AnalysisRunId $runId `
            -FallbackTimestamp ([string]($release.created_at ?? $release.published_at)) | ConvertFrom-Json

        $entries.Add([pscustomobject]@{
                releaseId = [int64]$release.id
                oldTag = [string]$release.tag_name
                oldTitle = [string]$release.name
                targetCommit = [string]$release.target_commitish
                releaseCreatedAt = [string]$release.created_at
                releasePublishedAt = [string]$release.published_at
                batchId = $batchId
                newTag = [string]$metadata.releaseTag
                newTitle = [string]$metadata.releaseTitle
                tagAnnotation = [string]$metadata.tagAnnotation
                tagTimestampIso = [string]$metadata.releaseTimestampIso
            })
    }

    if ($entries.Count -eq 0) {
        Write-Host "No matching untrusted-analysis releases found in $resolvedRepository."
        exit 0
    }

    $duplicateTagGroups = @($entries | Group-Object newTag | Where-Object { $_.Count -gt 1 })
    if ($duplicateTagGroups.Count -gt 0) {
        $duplicateTags = @($duplicateTagGroups | ForEach-Object { $_.Name }) -join ', '
        throw "Backfill would create duplicate release tags: $duplicateTags"
    }

    foreach ($entry in @($entries | Sort-Object tagTimestampIso, releaseId)) {
        if ($PSCmdlet.ShouldProcess($entry.newTag, "Create annotated tag at $($entry.targetCommit)")) {
            Ensure-AnnotatedTag `
                -TagName $entry.newTag `
                -TargetCommit $entry.targetCommit `
                -Annotation $entry.tagAnnotation `
                -TagTimestampIso $entry.tagTimestampIso
        }

        if ($entry.oldTag -ne $entry.newTag -or $entry.oldTitle -ne $entry.newTitle) {
            if ($PSCmdlet.ShouldProcess($entry.oldTag, "Retarget release to '$($entry.newTag)' and rename it")) {
                gh release edit $entry.oldTag `
                    --repo $resolvedRepository `
                    --tag $entry.newTag `
                    --target $entry.targetCommit `
                    --title $entry.newTitle | Out-Null
                if ($LASTEXITCODE -ne 0) {
                    throw "Failed to retarget release '$($entry.oldTag)' to '$($entry.newTag)'."
                }
            }
        }

        if ($entry.oldTag -ne $entry.newTag) {
            if ($PSCmdlet.ShouldProcess($entry.oldTag, 'Delete superseded tag')) {
                Remove-Tag -TagName $entry.oldTag
            }
        }
    }

    $latestTag = ($entries | Sort-Object tagTimestampIso, releaseId -Descending | Select-Object -First 1).newTag
    foreach ($entry in $entries) {
        $makeLatest = if ($entry.newTag -eq $latestTag) { 'true' } else { 'false' }
        if ($PSCmdlet.ShouldProcess($entry.newTag, "Set make_latest=$makeLatest")) {
            gh api --method PATCH "repos/$resolvedRepository/releases/$($entry.releaseId)" -f make_latest=$makeLatest | Out-Null
            if ($LASTEXITCODE -ne 0) {
                throw "Failed to update latest-release metadata for '$($entry.newTag)'."
            }
        }
    }

    Write-Host "Backfilled $($entries.Count) untrusted-analysis releases in $resolvedRepository."
    Write-Host "Latest release tag: $latestTag"
}
finally {
    Pop-Location
}

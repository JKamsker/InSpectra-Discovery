[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BatchId,

    [Parameter(Mandatory = $true)]
    [string]$AnalysisRunId,

    [AllowNull()][string]$FallbackTimestamp
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Convert-BatchTimestamp {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    $normalized = $Value.Trim().ToLowerInvariant()
    $match = [regex]::Match($normalized, '^(?<date>\d{8})t(?<time>\d{6})(?<fraction>\d+)?z?$')
    if (-not $match.Success) {
        throw "Timestamp '$Value' is not in the expected batch timestamp format."
    }

    $combined = '{0}{1}' -f $match.Groups['date'].Value, $match.Groups['time'].Value
    return [DateTimeOffset]::ParseExact(
        $combined,
        'yyyyMMddHHmmss',
        [System.Globalization.CultureInfo]::InvariantCulture,
        [System.Globalization.DateTimeStyles]::AssumeUniversal
    ).ToUniversalTime()
}

function Resolve-ReleaseTimestamp {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceBatchId,

        [AllowNull()][string]$SourceFallbackTimestamp
    )

    $matches = [regex]::Matches($SourceBatchId, '\d{8}t\d{6}(?:\d+)?z?', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if ($matches.Count -gt 0) {
        return [ordered]@{
            source = 'batch-id'
            value = Convert-BatchTimestamp -Value $matches[$matches.Count - 1].Value
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($SourceFallbackTimestamp)) {
        return [ordered]@{
            source = 'fallback'
            value = ([DateTimeOffset]$SourceFallbackTimestamp).ToUniversalTime()
        }
    }

    throw "Unable to resolve a release timestamp for batch '$SourceBatchId'."
}

function Get-NormalizedSlug {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Value
    )

    $normalized = ($Value.Trim().ToLowerInvariant() -replace '[^a-z0-9]+', '-').Trim('-')
    if ([string]::IsNullOrWhiteSpace($normalized)) {
        throw 'A normalized slug must not be empty.'
    }

    return $normalized
}

$resolvedTimestamp = Resolve-ReleaseTimestamp -SourceBatchId $BatchId -SourceFallbackTimestamp $FallbackTimestamp
$releaseTimestamp = $resolvedTimestamp.value
$releaseTimestampTag = $releaseTimestamp.ToString('yyyyMMddTHHmmssZ').ToLowerInvariant()
$releaseTimestampDisplay = '{0} UTC' -f $releaseTimestamp.ToString('yyyy-MM-dd HH:mm:ss')
$normalizedBatchId = Get-NormalizedSlug -Value $BatchId
$normalizedRunId = Get-NormalizedSlug -Value $AnalysisRunId

$metadata = [ordered]@{
    batchId = $BatchId
    analysisRunId = $AnalysisRunId
    releaseTimestampIso = $releaseTimestamp.ToString('o')
    releaseTimestampTag = $releaseTimestampTag
    releaseTimestampDisplay = $releaseTimestampDisplay
    releaseTimestampSource = $resolvedTimestamp.source
    releaseTag = ('untrusted-analysis-{0}-{1}-run-{2}' -f $releaseTimestampTag, $normalizedBatchId, $normalizedRunId)
    releaseTitle = ('{0} - Promote untrusted analysis {1}' -f $releaseTimestampDisplay, $BatchId)
    tagAnnotation = ('Promote untrusted analysis {0}' -f $BatchId)
}

$metadata | ConvertTo-Json -Compress

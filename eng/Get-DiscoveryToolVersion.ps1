param(
  [string]$Ref = 'HEAD',
  [string]$SourcePath = 'src',
  [string]$VersionPrefix = '0.0.0-ci'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$commitSha = (git log -1 --format=%H $Ref -- $SourcePath).Trim()
if ([string]::IsNullOrWhiteSpace($commitSha)) {
  throw "Unable to resolve a commit that touched '$SourcePath' from ref '$Ref'."
}

# Use commit timestamp (UTC, compact) so NuGet sorts versions chronologically.
# Format: 0.0.0-ci.YYYYMMDDHHmmss.<short-sha>
# The timestamp ensures monotonic ordering; the sha identifies the exact commit.
$commitTimestamp = (git log -1 --format=%cd --date=format:'%Y%m%d%H%M%S' $commitSha).Trim()
$shortSha = $commitSha.Substring(0, [Math]::Min(12, $commitSha.Length)).ToLowerInvariant()

return "$VersionPrefix.$commitTimestamp.$shortSha"

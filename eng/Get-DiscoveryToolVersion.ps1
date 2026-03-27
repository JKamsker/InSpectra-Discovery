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

$shortShaLength = [Math]::Min(12, $commitSha.Length)
$shortSha = $commitSha.Substring(0, $shortShaLength).ToLowerInvariant()

return "$VersionPrefix.$shortSha"

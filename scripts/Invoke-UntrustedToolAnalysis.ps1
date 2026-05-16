[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageId,

    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$OutputRoot,

    [Parameter(Mandatory = $true)]
    [string]$BatchId,

    [int]$Attempt = 1,

    [string]$Source = 'untrusted-batch',

    [int]$InstallTimeoutSeconds = 300,

    [int]$CommandTimeoutSeconds = 60
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$TempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("inspectra-untrusted-$($PackageId.ToLowerInvariant())-$($Version.ToLowerInvariant())-$([System.Guid]::NewGuid().ToString('n'))")
$GeneratedAt = [DateTimeOffset]::UtcNow
$Timer = [System.Diagnostics.Stopwatch]::StartNew()
$ResultPath = Join-Path $OutputRoot 'result.json'

function Write-JsonFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [object]$InputObject
    )

    $directory = Split-Path -Parent $Path
    if ($directory) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $json = $InputObject | ConvertTo-Json -Depth 100
    [System.IO.File]::WriteAllText($Path, $json + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
}

function Write-TextFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Content
    )

    $directory = Split-Path -Parent $Path
    if ($directory) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    [System.IO.File]::WriteAllText($Path, $Content, [System.Text.UTF8Encoding]::new($false))
}

function Get-OptionalPropertyValue {
    param(
        [AllowNull()][object]$InputObject,
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    if ($null -eq $InputObject) {
        return $null
    }

    if ($InputObject.PSObject.Properties.Name -contains $Name) {
        return $InputObject.$Name
    }

    return $null
}

function Get-NuGetRegistrationLeafVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Version
    )

    $normalized = $Version.Trim().ToLowerInvariant()
    $buildMetadataIndex = $normalized.IndexOf('+')
    if ($buildMetadataIndex -ge 0) {
        $normalized = $normalized.Substring(0, $buildMetadataIndex)
    }

    return $normalized
}

function Get-FirstXmlNodeValue {
    param(
        [Parameter(Mandatory = $true)]
        [xml]$Document,

        [Parameter(Mandatory = $true)]
        [string[]]$Xpaths
    )

    foreach ($xpath in $Xpaths) {
        $node = $Document.SelectSingleNode($xpath)
        if ($null -eq $node) {
            continue
        }

        $value = if ($node -is [System.Xml.XmlAttribute]) { $node.Value } else { $node.InnerText }
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            return $value.Trim()
        }
    }

    return $null
}

function Get-DotnetToolCommandDescriptor {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ToolSettingsFile
    )

    [xml]$toolSettings = Get-Content -Path $ToolSettingsFile -Raw

    $command = Get-FirstXmlNodeValue -Document $toolSettings -Xpaths @(
        "//*[local-name()='Command'][1]/@Name",
        "//*[local-name()='ToolCommandName'][1]",
        "//*[local-name()='CommandName'][1]"
    )
    $entryPoint = Get-FirstXmlNodeValue -Document $toolSettings -Xpaths @(
        "//*[local-name()='Command'][1]/@EntryPoint",
        "//*[local-name()='EntryPoint'][1]"
    )
    $runner = Get-FirstXmlNodeValue -Document $toolSettings -Xpaths @(
        "//*[local-name()='Command'][1]/@Runner",
        "//*[local-name()='Runner'][1]"
    )

    return [ordered]@{
        command = $command
        entryPoint = $entryPoint
        runner = $runner
    }
}

function Get-TextLength {
    param([AllowNull()][string]$Value)
    if ($null -eq $Value) { return 0 }
    return [System.Text.Encoding]::UTF8.GetByteCount($Value)
}

function Invoke-ProcessCapture {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string[]]$ArgumentList,

        [Parameter(Mandatory = $true)]
        [string]$WorkingDirectory,

        [int]$TimeoutSeconds = 300
    )

    $stdoutPath = Join-Path $TempRoot ([System.Guid]::NewGuid().ToString('n') + '.stdout.txt')
    $stderrPath = Join-Path $TempRoot ([System.Guid]::NewGuid().ToString('n') + '.stderr.txt')
    $timer = [System.Diagnostics.Stopwatch]::StartNew()
    $process = Start-Process `
        -FilePath $FilePath `
        -ArgumentList $ArgumentList `
        -WorkingDirectory $WorkingDirectory `
        -PassThru `
        -NoNewWindow `
        -RedirectStandardOutput $stdoutPath `
        -RedirectStandardError $stderrPath

    $completed = $process.WaitForExit($TimeoutSeconds * 1000)
    if (-not $completed) {
        try { $process.Kill($true) } catch { }
        $process.WaitForExit()
    }

    $timer.Stop()

    return [ordered]@{
        status = if ($completed -and $process.ExitCode -eq 0) { 'ok' } elseif ($completed) { 'failed' } else { 'timed-out' }
        timedOut = -not $completed
        exitCode = if ($completed) { $process.ExitCode } else { $null }
        durationMs = [int][Math]::Round($timer.Elapsed.TotalMilliseconds)
        stdout = [System.IO.File]::ReadAllText($stdoutPath)
        stderr = [System.IO.File]::ReadAllText($stderrPath)
    }
}

function Normalize-ConsoleText {
    param([AllowNull()][string]$Value)

    if ([string]::IsNullOrEmpty($Value)) {
        return $null
    }

    $normalized = $Value -replace '^\uFEFF', ''
    $escape = [regex]::Escape([string][char]27)
    $normalized = $normalized -replace ($escape + '\[[0-?]*[ -/]*[@-~]'), ''
    $normalized = $normalized -replace ($escape + '[@-_]'), ''
    $normalized = $normalized -replace "`0", ''

    return $normalized.Trim()
}

function Get-PreferredMessage {
    param(
        [AllowNull()][string]$Stdout,
        [AllowNull()][string]$Stderr
    )

    $normalizedStderr = Normalize-ConsoleText $Stderr
    if (-not [string]::IsNullOrWhiteSpace($normalizedStderr)) {
        return $normalizedStderr
    }

    $normalizedStdout = Normalize-ConsoleText $Stdout
    if (-not [string]::IsNullOrWhiteSpace($normalizedStdout)) {
        return $normalizedStdout
    }

    return $null
}

function Test-MatchesAnyPattern {
    param(
        [AllowNull()][string]$Text,
        [Parameter(Mandatory = $true)]
        [string[]]$Patterns
    )

    if ([string]::IsNullOrWhiteSpace($Text)) {
        return $false
    }

    foreach ($pattern in $Patterns) {
        if ($Text -match $pattern) {
            return $true
        }
    }

    return $false
}

function Get-BalancedJsonSegment {
    param([AllowNull()][string]$Text)

    if ([string]::IsNullOrWhiteSpace($Text)) {
        return $null
    }

    $start = $null
    for ($i = 0; $i -lt $Text.Length; $i++) {
        $char = $Text[$i]
        if ($char -eq '{' -or $char -eq '[') {
            $start = $i
            break
        }
    }

    if ($null -eq $start) {
        return $null
    }

    $stack = [System.Collections.Generic.Stack[char]]::new()
    $inString = $false
    $escapeNext = $false

    for ($i = $start; $i -lt $Text.Length; $i++) {
        $char = $Text[$i]

        if ($escapeNext) {
            $escapeNext = $false
            continue
        }

        if ($inString) {
            if ($char -eq '\') {
                $escapeNext = $true
            }
            elseif ($char -eq '"') {
                $inString = $false
            }

            continue
        }

        if ($char -eq '"') {
            $inString = $true
            continue
        }

        if ($char -eq '{') {
            $stack.Push('}')
            continue
        }

        if ($char -eq '[') {
            $stack.Push(']')
            continue
        }

        if (($char -eq '}' -or $char -eq ']') -and $stack.Count -gt 0 -and $stack.Peek() -eq $char) {
            $null = $stack.Pop()
            if ($stack.Count -eq 0) {
                return $Text.Substring($start, ($i - $start) + 1).Trim()
            }
        }
    }

    return $null
}

function Try-ParseJsonPayload {
    param([AllowNull()][string]$Text)

    $normalized = Normalize-ConsoleText $Text
    if ([string]::IsNullOrWhiteSpace($normalized)) {
        return [ordered]@{
            success = $false
            document = $null
            artifactText = $null
            normalizedText = $normalized
            error = 'Output was empty.'
        }
    }

    $candidates = [System.Collections.Generic.List[string]]::new()
    $candidates.Add($normalized)

    $firstBrace = $normalized.IndexOf('{')
    $firstBracket = $normalized.IndexOf('[')
    $startIndex = @($firstBrace, $firstBracket) | Where-Object { $_ -ge 0 } | Sort-Object | Select-Object -First 1
    if ($null -ne $startIndex -and $startIndex -gt 0) {
        $candidate = $normalized.Substring($startIndex).Trim()
        if (-not [string]::IsNullOrWhiteSpace($candidate)) {
            $candidates.Add($candidate)
        }
    }

    $balancedCandidate = Get-BalancedJsonSegment -Text $normalized
    if (-not [string]::IsNullOrWhiteSpace($balancedCandidate) -and -not $candidates.Contains($balancedCandidate)) {
        $candidates.Add($balancedCandidate)
    }

    $lastError = $null
    foreach ($candidate in $candidates) {
        try {
            return [ordered]@{
                success = $true
                document = $candidate | ConvertFrom-Json
                artifactText = $candidate
                normalizedText = $normalized
                error = $null
            }
        }
        catch {
            $lastError = $_.Exception.Message
        }
    }

    return [ordered]@{
        success = $false
        document = $null
        artifactText = $null
        normalizedText = $normalized
        error = if ($lastError) { $lastError } else { 'JSON parsing failed.' }
    }
}

function Try-ParseXmlPayload {
    param([AllowNull()][string]$Text)

    $normalized = Normalize-ConsoleText $Text
    if ([string]::IsNullOrWhiteSpace($normalized)) {
        return [ordered]@{
            success = $false
            document = $null
            artifactText = $null
            normalizedText = $normalized
            error = 'Output was empty.'
        }
    }

    $candidates = [System.Collections.Generic.List[string]]::new()
    $candidates.Add($normalized)

    $firstAngle = $normalized.IndexOf('<')
    if ($firstAngle -gt 0) {
        $candidate = $normalized.Substring($firstAngle).Trim()
        if (-not [string]::IsNullOrWhiteSpace($candidate)) {
            $candidates.Add($candidate)
        }
    }

    $lastError = $null
    foreach ($candidate in $candidates) {
        try {
            [xml]$document = $candidate
            return [ordered]@{
                success = $true
                document = $document
                artifactText = $candidate
                normalizedText = $normalized
                error = $null
            }
        }
        catch {
            $lastError = $_.Exception.Message
        }
    }

    return [ordered]@{
        success = $false
        document = $null
        artifactText = $null
        normalizedText = $normalized
        error = if ($lastError) { $lastError } else { 'XML parsing failed.' }
    }
}

function Get-IntrospectionClassification {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$ArgumentList,

        [AllowNull()][string]$Text
    )

    $escapedSegments = @($ArgumentList | ForEach-Object { [regex]::Escape($_) })
    $subcommandPattern = if ($escapedSegments.Count -gt 0) { '(?:' + ($escapedSegments -join '|') + ')' } else { '(?:cli|opencli|xmldoc)' }

    if (Test-MatchesAnyPattern -Text $Text -Patterns @(
        "(?is)\bunknown command\b.*\b$subcommandPattern\b",
        "(?is)\bunrecognized command\b.*\b$subcommandPattern\b",
        "(?is)\bunknown argument\b.*\b$subcommandPattern\b",
        "(?is)\bunrecognized argument\b.*\b$subcommandPattern\b",
        "(?is)\b$subcommandPattern\b.*\b(?:not recognized|not found|not a valid command|invalid command)\b",
        "(?is)\bcould not match\b.*\b$subcommandPattern\b",
        "(?is)\bcould not resolve type\b.*\b(?:opencli|xmldoc|spectre\.console\.cli\.(?:opendoc|xmldoc|xml?doc)command|spectre\.console\.cli\.xmldoccommand)\b",
        '(?is)\brequired command was not provided\b'
    )) {
        return 'unsupported-command'
    }

    if (Test-MatchesAnyPattern -Text $Text -Patterns @(
        '(?is)\byou must install or update \.net\b',
        '(?is)\bframework:\s+''?microsoft\.netcore\.app',
        '(?is)\bno frameworks? were found\b',
        '(?is)\bthe following frameworks were found\b'
    )) {
        return 'environment-missing-runtime'
    }

    if (Test-MatchesAnyPattern -Text $Text -Patterns @(
        '(?is)\b(unable to load shared library|cannot open shared object file|dllnotfoundexception|could not load file or assembly|libsecret)\b'
    )) {
        return 'environment-missing-dependency'
    }

    if (Test-MatchesAnyPattern -Text $Text -Patterns @(
        '(?is)\b(?:current terminal isn''t interactive|non-interactive mode|cannot prompt|cannot show selection prompt|failed to read input in non-interactive mode)\b'
    )) {
        return 'requires-interactive-input'
    }

    if (Test-MatchesAnyPattern -Text $Text -Patterns @(
        '(?is)\b(?:windows only|unsupported operating system|platform not supported|os platform is not supported)\b'
    )) {
        return 'unsupported-platform'
    }

    if (Test-MatchesAnyPattern -Text $Text -Patterns @(
        '(?is)\b(checking your credentials|credential|authenticate|authentication|device code|sign in|login|log in|open (?:the )?browser)\b'
    )) {
        return 'requires-interactive-authentication'
    }

    if (Test-MatchesAnyPattern -Text $Text -Patterns @(
        '(?is)\b(no|missing)\b.*\b(config|configuration)\b',
        '(?is)\bconfiguration\b',
        '(?is)\b(required option|required argument)\b',
        '(?is)\bmust be specified\b',
        '(?is)\bnot enough arguments\b'
    )) {
        return 'requires-configuration'
    }

    return $null
}

function Invoke-IntrospectionCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$CommandPath,

        [Parameter(Mandatory = $true)]
        [string[]]$ArgumentList,

        [Parameter(Mandatory = $true)]
        [string]$WorkingDirectory,

        [Parameter(Mandatory = $true)]
        [ValidateSet('json', 'xml')]
        [string]$ExpectedFormat,

        [int]$TimeoutSeconds = 300
    )

    $result = Invoke-ProcessCapture -FilePath $CommandPath -ArgumentList $ArgumentList -WorkingDirectory $WorkingDirectory -TimeoutSeconds $TimeoutSeconds
    $preferredMessage = Get-PreferredMessage -Stdout $result.stdout -Stderr $result.stderr
    $classification = Get-IntrospectionClassification -ArgumentList $ArgumentList -Text $preferredMessage
    $parse = if ($ExpectedFormat -eq 'json') { Try-ParseJsonPayload -Text $result.stdout } else { Try-ParseXmlPayload -Text $result.stdout }

    $status = 'failed'
    $dispositionHint = 'retryable-failure'
    $message = $preferredMessage
    $artifactObject = $null
    $artifactText = $null

    if ($result.timedOut) {
        $status = 'timed-out'
        if ($classification -in @('requires-configuration', 'environment-missing-dependency', 'requires-interactive-authentication', 'requires-interactive-input', 'unsupported-platform')) {
            $dispositionHint = 'terminal-failure'
        }
        elseif ($classification -eq 'environment-missing-runtime') {
            # Runner images change over time; keep missing runtimes retryable so a later run can recover.
            $dispositionHint = 'retryable-failure'
        }
        else {
            $classification = 'timeout'
            $dispositionHint = 'retryable-failure'
        }
        $message = if ($preferredMessage) { $preferredMessage } else { 'Command timed out.' }
    }
    elseif ($parse.success) {
        $status = 'ok'
        $classification = if ($ExpectedFormat -eq 'json') {
            if ($result.exitCode -eq 0) { 'json-ready' } else { 'json-ready-with-nonzero-exit' }
        }
        else {
            if ($result.exitCode -eq 0) { 'xml-ready' } else { 'xml-ready-with-nonzero-exit' }
        }
        $dispositionHint = 'success'
        $artifactObject = $parse.document
        $artifactText = $parse.artifactText
        $message = if ($result.exitCode -eq 0) { $null } else { $preferredMessage }
    }
    elseif ($classification) {
        $status = if ($classification -eq 'unsupported-command') { 'unsupported' } else { 'failed' }
        $dispositionHint = if ($classification -eq 'environment-missing-runtime') { 'retryable-failure' } else { 'terminal-failure' }
        if (-not $message -and $parse.error) {
            $message = $parse.error
        }
    }
    elseif ($result.exitCode -eq 0) {
        $status = 'invalid-output'
        $classification = if ($ExpectedFormat -eq 'json') { 'invalid-json' } else { 'invalid-xml' }
        $dispositionHint = 'terminal-failure'
        $message = if ($parse.error) { $parse.error } else { 'Command exited successfully but did not emit valid output.' }
    }
    else {
        $status = 'failed'
        $classification = 'command-failed'
        $dispositionHint = 'retryable-failure'
        if (-not $message -and $parse.error) {
            $message = $parse.error
        }
    }

    return [ordered]@{
        commandName = $ArgumentList[-1]
        expectedFormat = $ExpectedFormat
        processResult = $result
        status = $status
        classification = $classification
        dispositionHint = $dispositionHint
        message = $message
        artifactObject = $artifactObject
        artifactText = $artifactText
    }
}

function Get-StepMetadata {
    param(
        [AllowNull()][object]$Result,
        [AllowNull()][string]$ArtifactPath,
        [switch]$IncludeStdout
    )

    if ($null -eq $Result) {
        return $null
    }

    $metadata = [ordered]@{
        status = $Result.status
        timedOut = [bool]$Result.timedOut
        exitCode = $Result.exitCode
        durationMs = $Result.durationMs
        stdoutLength = Get-TextLength $Result.stdout
        stderrLength = Get-TextLength $Result.stderr
    }

    if ($ArtifactPath) {
        $metadata.path = $ArtifactPath
    }

    if ($IncludeStdout) {
        $metadata.stdout = Normalize-ConsoleText $Result.stdout
    }

    if (-not [string]::IsNullOrEmpty($Result.stderr)) {
        $metadata.stderr = Normalize-ConsoleText $Result.stderr
    }

    return $metadata
}

function Get-OutcomeMetadata {
    param(
        [AllowNull()][object]$Outcome,
        [AllowNull()][string]$ArtifactPath
    )

    if ($null -eq $Outcome) {
        return $null
    }

    $metadata = Get-StepMetadata -Result $Outcome.processResult -ArtifactPath $ArtifactPath -IncludeStdout:($Outcome.status -ne 'ok')
    $metadata.outcomeStatus = $Outcome.status
    $metadata.classification = $Outcome.classification
    if ($Outcome.message) {
        $metadata.message = $Outcome.message
    }

    return $metadata
}

function Get-FailureSignature {
    param(
        [string]$Phase,
        [string]$Classification,
        [string]$Message
    )

    $normalized = if ($Message) { ($Message -replace '\s+', ' ').Trim() } else { '' }
    return "$Phase|$Classification|$normalized"
}

$result = [ordered]@{
    schemaVersion = 1
    packageId = $PackageId
    version = $Version
    batchId = $BatchId
    attempt = $Attempt
    trusted = $false
    source = $Source
    analyzedAt = $GeneratedAt.ToString('o')
    disposition = 'retryable-failure'
    retryEligible = $true
    phase = 'bootstrap'
    classification = 'uninitialized'
    failureMessage = $null
    failureSignature = $null
    packageUrl = "https://www.nuget.org/packages/$PackageId/$Version"
    packageContentUrl = $null
    registrationLeafUrl = $null
    catalogEntryUrl = $null
    publishedAt = $null
    command = $null
    entryPoint = $null
    runner = $null
    toolSettingsPath = $null
    detection = [ordered]@{
        hasSpectreConsole = $false
        hasSpectreConsoleCli = $false
        matchedPackageEntries = @()
        matchedDependencyIds = @()
    }
    introspection = [ordered]@{
        opencli = $null
        xmldoc = $null
    }
    timings = [ordered]@{
        totalMs = $null
        installMs = $null
        opencliMs = $null
        xmldocMs = $null
    }
    steps = [ordered]@{
        install = $null
        opencli = $null
        xmldoc = $null
    }
    artifacts = [ordered]@{
        opencliArtifact = $null
        xmldocArtifact = $null
    }
}

New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null
Remove-Item $TempRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $TempRoot -Force | Out-Null

try {
    $env:HOME = Join-Path $TempRoot 'home'
    $env:DOTNET_CLI_HOME = Join-Path $TempRoot 'dotnet-home'
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    $env:DOTNET_NOLOGO = '1'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_SYSTEM_CONSOLE_ALLOW_ANSI_COLOR_REDIRECTION = '0'
    $env:NUGET_PACKAGES = Join-Path $TempRoot 'nuget-packages'
    $env:NUGET_HTTP_CACHE_PATH = Join-Path $TempRoot 'nuget-http-cache'
    $env:XDG_CONFIG_HOME = Join-Path $TempRoot 'xdg-config'
    $env:XDG_CACHE_HOME = Join-Path $TempRoot 'xdg-cache'
    $env:XDG_DATA_HOME = Join-Path $TempRoot 'xdg-data'
    $env:XDG_RUNTIME_DIR = Join-Path $TempRoot 'xdg-runtime'
    $env:TMPDIR = Join-Path $TempRoot 'tmp'
    $env:TMP = $env:TMPDIR
    $env:TEMP = $env:TMPDIR
    $env:USERPROFILE = $env:HOME
    $env:APPDATA = $env:XDG_CONFIG_HOME
    $env:LOCALAPPDATA = $env:XDG_DATA_HOME
    $env:CI = 'true'
    $env:NO_COLOR = '1'
    $env:FORCE_COLOR = '0'
    $env:TERM = 'dumb'
    $env:GCM_CREDENTIAL_STORE = 'none'
    $env:GCM_INTERACTIVE = 'never'
    $env:GIT_TERMINAL_PROMPT = '0'

    foreach ($directory in @(
        $env:HOME,
        $env:DOTNET_CLI_HOME,
        $env:NUGET_PACKAGES,
        $env:NUGET_HTTP_CACHE_PATH,
        $env:XDG_CONFIG_HOME,
        $env:XDG_CACHE_HOME,
        $env:XDG_DATA_HOME,
        $env:XDG_RUNTIME_DIR,
        $env:TMPDIR
    )) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $lowerId = $PackageId.ToLowerInvariant()
    $lowerVersion = $Version.ToLowerInvariant()
    $registrationLeafVersion = Get-NuGetRegistrationLeafVersion -Version $Version
    $leafUrl = "https://api.nuget.org/v3/registration5-gz-semver2/$lowerId/$registrationLeafVersion.json"
    $leaf = Invoke-RestMethod -Uri $leafUrl
    $catalogLeaf = Invoke-RestMethod -Uri ([string]$leaf.catalogEntry)

    $result.registrationLeafUrl = $leafUrl
    $result.catalogEntryUrl = [string]$leaf.catalogEntry
    $result.packageContentUrl = [string]$leaf.packageContent
    $result.publishedAt = ([DateTimeOffset]$catalogLeaf.published).ToUniversalTime().ToString('o')

    $packageEntries = if ($catalogLeaf.PSObject.Properties.Name -contains 'packageEntries') { @($catalogLeaf.packageEntries) } else { @() }
    $dependencyGroups = if ($catalogLeaf.PSObject.Properties.Name -contains 'dependencyGroups') { @($catalogLeaf.dependencyGroups) } else { @() }

    $detectionEntries = @(
        $packageEntries |
            Where-Object { $_.name -in @('Spectre.Console.dll', 'Spectre.Console.Cli.dll') } |
            Select-Object -ExpandProperty fullName
    )

    $dependencyIds = @(
        $dependencyGroups |
            ForEach-Object { @(Get-OptionalPropertyValue -InputObject $_ -Name 'dependencies') } |
            ForEach-Object {
                $dependencyId = [string](Get-OptionalPropertyValue -InputObject $_ -Name 'id')
                if (-not [string]::IsNullOrWhiteSpace($dependencyId) -and $dependencyId -like 'Spectre.Console*') {
                    $dependencyId
                }
            } |
            Select-Object -Unique
    )

    $result.detection.hasSpectreConsole = @($detectionEntries | Where-Object { $_ -like '*Spectre.Console.dll' }).Count -gt 0
    $result.detection.hasSpectreConsoleCli = @($detectionEntries | Where-Object { $_ -like '*Spectre.Console.Cli.dll' }).Count -gt 0 -or @($dependencyIds | Where-Object { $_ -eq 'Spectre.Console.Cli' }).Count -gt 0
    $result.detection.matchedPackageEntries = $detectionEntries
    $result.detection.matchedDependencyIds = $dependencyIds

    if (-not $result.detection.hasSpectreConsoleCli) {
        $result.disposition = 'terminal-negative'
        $result.retryEligible = $false
        $result.phase = 'prefilter'
        $result.classification = 'spectre-cli-missing'
    }
    else {
        $packageDir = Join-Path $TempRoot 'package'
        $packageFile = Join-Path $TempRoot "$lowerId.$lowerVersion.nupkg"
        $packageZip = Join-Path $TempRoot "$lowerId.$lowerVersion.zip"
        Invoke-WebRequest -Uri $result.packageContentUrl -OutFile $packageFile
        Copy-Item $packageFile $packageZip -Force
        Expand-Archive -Path $packageZip -DestinationPath $packageDir -Force

        $toolSettingsFile = Get-ChildItem -Path $packageDir -Filter 'DotnetToolSettings.xml' -Recurse |
            Select-Object -First 1 -ExpandProperty FullName
        if (-not $toolSettingsFile) {
            throw "DotnetToolSettings.xml was not found in package '$PackageId' version '$Version'."
        }

        $result.toolSettingsPath = ([System.IO.Path]::GetRelativePath($packageDir, $toolSettingsFile)) -replace '\\', '/'
        $toolDescriptor = Get-DotnetToolCommandDescriptor -ToolSettingsFile $toolSettingsFile
        $result.command = [string]$toolDescriptor.command
        $result.entryPoint = [string]$toolDescriptor.entryPoint
        $result.runner = [string]$toolDescriptor.runner

        if ([string]::IsNullOrWhiteSpace($result.command)) {
            throw "DotnetToolSettings.xml did not contain a usable command name for package '$PackageId' version '$Version'."
        }

        $installDirectory = Join-Path $TempRoot 'tool'
        $installResult = Invoke-ProcessCapture -FilePath 'dotnet' -ArgumentList @('tool', 'install', $PackageId, '--version', $Version, '--tool-path', $installDirectory) -WorkingDirectory $TempRoot -TimeoutSeconds $InstallTimeoutSeconds
        $result.steps.install = Get-StepMetadata -Result $installResult -IncludeStdout
        $result.timings.installMs = $installResult.durationMs

        if ($installResult.timedOut -or $installResult.exitCode -ne 0) {
            $result.phase = 'install'
            $result.classification = if ($installResult.timedOut) { 'install-timeout' } else { 'install-failed' }
            $result.failureMessage = Get-PreferredMessage -Stdout $installResult.stdout -Stderr $installResult.stderr
        }
        else {
            $commandPath = foreach ($candidate in @(
                (Join-Path $installDirectory $result.command),
                (Join-Path $installDirectory ($result.command + '.exe'))
            )) { if (Test-Path $candidate) { $candidate; break } }

            if (-not $commandPath) {
                $result.phase = 'install'
                $result.classification = 'installed-command-missing'
                $result.failureMessage = "Installed tool command '$($result.command)' was not found."
            }
            else {
                $openCliOutcome = Invoke-IntrospectionCommand -CommandPath $commandPath -ArgumentList @('cli', 'opencli') -WorkingDirectory $TempRoot -ExpectedFormat 'json' -TimeoutSeconds $CommandTimeoutSeconds
                $xmlDocOutcome = Invoke-IntrospectionCommand -CommandPath $commandPath -ArgumentList @('cli', 'xmldoc') -WorkingDirectory $TempRoot -ExpectedFormat 'xml' -TimeoutSeconds $CommandTimeoutSeconds

                $result.timings.opencliMs = $openCliOutcome.processResult.durationMs
                $result.timings.xmldocMs = $xmlDocOutcome.processResult.durationMs

                if ($null -ne $openCliOutcome.artifactObject) {
                    Write-JsonFile -Path (Join-Path $OutputRoot 'opencli.json') -InputObject $openCliOutcome.artifactObject
                    $result.artifacts.opencliArtifact = 'opencli.json'
                }

                if ($null -ne $xmlDocOutcome.artifactText) {
                    Write-TextFile -Path (Join-Path $OutputRoot 'xmldoc.xml') -Content $xmlDocOutcome.artifactText
                    $result.artifacts.xmldocArtifact = 'xmldoc.xml'
                }

                $result.introspection.opencli = [ordered]@{
                    status = $openCliOutcome.status
                    classification = $openCliOutcome.classification
                    message = $openCliOutcome.message
                }
                $result.introspection.xmldoc = [ordered]@{
                    status = $xmlDocOutcome.status
                    classification = $xmlDocOutcome.classification
                    message = $xmlDocOutcome.message
                }

                $result.steps.opencli = Get-OutcomeMetadata -Outcome $openCliOutcome -ArtifactPath $result.artifacts.opencliArtifact
                $result.steps.xmldoc = Get-OutcomeMetadata -Outcome $xmlDocOutcome -ArtifactPath $result.artifacts.xmldocArtifact

                $successfulOutcomes = @($openCliOutcome, $xmlDocOutcome | Where-Object { $_.status -eq 'ok' })
                $retryableOutcomes = @($openCliOutcome, $xmlDocOutcome | Where-Object { $_.status -ne 'ok' -and $_.dispositionHint -eq 'retryable-failure' })
                $deterministicOutcomes = @($openCliOutcome, $xmlDocOutcome | Where-Object { $_.status -ne 'ok' -and $_.dispositionHint -eq 'terminal-failure' })

                if ($successfulOutcomes.Count -eq 2) {
                    $result.disposition = 'success'
                    $result.retryEligible = $false
                    $result.phase = 'complete'
                    $result.classification = 'spectre-cli-confirmed'
                }
                elseif ($successfulOutcomes.Count -eq 1 -and $retryableOutcomes.Count -eq 0) {
                    $successName = $successfulOutcomes[0].commandName
                    $result.disposition = 'success'
                    $result.retryEligible = $false
                    $result.phase = 'complete'
                    $result.classification = if ($successName -eq 'opencli') { 'spectre-cli-opencli-only' } else { 'spectre-cli-xmldoc-only' }
                }
                elseif ($retryableOutcomes.Count -gt 0) {
                    $primaryFailure = $retryableOutcomes[0]
                    $result.disposition = 'retryable-failure'
                    $result.retryEligible = $true
                    $result.phase = $primaryFailure.commandName
                    $result.classification = $primaryFailure.classification
                    $result.failureMessage = $primaryFailure.message
                }
                elseif ($deterministicOutcomes.Count -gt 0) {
                    $primaryFailure = $deterministicOutcomes[0]
                    $result.disposition = 'terminal-failure'
                    $result.retryEligible = $false
                    $result.phase = $primaryFailure.commandName
                    $result.classification = $primaryFailure.classification
                    $result.failureMessage = $primaryFailure.message
                }
                else {
                    $result.disposition = 'retryable-failure'
                    $result.retryEligible = $true
                    $result.phase = 'introspection'
                    $result.classification = 'introspection-unresolved'
                    $result.failureMessage = 'The tool did not yield a usable introspection result.'
                }
            }
        }
    }
}
catch {
    $result.disposition = 'retryable-failure'
    $result.retryEligible = $true
    if ($result.phase -eq 'bootstrap') {
        $result.phase = 'bootstrap'
        $result.classification = 'unexpected-exception'
    }
    $result.failureMessage = $_.Exception.Message
}
finally {
    $Timer.Stop()
    $result.timings.totalMs = [int][Math]::Round($Timer.Elapsed.TotalMilliseconds)
    if ($result.disposition -in @('retryable-failure', 'terminal-failure')) {
        $result.failureSignature = Get-FailureSignature -Phase $result.phase -Classification $result.classification -Message $result.failureMessage
    }

    Write-JsonFile -Path $ResultPath -InputObject $result
    Remove-Item $TempRoot -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "Analyzed $PackageId $Version"
Write-Host "Disposition: $($result.disposition)"
Write-Host "Result artifact: result.json"

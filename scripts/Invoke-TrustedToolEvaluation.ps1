[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageId,

    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$Source = 'workflow-dispatch',

    [switch]$Trusted
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$IndexRoot = Join-Path $RepositoryRoot 'index'
$PackagesRoot = Join-Path $IndexRoot 'packages'
$TempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("inspectra-$($PackageId.ToLowerInvariant())-$($Version.ToLowerInvariant())")
$GeneratedAt = [DateTimeOffset]::UtcNow
$EvaluationTimer = [System.Diagnostics.Stopwatch]::StartNew()

. (Join-Path $PSScriptRoot 'OpenCliSynthesis.ps1')

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
        error = if ($lastError) { $lastError } else { 'XML parsing failed.' }
    }
}

function Invoke-ProcessCapture {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string[]]$ArgumentList,

        [Parameter(Mandatory = $true)]
        [string]$WorkingDirectory
    )

    $stdoutPath = Join-Path $TempRoot ([System.Guid]::NewGuid().ToString('n') + '.stdout.txt')
    $stderrPath = Join-Path $TempRoot ([System.Guid]::NewGuid().ToString('n') + '.stderr.txt')
    $timer = [System.Diagnostics.Stopwatch]::StartNew()

    $process = Start-Process `
        -FilePath $FilePath `
        -ArgumentList $ArgumentList `
        -WorkingDirectory $WorkingDirectory `
        -Wait `
        -PassThru `
        -NoNewWindow `
        -RedirectStandardOutput $stdoutPath `
        -RedirectStandardError $stderrPath

    $timer.Stop()

    [ordered]@{
        status = if ($process.ExitCode -eq 0) { 'ok' } else { 'failed' }
        exitCode = $process.ExitCode
        durationMs = [int][Math]::Round($timer.Elapsed.TotalMilliseconds)
        stdout = [System.IO.File]::ReadAllText($stdoutPath)
        stderr = [System.IO.File]::ReadAllText($stderrPath)
    }
}

function Resolve-CommandPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ToolDirectory,

        [Parameter(Mandatory = $true)]
        [string]$CommandName
    )

    foreach ($candidate in @(
        (Join-Path $ToolDirectory $CommandName),
        (Join-Path $ToolDirectory ($CommandName + '.exe'))
    )) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    throw "Could not locate installed command '$CommandName' under '$ToolDirectory'."
}

function Get-RelativeRepositoryPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $relative = [System.IO.Path]::GetRelativePath($RepositoryRoot, $Path)
    return $relative -replace '\\', '/'
}

function Convert-ToIsoTimestamp {
    param(
        [Parameter(Mandatory = $false)]
        [AllowNull()]
        [object]$Value
    )

    if ($null -eq $Value -or [string]::IsNullOrWhiteSpace([string]$Value)) {
        return $null
    }

    return ([DateTimeOffset]$Value).ToUniversalTime().ToString('o')
}

function Get-TextLength {
    param(
        [Parameter(Mandatory = $false)]
        [AllowNull()]
        [string]$Value
    )

    if ($null -eq $Value) {
        return 0
    }

    return [System.Text.Encoding]::UTF8.GetByteCount($Value)
}

function Get-StepMetadata {
    param(
        [Parameter(Mandatory = $false)]
        [AllowNull()]
        [object]$Result,

        [Parameter(Mandatory = $false)]
        [AllowNull()]
        [string]$ArtifactPath,

        [switch]$IncludeStdout,

        [switch]$IncludeStderr
    )

    if ($null -eq $Result) {
        return $null
    }

    $metadata = [ordered]@{
        status = $Result.status
        exitCode = $Result.exitCode
        durationMs = $Result.durationMs
        stdoutLength = Get-TextLength $Result.stdout
        stderrLength = Get-TextLength $Result.stderr
    }

    if ($ArtifactPath) {
        $metadata.path = $ArtifactPath
    }

    if ($IncludeStdout) {
        $metadata.stdout = $Result.stdout
    }

    if ($IncludeStderr -or -not [string]::IsNullOrEmpty($Result.stderr)) {
        $metadata.stderr = $Result.stderr
    }

    return $metadata
}

function Sync-LatestDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$VersionDirectory,

        [Parameter(Mandatory = $true)]
        [string]$LatestDirectory
    )

    New-Item -ItemType Directory -Path $LatestDirectory -Force | Out-Null

    foreach ($artifactName in @('metadata.json', 'opencli.json', 'xmldoc.xml')) {
        $sourcePath = Join-Path $VersionDirectory $artifactName
        $targetPath = Join-Path $LatestDirectory $artifactName

        if (Test-Path $sourcePath) {
            Copy-Item -Path $sourcePath -Destination $targetPath -Force
        }
        else {
            Remove-Item -Path $targetPath -Force -ErrorAction SilentlyContinue
        }
    }
}

function Get-PackageSummary {
    param(
        [Parameter(Mandatory = $true)]
        [array]$Records
    )

    $ordered = $Records |
        Sort-Object `
            @{ Expression = { if ($_.publishedAt) { [DateTimeOffset]$_.publishedAt } else { [DateTimeOffset]::MinValue } }; Descending = $true }, `
            @{ Expression = { [DateTimeOffset]$_.evaluatedAt }; Descending = $true }

    $latest = $ordered[0]
    $lowerId = $latest.packageId.ToLowerInvariant()

    [ordered]@{
        schemaVersion = 1
        packageId = $latest.packageId
        trusted = [bool]$latest.trusted
        latestVersion = $latest.version
        latestStatus = $latest.status
        latestPaths = [ordered]@{
            metadataPath = "index/packages/$lowerId/latest/metadata.json"
            opencliPath = if ($latest.artifacts.opencliPath) { "index/packages/$lowerId/latest/opencli.json" } else { $null }
            xmldocPath = if ($latest.artifacts.xmldocPath) { "index/packages/$lowerId/latest/xmldoc.xml" } else { $null }
        }
        versions = @(
            $ordered | ForEach-Object {
                [ordered]@{
                    version = $_.version
                    publishedAt = Convert-ToIsoTimestamp $_.publishedAt
                    evaluatedAt = Convert-ToIsoTimestamp $_.evaluatedAt
                    status = $_.status
                    command = $_.command
                    timings = $_.timings
                    paths = $_.artifacts
                }
            }
        )
    }
}

Remove-Item $TempRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $TempRoot -Force | Out-Null

try {
    $lowerId = $PackageId.ToLowerInvariant()
    $lowerVersion = $Version.ToLowerInvariant()
    $env:HOME = Join-Path $TempRoot 'home'
    $env:DOTNET_CLI_HOME = Join-Path $TempRoot 'dotnet-home'
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    $env:DOTNET_NOLOGO = '1'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
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

    $registrationLeafVersion = Get-NuGetRegistrationLeafVersion -Version $Version
    $leafUrl = "https://api.nuget.org/v3/registration5-gz-semver2/$lowerId/$registrationLeafVersion.json"
    $leaf = Invoke-RestMethod -Uri $leafUrl
    $catalogEntryUrl = [string]$leaf.catalogEntry
    $catalogLeaf = Invoke-RestMethod -Uri $catalogEntryUrl
    $packageContentUrl = [string]$leaf.packageContent
    $packageUrl = "https://www.nuget.org/packages/$PackageId/$Version"

    $packageDir = Join-Path $TempRoot 'package'
    $packageFile = Join-Path $TempRoot "$lowerId.$lowerVersion.nupkg"
    $packageZip = Join-Path $TempRoot "$lowerId.$lowerVersion.zip"
    Invoke-WebRequest -Uri $packageContentUrl -OutFile $packageFile
    Copy-Item $packageFile $packageZip -Force
    Expand-Archive -Path $packageZip -DestinationPath $packageDir -Force

    $toolSettingsFile = Get-ChildItem -Path $packageDir -Filter 'DotnetToolSettings.xml' -Recurse |
        Select-Object -First 1 -ExpandProperty FullName
    if (-not $toolSettingsFile) {
        throw "DotnetToolSettings.xml was not found in package '$PackageId' version '$Version'."
    }

    $packageRelativeToolSettingsPath = ([System.IO.Path]::GetRelativePath($packageDir, $toolSettingsFile)) -replace '\\', '/'

    $toolDescriptor = Get-DotnetToolCommandDescriptor -ToolSettingsFile $toolSettingsFile
    $commandName = [string]$toolDescriptor.command
    $entryPoint = [string]$toolDescriptor.entryPoint
    $runner = [string]$toolDescriptor.runner

    if ([string]::IsNullOrWhiteSpace($commandName)) {
        throw "DotnetToolSettings.xml did not contain a usable command name for package '$PackageId' version '$Version'."
    }

    $installDirectory = Join-Path $TempRoot 'tool'
    $installResult = Invoke-ProcessCapture `
        -FilePath 'dotnet' `
        -ArgumentList @('tool', 'install', $PackageId, '--version', $Version, '--tool-path', $installDirectory) `
        -WorkingDirectory $TempRoot

    $commandPath = $null
    $openCliResult = $null
    $xmlDocResult = $null
    $openCliDocument = $null
    $validatedXmlDoc = $null
    $openCliSource = $null

    if ($installResult.exitCode -eq 0) {
        $commandPath = Resolve-CommandPath -ToolDirectory $installDirectory -CommandName $commandName
        $openCliResult = Invoke-ProcessCapture -FilePath $commandPath -ArgumentList @('cli', 'opencli') -WorkingDirectory $TempRoot
        $xmlDocResult = Invoke-ProcessCapture -FilePath $commandPath -ArgumentList @('cli', 'xmldoc') -WorkingDirectory $TempRoot
    }

    $openCliParse = if ($openCliResult) { Try-ParseJsonPayload -Text $openCliResult.stdout } else { $null }
    $xmlDocParse = if ($xmlDocResult) { Try-ParseXmlPayload -Text $xmlDocResult.stdout } else { $null }

    if ($openCliParse -and $openCliParse.success) {
        $openCliDocument = $openCliParse.document
        $openCliSource = 'tool-output'
    }

    if ($xmlDocParse -and $xmlDocParse.success) {
        $validatedXmlDoc = $xmlDocParse.document
    }

    if ($null -eq $openCliDocument -and $null -ne $validatedXmlDoc) {
        $openCliDocument = Convert-XmldocToOpenCliDocument `
            -XmlDocument $validatedXmlDoc `
            -Title $commandName
        $openCliSource = 'synthesized-from-xmldoc'
    }

    $status = if (
        $installResult.exitCode -eq 0 -and
        $xmlDocResult -and $xmlDocResult.exitCode -eq 0 -and
        $null -ne $openCliDocument -and
        $null -ne $validatedXmlDoc -and
        $openCliSource -eq 'tool-output'
    ) {
        'ok'
    }
    elseif (
        $installResult.exitCode -eq 0 -and
        ($null -ne $openCliDocument -or $null -ne $validatedXmlDoc)
    ) {
        'partial'
    }
    else {
        'failed'
    }

    $detectionEntries = @(
        @($catalogLeaf.packageEntries) |
            Where-Object { $_.name -in @('Spectre.Console.dll', 'Spectre.Console.Cli.dll') } |
            Select-Object -ExpandProperty fullName
    )

    $packageIndexRoot = Join-Path $PackagesRoot $lowerId
    $versionRoot = Join-Path $packageIndexRoot $lowerVersion
    $metadataPath = Join-Path $versionRoot 'metadata.json'
    $openCliPath = if ($null -ne $openCliDocument) { Join-Path $versionRoot 'opencli.json' } else { $null }
    $xmlDocPath = if ($null -ne $validatedXmlDoc) { Join-Path $versionRoot 'xmldoc.xml' } else { $null }
    $openCliArtifactPath = if ($openCliPath) { Get-RelativeRepositoryPath -Path $openCliPath } else { $null }
    $xmlDocArtifactPath = if ($xmlDocPath) { Get-RelativeRepositoryPath -Path $xmlDocPath } else { $null }

    if ($openCliPath) {
        Write-JsonFile -Path $openCliPath -InputObject $openCliDocument
    }

    if ($xmlDocPath) {
        Write-TextFile -Path $xmlDocPath -Content $xmlDocParse.artifactText
    }

    $EvaluationTimer.Stop()

    $metadata = [ordered]@{
        schemaVersion = 1
        packageId = $PackageId
        version = $Version
        trusted = [bool]$Trusted
        source = $Source
        status = $status
        evaluatedAt = $GeneratedAt.ToString('o')
        publishedAt = ([DateTimeOffset]$catalogLeaf.published).ToUniversalTime().ToString('o')
        packageUrl = $packageUrl
        packageContentUrl = $packageContentUrl
        registrationLeafUrl = $leafUrl
        catalogEntryUrl = $catalogEntryUrl
        command = $commandName
        entryPoint = $entryPoint
        runner = $runner
        toolSettingsPath = $packageRelativeToolSettingsPath
        detection = [ordered]@{
            hasSpectreConsole = @($detectionEntries | Where-Object { $_ -like '*Spectre.Console.dll' }).Count -gt 0
            hasSpectreConsoleCli = @($detectionEntries | Where-Object { $_ -like '*Spectre.Console.Cli.dll' }).Count -gt 0
            matchedPackageEntries = $detectionEntries
        }
        timings = [ordered]@{
            totalMs = [int][Math]::Round($EvaluationTimer.Elapsed.TotalMilliseconds)
            installMs = if ($installResult) { $installResult.durationMs } else { $null }
            opencliMs = if ($openCliResult) { $openCliResult.durationMs } else { $null }
            xmldocMs = if ($xmlDocResult) { $xmlDocResult.durationMs } else { $null }
        }
        steps = [ordered]@{
            install = Get-StepMetadata -Result $installResult -IncludeStdout -IncludeStderr
            opencli = Get-StepMetadata `
                -Result $openCliResult `
                -ArtifactPath $openCliArtifactPath `
                -IncludeStderr
            xmldoc = Get-StepMetadata `
                -Result $xmlDocResult `
                -ArtifactPath $xmlDocArtifactPath `
                -IncludeStderr
        }
        artifacts = [ordered]@{
            metadataPath = Get-RelativeRepositoryPath -Path $metadataPath
            opencliPath = $openCliArtifactPath
            opencliSource = if ($openCliArtifactPath) { $openCliSource } else { $null }
            xmldocPath = $xmlDocArtifactPath
        }
    }

    Write-JsonFile -Path $metadataPath -InputObject $metadata

    $versionRecords = @(
        Get-ChildItem -Path $PackagesRoot -Filter 'metadata.json' -Recurse |
            Where-Object { (Split-Path -Leaf (Split-Path -Parent $_.FullName)) -ne 'latest' } |
            ForEach-Object {
                $data = Get-Content $_.FullName -Raw | ConvertFrom-Json
                $data | Add-Member -NotePropertyName metadataFileFullPath -NotePropertyValue $_.FullName -Force
                $data | Add-Member -NotePropertyName versionDirectoryFullPath -NotePropertyValue (Split-Path -Parent $_.FullName) -Force
                $data
            }
    )

    $packageSummaries = @(
        $versionRecords |
            Group-Object packageId |
            ForEach-Object {
                $summary = Get-PackageSummary -Records $_.Group
                $summaryPath = Join-Path $PackagesRoot ("{0}/index.json" -f $_.Name.ToLowerInvariant())
                $latestRecord = ($_.Group |
                    Sort-Object `
                        @{ Expression = { if ($_.publishedAt) { [DateTimeOffset]$_.publishedAt } else { [DateTimeOffset]::MinValue } }; Descending = $true }, `
                        @{ Expression = { [DateTimeOffset]$_.evaluatedAt }; Descending = $true })[0]
                $latestDirectory = Join-Path $PackagesRoot ("{0}/latest" -f $_.Name.ToLowerInvariant())

                Sync-LatestDirectory -VersionDirectory $latestRecord.versionDirectoryFullPath -LatestDirectory $latestDirectory
                Write-JsonFile -Path $summaryPath -InputObject $summary
                $summary
            } |
            Sort-Object packageId
    )

    $allIndex = [ordered]@{
        schemaVersion = 1
        generatedAt = [DateTimeOffset]::UtcNow.ToString('o')
        packageCount = $packageSummaries.Count
        packages = $packageSummaries
    }

    Write-JsonFile -Path (Join-Path $IndexRoot 'all.json') -InputObject $allIndex

    Write-Host "Evaluated $PackageId $Version"
    Write-Host "Version metadata: $(Get-RelativeRepositoryPath -Path $metadataPath)"
    if ($openCliPath) {
        Write-Host "OpenCLI JSON: $(Get-RelativeRepositoryPath -Path $openCliPath)"
    }
    if ($xmlDocPath) {
        Write-Host "XML doc: $(Get-RelativeRepositoryPath -Path $xmlDocPath)"
    }
    Write-Host "Latest alias: index/packages/$lowerId/latest/"
    Write-Host "Package index: index/packages/$lowerId/index.json"
    Write-Host "Aggregate index: index/all.json"
}
finally {
    Remove-Item $TempRoot -Recurse -Force -ErrorAction SilentlyContinue
}

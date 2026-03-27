using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;

internal sealed class AnalysisCommandService
{
    private static readonly Regex AnsiCsiRegex = new(@"\x1B\[[0-?]*[ -/]*[@-~]", RegexOptions.Compiled);
    private static readonly Regex AnsiEscapeRegex = new(@"\x1B[@-_]", RegexOptions.Compiled);

    public async Task<int> RunUntrustedAsync(
        string packageId,
        string version,
        string outputRoot,
        string batchId,
        int attempt,
        string source,
        int installTimeoutSeconds,
        int commandTimeoutSeconds,
        bool json,
        CancellationToken cancellationToken)
    {
        var generatedAt = DateTimeOffset.UtcNow;
        var tempRoot = Path.Combine(Path.GetTempPath(), $"inspectra-untrusted-{packageId.ToLowerInvariant()}-{version.ToLowerInvariant()}-{Guid.NewGuid():N}");
        var outputDirectory = Path.GetFullPath(outputRoot);
        var resultPath = Path.Combine(outputDirectory, "result.json");
        var stopwatch = Stopwatch.StartNew();
        Directory.CreateDirectory(outputDirectory);

        var result = CreateInitialResult(packageId, version, batchId, attempt, source, generatedAt);
        Directory.CreateDirectory(tempRoot);

        try
        {
            var environment = CreateSandboxEnvironment(tempRoot);
            foreach (var directory in environment.Directories)
            {
                Directory.CreateDirectory(directory);
            }

            using var scope = ToolRuntime.CreateNuGetApiClientScope();
            var lowerId = packageId.ToLowerInvariant();
            var normalizedVersion = NormalizeVersionForRegistrationLeaf(version);
            var leafUrl = $"https://api.nuget.org/v3/registration5-gz-semver2/{lowerId}/{normalizedVersion}.json";
            var registrationLeaf = await scope.Client.GetRegistrationLeafAsync(leafUrl, cancellationToken);
            var catalogLeaf = await scope.Client.GetCatalogLeafAsync(registrationLeaf.CatalogEntryUrl, cancellationToken);

            result["registrationLeafUrl"] = leafUrl;
            result["catalogEntryUrl"] = registrationLeaf.CatalogEntryUrl;
            result["packageContentUrl"] = registrationLeaf.PackageContent;
            result["publishedAt"] = registrationLeaf.Published?.ToUniversalTime().ToString("O");

            var detection = BuildDetection(catalogLeaf);
            result["detection"] = detection.ToJsonObject();

            if (!detection.HasSpectreConsoleCli)
            {
                result["disposition"] = "terminal-negative";
                result["retryEligible"] = false;
                result["phase"] = "prefilter";
                result["classification"] = "spectre-cli-missing";
            }
            else
            {
                var packageInspection = await new PackageArchiveInspector(scope.Client).InspectAsync(registrationLeaf.PackageContent, cancellationToken);
                MergePackageInspection(result["detection"]!.AsObject(), packageInspection);

                var commandName = packageInspection.ToolCommandNames.FirstOrDefault();
                result["command"] = commandName;
                result["entryPoint"] = packageInspection.ToolEntryPointPaths.FirstOrDefault();
                result["runner"] = null;
                result["toolSettingsPath"] = packageInspection.ToolSettingsPaths.FirstOrDefault();

                if (string.IsNullOrWhiteSpace(commandName))
                {
                    result["phase"] = "bootstrap";
                    result["classification"] = "tool-command-missing";
                    result["failureMessage"] = $"No tool command could be resolved for package '{packageId}' version '{version}'.";
                }
                else
                {
                    await AnalyzeInstalledToolAsync(
                        result,
                        packageId,
                        version,
                        outputDirectory,
                        tempRoot,
                        commandName,
                        environment.Values,
                        installTimeoutSeconds,
                        commandTimeoutSeconds,
                        cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            result["disposition"] = "retryable-failure";
            result["retryEligible"] = true;

            if (string.Equals(result["phase"]?.GetValue<string>(), "bootstrap", StringComparison.Ordinal))
            {
                result["classification"] = "unexpected-exception";
            }

            result["failureMessage"] = ex.Message;
        }
        finally
        {
            stopwatch.Stop();
            result["timings"]!.AsObject()["totalMs"] = (int)Math.Round(stopwatch.Elapsed.TotalMilliseconds);
            var disposition = result["disposition"]?.GetValue<string>();
            if (disposition is "retryable-failure" or "terminal-failure")
            {
                result["failureSignature"] = GetFailureSignature(
                    result["phase"]?.GetValue<string>() ?? "unknown",
                    result["classification"]?.GetValue<string>() ?? "unknown",
                    result["failureMessage"]?.GetValue<string>());
            }

            RepositoryPathResolver.WriteJsonFile(resultPath, result);
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        var output = ToolRuntime.CreateOutput();
        return await output.WriteSuccessAsync(
            new
            {
                packageId,
                version,
                disposition = result["disposition"]?.GetValue<string>(),
                resultPath,
            },
            [
                new SummaryRow("Package", $"{packageId} {version}"),
                new SummaryRow("Disposition", result["disposition"]?.GetValue<string>() ?? string.Empty),
                new SummaryRow("Result artifact", resultPath),
            ],
            json,
            cancellationToken);
    }

    private static JsonObject CreateInitialResult(string packageId, string version, string batchId, int attempt, string source, DateTimeOffset analyzedAt)
        => new()
        {
            ["schemaVersion"] = 1,
            ["packageId"] = packageId,
            ["version"] = version,
            ["batchId"] = batchId,
            ["attempt"] = attempt,
            ["trusted"] = false,
            ["source"] = source,
            ["analyzedAt"] = analyzedAt.ToString("O"),
            ["disposition"] = "retryable-failure",
            ["retryEligible"] = true,
            ["phase"] = "bootstrap",
            ["classification"] = "uninitialized",
            ["failureMessage"] = null,
            ["failureSignature"] = null,
            ["packageUrl"] = $"https://www.nuget.org/packages/{packageId}/{version}",
            ["totalDownloads"] = null,
            ["packageContentUrl"] = null,
            ["registrationLeafUrl"] = null,
            ["catalogEntryUrl"] = null,
            ["publishedAt"] = null,
            ["command"] = null,
            ["entryPoint"] = null,
            ["runner"] = null,
            ["toolSettingsPath"] = null,
            ["detection"] = new JsonObject
            {
                ["hasSpectreConsole"] = false,
                ["hasSpectreConsoleCli"] = false,
                ["matchedPackageEntries"] = new JsonArray(),
                ["matchedDependencyIds"] = new JsonArray(),
            },
            ["introspection"] = new JsonObject
            {
                ["opencli"] = null,
                ["xmldoc"] = null,
            },
            ["timings"] = new JsonObject
            {
                ["totalMs"] = null,
                ["installMs"] = null,
                ["opencliMs"] = null,
                ["xmldocMs"] = null,
            },
            ["steps"] = new JsonObject
            {
                ["install"] = null,
                ["opencli"] = null,
                ["xmldoc"] = null,
            },
            ["artifacts"] = new JsonObject
            {
                ["opencliArtifact"] = null,
                ["xmldocArtifact"] = null,
            },
        };

    private static async Task AnalyzeInstalledToolAsync(
        JsonObject result,
        string packageId,
        string version,
        string outputDirectory,
        string tempRoot,
        string commandName,
        IReadOnlyDictionary<string, string> environment,
        int installTimeoutSeconds,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken)
    {
        var installDirectory = Path.Combine(tempRoot, "tool");
        var installResult = await InvokeProcessCaptureAsync(
            "dotnet",
            ["tool", "install", packageId, "--version", version, "--tool-path", installDirectory],
            tempRoot,
            environment,
            installTimeoutSeconds,
            cancellationToken);
        result["steps"]!.AsObject()["install"] = installResult.ToStepMetadata(includeStdout: true);
        result["timings"]!.AsObject()["installMs"] = installResult.DurationMs;

        if (installResult.TimedOut || installResult.ExitCode != 0)
        {
            result["phase"] = "install";
            result["classification"] = installResult.TimedOut ? "install-timeout" : "install-failed";
            result["failureMessage"] = GetPreferredMessage(installResult.Stdout, installResult.Stderr);
            return;
        }

        var commandPath = ResolveInstalledCommandPath(installDirectory, commandName);
        if (commandPath is null)
        {
            result["phase"] = "install";
            result["classification"] = "installed-command-missing";
            result["failureMessage"] = $"Installed tool command '{commandName}' was not found.";
            return;
        }

        var openCliOutcome = await InvokeIntrospectionCommandAsync(commandPath, ["cli", "opencli"], "json", tempRoot, environment, commandTimeoutSeconds, cancellationToken);
        var xmlDocOutcome = await InvokeIntrospectionCommandAsync(commandPath, ["cli", "xmldoc"], "xml", tempRoot, environment, commandTimeoutSeconds, cancellationToken);
        ApplyIntrospectionOutputs(result, outputDirectory, openCliOutcome, xmlDocOutcome);
        ApplyOutcomeClassification(result, openCliOutcome, xmlDocOutcome);
    }

    private static SandboxEnvironment CreateSandboxEnvironment(string tempRoot)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["HOME"] = Path.Combine(tempRoot, "home"),
            ["DOTNET_CLI_HOME"] = Path.Combine(tempRoot, "dotnet-home"),
            ["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1",
            ["DOTNET_NOLOGO"] = "1",
            ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
            ["DOTNET_SYSTEM_CONSOLE_ALLOW_ANSI_COLOR_REDIRECTION"] = "0",
            ["NUGET_PACKAGES"] = Path.Combine(tempRoot, "nuget-packages"),
            ["NUGET_HTTP_CACHE_PATH"] = Path.Combine(tempRoot, "nuget-http-cache"),
            ["XDG_CONFIG_HOME"] = Path.Combine(tempRoot, "xdg-config"),
            ["XDG_CACHE_HOME"] = Path.Combine(tempRoot, "xdg-cache"),
            ["XDG_DATA_HOME"] = Path.Combine(tempRoot, "xdg-data"),
            ["XDG_RUNTIME_DIR"] = Path.Combine(tempRoot, "xdg-runtime"),
            ["TMPDIR"] = Path.Combine(tempRoot, "tmp"),
            ["CI"] = "true",
            ["NO_COLOR"] = "1",
            ["FORCE_COLOR"] = "0",
            ["TERM"] = "dumb",
            ["GCM_CREDENTIAL_STORE"] = "none",
            ["GCM_INTERACTIVE"] = "never",
            ["GIT_TERMINAL_PROMPT"] = "0",
        };

        values["TMP"] = values["TMPDIR"];
        values["TEMP"] = values["TMPDIR"];
        values["USERPROFILE"] = values["HOME"];
        values["APPDATA"] = values["XDG_CONFIG_HOME"];
        values["LOCALAPPDATA"] = values["XDG_DATA_HOME"];

        return new SandboxEnvironment(
            values,
            [
                values["HOME"],
                values["DOTNET_CLI_HOME"],
                values["NUGET_PACKAGES"],
                values["NUGET_HTTP_CACHE_PATH"],
                values["XDG_CONFIG_HOME"],
                values["XDG_CACHE_HOME"],
                values["XDG_DATA_HOME"],
                values["XDG_RUNTIME_DIR"],
                values["TMPDIR"],
            ]);
    }

    private static DetectionInfo BuildDetection(CatalogLeaf catalogLeaf)
    {
        var matchedPackageEntries = catalogLeaf.PackageEntries?
            .Where(entry => entry.Name is "Spectre.Console.dll" or "Spectre.Console.Cli.dll")
            .Select(entry => entry.FullName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

        var matchedDependencyIds = catalogLeaf.DependencyGroups?
            .SelectMany(group => group.Dependencies ?? [])
            .Select(dependency => dependency.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id) && id.StartsWith("Spectre.Console", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

        return new DetectionInfo(
            matchedPackageEntries.Any(entry => entry.EndsWith("Spectre.Console.dll", StringComparison.OrdinalIgnoreCase)),
            matchedPackageEntries.Any(entry => entry.EndsWith("Spectre.Console.Cli.dll", StringComparison.OrdinalIgnoreCase)) ||
            matchedDependencyIds.Contains("Spectre.Console.Cli", StringComparer.OrdinalIgnoreCase),
            matchedPackageEntries,
            matchedDependencyIds);
    }

    private static void MergePackageInspection(JsonObject detection, SpectrePackageInspection inspection)
    {
        detection["depsFilePaths"] = ToJsonArray(inspection.DepsFilePaths);
        detection["spectreConsoleDependencyVersions"] = ToJsonArray(inspection.SpectreConsoleDependencyVersions);
        detection["spectreConsoleCliDependencyVersions"] = ToJsonArray(inspection.SpectreConsoleCliDependencyVersions);
        detection["spectreConsoleAssemblies"] = JsonSerializer.SerializeToNode(inspection.SpectreConsoleAssemblies, JsonOptions.Default);
        detection["spectreConsoleCliAssemblies"] = JsonSerializer.SerializeToNode(inspection.SpectreConsoleCliAssemblies, JsonOptions.Default);
        detection["toolSettingsPaths"] = ToJsonArray(inspection.ToolSettingsPaths);
        detection["toolCommandNames"] = ToJsonArray(inspection.ToolCommandNames);
        detection["toolEntryPointPaths"] = ToJsonArray(inspection.ToolEntryPointPaths);
        detection["toolAssembliesReferencingSpectreConsole"] = ToJsonArray(inspection.ToolAssembliesReferencingSpectreConsole);
        detection["toolAssembliesReferencingSpectreConsoleCli"] = ToJsonArray(inspection.ToolAssembliesReferencingSpectreConsoleCli);
    }

    private static async Task<ProcessResult> InvokeProcessCaptureAsync(
        string filePath,
        IReadOnlyList<string> argumentList,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = filePath,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var argument in argumentList)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var pair in environment)
        {
            startInfo.Environment[pair.Key] = pair.Value;
        }

        using var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true,
        };

        var stopwatch = Stopwatch.StartNew();
        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        var waitTask = process.WaitForExitAsync(cancellationToken);
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds), cancellationToken);

        var completedTask = await Task.WhenAny(waitTask, timeoutTask);
        var timedOut = completedTask == timeoutTask;
        if (timedOut)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
            }

            await process.WaitForExitAsync(CancellationToken.None);
        }
        else
        {
            await waitTask;
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        stopwatch.Stop();

        return new ProcessResult(
            Status: timedOut ? "timed-out" : process.ExitCode == 0 ? "ok" : "failed",
            TimedOut: timedOut,
            ExitCode: timedOut ? null : process.ExitCode,
            DurationMs: (int)Math.Round(stopwatch.Elapsed.TotalMilliseconds),
            Stdout: stdout,
            Stderr: stderr);
    }

    private static async Task<IntrospectionOutcome> InvokeIntrospectionCommandAsync(
        string commandPath,
        IReadOnlyList<string> argumentList,
        string expectedFormat,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        var processResult = await InvokeProcessCaptureAsync(
            commandPath,
            argumentList,
            workingDirectory,
            environment,
            timeoutSeconds,
            cancellationToken);
        var preferredMessage = GetPreferredMessage(processResult.Stdout, processResult.Stderr);
        var classification = ClassifyIntrospectionFailure(argumentList, preferredMessage);
        var parse = string.Equals(expectedFormat, "json", StringComparison.OrdinalIgnoreCase)
            ? TryParseJsonPayload(processResult.Stdout)
            : TryParseXmlPayload(processResult.Stdout);

        var status = "failed";
        var dispositionHint = "retryable-failure";
        var message = preferredMessage;
        JsonNode? artifactObject = null;
        string? artifactText = null;

        if (processResult.TimedOut)
        {
            status = "timed-out";
            if (classification is "requires-configuration" or "environment-missing-dependency" or "requires-interactive-authentication" or "requires-interactive-input" or "unsupported-platform")
            {
                dispositionHint = "terminal-failure";
            }
            else if (classification == "environment-missing-runtime")
            {
                dispositionHint = "retryable-failure";
            }
            else
            {
                classification = "timeout";
                dispositionHint = "retryable-failure";
            }

            message ??= "Command timed out.";
        }
        else if (parse.Success)
        {
            status = "ok";
            classification = string.Equals(expectedFormat, "json", StringComparison.OrdinalIgnoreCase)
                ? processResult.ExitCode == 0 ? "json-ready" : "json-ready-with-nonzero-exit"
                : processResult.ExitCode == 0 ? "xml-ready" : "xml-ready-with-nonzero-exit";
            dispositionHint = "success";
            artifactObject = parse.Document;
            artifactText = parse.ArtifactText;
            message = processResult.ExitCode == 0 ? null : preferredMessage;
        }
        else if (!string.IsNullOrWhiteSpace(classification))
        {
            status = classification == "unsupported-command" ? "unsupported" : "failed";
            dispositionHint = classification == "environment-missing-runtime" ? "retryable-failure" : "terminal-failure";
            message ??= parse.Error;
        }
        else if (processResult.ExitCode == 0)
        {
            status = "invalid-output";
            classification = string.Equals(expectedFormat, "json", StringComparison.OrdinalIgnoreCase) ? "invalid-json" : "invalid-xml";
            dispositionHint = "terminal-failure";
            message = parse.Error ?? "Command exited successfully but did not emit valid output.";
        }
        else
        {
            status = "failed";
            classification = "command-failed";
            dispositionHint = "retryable-failure";
            message ??= parse.Error;
        }

        return new IntrospectionOutcome(
            CommandName: argumentList[^1],
            ExpectedFormat: expectedFormat,
            ProcessResult: processResult,
            Status: status,
            Classification: classification ?? "command-failed",
            DispositionHint: dispositionHint,
            Message: message,
            ArtifactObject: artifactObject,
            ArtifactText: artifactText);
    }

    private static void ApplyIntrospectionOutputs(
        JsonObject result,
        string outputDirectory,
        IntrospectionOutcome openCliOutcome,
        IntrospectionOutcome xmlDocOutcome)
    {
        result["timings"]!.AsObject()["opencliMs"] = openCliOutcome.ProcessResult.DurationMs;
        result["timings"]!.AsObject()["xmldocMs"] = xmlDocOutcome.ProcessResult.DurationMs;

        if (openCliOutcome.ArtifactObject is not null)
        {
            RepositoryPathResolver.WriteJsonFile(Path.Combine(outputDirectory, "opencli.json"), openCliOutcome.ArtifactObject);
            result["artifacts"]!.AsObject()["opencliArtifact"] = "opencli.json";
        }

        if (!string.IsNullOrWhiteSpace(xmlDocOutcome.ArtifactText))
        {
            RepositoryPathResolver.WriteTextFile(Path.Combine(outputDirectory, "xmldoc.xml"), xmlDocOutcome.ArtifactText);
            result["artifacts"]!.AsObject()["xmldocArtifact"] = "xmldoc.xml";
        }

        result["introspection"]!.AsObject()["opencli"] = new JsonObject
        {
            ["status"] = openCliOutcome.Status,
            ["classification"] = openCliOutcome.Classification,
            ["message"] = openCliOutcome.Message,
        };
        result["introspection"]!.AsObject()["xmldoc"] = new JsonObject
        {
            ["status"] = xmlDocOutcome.Status,
            ["classification"] = xmlDocOutcome.Classification,
            ["message"] = xmlDocOutcome.Message,
        };

        result["steps"]!.AsObject()["opencli"] = openCliOutcome.ToStepMetadata(result["artifacts"]?["opencliArtifact"]?.GetValue<string>());
        result["steps"]!.AsObject()["xmldoc"] = xmlDocOutcome.ToStepMetadata(result["artifacts"]?["xmldocArtifact"]?.GetValue<string>());
    }

    private static void ApplyOutcomeClassification(JsonObject result, IntrospectionOutcome openCliOutcome, IntrospectionOutcome xmlDocOutcome)
    {
        var outcomes = new[] { openCliOutcome, xmlDocOutcome };
        var successfulOutcomes = outcomes.Where(outcome => outcome.Status == "ok").ToArray();
        var retryableOutcomes = outcomes.Where(outcome => outcome.Status != "ok" && outcome.DispositionHint == "retryable-failure").ToArray();
        var deterministicOutcomes = outcomes.Where(outcome => outcome.Status != "ok" && outcome.DispositionHint == "terminal-failure").ToArray();

        if (successfulOutcomes.Length == 2)
        {
            result["disposition"] = "success";
            result["retryEligible"] = false;
            result["phase"] = "complete";
            result["classification"] = "spectre-cli-confirmed";
            result["failureMessage"] = null;
            return;
        }

        if (successfulOutcomes.Length == 1 && retryableOutcomes.Length == 0)
        {
            result["disposition"] = "success";
            result["retryEligible"] = false;
            result["phase"] = "complete";
            result["classification"] = successfulOutcomes[0].CommandName == "opencli"
                ? "spectre-cli-opencli-only"
                : "spectre-cli-xmldoc-only";
            result["failureMessage"] = null;
            return;
        }

        if (retryableOutcomes.Length > 0)
        {
            var primaryFailure = retryableOutcomes[0];
            result["disposition"] = "retryable-failure";
            result["retryEligible"] = true;
            result["phase"] = primaryFailure.CommandName;
            result["classification"] = primaryFailure.Classification;
            result["failureMessage"] = primaryFailure.Message;
            return;
        }

        if (deterministicOutcomes.Length > 0)
        {
            var primaryFailure = deterministicOutcomes[0];
            result["disposition"] = "terminal-failure";
            result["retryEligible"] = false;
            result["phase"] = primaryFailure.CommandName;
            result["classification"] = primaryFailure.Classification;
            result["failureMessage"] = primaryFailure.Message;
            return;
        }

        result["disposition"] = "retryable-failure";
        result["retryEligible"] = true;
        result["phase"] = "introspection";
        result["classification"] = "introspection-unresolved";
        result["failureMessage"] = "The tool did not yield a usable introspection result.";
    }

    private static string? ResolveInstalledCommandPath(string installDirectory, string commandName)
    {
        foreach (var candidate in new[]
        {
            Path.Combine(installDirectory, commandName),
            Path.Combine(installDirectory, commandName + ".exe"),
        })
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string NormalizeVersionForRegistrationLeaf(string version)
    {
        var normalized = version.Trim().ToLowerInvariant();
        var buildMetadataIndex = normalized.IndexOf('+');
        return buildMetadataIndex >= 0 ? normalized[..buildMetadataIndex] : normalized;
    }

    private static string? GetPreferredMessage(string? stdout, string? stderr)
    {
        var normalizedStderr = NormalizeConsoleText(stderr);
        if (!string.IsNullOrWhiteSpace(normalizedStderr))
        {
            return normalizedStderr;
        }

        var normalizedStdout = NormalizeConsoleText(stdout);
        return string.IsNullOrWhiteSpace(normalizedStdout) ? null : normalizedStdout;
    }

    private static string? NormalizeConsoleText(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        var normalized = value.TrimStart('\uFEFF').Replace("\0", string.Empty, StringComparison.Ordinal);
        normalized = AnsiCsiRegex.Replace(normalized, string.Empty);
        normalized = AnsiEscapeRegex.Replace(normalized, string.Empty);
        normalized = normalized.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static JsonParseResult TryParseJsonPayload(string? text)
    {
        var normalized = NormalizeConsoleText(text);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return new JsonParseResult(false, null, null, normalized, "Output was empty.");
        }

        string? lastError = null;
        foreach (var candidate in GetJsonCandidates(normalized))
        {
            try
            {
                return new JsonParseResult(true, JsonNode.Parse(candidate), candidate, normalized, null);
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
            }
        }

        return new JsonParseResult(false, null, null, normalized, lastError ?? "JSON parsing failed.");
    }

    private static IEnumerable<string> GetJsonCandidates(string normalized)
    {
        var candidates = new List<string> { normalized };
        var firstBrace = normalized.IndexOf('{');
        var firstBracket = normalized.IndexOf('[');
        var startIndex = new[] { firstBrace, firstBracket }
            .Where(index => index >= 0)
            .OrderBy(index => index)
            .FirstOrDefault(-1);
        if (startIndex > 0)
        {
            var candidate = normalized[startIndex..].Trim();
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                candidates.Add(candidate);
            }
        }

        var balancedCandidate = GetBalancedJsonSegment(normalized);
        if (!string.IsNullOrWhiteSpace(balancedCandidate) && !candidates.Contains(balancedCandidate, StringComparer.Ordinal))
        {
            candidates.Add(balancedCandidate);
        }

        return candidates;
    }

    private static string? GetBalancedJsonSegment(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var start = -1;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] is '{' or '[')
            {
                start = index;
                break;
            }
        }

        if (start < 0)
        {
            return null;
        }

        var stack = new Stack<char>();
        var inString = false;
        var escapeNext = false;

        for (var index = start; index < text.Length; index++)
        {
            var ch = text[index];
            if (escapeNext)
            {
                escapeNext = false;
                continue;
            }

            if (inString)
            {
                if (ch == '\\')
                {
                    escapeNext = true;
                }
                else if (ch == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (ch == '"')
            {
                inString = true;
                continue;
            }

            if (ch == '{')
            {
                stack.Push('}');
                continue;
            }

            if (ch == '[')
            {
                stack.Push(']');
                continue;
            }

            if ((ch is '}' or ']') && stack.Count > 0 && stack.Peek() == ch)
            {
                stack.Pop();
                if (stack.Count == 0)
                {
                    return text.Substring(start, (index - start) + 1).Trim();
                }
            }
        }

        return null;
    }

    private static JsonParseResult TryParseXmlPayload(string? text)
    {
        var normalized = NormalizeConsoleText(text);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return new JsonParseResult(false, null, null, normalized, "Output was empty.");
        }

        var candidates = new List<string> { normalized };
        var firstAngle = normalized.IndexOf('<');
        if (firstAngle > 0)
        {
            var candidate = normalized[firstAngle..].Trim();
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                candidates.Add(candidate);
            }
        }

        string? lastError = null;
        foreach (var candidate in candidates)
        {
            try
            {
                _ = XDocument.Parse(candidate);
                return new JsonParseResult(true, null, candidate, normalized, null);
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
            }
        }

        return new JsonParseResult(false, null, null, normalized, lastError ?? "XML parsing failed.");
    }

    private static string? ClassifyIntrospectionFailure(IReadOnlyList<string> argumentList, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var escapedSegments = argumentList.Select(Regex.Escape).ToArray();
        var subcommandPattern = escapedSegments.Length > 0
            ? $"(?:{string.Join("|", escapedSegments)})"
            : "(?:cli|opencli|xmldoc)";

        if (MatchesAny(text, [
            $@"\bunknown command\b.*\b{subcommandPattern}\b",
            $@"\bunrecognized command\b.*\b{subcommandPattern}\b",
            $@"\bunknown argument\b.*\b{subcommandPattern}\b",
            $@"\bunrecognized argument\b.*\b{subcommandPattern}\b",
            $@"\b{subcommandPattern}\b.*\b(?:not recognized|not found|not a valid command|invalid command)\b",
            $@"\bcould not match\b.*\b{subcommandPattern}\b",
            @"\bcould not resolve type\b.*\b(?:opencli|xmldoc|spectre\.console\.cli\.(?:opendoc|xmldoc|xml?doc)command|spectre\.console\.cli\.xmldoccommand)\b",
            @"\brequired command was not provided\b",
        ]))
        {
            return "unsupported-command";
        }

        if (MatchesAny(text, [
            @"\byou must install or update \.net\b",
            @"\bframework:\s+'?microsoft\.netcore\.app",
            @"\bno frameworks? were found\b",
            @"\bthe following frameworks were found\b",
        ]))
        {
            return "environment-missing-runtime";
        }

        if (MatchesAny(text, [
            @"\b(?:unable to load shared library|cannot open shared object file|dllnotfoundexception|could not load file or assembly|libsecret)\b",
        ]))
        {
            return "environment-missing-dependency";
        }

        if (MatchesAny(text, [
            @"\b(?:current terminal isn't interactive|non-interactive mode|cannot prompt|cannot show selection prompt|failed to read input in non-interactive mode)\b",
        ]))
        {
            return "requires-interactive-input";
        }

        if (MatchesAny(text, [
            @"\b(?:windows only|unsupported operating system|platform not supported|os platform is not supported)\b",
        ]))
        {
            return "unsupported-platform";
        }

        if (MatchesAny(text, [
            @"\b(?:checking your credentials|credential|authenticate|authentication|device code|sign in|login|log in|open (?:the )? browser)\b",
        ]))
        {
            return "requires-interactive-authentication";
        }

        if (MatchesAny(text, [
            @"\b(?:no|missing)\b.*\b(?:config|configuration)\b",
            @"\bconfiguration\b",
            @"\b(?:required option|required argument)\b",
            @"\bmust be specified\b",
            @"\bnot enough arguments\b",
        ]))
        {
            return "requires-configuration";
        }

        return null;
    }

    private static bool MatchesAny(string text, IEnumerable<string> patterns)
        => patterns.Any(pattern => Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline));

    private static string GetFailureSignature(string phase, string classification, string? message)
    {
        var normalized = string.IsNullOrWhiteSpace(message)
            ? string.Empty
            : Regex.Replace(message, @"\s+", " ").Trim();
        return $"{phase}|{classification}|{normalized}";
    }

    private static JsonArray ToJsonArray(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(value);
        }

        return array;
    }

    private sealed record SandboxEnvironment(
        IReadOnlyDictionary<string, string> Values,
        IReadOnlyList<string> Directories);

    private sealed record DetectionInfo(
        bool HasSpectreConsole,
        bool HasSpectreConsoleCli,
        IReadOnlyList<string> MatchedPackageEntries,
        IReadOnlyList<string> MatchedDependencyIds)
    {
        public JsonObject ToJsonObject()
            => new()
            {
                ["hasSpectreConsole"] = HasSpectreConsole,
                ["hasSpectreConsoleCli"] = HasSpectreConsoleCli,
                ["matchedPackageEntries"] = ToJsonArray(MatchedPackageEntries),
                ["matchedDependencyIds"] = ToJsonArray(MatchedDependencyIds),
            };
    }

    private sealed record ProcessResult(
        string Status,
        bool TimedOut,
        int? ExitCode,
        int DurationMs,
        string Stdout,
        string Stderr)
    {
        public JsonObject ToStepMetadata(bool includeStdout)
        {
            var metadata = new JsonObject
            {
                ["status"] = Status,
                ["timedOut"] = TimedOut,
                ["exitCode"] = ExitCode,
                ["durationMs"] = DurationMs,
                ["stdoutLength"] = Encoding.UTF8.GetByteCount(Stdout ?? string.Empty),
                ["stderrLength"] = Encoding.UTF8.GetByteCount(Stderr ?? string.Empty),
            };

            if (includeStdout)
            {
                metadata["stdout"] = NormalizeConsoleText(Stdout);
            }

            var normalizedStderr = NormalizeConsoleText(Stderr);
            if (!string.IsNullOrWhiteSpace(normalizedStderr))
            {
                metadata["stderr"] = normalizedStderr;
            }

            return metadata;
        }
    }

    private sealed record JsonParseResult(
        bool Success,
        JsonNode? Document,
        string? ArtifactText,
        string? NormalizedText,
        string? Error);

    private sealed record IntrospectionOutcome(
        string CommandName,
        string ExpectedFormat,
        ProcessResult ProcessResult,
        string Status,
        string Classification,
        string DispositionHint,
        string? Message,
        JsonNode? ArtifactObject,
        string? ArtifactText)
    {
        public JsonObject ToStepMetadata(string? artifactPath)
        {
            var metadata = ProcessResult.ToStepMetadata(includeStdout: Status != "ok");
            if (!string.IsNullOrWhiteSpace(artifactPath))
            {
                metadata["path"] = artifactPath;
            }

            metadata["outcomeStatus"] = Status;
            metadata["classification"] = Classification;
            if (!string.IsNullOrWhiteSpace(Message))
            {
                metadata["message"] = Message;
            }

            return metadata;
        }
    }
}

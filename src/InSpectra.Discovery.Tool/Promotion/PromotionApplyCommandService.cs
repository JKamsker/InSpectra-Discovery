using System.Text.Json.Nodes;
using System.Xml.Linq;

internal sealed class PromotionApplyCommandService
{
    public async Task<int> ApplyUntrustedAsync(
        string downloadRoot,
        string? summaryOutputPath,
        bool json,
        CancellationToken cancellationToken)
    {
        var repositoryRoot = RepositoryPathResolver.ResolveRepositoryRoot();
        var packagesRoot = Path.Combine(repositoryRoot, "index", "packages");
        var stateRoot = Path.Combine(repositoryRoot, "state");
        var now = DateTimeOffset.UtcNow;
        var downloadDirectory = Path.GetFullPath(downloadRoot);
        var expectedPath = Directory.GetFiles(downloadDirectory, "expected.json", SearchOption.AllDirectories).FirstOrDefault()
            ?? throw new InvalidOperationException($"expected.json was not found under '{downloadRoot}'.");
        var plan = JsonNode.Parse(await File.ReadAllTextAsync(expectedPath, cancellationToken))?.AsObject()
            ?? throw new InvalidOperationException($"Plan '{expectedPath}' is empty.");

        var resultLookup = new Dictionary<string, (JsonObject Result, string ArtifactDirectory)>(StringComparer.OrdinalIgnoreCase);
        foreach (var resultPath in Directory.GetFiles(downloadDirectory, "result.json", SearchOption.AllDirectories))
        {
            var result = JsonNode.Parse(await File.ReadAllTextAsync(resultPath, cancellationToken))?.AsObject();
            if (result is null)
            {
                continue;
            }

            var key = $"{result["packageId"]?.GetValue<string>()}|{result["version"]?.GetValue<string>()}";
            resultLookup[key] = (result, Path.GetDirectoryName(resultPath)!);
        }

        var summary = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["batchId"] = plan["batchId"]?.GetValue<string>(),
            ["targetBranch"] = plan["targetBranch"]?.GetValue<string>() ?? "main",
            ["promotedAt"] = now.ToString("O"),
            ["expectedCount"] = plan["items"]?.AsArray().Count ?? 0,
            ["successCount"] = 0,
            ["terminalNegativeCount"] = 0,
            ["retryableFailureCount"] = 0,
            ["terminalFailureCount"] = 0,
            ["missingCount"] = 0,
            ["createdPackages"] = new JsonArray(),
            ["updatedPackages"] = new JsonArray(),
            ["nonSuccessItems"] = new JsonArray(),
        };

        foreach (var item in plan["items"]?.AsArray().OfType<JsonObject>() ?? [])
        {
            var key = $"{item["packageId"]?.GetValue<string>()}|{item["version"]?.GetValue<string>()}";
            var hasResultArtifact = resultLookup.TryGetValue(key, out var resultEntry);
            var result = hasResultArtifact
                ? resultEntry.Result
                : NewSyntheticFailureResult(
                    item,
                    item["attempt"]?.GetValue<int?>() ?? 1,
                    "missing-result-artifact",
                    "No result artifact was uploaded for this matrix item.",
                    plan["batchId"]?.GetValue<string>() ?? string.Empty,
                    now);
            MergePlanItemIntoResult(item, result);
            var artifactDirectory = hasResultArtifact ? resultEntry.ArtifactDirectory : null;
            if (!hasResultArtifact)
            {
                summary["missingCount"] = (summary["missingCount"]?.GetValue<int>() ?? 0) + 1;
            }

            if (string.Equals(result["disposition"]?.GetValue<string>(), "success", StringComparison.Ordinal))
            {
                var openCliArtifact = result["artifacts"]?["opencliArtifact"]?.GetValue<string>();
                var xmlDocArtifact = result["artifacts"]?["xmldocArtifact"]?.GetValue<string>();
                var openCliExists = !string.IsNullOrWhiteSpace(openCliArtifact) && artifactDirectory is not null && File.Exists(Path.Combine(artifactDirectory, openCliArtifact));
                var xmlDocExists = !string.IsNullOrWhiteSpace(xmlDocArtifact) && artifactDirectory is not null && File.Exists(Path.Combine(artifactDirectory, xmlDocArtifact));
                var declaredMissing = new List<string>();

                if (!string.IsNullOrWhiteSpace(openCliArtifact) && !openCliExists)
                {
                    declaredMissing.Add(openCliArtifact);
                }

                if (!string.IsNullOrWhiteSpace(xmlDocArtifact) && !xmlDocExists)
                {
                    declaredMissing.Add(xmlDocArtifact);
                }

                if (declaredMissing.Count > 0 || !(openCliExists || xmlDocExists))
                {
                    var message = declaredMissing.Count > 0
                        ? "Success result declared artifact(s) that were not uploaded: " + string.Join(", ", declaredMissing)
                        : "Success result did not include either opencli.json or xmldoc.xml.";
                    result = NewSyntheticFailureResult(
                        item,
                        result["attempt"]?.GetValue<int?>() ?? item["attempt"]?.GetValue<int?>() ?? 1,
                        "missing-success-artifact",
                        message,
                        plan["batchId"]?.GetValue<string>() ?? string.Empty,
                        now);
                    artifactDirectory = null;
                }
            }

            var packageId = item["packageId"]?.GetValue<string>() ?? throw new InvalidOperationException("Plan item is missing packageId.");
            var version = item["version"]?.GetValue<string>() ?? throw new InvalidOperationException($"Plan item '{packageId}' is missing version.");
            var lowerId = packageId.ToLowerInvariant();
            var lowerVersion = version.ToLowerInvariant();
            var statePath = Path.Combine(stateRoot, "packages", lowerId, $"{lowerVersion}.json");
            var existingState = File.Exists(statePath)
                ? JsonNode.Parse(await File.ReadAllTextAsync(statePath, cancellationToken))?.AsObject()
                : null;
            var existingPackageIndexPath = Path.Combine(packagesRoot, lowerId, "index.json");
            var existingPackageIndex = File.Exists(existingPackageIndexPath)
                ? JsonNode.Parse(await File.ReadAllTextAsync(existingPackageIndexPath, cancellationToken))?.AsObject()
                : null;

            var indexedPaths = string.Equals(result["disposition"]?.GetValue<string>(), "success", StringComparison.Ordinal)
                ? await WriteSuccessArtifactsAsync(repositoryRoot, packagesRoot, result, artifactDirectory, cancellationToken)
                : null;
            var stateRecord = UpdateStateRecord(existingState, result, indexedPaths, now);
            RepositoryPathResolver.WriteJsonFile(statePath, stateRecord);

            IncrementSummaryCount(summary, stateRecord["currentStatus"]?.GetValue<string>());
            UpdatePackageChangeSummary(summary, existingPackageIndex, result);

            if (!string.Equals(stateRecord["currentStatus"]?.GetValue<string>(), "success", StringComparison.Ordinal))
            {
                ((JsonArray)summary["nonSuccessItems"]!).Add(new JsonObject
                {
                    ["packageId"] = result["packageId"]?.GetValue<string>(),
                    ["version"] = result["version"]?.GetValue<string>(),
                    ["status"] = stateRecord["currentStatus"]?.GetValue<string>(),
                    ["disposition"] = result["disposition"]?.GetValue<string>(),
                    ["phase"] = result["phase"]?.GetValue<string>(),
                    ["classification"] = result["classification"]?.GetValue<string>(),
                    ["reason"] = GetNonSuccessReason(result, stateRecord),
                });
            }
        }

        RepositoryPackageIndexBuilder.Rebuild(repositoryRoot, writeBrowserIndex: true);

        if (!string.IsNullOrWhiteSpace(summaryOutputPath))
        {
            RepositoryPathResolver.WriteJsonFile(summaryOutputPath, summary);
        }

        var output = ToolRuntime.CreateOutput();
        return await output.WriteSuccessAsync(
            new
            {
                batchId = summary["batchId"]?.GetValue<string>(),
                targetBranch = summary["targetBranch"]?.GetValue<string>(),
                successCount = summary["successCount"]?.GetValue<int>() ?? 0,
                terminalNegativeCount = summary["terminalNegativeCount"]?.GetValue<int>() ?? 0,
                retryableFailureCount = summary["retryableFailureCount"]?.GetValue<int>() ?? 0,
                terminalFailureCount = summary["terminalFailureCount"]?.GetValue<int>() ?? 0,
                missingCount = summary["missingCount"]?.GetValue<int>() ?? 0,
                summaryOutputPath = string.IsNullOrWhiteSpace(summaryOutputPath) ? null : Path.GetFullPath(summaryOutputPath),
            },
            [
                new SummaryRow("Batch", summary["batchId"]?.GetValue<string>() ?? string.Empty),
                new SummaryRow("Success", (summary["successCount"]?.GetValue<int>() ?? 0).ToString()),
                new SummaryRow("Terminal negative", (summary["terminalNegativeCount"]?.GetValue<int>() ?? 0).ToString()),
                new SummaryRow("Retryable failure", (summary["retryableFailureCount"]?.GetValue<int>() ?? 0).ToString()),
                new SummaryRow("Terminal failure", (summary["terminalFailureCount"]?.GetValue<int>() ?? 0).ToString()),
                new SummaryRow("Missing artifacts", (summary["missingCount"]?.GetValue<int>() ?? 0).ToString()),
            ],
            json,
            cancellationToken);
    }

    private static async Task<JsonObject> WriteSuccessArtifactsAsync(
        string repositoryRoot,
        string packagesRoot,
        JsonObject result,
        string? artifactDirectory,
        CancellationToken cancellationToken)
    {
        var packageId = result["packageId"]?.GetValue<string>() ?? throw new InvalidOperationException("Result is missing packageId.");
        var version = result["version"]?.GetValue<string>() ?? throw new InvalidOperationException($"Result for '{packageId}' is missing version.");
        var lowerId = packageId.ToLowerInvariant();
        var lowerVersion = version.ToLowerInvariant();
        var versionRoot = Path.Combine(packagesRoot, lowerId, lowerVersion);
        var metadataPath = Path.Combine(versionRoot, "metadata.json");
        var openCliPath = Path.Combine(versionRoot, "opencli.json");
        var xmlDocPath = Path.Combine(versionRoot, "xmldoc.xml");
        var openCliArtifact = result["artifacts"]?["opencliArtifact"]?.GetValue<string>();
        var xmlDocArtifact = result["artifacts"]?["xmldocArtifact"]?.GetValue<string>();
        var hasOpenCliArtifact = !string.IsNullOrWhiteSpace(openCliArtifact) && artifactDirectory is not null && File.Exists(Path.Combine(artifactDirectory, openCliArtifact));
        var hasXmlDocArtifact = !string.IsNullOrWhiteSpace(xmlDocArtifact) && artifactDirectory is not null && File.Exists(Path.Combine(artifactDirectory, xmlDocArtifact));
        string? openCliSource = null;
        JsonNode? openCliDocument = null;
        string? xmlDocContent = null;

        if (hasOpenCliArtifact)
        {
            openCliDocument = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(artifactDirectory!, openCliArtifact!), cancellationToken));
            openCliSource = "tool-output";
        }

        if (hasXmlDocArtifact)
        {
            xmlDocContent = await File.ReadAllTextAsync(Path.Combine(artifactDirectory!, xmlDocArtifact!), cancellationToken);
        }

        if (openCliDocument is null && !string.IsNullOrWhiteSpace(xmlDocContent))
        {
            openCliDocument = OpenCliDocumentSynthesizer.ConvertFromXmldoc(
                XDocument.Parse(xmlDocContent),
                result["command"]?.GetValue<string>() ?? packageId);
            openCliSource = "synthesized-from-xmldoc";
        }

        var hasOpenCliOutput = openCliDocument is not null;
        if (hasOpenCliOutput)
        {
            RepositoryPathResolver.WriteJsonFile(openCliPath, openCliDocument);
        }
        else if (File.Exists(openCliPath))
        {
            File.Delete(openCliPath);
        }

        if (!string.IsNullOrWhiteSpace(xmlDocContent))
        {
            RepositoryPathResolver.WriteTextFile(xmlDocPath, xmlDocContent);
        }
        else if (File.Exists(xmlDocPath))
        {
            File.Delete(xmlDocPath);
        }

        var openCliStep = result["steps"]?["opencli"]?.DeepClone() as JsonObject;
        if (openCliStep is not null)
        {
            if (hasOpenCliOutput)
            {
                openCliStep["path"] = RepositoryPathResolver.GetRelativePath(repositoryRoot, openCliPath);
                openCliStep["artifactSource"] = openCliSource;
            }
            else
            {
                openCliStep.Remove("path");
            }
        }

        var xmlDocStep = result["steps"]?["xmldoc"]?.DeepClone() as JsonObject;
        if (xmlDocStep is not null)
        {
            if (!string.IsNullOrWhiteSpace(xmlDocContent))
            {
                xmlDocStep["path"] = RepositoryPathResolver.GetRelativePath(repositoryRoot, xmlDocPath);
            }
            else
            {
                xmlDocStep.Remove("path");
            }
        }

        var introspection = result["introspection"]?.DeepClone() as JsonObject ?? new JsonObject();
        var openCliIntrospection = introspection["opencli"]?.DeepClone() as JsonObject;
        if (openCliIntrospection is not null && string.Equals(openCliSource, "synthesized-from-xmldoc", StringComparison.Ordinal))
        {
            openCliIntrospection["synthesizedArtifact"] = true;
            openCliIntrospection["artifactSource"] = openCliSource;
            introspection["opencli"] = openCliIntrospection;
        }

        var metadata = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["packageId"] = packageId,
            ["version"] = version,
            ["trusted"] = false,
            ["source"] = result["source"]?.GetValue<string>(),
            ["batchId"] = result["batchId"]?.GetValue<string>(),
            ["attempt"] = result["attempt"]?.GetValue<int?>(),
            ["status"] = hasOpenCliArtifact && hasXmlDocArtifact ? "ok" : "partial",
            ["evaluatedAt"] = result["analyzedAt"]?.GetValue<string>(),
            ["publishedAt"] = RepositoryPackageIndexBuilder.ToIsoTimestamp(result["publishedAt"]),
            ["packageUrl"] = result["packageUrl"]?.GetValue<string>(),
            ["totalDownloads"] = result["totalDownloads"]?.GetValue<long?>(),
            ["packageContentUrl"] = result["packageContentUrl"]?.GetValue<string>(),
            ["registrationLeafUrl"] = result["registrationLeafUrl"]?.GetValue<string>(),
            ["catalogEntryUrl"] = result["catalogEntryUrl"]?.GetValue<string>(),
            ["projectUrl"] = result["projectUrl"]?.GetValue<string>(),
            ["sourceRepositoryUrl"] = result["sourceRepositoryUrl"]?.GetValue<string>(),
            ["command"] = result["command"]?.GetValue<string>(),
            ["entryPoint"] = result["entryPoint"]?.GetValue<string>(),
            ["runner"] = result["runner"]?.GetValue<string>(),
            ["toolSettingsPath"] = result["toolSettingsPath"]?.GetValue<string>(),
            ["detection"] = result["detection"]?.DeepClone(),
            ["introspection"] = introspection,
            ["timings"] = result["timings"]?.DeepClone(),
            ["steps"] = new JsonObject
            {
                ["install"] = result["steps"]?["install"]?.DeepClone(),
                ["opencli"] = openCliStep,
                ["xmldoc"] = xmlDocStep,
            },
            ["artifacts"] = new JsonObject
            {
                ["metadataPath"] = RepositoryPathResolver.GetRelativePath(repositoryRoot, metadataPath),
                ["opencliPath"] = hasOpenCliOutput ? RepositoryPathResolver.GetRelativePath(repositoryRoot, openCliPath) : null,
                ["opencliSource"] = hasOpenCliOutput ? openCliSource : null,
                ["xmldocPath"] = !string.IsNullOrWhiteSpace(xmlDocContent) ? RepositoryPathResolver.GetRelativePath(repositoryRoot, xmlDocPath) : null,
            },
        };

        RepositoryPathResolver.WriteJsonFile(metadataPath, metadata);
        return metadata["artifacts"]!.DeepClone().AsObject();
    }

    private static JsonObject UpdateStateRecord(JsonObject? existingState, JsonObject result, JsonObject? indexedPaths, DateTimeOffset now)
    {
        var sameSignature =
            !string.IsNullOrWhiteSpace(existingState?["lastFailureSignature"]?.GetValue<string>()) &&
            string.Equals(existingState?["lastFailureSignature"]?.GetValue<string>(), result["failureSignature"]?.GetValue<string>(), StringComparison.Ordinal);
        var consecutiveFailures = string.Equals(result["disposition"]?.GetValue<string>(), "retryable-failure", StringComparison.Ordinal)
            ? sameSignature
                ? (existingState?["consecutiveFailureCount"]?.GetValue<int?>() ?? 0) + 1
                : 1
            : 0;
        var attemptCount = result["attempt"]?.GetValue<int?>() ?? 1;
        var allowTerminalEscalation =
            !string.Equals(result["disposition"]?.GetValue<string>(), "retryable-failure", StringComparison.Ordinal) ||
            !string.Equals(result["classification"]?.GetValue<string>(), "environment-missing-runtime", StringComparison.Ordinal);

        var status = result["disposition"]?.GetValue<string>() switch
        {
            "success" => "success",
            "terminal-negative" => "terminal-negative",
            "terminal-failure" => "terminal-failure",
            _ => allowTerminalEscalation && consecutiveFailures >= 3 ? "terminal-failure" : "retryable-failure",
        };

        return new JsonObject
        {
            ["schemaVersion"] = 1,
            ["packageId"] = result["packageId"]?.GetValue<string>(),
            ["version"] = result["version"]?.GetValue<string>(),
            ["trusted"] = false,
            ["currentStatus"] = status,
            ["lastDisposition"] = result["disposition"]?.GetValue<string>(),
            ["attemptCount"] = attemptCount,
            ["consecutiveFailureCount"] = consecutiveFailures,
            ["lastFailureSignature"] = status.Contains("failure", StringComparison.Ordinal) ? result["failureSignature"]?.GetValue<string>() : null,
            ["lastFailurePhase"] = status.Contains("failure", StringComparison.Ordinal) ? result["phase"]?.GetValue<string>() : null,
            ["lastFailureMessage"] = status.Contains("failure", StringComparison.Ordinal) ? result["failureMessage"]?.GetValue<string>() : null,
            ["firstEvaluatedAt"] = existingState?["firstEvaluatedAt"]?.GetValue<string>() ?? result["analyzedAt"]?.GetValue<string>(),
            ["lastEvaluatedAt"] = result["analyzedAt"]?.GetValue<string>(),
            ["lastBatchId"] = result["batchId"]?.GetValue<string>(),
            ["retryEligible"] = status == "retryable-failure",
            ["nextAttemptAt"] = status == "retryable-failure" ? now.AddHours(GetBackoffHours(attemptCount)).ToString("O") : null,
            ["lastSuccessfulAt"] = status == "success"
                ? result["analyzedAt"]?.GetValue<string>()
                : existingState?["lastSuccessfulAt"]?.GetValue<string>(),
            ["indexedPaths"] = indexedPaths?.DeepClone(),
        };
    }

    private static void IncrementSummaryCount(JsonObject summary, string? status)
    {
        switch (status)
        {
            case "success":
                summary["successCount"] = (summary["successCount"]?.GetValue<int>() ?? 0) + 1;
                break;
            case "terminal-negative":
                summary["terminalNegativeCount"] = (summary["terminalNegativeCount"]?.GetValue<int>() ?? 0) + 1;
                break;
            case "retryable-failure":
                summary["retryableFailureCount"] = (summary["retryableFailureCount"]?.GetValue<int>() ?? 0) + 1;
                break;
            case "terminal-failure":
                summary["terminalFailureCount"] = (summary["terminalFailureCount"]?.GetValue<int>() ?? 0) + 1;
                break;
        }
    }

    private static void UpdatePackageChangeSummary(JsonObject summary, JsonObject? existingPackageIndex, JsonObject result)
    {
        if (!string.Equals(result["disposition"]?.GetValue<string>(), "success", StringComparison.Ordinal))
        {
            return;
        }

        if (existingPackageIndex is null)
        {
            ((JsonArray)summary["createdPackages"]!).Add(new JsonObject
            {
                ["packageId"] = result["packageId"]?.GetValue<string>(),
                ["version"] = result["version"]?.GetValue<string>(),
            });
            return;
        }

        var previousVersion = existingPackageIndex["latestVersion"]?.GetValue<string>();
        var newVersion = result["version"]?.GetValue<string>();
        if (!string.Equals(previousVersion, newVersion, StringComparison.OrdinalIgnoreCase))
        {
            ((JsonArray)summary["updatedPackages"]!).Add(new JsonObject
            {
                ["packageId"] = result["packageId"]?.GetValue<string>(),
                ["previousVersion"] = previousVersion,
                ["version"] = newVersion,
            });
        }
    }

    private static JsonObject NewSyntheticFailureResult(JsonObject item, int attempt, string classification, string message, string batchId, DateTimeOffset now)
        => new()
        {
            ["schemaVersion"] = 1,
            ["packageId"] = item["packageId"]?.GetValue<string>(),
            ["version"] = item["version"]?.GetValue<string>(),
            ["batchId"] = batchId,
            ["attempt"] = attempt,
            ["trusted"] = false,
            ["source"] = "workflow_run",
            ["analyzedAt"] = now.ToString("O"),
            ["disposition"] = "retryable-failure",
            ["retryEligible"] = true,
            ["phase"] = "infra",
            ["classification"] = classification,
            ["failureMessage"] = message,
            ["failureSignature"] = $"infra|{classification}|{message}",
            ["packageUrl"] = item["packageUrl"]?.GetValue<string>(),
            ["totalDownloads"] = item["totalDownloads"]?.GetValue<long?>(),
            ["packageContentUrl"] = item["packageContentUrl"]?.GetValue<string>(),
            ["registrationLeafUrl"] = null,
            ["catalogEntryUrl"] = item["catalogEntryUrl"]?.GetValue<string>(),
            ["command"] = null,
            ["entryPoint"] = null,
            ["runner"] = null,
            ["toolSettingsPath"] = null,
            ["publishedAt"] = null,
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

    private static void MergePlanItemIntoResult(JsonObject item, JsonObject result)
    {
        if (result["totalDownloads"] is null && item["totalDownloads"] is not null)
        {
            result["totalDownloads"] = item["totalDownloads"]?.DeepClone();
        }
    }

    private static string GetNonSuccessReason(JsonObject result, JsonObject stateRecord)
        => result["failureMessage"]?.GetValue<string>() ??
           stateRecord["lastFailureMessage"]?.GetValue<string>() ??
           GetDefaultReasonMessage(stateRecord["currentStatus"]?.GetValue<string>(), result["classification"]?.GetValue<string>());

    private static string GetDefaultReasonMessage(string? status, string? classification)
        => classification switch
        {
            "spectre-cli-missing" => "No Spectre.Console.Cli evidence was found in the published package.",
            "missing-result-artifact" => "No result artifact was uploaded for this matrix item.",
            "missing-success-artifact" => "The analyzer reported success, but the expected success artifact was missing.",
            "missing-result" => "No result was recorded for this matrix item.",
            "environment-missing-runtime" => "The runner did not have the .NET runtime required by this tool.",
            "environment-missing-dependency" => "The tool required a native dependency that is not available on the runner.",
            "requires-interactive-input" => "The tool attempted to prompt for interactive input, which is not available in batch mode.",
            "requires-interactive-authentication" => "The tool attempted an interactive authentication flow.",
            "unsupported-platform" => "The tool does not support the runner operating system.",
            "unsupported-command" => "The tool does not implement the expected introspection command.",
            "invalid-json" => "The tool exited, but its JSON output could not be parsed.",
            _ when string.Equals(status, "terminal-negative", StringComparison.Ordinal) => "The package did not satisfy the Spectre.Console.Cli prefilter.",
            _ => "No explicit reason was recorded.",
        };

    private static int GetBackoffHours(int attempt)
        => attempt switch
        {
            <= 1 => 1,
            2 => 6,
            _ => 24,
        };

}

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
        var plan = await PromotionPlanSupport.LoadMergedPlanAsync(downloadDirectory, cancellationToken);

        var resultLookup = new Dictionary<string, (JsonObject Result, string ArtifactDirectory)>(StringComparer.OrdinalIgnoreCase);
        foreach (var resultPath in Directory.GetFiles(downloadDirectory, "result.json", SearchOption.AllDirectories))
        {
            var result = JsonNode.Parse(await File.ReadAllTextAsync(resultPath, cancellationToken))?.AsObject();
            if (result is null)
            {
                continue;
            }

            var key = $"{result["packageId"]?.GetValue<string>()}|{result["version"]?.GetValue<string>()}";
            if (!resultLookup.TryGetValue(key, out var existing)
                || GetAttempt(result) >= GetAttempt(existing.Result))
            {
                resultLookup[key] = (result, Path.GetDirectoryName(resultPath)!);
            }
        }

        var summary = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["batchId"] = plan.BatchId,
            ["targetBranch"] = plan.TargetBranch,
            ["promotedAt"] = now.ToString("O"),
            ["expectedCount"] = plan.Items.Count,
            ["successCount"] = 0,
            ["terminalNegativeCount"] = 0,
            ["retryableFailureCount"] = 0,
            ["terminalFailureCount"] = 0,
            ["missingCount"] = 0,
            ["createdPackages"] = new JsonArray(),
            ["updatedPackages"] = new JsonArray(),
            ["nonSuccessItems"] = new JsonArray(),
        };

        foreach (var item in plan.Items.OfType<JsonObject>())
        {
            var key = $"{item["packageId"]?.GetValue<string>()}|{item["version"]?.GetValue<string>()}";
            var hasResultArtifact = resultLookup.TryGetValue(key, out var resultEntry);
            var result = hasResultArtifact
                ? resultEntry.Result
                : PromotionResultSupport.NewSyntheticFailureResult(
                    item,
                    item["attempt"]?.GetValue<int?>() ?? 1,
                    "missing-result-artifact",
                    "No result artifact was uploaded for this matrix item.",
                    plan.BatchId ?? string.Empty,
                    now);
            PromotionResultSupport.MergePlanItemIntoResult(item, result);
            var artifactDirectory = hasResultArtifact ? resultEntry.ArtifactDirectory : null;
            if (!hasResultArtifact)
            {
                summary["missingCount"] = (summary["missingCount"]?.GetValue<int>() ?? 0) + 1;
            }

            if (string.Equals(result["disposition"]?.GetValue<string>(), "success", StringComparison.Ordinal))
            {
                var openCliArtifact = result["artifacts"]?["opencliArtifact"]?.GetValue<string>();
                var xmlDocArtifact = result["artifacts"]?["xmldocArtifact"]?.GetValue<string>();
                var openCliArtifactPath = PromotionArtifactSupport.ResolveOptionalArtifactPath(artifactDirectory, openCliArtifact);
                var xmlDocArtifactPath = PromotionArtifactSupport.ResolveOptionalArtifactPath(artifactDirectory, xmlDocArtifact);
                var openCliExists = openCliArtifactPath is not null;
                var xmlDocExists = xmlDocArtifactPath is not null;
                var hasUsableOpenCli = openCliArtifactPath is not null
                    && PromotionArtifactSupport.TryLoadJsonObject(openCliArtifactPath, out _);
                var declaredMissing = new List<string>();
                var invalidArtifacts = new List<string>();

                if (!string.IsNullOrWhiteSpace(openCliArtifact) && !openCliExists)
                {
                    declaredMissing.Add(openCliArtifact);
                }

                if (!string.IsNullOrWhiteSpace(xmlDocArtifact) && !xmlDocExists)
                {
                    declaredMissing.Add(xmlDocArtifact);
                }

                if (openCliArtifactPath is not null && !hasUsableOpenCli)
                {
                    invalidArtifacts.Add(openCliArtifact!);
                }

                if (declaredMissing.Count > 0 || invalidArtifacts.Count > 0 || !(hasUsableOpenCli || xmlDocExists))
                {
                    var message = declaredMissing.Count > 0
                        ? "Success result declared artifact(s) that were not uploaded: " + string.Join(", ", declaredMissing)
                        : invalidArtifacts.Count > 0
                            ? "Success result declared OpenCLI artifact(s) that are not JSON objects: " + string.Join(", ", invalidArtifacts)
                        : "Success result did not include either opencli.json or xmldoc.xml.";
                    result = PromotionResultSupport.NewSyntheticFailureResult(
                        item,
                        result["attempt"]?.GetValue<int?>() ?? item["attempt"]?.GetValue<int?>() ?? 1,
                        "missing-success-artifact",
                        message,
                        plan.BatchId ?? string.Empty,
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

            JsonObject? indexedPaths = null;
            if (string.Equals(result["disposition"]?.GetValue<string>(), "success", StringComparison.Ordinal))
            {
                try
                {
                    indexedPaths = await WriteSuccessArtifactsAsync(repositoryRoot, packagesRoot, result, artifactDirectory, cancellationToken);
                }
                catch (Exception ex)
                {
                    result = PromotionResultSupport.NewSyntheticFailureResult(
                        item,
                        result["attempt"]?.GetValue<int?>() ?? item["attempt"]?.GetValue<int?>() ?? 1,
                        "invalid-success-artifact",
                        $"Success artifacts could not be promoted: {ex.Message}",
                        plan.BatchId ?? string.Empty,
                        now);
                    PromotionResultSupport.MergePlanItemIntoResult(item, result);
                }
            }

            var stateRecord = PromotionResultSupport.UpdateStateRecord(existingState, result, indexedPaths, now);
            RepositoryPathResolver.WriteJsonFile(statePath, stateRecord);

            PromotionResultSupport.IncrementSummaryCount(summary, stateRecord["currentStatus"]?.GetValue<string>());
            PromotionResultSupport.UpdatePackageChangeSummary(summary, existingPackageIndex, result);

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
                    ["reason"] = PromotionResultSupport.GetNonSuccessReason(result, stateRecord),
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
        var crawlPath = Path.Combine(versionRoot, "crawl.json");
        var xmlDocPath = Path.Combine(versionRoot, "xmldoc.xml");
        var openCliArtifact = result["artifacts"]?["opencliArtifact"]?.GetValue<string>();
        var crawlArtifact = result["artifacts"]?["crawlArtifact"]?.GetValue<string>();
        var xmlDocArtifact = result["artifacts"]?["xmldocArtifact"]?.GetValue<string>();
        var openCliArtifactPath = PromotionArtifactSupport.ResolveOptionalArtifactPath(artifactDirectory, openCliArtifact);
        var xmlDocArtifactPath = PromotionArtifactSupport.ResolveOptionalArtifactPath(artifactDirectory, xmlDocArtifact);
        string? openCliSource = null;
        JsonObject? openCliDocument = null;
        string? xmlDocContent = null;
        if (openCliArtifactPath is not null && PromotionArtifactSupport.TryLoadJsonObject(openCliArtifactPath, out var parsedOpenCli))
        {
            openCliSource = ResolveOpenCliSource(parsedOpenCli!);
            OpenCliDocumentSanitizer.EnsureArtifactSource(parsedOpenCli!, openCliSource);
            openCliDocument = OpenCliDocumentSanitizer.Sanitize(parsedOpenCli!);
        }

        if (xmlDocArtifactPath is not null)
        {
            xmlDocContent = await File.ReadAllTextAsync(xmlDocArtifactPath, cancellationToken);
        }

        if (openCliDocument is null && !string.IsNullOrWhiteSpace(xmlDocContent))
        {
            openCliDocument = OpenCliDocumentSynthesizer.ConvertFromXmldoc(
                XDocument.Parse(xmlDocContent),
                result["command"]?.GetValue<string>() ?? packageId,
                version);
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
        var hasCrawlArtifact = PromotionArtifactSupport.SyncOptionalArtifact(artifactDirectory, crawlArtifact, crawlPath);

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
        if (openCliIntrospection is not null && hasOpenCliOutput)
        {
            if (string.Equals(openCliSource, "synthesized-from-xmldoc", StringComparison.Ordinal))
            {
                openCliIntrospection["synthesizedArtifact"] = true;
            }

            openCliIntrospection["artifactSource"] = openCliSource;
            introspection["opencli"] = openCliIntrospection;
        }

        var metadata = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["packageId"] = packageId,
            ["version"] = version,
            ["trusted"] = false,
            ["analysisMode"] = result["analysisMode"]?.GetValue<string>(),
            ["analysisSelection"] = result["analysisSelection"]?.DeepClone(),
            ["fallback"] = result["fallback"]?.DeepClone(),
            ["cliFramework"] = result["cliFramework"]?.GetValue<string>(),
            ["source"] = result["source"]?.GetValue<string>(),
            ["batchId"] = result["batchId"]?.GetValue<string>(),
            ["attempt"] = result["attempt"]?.GetValue<int?>(),
            ["status"] = hasOpenCliOutput ? "ok" : "partial",
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
            ["coverage"] = result["coverage"]?.DeepClone(),
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
                ["crawlPath"] = hasCrawlArtifact ? RepositoryPathResolver.GetRelativePath(repositoryRoot, crawlPath) : null,
                ["xmldocPath"] = !string.IsNullOrWhiteSpace(xmlDocContent) ? RepositoryPathResolver.GetRelativePath(repositoryRoot, xmlDocPath) : null,
            },
        };

        RepositoryPathResolver.WriteJsonFile(metadataPath, metadata);
        return metadata["artifacts"]!.DeepClone().AsObject();
    }

    private static int GetAttempt(JsonObject result)
        => result["attempt"]?.GetValue<int?>() ?? 0;

    private static string ResolveOpenCliSource(JsonObject document)
        => document["x-inspectra"]?["artifactSource"]?.GetValue<string>() switch
        {
            { Length: > 0 } artifactSource => artifactSource,
            _ => "tool-output",
        };
}

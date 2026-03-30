using System.Text.Json.Nodes;

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
        var resultLookup = await PromotionResultArtifactLookup.BuildAsync(downloadDirectory, cancellationToken);

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
            var hasResultArtifact = resultLookup.TryResolve(item, out var resultEntry);
            var result = hasResultArtifact
                ? resultEntry!.Result
                : PromotionResultSupport.NewSyntheticFailureResult(
                    item,
                    item["attempt"]?.GetValue<int?>() ?? 1,
                    "missing-result-artifact",
                    "No result artifact was uploaded for this matrix item.",
                    plan.BatchId ?? string.Empty,
                    now);
            PromotionResultSupport.MergePlanItemIntoResult(item, result);
            var artifactDirectory = hasResultArtifact ? resultEntry!.ArtifactDirectory : null;
            if (!hasResultArtifact)
            {
                summary["missingCount"] = (summary["missingCount"]?.GetValue<int>() ?? 0) + 1;
            }

            if (string.Equals(result["disposition"]?.GetValue<string>(), "success", StringComparison.Ordinal))
            {
                var openCliArtifact = result["artifacts"]?["opencliArtifact"]?.GetValue<string>();
                var crawlArtifact = result["artifacts"]?["crawlArtifact"]?.GetValue<string>();
                var xmlDocArtifact = result["artifacts"]?["xmldocArtifact"]?.GetValue<string>();
                var openCliArtifactPath = PromotionArtifactSupport.ResolveOptionalArtifactPath(artifactDirectory, openCliArtifact);
                var crawlArtifactPath = PromotionArtifactSupport.ResolveOptionalArtifactPath(artifactDirectory, crawlArtifact);
                var xmlDocArtifactPath = PromotionArtifactSupport.ResolveOptionalArtifactPath(artifactDirectory, xmlDocArtifact);
                var openCliExists = openCliArtifactPath is not null;
                var crawlExists = crawlArtifactPath is not null;
                var xmlDocExists = xmlDocArtifactPath is not null;
                string? openCliValidationError = null;
                string? xmlDocValidationError = null;
                JsonObject? openCliDocument = null;
                JsonObject? crawlDocument = null;
                var hasUsableOpenCli = openCliArtifactPath is not null
                    && OpenCliDocumentValidator.TryLoadValidDocument(openCliArtifactPath, out openCliDocument, out openCliValidationError);
                var hasUsableCrawl = crawlArtifactPath is not null
                    && PromotionArtifactSupport.TryLoadJsonObject(crawlArtifactPath, out crawlDocument);
                var hasUsableXmlDoc = xmlDocArtifactPath is not null
                    && PromotionAnalysisModeSupport.TryLoadXmlArtifact(xmlDocArtifactPath, out xmlDocValidationError);
                var selectedAnalysisMode = PromotionAnalysisModeSupport.ResolveAnalysisMode(
                    hasUsableOpenCli ? openCliDocument : null,
                    hasUsableCrawl ? crawlDocument : null,
                    item,
                    result);
                PromotionAnalysisModeSupport.BackfillAnalysisModeSelection(result, selectedAnalysisMode);
                var requiresCrawlArtifact = HelpBatchArtifactSupport.RequiresCrawlArtifact(selectedAnalysisMode);
                var declaredMissing = new List<string>();
                var invalidArtifacts = new List<string>();

                if (!string.IsNullOrWhiteSpace(openCliArtifact) && !openCliExists)
                {
                    declaredMissing.Add(openCliArtifact);
                }

                if (!string.IsNullOrWhiteSpace(crawlArtifact) && !crawlExists)
                {
                    declaredMissing.Add(crawlArtifact);
                }

                if (!string.IsNullOrWhiteSpace(xmlDocArtifact) && !xmlDocExists)
                {
                    declaredMissing.Add(xmlDocArtifact);
                }

                if (openCliArtifactPath is not null && !hasUsableOpenCli && (!hasUsableXmlDoc || requiresCrawlArtifact))
                {
                    invalidArtifacts.Add(openCliArtifact!);
                }

                if (crawlArtifactPath is not null && !hasUsableCrawl)
                {
                    invalidArtifacts.Add(crawlArtifact!);
                }

                if (xmlDocArtifactPath is not null && !hasUsableXmlDoc)
                {
                    invalidArtifacts.Add(xmlDocArtifact!);
                }

                if (declaredMissing.Count > 0
                    || invalidArtifacts.Count > 0
                    || !(hasUsableOpenCli || hasUsableXmlDoc)
                    || (requiresCrawlArtifact && !hasUsableCrawl))
                {
                    var message = declaredMissing.Count > 0
                        ? "Success result declared artifact(s) that were not uploaded: " + string.Join(", ", declaredMissing)
                        : invalidArtifacts.Count > 0
                            ? !string.IsNullOrWhiteSpace(openCliValidationError)
                                ? openCliValidationError
                                : !string.IsNullOrWhiteSpace(xmlDocValidationError)
                                    ? xmlDocValidationError
                                    : "Success result declared JSON artifact(s) that are not usable JSON objects: " + string.Join(", ", invalidArtifacts)
                        : requiresCrawlArtifact && !hasUsableCrawl
                            ? "Success result did not include a usable crawl.json artifact."
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
                    indexedPaths = await PromotionArtifactWriter.WriteSuccessArtifactsAsync(
                        repositoryRoot,
                        packagesRoot,
                        result,
                        artifactDirectory,
                        cancellationToken);
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

            if (!string.Equals(result["disposition"]?.GetValue<string>(), "success", StringComparison.Ordinal))
            {
                PromotionIndexCleanupSupport.RemoveIndexedVersionArtifacts(packagesRoot, packageId, version);
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
}

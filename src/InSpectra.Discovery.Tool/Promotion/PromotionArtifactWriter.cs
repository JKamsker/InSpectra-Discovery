using System.Text.Json.Nodes;
using System.Xml.Linq;

internal static class PromotionArtifactWriter
{
    public static async Task<JsonObject> WriteSuccessArtifactsAsync(
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

        if (openCliArtifactPath is not null
            && OpenCliDocumentValidator.TryLoadValidDocument(openCliArtifactPath, out var parsedOpenCli, out _))
        {
            openCliSource = ResolveOpenCliSource(parsedOpenCli!, result);
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

        PromotionAnalysisModeSupport.BackfillAnalysisModeSelection(
            result,
            OpenCliArtifactSourceSupport.InferAnalysisMode(openCliSource)
            ?? OpenCliArtifactSourceSupport.InferAnalysisMode(openCliDocument?["x-inspectra"]?["artifactSource"]?.GetValue<string>()));

        if (openCliDocument is not null && !string.IsNullOrWhiteSpace(result["cliFramework"]?.GetValue<string>()))
        {
            openCliDocument["x-inspectra"]!.AsObject()["cliFramework"] = result["cliFramework"]!.GetValue<string>();
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
        var introspection = result["introspection"]?.DeepClone() as JsonObject ?? new JsonObject();
        var openCliIntrospection = introspection["opencli"]?.DeepClone() as JsonObject;
        var inferredOpenCliClassification = ResolveOpenCliClassification(openCliSource, openCliStep, openCliIntrospection);
        if (openCliStep is null && hasOpenCliOutput)
        {
            openCliStep = new JsonObject
            {
                ["status"] = "ok",
            };

            if (!string.IsNullOrWhiteSpace(inferredOpenCliClassification))
            {
                openCliStep["classification"] = inferredOpenCliClassification;
            }
        }

        if (openCliStep is not null)
        {
            if (hasOpenCliOutput)
            {
                BackfillOpenCliStepMetadata(openCliStep, repositoryRoot, openCliPath, openCliSource, inferredOpenCliClassification);
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

        if (openCliIntrospection is null && hasOpenCliOutput)
        {
            openCliIntrospection = new JsonObject
            {
                ["status"] = "ok",
            };

            if (!string.IsNullOrWhiteSpace(inferredOpenCliClassification))
            {
                openCliIntrospection["classification"] = inferredOpenCliClassification;
            }
        }

        if (openCliIntrospection is not null && hasOpenCliOutput)
        {
            BackfillOpenCliIntrospectionMetadata(openCliIntrospection, openCliSource, inferredOpenCliClassification);
            introspection["opencli"] = openCliIntrospection;
        }

        var metadataAnalysisMode = OpenCliArtifactSourceSupport.InferAnalysisMode(openCliSource)
            ?? OpenCliArtifactSourceSupport.InferAnalysisMode(openCliDocument?["x-inspectra"]?["artifactSource"]?.GetValue<string>())
            ?? result["analysisMode"]?.GetValue<string>();
        var metadataAnalysisSelection = result["analysisSelection"]?.DeepClone() as JsonObject;
        if (!string.IsNullOrWhiteSpace(metadataAnalysisMode))
        {
            metadataAnalysisSelection ??= new JsonObject();
            metadataAnalysisSelection["selectedMode"] = metadataAnalysisMode;
            if (metadataAnalysisSelection["preferredMode"] is null)
            {
                metadataAnalysisSelection["preferredMode"] = metadataAnalysisMode;
            }
        }

        var metadata = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["packageId"] = packageId,
            ["version"] = version,
            ["trusted"] = false,
            ["analysisMode"] = metadataAnalysisMode,
            ["analysisSelection"] = metadataAnalysisSelection,
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

        if (hasOpenCliOutput && !string.IsNullOrWhiteSpace(openCliSource))
        {
            OpenCliArtifactMetadataRepair.SyncMetadata(
                repositoryRoot,
                metadataPath,
                openCliPath,
                openCliSource,
                crawlPath: hasCrawlArtifact ? crawlPath : null,
                xmldocPath: !string.IsNullOrWhiteSpace(xmlDocContent) ? xmlDocPath : null,
                synthesizedArtifact: string.Equals(openCliSource, "synthesized-from-xmldoc", StringComparison.OrdinalIgnoreCase));
        }

        return metadata["artifacts"]!.DeepClone().AsObject();
    }

    private static string ResolveOpenCliSource(JsonObject document, JsonObject result)
        => FirstNonEmpty(
            document["x-inspectra"]?["artifactSource"]?.GetValue<string>(),
            result["artifacts"]?["opencliSource"]?.GetValue<string>(),
            result["steps"]?["opencli"]?["artifactSource"]?.GetValue<string>(),
            result["introspection"]?["opencli"]?["artifactSource"]?.GetValue<string>(),
            OpenCliArtifactSourceSupport.InferArtifactSource(result["analysisMode"]?.GetValue<string>()),
            "tool-output") ?? "tool-output";

    private static string? InferOpenCliClassification(string? openCliSource)
        => OpenCliArtifactSourceSupport.InferClassification(openCliSource);

    private static string? ResolveOpenCliClassification(string? openCliSource, params JsonObject?[] sources)
    {
        if (string.Equals(openCliSource, "tool-output", StringComparison.OrdinalIgnoreCase))
        {
            var preserved = sources
                .Select(source => source?["classification"]?.GetValue<string>())
                .FirstOrDefault(classification => string.Equals(classification, "json-ready-with-nonzero-exit", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(preserved))
            {
                return preserved;
            }
        }

        return InferOpenCliClassification(openCliSource);
    }

    private static void BackfillOpenCliStepMetadata(
        JsonObject openCliStep,
        string repositoryRoot,
        string openCliPath,
        string? openCliSource,
        string? inferredOpenCliClassification)
    {
        openCliStep["status"] = "ok";
        openCliStep["path"] = RepositoryPathResolver.GetRelativePath(repositoryRoot, openCliPath);
        openCliStep.Remove("message");
        if (!string.IsNullOrWhiteSpace(openCliSource))
        {
            openCliStep["artifactSource"] = openCliSource;
        }

        if (!string.IsNullOrWhiteSpace(inferredOpenCliClassification))
        {
            openCliStep["classification"] = inferredOpenCliClassification;
        }
    }

    private static void BackfillOpenCliIntrospectionMetadata(
        JsonObject openCliIntrospection,
        string? openCliSource,
        string? inferredOpenCliClassification)
    {
        openCliIntrospection["status"] = "ok";
        openCliIntrospection.Remove("message");
        if (string.Equals(openCliSource, "synthesized-from-xmldoc", StringComparison.Ordinal))
        {
            openCliIntrospection["synthesizedArtifact"] = true;
        }
        else
        {
            openCliIntrospection.Remove("synthesizedArtifact");
        }

        if (!string.IsNullOrWhiteSpace(openCliSource))
        {
            openCliIntrospection["artifactSource"] = openCliSource;
        }

        if (!string.IsNullOrWhiteSpace(inferredOpenCliClassification))
        {
            openCliIntrospection["classification"] = inferredOpenCliClassification;
        }
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}

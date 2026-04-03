namespace InSpectra.Discovery.Tool.Promotion.Artifacts;

using InSpectra.Discovery.Tool.Infrastructure.Json;
using InSpectra.Discovery.Tool.Indexing;

using InSpectra.Discovery.Tool.Infrastructure.Paths;

using InSpectra.Discovery.Tool.OpenCli.Artifacts;

using InSpectra.Discovery.Tool.Promotion.Planning;

using System.Text.Json.Nodes;
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
        var existingMetadata = await JsonNodeFileLoader.TryLoadJsonObjectAsync(metadataPath, cancellationToken);
        var openCliArtifactPath = PromotionArtifactSupport.ResolveOptionalArtifactPath(artifactDirectory, openCliArtifact);
        var xmlDocArtifactPath = PromotionArtifactSupport.ResolveOptionalArtifactPath(artifactDirectory, xmlDocArtifact);
        var preparedOpenCliArtifact = await PromotionOpenCliArtifactSupport.PrepareAsync(
            result,
            packageId,
            version,
            openCliArtifactPath,
            xmlDocArtifactPath,
            cancellationToken);
        var openCliDocument = preparedOpenCliArtifact.OpenCliDocument;
        var openCliSource = preparedOpenCliArtifact.OpenCliSource;
        var xmlDocContent = preparedOpenCliArtifact.XmlDocContent;

        PromotionAnalysisModeSupport.BackfillAnalysisModeSelection(
            result,
            OpenCliArtifactSourceSupport.InferAnalysisMode(openCliSource)
            ?? OpenCliArtifactSourceSupport.InferAnalysisMode(openCliDocument?["x-inspectra"]?["artifactSource"]?.GetValue<string>()));

        var hasOpenCliOutput = preparedOpenCliArtifact.HasOpenCliOutput;
        var indexedArtifactsChanged = HaveIndexedArtifactsChanged(
            artifactDirectory,
            crawlArtifact,
            crawlPath,
            hasOpenCliOutput ? openCliDocument : null,
            openCliPath,
            preparedOpenCliArtifact.HasXmlDocContent ? xmlDocContent : null,
            xmlDocPath);

        if (hasOpenCliOutput)
        {
            RepositoryPathResolver.WriteJsonFile(openCliPath, openCliDocument);
        }
        else if (File.Exists(openCliPath))
        {
            File.Delete(openCliPath);
        }

        var hasCrawlArtifact = PromotionArtifactSupport.SyncOptionalArtifact(artifactDirectory, crawlArtifact, crawlPath);

        if (preparedOpenCliArtifact.HasXmlDocContent)
        {
            RepositoryPathResolver.WriteTextFile(xmlDocPath, xmlDocContent!);
        }
        else if (File.Exists(xmlDocPath))
        {
            File.Delete(xmlDocPath);
        }

        var openCliStep = result["steps"]?["opencli"]?.DeepClone() as JsonObject;
        var introspection = result["introspection"]?.DeepClone() as JsonObject ?? new JsonObject();
        var openCliIntrospection = introspection["opencli"]?.DeepClone() as JsonObject;
        var inferredOpenCliClassification = PromotionOpenCliArtifactSupport.ResolveOpenCliClassification(openCliSource, openCliStep, openCliIntrospection);
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
                PromotionOpenCliArtifactSupport.BackfillOpenCliStepMetadata(
                    openCliStep,
                    repositoryRoot,
                    openCliPath,
                    openCliSource,
                    inferredOpenCliClassification);
            }
            else
            {
                openCliStep.Remove("path");
            }
        }

        var xmlDocStep = result["steps"]?["xmldoc"]?.DeepClone() as JsonObject;
        if (xmlDocStep is not null)
        {
            if (preparedOpenCliArtifact.HasXmlDocContent)
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
            PromotionOpenCliArtifactSupport.BackfillOpenCliIntrospectionMetadata(
                openCliIntrospection,
                openCliSource,
                inferredOpenCliClassification);
            introspection["opencli"] = openCliIntrospection;
        }

        var metadataAnalysisMode = PromotionOpenCliArtifactSupport.ResolveMetadataAnalysisMode(result, preparedOpenCliArtifact);
        var metadataAnalysisSelection = PromotionOpenCliArtifactSupport.BuildMetadataAnalysisSelection(result, metadataAnalysisMode);

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
            ["opencliSource"] = hasOpenCliOutput ? openCliSource : null,
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
                ["xmldocPath"] = preparedOpenCliArtifact.HasXmlDocContent ? RepositoryPathResolver.GetRelativePath(repositoryRoot, xmlDocPath) : null,
            },
        };
        BackfillStableMetadataFromExisting(metadata, existingMetadata);
        PreserveVolatileMetadataForNoOpPromotion(metadata, existingMetadata, indexedArtifactsChanged);

        RepositoryPathResolver.WriteJsonFile(metadataPath, metadata);

        if (hasOpenCliOutput && !string.IsNullOrWhiteSpace(openCliSource))
        {
            OpenCliArtifactMetadataRepair.SyncMetadata(
                repositoryRoot,
                metadataPath,
                openCliPath,
                openCliSource,
                crawlPath: hasCrawlArtifact ? crawlPath : null,
                xmldocPath: preparedOpenCliArtifact.HasXmlDocContent ? xmlDocPath : null,
                synthesizedArtifact: string.Equals(openCliSource, "synthesized-from-xmldoc", StringComparison.OrdinalIgnoreCase));
        }

        return metadata["artifacts"]!.DeepClone().AsObject();
    }

    private static void BackfillStableMetadataFromExisting(JsonObject metadata, JsonObject? existingMetadata)
    {
        if (existingMetadata is null)
        {
            return;
        }

        foreach (var propertyName in new[]
        {
            "cliFramework",
            "publishedAt",
            "packageUrl",
            "totalDownloads",
            "packageContentUrl",
            "registrationLeafUrl",
            "catalogEntryUrl",
            "projectUrl",
            "sourceRepositoryUrl",
            "command",
            "entryPoint",
            "runner",
            "toolSettingsPath",
            "detection",
        })
        {
            if (metadata[propertyName] is null && existingMetadata[propertyName] is not null)
            {
                metadata[propertyName] = existingMetadata[propertyName]!.DeepClone();
            }
        }
    }

    private static void PreserveVolatileMetadataForNoOpPromotion(
        JsonObject metadata,
        JsonObject? existingMetadata,
        bool indexedArtifactsChanged)
    {
        if (indexedArtifactsChanged || existingMetadata is null)
        {
            return;
        }

        JsonDocumentStabilitySupport.TryPreserveTopLevelProperties(
            metadata,
            existingMetadata,
            "source",
            "batchId",
            "attempt",
            "evaluatedAt",
            "timings");
    }

    private static bool HaveIndexedArtifactsChanged(
        string? artifactDirectory,
        string? crawlArtifact,
        string crawlPath,
        JsonObject? openCliDocument,
        string openCliPath,
        string? xmlDocContent,
        string xmlDocPath)
        => !PromotionArtifactSupport.HasSameJsonObjectContent(openCliPath, openCliDocument)
           || !PromotionArtifactSupport.HasSameOptionalArtifactContent(artifactDirectory, crawlArtifact, crawlPath)
           || !PromotionArtifactSupport.HasSameTextContent(xmlDocPath, xmlDocContent);
}


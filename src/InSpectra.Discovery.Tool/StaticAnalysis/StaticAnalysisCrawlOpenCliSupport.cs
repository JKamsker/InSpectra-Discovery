namespace InSpectra.Discovery.Tool.StaticAnalysis;

using System.Text.Json.Nodes;

internal sealed class StaticAnalysisCrawlOpenCliSupport
{
    private readonly StaticAnalysisOpenCliBuilder _openCliBuilder = new();
    private readonly TextParser _parser = new();

    public JsonObject RegenerateOpenCli(StaticAnalysisCrawlArtifactCandidate candidate)
    {
        var crawl = JsonNode.Parse(File.ReadAllText(candidate.CrawlPath))?.AsObject()
            ?? throw new InvalidOperationException($"Crawl artifact '{candidate.CrawlPath}' is empty.");
        var helpDocuments = ParseCaptures(crawl["commands"] as JsonArray);
        var staticCommands = StaticAnalysisCrawlArtifactSupport.DeserializeStaticCommands(crawl["staticCommands"]);
        var existingOpenCli = JsonNodeFileLoader.TryLoadJsonNode(candidate.OpenCliPath) as JsonObject;

        var framework = ResolveFramework(candidate.CliFramework);
        var openCli = _openCliBuilder.Build(candidate.CommandName, candidate.Version, framework, staticCommands, helpDocuments);
        if (!string.IsNullOrWhiteSpace(candidate.CliFramework))
        {
            openCli["x-inspectra"]!.AsObject()["cliFramework"] = candidate.CliFramework;
        }

        StaticAnalysisCliMetadataRestoreSupport.RestoreExistingCliMetadataEnrichment(openCli, existingOpenCli);
        return openCli;
    }

    private Dictionary<string, Document> ParseCaptures(JsonArray? captures)
    {
        var documents = new Dictionary<string, Document>(StringComparer.OrdinalIgnoreCase);
        foreach (var capture in captures?.OfType<JsonObject>() ?? [])
        {
            var payload = ExtractPayload(capture);
            if (string.IsNullOrWhiteSpace(payload))
            {
                continue;
            }

            var commandKey = capture["command"]?.GetValue<string>() ?? string.Empty;
            var document = _parser.Parse(payload);
            if (!document.HasContent)
            {
                continue;
            }

            documents[commandKey] = document;
        }

        return documents;
    }

    private static string? ExtractPayload(JsonObject capture)
    {
        var payload = CommandRuntime.NormalizeConsoleText(capture["payload"]?.GetValue<string>());
        if (!string.IsNullOrWhiteSpace(payload))
        {
            return payload;
        }

        var processResult = capture["result"] as JsonObject;
        if (processResult is null)
        {
            return null;
        }

        return CommandRuntime.NormalizeConsoleText(processResult["stdout"]?.GetValue<string>())
            ?? CommandRuntime.NormalizeConsoleText(processResult["stderr"]?.GetValue<string>());
    }

    private static string ResolveFramework(string? cliFramework)
    {
        if (cliFramework is not null && cliFramework.Contains("CommandLineParser", StringComparison.OrdinalIgnoreCase))
        {
            return "CommandLineParser";
        }

        return cliFramework ?? "unknown";
    }
}


namespace InSpectra.Discovery.Tool.Help.Crawling;

using InSpectra.Discovery.Tool.Help.Documents;


using System.Text.Json.Nodes;

internal sealed record CrawlResult(
    IReadOnlyDictionary<string, Document> Documents,
    IReadOnlyDictionary<string, JsonObject> Captures,
    IReadOnlyDictionary<string, CaptureSummary> CaptureSummaries);

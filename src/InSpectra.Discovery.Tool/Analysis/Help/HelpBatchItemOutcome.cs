namespace InSpectra.Discovery.Tool.Analysis.Help;

using System.Text.Json.Nodes;

internal sealed record HelpBatchItemOutcome(bool Success, string FailureSummary, JsonObject ExpectedItem);

namespace InSpectra.Discovery.Tool.Analysis.Introspection;

using InSpectra.Discovery.Tool.Analysis.Execution;

using System.Text.Json.Nodes;

internal sealed record JsonParseResult(
    bool Success,
    JsonNode? Document,
    string? ArtifactText,
    string? Error);

internal sealed record IntrospectionOutcome(
    string CommandName,
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



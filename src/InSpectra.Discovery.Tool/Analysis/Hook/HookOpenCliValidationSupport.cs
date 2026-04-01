namespace InSpectra.Discovery.Tool.Analysis.Hook;

using InSpectra.Discovery.Tool.Analysis.NonSpectre;
using InSpectra.Discovery.Tool.Infrastructure.Paths;
using InSpectra.Discovery.Tool.OpenCli.Documents;

using System.Text.Json.Nodes;

internal static class HookOpenCliValidationSupport
{
    public static bool TryWriteValidatedArtifact(JsonObject result, string outputDirectory, JsonObject openCliDocument)
    {
        if (!OpenCliDocumentValidator.TryValidateDocument(openCliDocument, out var validationError))
        {
            NonSpectreResultSupport.ApplyTerminalFailure(
                result,
                phase: "opencli",
                classification: "invalid-opencli-artifact",
                validationError ?? "Generated OpenCLI artifact is not publishable.");
            return false;
        }

        RepositoryPathResolver.WriteJsonFile(Path.Combine(outputDirectory, "opencli.json"), openCliDocument);
        result["artifacts"]!.AsObject()["opencliArtifact"] = "opencli.json";
        NonSpectreResultSupport.ApplySuccess(result, classification: "startup-hook", artifactSource: "startup-hook");
        return true;
    }
}

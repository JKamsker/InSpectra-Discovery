using System.Text.Json.Nodes;
using Xunit;

public sealed class PromotionResultArtifactLookupTests
{
    [Fact]
    public async Task BuildAsync_Prefers_Higher_Attempt_For_Matching_Artifact_Key()
    {
        var root = CreateTempDirectory();
        try
        {
            WriteResult(root, "artifact-a", CreateResult("Pkg.Tool", "1.2.3", attempt: 1, artifactName: "artifact-a"));
            WriteResult(root, "artifact-b", CreateResult("Pkg.Tool", "1.2.3", attempt: 3, artifactName: "artifact-a"));
            var lookup = await PromotionResultArtifactLookup.BuildAsync(root, CancellationToken.None);

            var found = lookup.TryResolve(CreatePlanItem("Pkg.Tool", "1.2.3", artifactName: "artifact-a"), out var entry);

            Assert.True(found);
            Assert.NotNull(entry);
            Assert.Equal(3, entry.Result["attempt"]?.GetValue<int>());
            Assert.Equal(Path.Combine(root, "artifact-b"), entry.ArtifactDirectory);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task TryResolve_Falls_Back_To_Legacy_Package_Version_Key()
    {
        var root = CreateTempDirectory();
        try
        {
            WriteResult(root, "artifact-a", CreateResult("Pkg.Tool", "1.2.3", attempt: 2, artifactName: null, command: null));
            var lookup = await PromotionResultArtifactLookup.BuildAsync(root, CancellationToken.None);

            var found = lookup.TryResolve(CreatePlanItem("Pkg.Tool", "1.2.3", artifactName: null, command: null), out var entry);

            Assert.True(found);
            Assert.NotNull(entry);
            Assert.Equal(2, entry.Result["attempt"]?.GetValue<int>());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"inspectra-promotion-lookup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private static void WriteResult(string root, string artifactDirectoryName, JsonObject result)
    {
        var artifactDirectory = Path.Combine(root, artifactDirectoryName);
        Directory.CreateDirectory(artifactDirectory);
        File.WriteAllText(Path.Combine(artifactDirectory, "result.json"), result.ToJsonString());
    }

    private static JsonObject CreateResult(string packageId, string version, int attempt, string? artifactName, string? command = null)
        => new()
        {
            ["packageId"] = packageId,
            ["version"] = version,
            ["attempt"] = attempt,
            ["artifactName"] = artifactName,
            ["command"] = command,
        };

    private static JsonObject CreatePlanItem(string packageId, string version, string? artifactName, string? command = null)
        => new()
        {
            ["packageId"] = packageId,
            ["version"] = version,
            ["artifactName"] = artifactName,
            ["command"] = command,
        };
}

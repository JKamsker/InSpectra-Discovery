namespace InSpectra.Discovery.Tool.Tests;

using Xunit;

public sealed class ArtifactRegenerationScopeTests
{
    [Fact]
    public void MatchesMetadataPath_FiltersByPackageId_And_Version()
    {
        var scope = new ArtifactRegenerationScope("Incursa.Workbench", "2026.3.30.692");
        var matchingPath = Path.Combine("index", "packages", "incursa.workbench", "2026.3.30.692", "metadata.json");
        var wrongVersionPath = Path.Combine("index", "packages", "incursa.workbench", "2026.3.30.565", "metadata.json");
        var wrongPackagePath = Path.Combine("index", "packages", "other.tool", "2026.3.30.692", "metadata.json");

        Assert.True(scope.MatchesMetadataPath(matchingPath));
        Assert.False(scope.MatchesMetadataPath(wrongVersionPath));
        Assert.False(scope.MatchesMetadataPath(wrongPackagePath));
    }

    [Fact]
    public void EnumerateMetadataPaths_Uses_Scope_Before_Regenerator_Candidate_Load()
    {
        using var tempDirectory = new TemporaryDirectory();
        var packagesRoot = Path.Combine(tempDirectory.Path, "index", "packages");
        Directory.CreateDirectory(Path.Combine(packagesRoot, "incursa.workbench", "2026.3.30.692"));
        Directory.CreateDirectory(Path.Combine(packagesRoot, "incursa.workbench", "latest"));
        Directory.CreateDirectory(Path.Combine(packagesRoot, "other.tool", "1.0.0"));

        File.WriteAllText(Path.Combine(packagesRoot, "incursa.workbench", "2026.3.30.692", "metadata.json"), "{}");
        File.WriteAllText(Path.Combine(packagesRoot, "incursa.workbench", "latest", "metadata.json"), "{}");
        File.WriteAllText(Path.Combine(packagesRoot, "other.tool", "1.0.0", "metadata.json"), "{}");

        var paths = ArtifactRegenerationMetadataPathSupport.EnumerateMetadataPaths(
            packagesRoot,
            new ArtifactRegenerationScope("Incursa.Workbench", null));

        var path = Assert.Single(paths);
        Assert.EndsWith(Path.Combine("incursa.workbench", "2026.3.30.692", "metadata.json"), path, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DocsArtifactRegenerationSettings_Rejects_Version_Without_PackageId()
    {
        var settings = new DocsArtifactRegenerationSettings
        {
            Version = "1.2.3",
        };

        var validation = settings.Validate();

        Assert.False(validation.Successful);
        Assert.Contains("--package-id", validation.Message, StringComparison.Ordinal);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"inspectra-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}


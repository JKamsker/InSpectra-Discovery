using Xunit;

public sealed class SpectreConsoleCatalogInspectorTests
{
    [Fact]
    public void ShouldInclude_CliOnly_RejectsBundledCliAssemblyWithoutDirectToolEvidence()
    {
        var detection = CreateDetection(
            hasSpectreConsoleCli: true,
            matchedPackageEntries: ["tools/net8.0/any/Spectre.Console.Cli.dll"]);

        var included = SpectreConsoleCatalogInspector.ShouldInclude(
            SpectreConsoleFilterMode.SpectreConsoleCliOnly,
            detection);

        Assert.False(included);
    }

    [Fact]
    public void ShouldInclude_CliOnly_AcceptsDirectCliDependency()
    {
        var detection = CreateDetection(
            hasSpectreConsoleCli: true,
            matchedDependencyIds: ["Spectre.Console.Cli"]);

        var included = SpectreConsoleCatalogInspector.ShouldInclude(
            SpectreConsoleFilterMode.SpectreConsoleCliOnly,
            detection);

        Assert.True(included);
    }

    [Fact]
    public void ShouldInclude_CliOnly_AcceptsToolAssemblyReference()
    {
        var detection = CreateDetection(
            hasSpectreConsoleCli: true,
            packageInspection: SpectrePackageInspection.Empty with
            {
                ToolAssembliesReferencingSpectreConsoleCli = ["tools/net8.0/any/MyTool.dll"],
            });

        var included = SpectreConsoleCatalogInspector.ShouldInclude(
            SpectreConsoleFilterMode.SpectreConsoleCliOnly,
            detection);

        Assert.True(included);
    }

    [Fact]
    public void ShouldInclude_AnySpectreConsole_PreservesBroadMatching()
    {
        var detection = CreateDetection(
            hasSpectreConsole: true,
            hasSpectreConsoleCli: false);

        var included = SpectreConsoleCatalogInspector.ShouldInclude(
            SpectreConsoleFilterMode.AnySpectreConsole,
            detection);

        Assert.True(included);
    }

    private static SpectreConsoleDetection CreateDetection(
        bool hasSpectreConsole = false,
        bool hasSpectreConsoleCli = false,
        IReadOnlyList<string>? matchedPackageEntries = null,
        IReadOnlyList<string>? matchedDependencyIds = null,
        SpectrePackageInspection? packageInspection = null)
        => new(
            HasSpectreConsole: hasSpectreConsole,
            HasSpectreConsoleCli: hasSpectreConsoleCli,
            MatchedPackageEntries: matchedPackageEntries ?? [],
            MatchedDependencyIds: matchedDependencyIds ?? [],
            PackageInspection: packageInspection ?? SpectrePackageInspection.Empty);
}

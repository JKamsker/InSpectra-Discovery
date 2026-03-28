using Xunit;

internal static class ValidatedGenericHelpFrameworkCases
{
    private static readonly IReadOnlyDictionary<string, LiveExpectations> Expectations = new Dictionary<string, LiveExpectations>(StringComparer.OrdinalIgnoreCase)
    {
        ["Husky"] = new(expectedCommands: ["add", "install"]),
        ["Paket"] = new(expectedCommands: ["add", "install"]),
        ["dotnet-serve"] = new(expectedOptions: ["--directory", "--port"]),
        ["Libplanet.Tools"] = new(expectedCommands: ["key", "tx"]),
        ["coveralls.net"] = new(expectedOptions: ["--input", "--repoToken"]),
        ["dotnet-trace"] = new(expectedCommands: ["collect", "report"]),
        ["snapx"] = new(expectedCommands: ["promote", "pack"]),
        ["Pickles.CommandLine"] = new(expectedOptions: ["--feature-directory", "--output-directory"]),
        ["dotnet-version-cli"] = new(expectedOptions: ["--output-format", "--project-file"]),
        ["MessagePack.Generator"] = new(expectedOptions: ["-input", "-output"]),
        ["Squidex.CLI"] = new(expectedCommands: ["apps", "schemas"]),
        ["DependenSee"] = new(expectedOptions: ["--help", "--include-packages"], expectedArguments: ["SOURCEFOLDER", "OUTPUTPATH"]),
    };

    public static TheoryData<ToolHelpAnalysisServiceLiveTests.LiveToolCase> LoadForLiveTests()
    {
        var repositoryRoot = RepositoryPathResolver.ResolveRepositoryRoot();
        var planPath = Path.Combine(repositoryRoot, "docs", "Plans", "validated-generic-help-frameworks.json");
        var plan = HelpBatchPlan.Load(planPath);
        var data = new TheoryData<ToolHelpAnalysisServiceLiveTests.LiveToolCase>();

        foreach (var item in plan.Items)
        {
            var framework = item.CliFramework
                ?? throw new InvalidOperationException($"Plan item '{item.PackageId} {item.Version}' is missing cliFramework.");
            var commandName = item.CommandName
                ?? throw new InvalidOperationException($"Plan item '{item.PackageId} {item.Version}' is missing command.");
            if (!Expectations.TryGetValue(item.PackageId, out var expectations))
            {
                throw new InvalidOperationException($"No live-test expectations are defined for '{item.PackageId}'.");
            }

            data.Add(new ToolHelpAnalysisServiceLiveTests.LiveToolCase(
                framework,
                item.PackageId,
                item.Version,
                commandName,
                expectations.ExpectedCommands,
                expectations.ExpectedOptions,
                expectations.ExpectedArguments));
        }

        // Cake.Tool stays outside the generic-help batch plan because the repository
        // already indexes it through the richer native OpenCLI/XMLDoc path.
        data.Add(new ToolHelpAnalysisServiceLiveTests.LiveToolCase(
            "Spectre.Console.Cli",
            "Cake.Tool",
            "6.1.0",
            "dotnet-cake",
            expectedOptions: ["--verbosity"],
            expectedArguments: ["SCRIPT"]));

        return data;
    }

    private sealed record LiveExpectations(
        IReadOnlyList<string>? expectedCommands = null,
        IReadOnlyList<string>? expectedOptions = null,
        IReadOnlyList<string>? expectedArguments = null)
    {
        public IReadOnlyList<string> ExpectedCommands { get; } = expectedCommands ?? [];
        public IReadOnlyList<string> ExpectedOptions { get; } = expectedOptions ?? [];
        public IReadOnlyList<string> ExpectedArguments { get; } = expectedArguments ?? [];
    }
}

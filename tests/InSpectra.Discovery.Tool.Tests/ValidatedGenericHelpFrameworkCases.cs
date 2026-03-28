using Xunit;

internal static class ValidatedGenericHelpFrameworkCases
{
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

            data.Add(new ToolHelpAnalysisServiceLiveTests.LiveToolCase(
                framework,
                item.PackageId,
                item.Version,
                commandName,
                item.ExpectedCommands,
                item.ExpectedOptions,
                item.ExpectedArguments));
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
}

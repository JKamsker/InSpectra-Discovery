using Xunit;

internal static class ValidatedGenericHelpFrameworkCases
{
    public static TheoryData<ToolHelpAnalysisServiceLiveTests.LiveToolCase> LoadForLiveTests()
    {
        var plan = LoadPlan();
        var data = new TheoryData<ToolHelpAnalysisServiceLiveTests.LiveToolCase>();

        foreach (var item in plan.Items)
        {
            var framework = item.CliFramework
                ?? throw new InvalidOperationException($"Plan item '{item.PackageId} {item.Version}' is missing cliFramework.");
            var commandName = item.CommandName
                ?? throw new InvalidOperationException($"Plan item '{item.PackageId} {item.Version}' is missing command.");
            if (item.ExpectedCommands.Count == 0 &&
                item.ExpectedOptions.Count == 0 &&
                item.ExpectedArguments.Count == 0)
            {
                throw new InvalidOperationException($"Plan item '{item.PackageId} {item.Version}' is missing live expectations.");
            }

            data.Add(new ToolHelpAnalysisServiceLiveTests.LiveToolCase(
                framework,
                item.PackageId,
                item.Version,
                commandName,
                item.ExpectedCommands,
                item.ExpectedOptions,
                item.ExpectedArguments));
        }

        return data;
    }

    public static TheoryData<AutoAnalysisServiceLiveTests.LiveAutoToolCase> LoadForAutoLiveTests()
    {
        var plan = LoadPlan();
        var data = new TheoryData<AutoAnalysisServiceLiveTests.LiveAutoToolCase>();

        AddCase(data, plan.Items.Single(item => string.Equals(item.PackageId, "Cake.Tool", StringComparison.OrdinalIgnoreCase)));
        AddCase(data, plan.Items.Single(item => string.Equals(item.PackageId, "Husky", StringComparison.OrdinalIgnoreCase)));

        return data;
    }

    private static HelpBatchPlan LoadPlan()
    {
        var repositoryRoot = RepositoryPathResolver.ResolveRepositoryRoot();
        var planPath = Path.Combine(repositoryRoot, "docs", "Plans", "validated-generic-help-frameworks.json");
        return HelpBatchPlan.Load(planPath);
    }

    private static void AddCase(TheoryData<AutoAnalysisServiceLiveTests.LiveAutoToolCase> data, HelpBatchItem item)
    {
        var framework = item.CliFramework
            ?? throw new InvalidOperationException($"Plan item '{item.PackageId} {item.Version}' is missing cliFramework.");
        var commandName = item.CommandName
            ?? throw new InvalidOperationException($"Plan item '{item.PackageId} {item.Version}' is missing command.");

        data.Add(new AutoAnalysisServiceLiveTests.LiveAutoToolCase(
            framework,
            item.AnalysisMode,
            item.PackageId,
            item.Version,
            commandName,
            item.ExpectedCommands,
            item.ExpectedOptions,
            item.ExpectedArguments));
    }
}

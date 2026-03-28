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
}

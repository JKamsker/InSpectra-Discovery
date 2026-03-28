using Xunit;

public sealed class ValidatedGenericHelpFrameworkCasesTests
{
    [Fact]
    public void LoadForLiveTests_Excludes_CliFx_Items()
    {
        ToolRuntime.Initialize();

        var cases = ValidatedGenericHelpFrameworkCases.LoadForLiveTests()
            .Select(entry => Assert.IsType<ToolHelpAnalysisServiceLiveTests.LiveToolCase>(Assert.Single(entry)))
            .ToArray();

        Assert.DoesNotContain(cases, testCase =>
            string.Equals(testCase.PackageId, "Husky", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(cases, testCase =>
            string.Equals(testCase.PackageId, "Paket", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LoadForAutoLiveTests_Uses_Current_Analysis_Mode_From_Plan()
    {
        ToolRuntime.Initialize();

        var cases = ValidatedGenericHelpFrameworkCases.LoadForAutoLiveTests()
            .Select(entry => Assert.IsType<AutoAnalysisServiceLiveTests.LiveAutoToolCase>(Assert.Single(entry)))
            .ToArray();

        Assert.Contains(cases, testCase =>
            string.Equals(testCase.PackageId, "Cake.Tool", StringComparison.OrdinalIgnoreCase)
            && string.Equals(testCase.ExpectedAnalysisMode, "native", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(cases, testCase =>
            string.Equals(testCase.PackageId, "Husky", StringComparison.OrdinalIgnoreCase)
            && string.Equals(testCase.ExpectedAnalysisMode, "clifx", StringComparison.OrdinalIgnoreCase));
    }
}

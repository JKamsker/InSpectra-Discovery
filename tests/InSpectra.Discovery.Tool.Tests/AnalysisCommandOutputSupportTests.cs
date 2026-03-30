using Xunit;

public sealed class AnalysisCommandOutputSupportTests
{
    [Fact]
    public void BuildSummaryRows_Includes_Command_And_Framework_From_Result_Artifact()
    {
        using var tempDirectory = new TestTemporaryDirectory();
        var resultPath = Path.Combine(tempDirectory.Path, "result.json");

        var rows = AnalysisCommandOutputSupport.BuildSummaryRows(
            "Sample.Tool",
            "1.2.3",
            resultPath,
            "success",
            new AnalysisCommandOutputSupport.AnalysisCommandResultSummary("clifx", "sample", "CliFx"),
            selectionReason: "confirmed-clifx",
            fallbackFrom: null);

        Assert.Contains(rows, row => row is { Key: "Mode", Value: "clifx" });
        Assert.Contains(rows, row => row is { Key: "Command", Value: "sample" });
        Assert.Contains(rows, row => row is { Key: "Framework", Value: "CliFx" });
        Assert.Contains(rows, row => row is { Key: "Selection reason", Value: "confirmed-clifx" });
    }
}

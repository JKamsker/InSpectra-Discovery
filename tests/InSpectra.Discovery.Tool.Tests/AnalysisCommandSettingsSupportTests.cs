using Spectre.Console.Cli;
using System.Reflection;
using Xunit;

public sealed class AnalysisCommandSettingsSupportTests
{
    [Fact]
    public void Auto_Settings_Expose_Shared_Package_And_Output_Aliases()
    {
        AssertOption<AnalysisRunAutoCommand.Settings>(nameof(AnalysisRunAutoCommand.Settings.PackageId), ["package-id", "package"], "ID");
        AssertOption<AnalysisRunAutoCommand.Settings>(nameof(AnalysisRunAutoCommand.Settings.OutputRoot), ["output-root", "output", "out"], "PATH");
        AssertOption<AnalysisRunAutoCommand.Settings>(nameof(AnalysisRunAutoCommand.Settings.BatchId), ["batch-id", "batch"], "ID");
    }

    [Fact]
    public void Help_Settings_Expose_Command_And_Framework_Aliases()
    {
        AssertOption<AnalysisRunHelpCommand.Settings>(nameof(AnalysisRunHelpCommand.Settings.Command), ["command", "tool-command"], "NAME");
        AssertOption<AnalysisRunHelpCommand.Settings>(nameof(AnalysisRunHelpCommand.Settings.CliFramework), ["cli-framework", "framework"], "NAME");
    }

    [Fact]
    public void HelpBatch_Settings_Expose_Output_And_Batch_Aliases()
    {
        AssertOption<AnalysisRunHelpBatchCommand.Settings>(nameof(AnalysisRunHelpBatchCommand.Settings.OutputRoot), ["output-root", "output", "out"], "PATH");
        AssertOption<AnalysisRunHelpBatchCommand.Settings>(nameof(AnalysisRunHelpBatchCommand.Settings.BatchId), ["batch-id", "batch"], "ID");
    }

    [Fact]
    public void Auto_Settings_Validate_Shared_Required_Fields_And_Positive_Timeouts()
    {
        var settings = new AnalysisRunAutoCommand.Settings
        {
            PackageId = "sample.tool",
            Version = "1.2.3",
            OutputRoot = "artifacts/sample",
            BatchId = "batch-001",
            Attempt = 1,
            InstallTimeoutSeconds = 300,
            AnalysisTimeoutSeconds = 600,
            CommandTimeoutSeconds = 60,
        };

        Assert.True(settings.Validate().Successful);

        settings.OutputRoot = string.Empty;
        Assert.False(settings.Validate().Successful);

        settings.OutputRoot = "artifacts/sample";
        settings.CommandTimeoutSeconds = 0;
        Assert.False(settings.Validate().Successful);
    }

    private static void AssertOption<TSettings>(string propertyName, IReadOnlyList<string> expectedLongNames, string expectedValueName)
    {
        var property = typeof(TSettings).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException($"Property '{propertyName}' was not found.");
        var attribute = property.GetCustomAttribute<CommandOptionAttribute>()
            ?? throw new InvalidOperationException($"Property '{propertyName}' is missing CommandOptionAttribute.");

        Assert.Equal(expectedLongNames, attribute.LongNames);
        Assert.Equal(expectedValueName, attribute.ValueName);
    }
}

namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Analysis.Auto.Selection;
using InSpectra.Discovery.Tool.Analysis.Tools;

using Xunit;

public sealed class AutoModeSupportTests
{
    [Fact]
    public void BuildAttemptPlan_TriesAllEligibleProviders_BeforeHelp()
    {
        var descriptor = CreateDescriptor("System.CommandLine + CommandLineParser", preferredMode: "static");

        var plan = AutoModeSupport.BuildAttemptPlan(descriptor);

        Assert.Collection(
            plan,
            attempt =>
            {
                Assert.Equal("hook", attempt.Mode);
                Assert.Equal("System.CommandLine", attempt.Framework);
            },
            attempt =>
            {
                Assert.Equal("static", attempt.Mode);
                Assert.Equal("System.CommandLine", attempt.Framework);
            },
            attempt =>
            {
                Assert.Equal("hook", attempt.Mode);
                Assert.Equal("CommandLineParser", attempt.Framework);
            },
            attempt =>
            {
                Assert.Equal("static", attempt.Mode);
                Assert.Equal("CommandLineParser", attempt.Framework);
            },
            attempt =>
            {
                Assert.Equal("help", attempt.Mode);
                Assert.Null(attempt.Framework);
            });
    }

    [Fact]
    public void BuildAttemptPlan_PrefersCliFx_BeforeLaterFrameworkSpecificFallbacks()
    {
        var descriptor = CreateDescriptor("System.CommandLine + CliFx", preferredMode: "native");

        var plan = AutoModeSupport.BuildAttemptPlan(descriptor);

        Assert.Collection(
            plan,
            attempt =>
            {
                Assert.Equal("clifx", attempt.Mode);
                Assert.Equal("CliFx", attempt.Framework);
            },
            attempt =>
            {
                Assert.Equal("hook", attempt.Mode);
                Assert.Equal("System.CommandLine", attempt.Framework);
            },
            attempt =>
            {
                Assert.Equal("static", attempt.Mode);
                Assert.Equal("System.CommandLine", attempt.Framework);
            },
            attempt =>
            {
                Assert.Equal("help", attempt.Mode);
                Assert.Null(attempt.Framework);
            });
    }

    [Fact]
    public void BuildAttemptPlan_TreatsBothCommandLineUtilsProviders_AsEligible()
    {
        var descriptor = CreateDescriptor(
            "Microsoft.Extensions.CommandLineUtils + McMaster.Extensions.CommandLineUtils",
            preferredMode: "static");

        var plan = AutoModeSupport.BuildAttemptPlan(descriptor);

        Assert.Collection(
            plan,
            attempt =>
            {
                Assert.Equal("hook", attempt.Mode);
                Assert.Equal("McMaster.Extensions.CommandLineUtils", attempt.Framework);
            },
            attempt =>
            {
                Assert.Equal("static", attempt.Mode);
                Assert.Equal("McMaster.Extensions.CommandLineUtils", attempt.Framework);
            },
            attempt =>
            {
                Assert.Equal("hook", attempt.Mode);
                Assert.Equal("Microsoft.Extensions.CommandLineUtils", attempt.Framework);
            },
            attempt =>
            {
                Assert.Equal("static", attempt.Mode);
                Assert.Equal("Microsoft.Extensions.CommandLineUtils", attempt.Framework);
            },
            attempt =>
            {
                Assert.Equal("help", attempt.Mode);
                Assert.Null(attempt.Framework);
            });
    }

    [Theory]
    [InlineData("System.CommandLine", "hook")]
    [InlineData("System.CommandLine + CommandLineParser", "hook")]
    [InlineData("CliFx + System.CommandLine", "clifx")]
    [InlineData("CommandLineParser", "hook")]
    [InlineData(null, "help")]
    public void ResolveFallbackMode_ReturnsFirstAttemptMode(string? cliFramework, string expectedMode)
    {
        var descriptor = CreateDescriptor(cliFramework, preferredMode: "native");

        var mode = AutoModeSupport.ResolveFallbackMode(descriptor);

        Assert.Equal(expectedMode, mode);
    }

    [Fact]
    public void BuildAttemptPlan_SkipsHookAttempts_ForCandidateStaticFramework()
    {
        var descriptor = new ToolDescriptor(
            "Sample.Tool",
            "1.2.3",
            "sample",
            "CommandLineParser",
            "static",
            "candidate-static-analysis-framework",
            "https://www.nuget.org/packages/Sample.Tool/1.2.3",
            "https://nuget.test/sample.tool.1.2.3.nupkg",
            "https://nuget.test/catalog/sample.tool.1.2.3.json");

        var plan = AutoModeSupport.BuildAttemptPlan(descriptor);

        Assert.Collection(
            plan,
            attempt =>
            {
                Assert.Equal("static", attempt.Mode);
                Assert.Equal("CommandLineParser", attempt.Framework);
            },
            attempt =>
            {
                Assert.Equal("help", attempt.Mode);
                Assert.Null(attempt.Framework);
            });
    }

    [Fact]
    public void BuildAttemptPlan_OnlyHooksConfirmedSubset_OfCompositeStaticFrameworks()
    {
        var descriptor = new ToolDescriptor(
            "Sample.Tool",
            "1.2.3",
            "sample",
            "System.CommandLine + CommandLineParser",
            "static",
            "confirmed-static-analysis-framework",
            "https://www.nuget.org/packages/Sample.Tool/1.2.3",
            "https://nuget.test/sample.tool.1.2.3.nupkg",
            "https://nuget.test/catalog/sample.tool.1.2.3.json",
            HookCliFramework: "System.CommandLine");

        var plan = AutoModeSupport.BuildAttemptPlan(descriptor);

        Assert.Collection(
            plan,
            attempt =>
            {
                Assert.Equal("hook", attempt.Mode);
                Assert.Equal("System.CommandLine", attempt.Framework);
            },
            attempt =>
            {
                Assert.Equal("static", attempt.Mode);
                Assert.Equal("System.CommandLine", attempt.Framework);
            },
            attempt =>
            {
                Assert.Equal("static", attempt.Mode);
                Assert.Equal("CommandLineParser", attempt.Framework);
            },
            attempt =>
            {
                Assert.Equal("help", attempt.Mode);
                Assert.Null(attempt.Framework);
            });
    }

    private static ToolDescriptor CreateDescriptor(string? cliFramework, string preferredMode)
        => new(
            "Sample.Tool",
            "1.2.3",
            "sample",
            cliFramework,
            preferredMode,
            "test",
            "https://www.nuget.org/packages/Sample.Tool/1.2.3",
            "https://nuget.test/sample.tool.1.2.3.nupkg",
            "https://nuget.test/catalog/sample.tool.1.2.3.json");
}

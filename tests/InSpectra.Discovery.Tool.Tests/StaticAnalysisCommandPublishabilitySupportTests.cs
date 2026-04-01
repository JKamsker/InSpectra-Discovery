namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.StaticAnalysis.Models;
using InSpectra.Discovery.Tool.StaticAnalysis.OpenCli;

using Xunit;

public sealed class StaticAnalysisCommandPublishabilitySupportTests
{
    [Fact]
    public void FilterPublishableCommands_ForCocona_Removes_Infrastructure_Commands_And_Keeps_User_Commands()
    {
        var commands = new Dictionary<string, StaticCommandDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            [string.Empty] = new(
                Name: null,
                Description: null,
                IsDefault: true,
                IsHidden: false,
                Values: [],
                Options: []),
            ["apply"] = new(
                Name: "apply",
                Description: null,
                IsDefault: false,
                IsHidden: false,
                Values: [],
                Options:
                [
                    new StaticOptionDefinition("configuration", null, true, false, false, "Microsoft.Extensions.Configuration.ConfigurationManager", null, null, null, []),
                    new StaticOptionDefinition("host-builder", null, true, false, false, "Microsoft.Extensions.Hosting.HostBuilder", null, null, null, []),
                ]),
            ["configureappconfiguration"] = new(
                Name: "configureappconfiguration",
                Description: null,
                IsDefault: false,
                IsHidden: false,
                Values: [],
                Options: []),
            ["createinstance"] = new(
                Name: "createinstance",
                Description: null,
                IsDefault: false,
                IsHidden: false,
                Values: [],
                Options:
                [
                    new StaticOptionDefinition("instance-type", null, true, false, false, "System.Type", null, null, null, []),
                    new StaticOptionDefinition("service-provider", null, true, false, false, "System.IServiceProvider", null, null, null, []),
                ]),
            ["dispose"] = new(
                Name: "dispose",
                Description: null,
                IsDefault: false,
                IsHidden: false,
                Values: [],
                Options: []),
            ["run"] = new(
                Name: "run",
                Description: null,
                IsDefault: false,
                IsHidden: false,
                Values: [],
                Options:
                [
                    new StaticOptionDefinition("args", null, true, true, false, "System.String[]", null, null, null, []),
                    new StaticOptionDefinition("command-types", null, true, true, false, "System.Type[]", null, null, null, []),
                    new StaticOptionDefinition("configure-options", null, false, false, false, "System.Action<Cocona.CoconaAppOptions>", null, null, null, []),
                ]),
            ["startasync"] = new(
                Name: "startasync",
                Description: null,
                IsDefault: false,
                IsHidden: false,
                Values: [],
                Options: []),
            ["generate"] = new(
                Name: "generate",
                Description: null,
                IsDefault: false,
                IsHidden: false,
                Values: [],
                Options:
                [
                    new StaticOptionDefinition("output", null, true, false, false, "System.String", null, null, null, []),
                ]),
        };

        var filtered = StaticAnalysisCommandPublishabilitySupport.FilterPublishableCommands("Cocona", commands);

        Assert.True(filtered.ContainsKey(string.Empty));
        Assert.True(filtered.ContainsKey("generate"));
        Assert.False(filtered.ContainsKey("apply"));
        Assert.False(filtered.ContainsKey("configureappconfiguration"));
        Assert.False(filtered.ContainsKey("createinstance"));
        Assert.False(filtered.ContainsKey("dispose"));
        Assert.False(filtered.ContainsKey("run"));
        Assert.False(filtered.ContainsKey("startasync"));
    }
}

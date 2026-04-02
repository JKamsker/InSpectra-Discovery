namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Analysis.Execution;
using InSpectra.Discovery.Tool.Analysis.Introspection;
using InSpectra.Discovery.Tool.Analysis.Output;

using System.Text.Json.Nodes;
using Xunit;

public sealed class IntrospectionSupportTests
{
    [Fact]
    public void ApplyOutputs_Downgrades_Invalid_OpenCli_Introspection_To_A_Terminal_Failure()
    {
        using var tempDirectory = new TestTemporaryDirectory();
        var result = ResultSupport.CreateInitialResult(
            packageId: "Demo.Tool",
            version: "1.2.3",
            batchId: "batch-001",
            attempt: 1,
            source: "unit-test",
            analyzedAt: DateTimeOffset.Parse("2026-04-02T00:00:00Z"));
        var openCliOutcome = CreateOutcome(
            commandName: "opencli",
            status: "ok",
            classification: "json-ready",
            dispositionHint: "success",
            message: null,
            artifactObject: new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["commands"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = "config",
                        ["options"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["name"] = "--set",
                                ["arguments"] = new JsonArray
                                {
                                    new JsonObject
                                    {
                                        ["name"] = "KEY=VALUE",
                                    },
                                },
                            },
                        },
                    },
                },
            });
        var xmlDocOutcome = CreateOutcome(
            commandName: "xmldoc",
            status: "failed",
            classification: "unsupported-command",
            dispositionHint: "terminal-failure",
            message: "xmldoc not supported",
            artifactObject: null);

        IntrospectionSupport.ApplyOutputs(result, tempDirectory.Path, ref openCliOutcome, xmlDocOutcome);
        IntrospectionSupport.ApplyClassification(result, openCliOutcome, xmlDocOutcome);

        Assert.Equal("terminal-failure", result["disposition"]?.GetValue<string>());
        Assert.Equal("opencli", result["phase"]?.GetValue<string>());
        Assert.Equal("invalid-opencli-artifact", result["classification"]?.GetValue<string>());
        Assert.Equal(
            "OpenCLI artifact has a non-publishable argument name 'KEY=VALUE' at '$.commands[0].options[0].arguments[0]'.",
            result["failureMessage"]?.GetValue<string>());
        Assert.Equal("invalid-output", result["introspection"]?["opencli"]?["status"]?.GetValue<string>());
        Assert.Equal("invalid-opencli-artifact", result["introspection"]?["opencli"]?["classification"]?.GetValue<string>());
        Assert.Equal("invalid-opencli-artifact", result["steps"]?["opencli"]?["classification"]?.GetValue<string>());
        Assert.Null(result["artifacts"]?["opencliArtifact"]?.GetValue<string>());
        Assert.False(File.Exists(Path.Combine(tempDirectory.Path, "opencli.json")));
    }

    private static IntrospectionOutcome CreateOutcome(
        string commandName,
        string status,
        string classification,
        string dispositionHint,
        string? message,
        JsonNode? artifactObject)
        => new(
            CommandName: commandName,
            ProcessResult: new ProcessResult(
                Status: status,
                TimedOut: false,
                ExitCode: 0,
                DurationMs: 1,
                Stdout: string.Empty,
                Stderr: string.Empty),
            Status: status,
            Classification: classification,
            DispositionHint: dispositionHint,
            Message: message,
            ArtifactObject: artifactObject,
            ArtifactText: null);
}

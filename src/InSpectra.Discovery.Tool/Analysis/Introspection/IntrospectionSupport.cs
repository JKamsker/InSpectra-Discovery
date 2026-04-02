namespace InSpectra.Discovery.Tool.Analysis.Introspection;

using InSpectra.Discovery.Tool.Infrastructure.Paths;

using InSpectra.Discovery.Tool.OpenCli.Documents;

using InSpectra.Discovery.Tool.Analysis.Execution;

using System.Text.Json.Nodes;

internal static class IntrospectionSupport
{
    public static async Task<IntrospectionOutcome> InvokeIntrospectionCommandAsync(
        string commandPath,
        IReadOnlyList<string> argumentList,
        string expectedFormat,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        var processResult = await RuntimeSupport.InvokeProcessCaptureAsync(
            commandPath,
            argumentList,
            workingDirectory,
            environment,
            timeoutSeconds,
            cancellationToken);
        var preferredMessage = RuntimeSupport.GetPreferredMessage(processResult.Stdout, processResult.Stderr);
        var classification = IntrospectionFailureClassifier.Classify(argumentList, preferredMessage);
        var parse = IntrospectionPayloadParser.TryParse(expectedFormat, processResult.Stdout);

        var status = "failed";
        var dispositionHint = "retryable-failure";
        var message = preferredMessage;
        JsonNode? artifactObject = null;
        string? artifactText = null;

        if (processResult.TimedOut)
        {
            status = "timed-out";
            if (classification is "requires-configuration" or "environment-missing-dependency" or "requires-interactive-authentication" or "requires-interactive-input" or "unsupported-platform")
            {
                dispositionHint = "terminal-failure";
            }
            else if (classification == "environment-missing-runtime")
            {
                dispositionHint = "retryable-failure";
            }
            else
            {
                classification = "timeout";
                dispositionHint = "retryable-failure";
            }

            message ??= "Command timed out.";
        }
        else if (parse.Success)
        {
            status = "ok";
            classification = string.Equals(expectedFormat, "json", StringComparison.OrdinalIgnoreCase)
                ? processResult.ExitCode == 0 ? "json-ready" : "json-ready-with-nonzero-exit"
                : processResult.ExitCode == 0 ? "xml-ready" : "xml-ready-with-nonzero-exit";
            dispositionHint = "success";
            artifactObject = parse.Document;
            artifactText = parse.ArtifactText;
            message = processResult.ExitCode == 0 ? null : preferredMessage;
        }
        else if (!string.IsNullOrWhiteSpace(classification))
        {
            status = classification == "unsupported-command" ? "unsupported" : "failed";
            dispositionHint = classification == "environment-missing-runtime" ? "retryable-failure" : "terminal-failure";
            message ??= parse.Error;
        }
        else if (processResult.ExitCode == 0)
        {
            status = "invalid-output";
            classification = string.Equals(expectedFormat, "json", StringComparison.OrdinalIgnoreCase) ? "invalid-json" : "invalid-xml";
            dispositionHint = "terminal-failure";
            message = parse.Error ?? "Command exited successfully but did not emit valid output.";
        }
        else
        {
            status = "failed";
            classification = "command-failed";
            dispositionHint = "retryable-failure";
            message ??= parse.Error;
        }

        return new IntrospectionOutcome(
            CommandName: argumentList[^1],
            ProcessResult: processResult,
            Status: status,
            Classification: classification ?? "command-failed",
            DispositionHint: dispositionHint,
            Message: message,
            ArtifactObject: artifactObject,
            ArtifactText: artifactText);
    }

    public static void ApplyOutputs(
        JsonObject result,
        string outputDirectory,
        ref IntrospectionOutcome openCliOutcome,
        IntrospectionOutcome xmlDocOutcome)
    {
        result["timings"]!.AsObject()["opencliMs"] = openCliOutcome.ProcessResult.DurationMs;
        result["timings"]!.AsObject()["xmldocMs"] = xmlDocOutcome.ProcessResult.DurationMs;

        openCliOutcome = NormalizeOpenCliOutcome(openCliOutcome);

        if (openCliOutcome.ArtifactObject is JsonObject openCliDocument)
        {
            RepositoryPathResolver.WriteJsonFile(Path.Combine(outputDirectory, "opencli.json"), openCliDocument);
            result["artifacts"]!.AsObject()["opencliArtifact"] = "opencli.json";
        }

        if (!string.IsNullOrWhiteSpace(xmlDocOutcome.ArtifactText))
        {
            RepositoryPathResolver.WriteTextFile(Path.Combine(outputDirectory, "xmldoc.xml"), xmlDocOutcome.ArtifactText);
            result["artifacts"]!.AsObject()["xmldocArtifact"] = "xmldoc.xml";
        }

        result["introspection"]!.AsObject()["opencli"] = new JsonObject
        {
            ["status"] = openCliOutcome.Status,
            ["classification"] = openCliOutcome.Classification,
            ["message"] = openCliOutcome.Message,
        };
        result["introspection"]!.AsObject()["xmldoc"] = new JsonObject
        {
            ["status"] = xmlDocOutcome.Status,
            ["classification"] = xmlDocOutcome.Classification,
            ["message"] = xmlDocOutcome.Message,
        };

        result["steps"]!.AsObject()["opencli"] = openCliOutcome.ToStepMetadata(result["artifacts"]?["opencliArtifact"]?.GetValue<string>());
        result["steps"]!.AsObject()["xmldoc"] = xmlDocOutcome.ToStepMetadata(result["artifacts"]?["xmldocArtifact"]?.GetValue<string>());
    }

    private static IntrospectionOutcome NormalizeOpenCliOutcome(IntrospectionOutcome openCliOutcome)
    {
        if (openCliOutcome.ArtifactObject is null)
        {
            return openCliOutcome;
        }

        if (openCliOutcome.ArtifactObject is not JsonObject openCliDocument)
        {
            return CreateInvalidOpenCliOutcome(openCliOutcome, "OpenCLI artifact is not a JSON object.");
        }

        OpenCliDocumentSanitizer.EnsureArtifactSource(openCliDocument, "tool-output");
        OpenCliDocumentSanitizer.Sanitize(openCliDocument);

        if (!OpenCliDocumentValidator.TryValidateDocument(openCliDocument, out var validationError))
        {
            return CreateInvalidOpenCliOutcome(
                openCliOutcome,
                validationError ?? "Generated OpenCLI artifact is not publishable.");
        }

        return openCliOutcome with
        {
            ArtifactObject = openCliDocument,
        };
    }

    private static IntrospectionOutcome CreateInvalidOpenCliOutcome(IntrospectionOutcome openCliOutcome, string message)
        => openCliOutcome with
        {
            Status = "invalid-output",
            Classification = "invalid-opencli-artifact",
            DispositionHint = "terminal-failure",
            Message = message,
            ArtifactObject = null,
            ArtifactText = null,
        };

    public static void ApplyClassification(JsonObject result, IntrospectionOutcome openCliOutcome, IntrospectionOutcome xmlDocOutcome)
    {
        var outcomes = new[] { openCliOutcome, xmlDocOutcome };
        var successfulOutcomes = outcomes.Where(outcome => outcome.Status == "ok").ToArray();
        var retryableOutcomes = outcomes.Where(outcome => outcome.Status != "ok" && outcome.DispositionHint == "retryable-failure").ToArray();
        var deterministicOutcomes = outcomes.Where(outcome => outcome.Status != "ok" && outcome.DispositionHint == "terminal-failure").ToArray();

        if (successfulOutcomes.Length == 2)
        {
            result["disposition"] = "success";
            result["retryEligible"] = false;
            result["phase"] = "complete";
            result["classification"] = "spectre-cli-confirmed";
            result["failureMessage"] = null;
            return;
        }

        if (successfulOutcomes.Length == 1 && retryableOutcomes.Length == 0)
        {
            result["disposition"] = "success";
            result["retryEligible"] = false;
            result["phase"] = "complete";
            result["classification"] = successfulOutcomes[0].CommandName == "opencli"
                ? "spectre-cli-opencli-only"
                : "spectre-cli-xmldoc-only";
            result["failureMessage"] = null;
            return;
        }

        if (retryableOutcomes.Length > 0)
        {
            var primaryFailure = retryableOutcomes[0];
            result["disposition"] = "retryable-failure";
            result["retryEligible"] = true;
            result["phase"] = primaryFailure.CommandName;
            result["classification"] = primaryFailure.Classification;
            result["failureMessage"] = primaryFailure.Message;
            return;
        }

        if (deterministicOutcomes.Length > 0)
        {
            var primaryFailure = deterministicOutcomes[0];
            result["disposition"] = "terminal-failure";
            result["retryEligible"] = false;
            result["phase"] = primaryFailure.CommandName;
            result["classification"] = primaryFailure.Classification;
            result["failureMessage"] = primaryFailure.Message;
            return;
        }

        result["disposition"] = "retryable-failure";
        result["retryEligible"] = true;
        result["phase"] = "introspection";
        result["classification"] = "introspection-unresolved";
        result["failureMessage"] = "The tool did not yield a usable introspection result.";
    }
}


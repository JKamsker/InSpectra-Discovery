namespace InSpectra.Discovery.Tool.Help.OpenCli;

using InSpectra.Discovery.Tool.Help.Inference.Descriptions;

using InSpectra.Discovery.Tool.Help.Signatures;

using InSpectra.Discovery.Tool.Help.Documents;

using System.Text.Json.Nodes;

internal sealed class OptionNodeBuilder
{
    public JsonArray? Build(string rootCommandName, string commandPath, Document? helpDocument)
    {
        if (helpDocument is null)
        {
            return null;
        }

        var optionItems = helpDocument.Options.Count > 0
            ? helpDocument.Options
            : InSpectra.Discovery.Tool.Help.Inference.Usage.UsageOptionInferenceSupport.ExtractOptions(
                rootCommandName,
                commandPath,
                helpDocument.UsageLines);
        if (optionItems.Count is not > 0)
        {
            return null;
        }

        var options = new JsonArray();
        foreach (var item in optionItems)
        {
            var signature = OptionSignatureSupport.Parse(item.Key);
            if (signature.PrimaryName is null)
            {
                continue;
            }

            var inferredArgumentRequired = OptionDescriptionInference.StartsWithRequiredPrefix(item.Description);
            var hasExplicitArgument = signature.ArgumentName is not null;
            var hasNonBooleanDefault = OptionDescriptionInference.HasNonBooleanDefault(item.Description ?? string.Empty);
            var argumentName = signature.ArgumentName
                ?? OptionDescriptionInference.InferArgumentName(signature, item.Description);
            var argumentRequired = argumentName is not null
                && (hasExplicitArgument
                    ? !hasNonBooleanDefault && (signature.ArgumentRequired || inferredArgumentRequired)
                    : inferredArgumentRequired);
            var description = OptionDescriptionInference.StartsWithRequiredPrefix(item.Description)
                ? OptionDescriptionInference.TrimLeadingRequiredPrefix(item.Description)
                : item.Description;

            var node = new JsonObject
            {
                ["name"] = signature.PrimaryName,
                ["recursive"] = false,
                ["hidden"] = false,
            };

            if (!string.IsNullOrWhiteSpace(description))
            {
                node["description"] = description;
            }

            if (signature.Aliases.Count > 0)
            {
                node["aliases"] = new JsonArray(signature.Aliases.Select(alias => JsonValue.Create(alias)).ToArray());
            }

            if (argumentName is not null)
            {
                node["arguments"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = argumentName.ToUpperInvariant(),
                        ["required"] = argumentRequired,
                        ["arity"] = BuildArity(argumentRequired ? 1 : 0),
                    },
                };
            }

            options.Add(node);
        }

        return options.Count > 0 ? options : null;
    }

    private static JsonObject BuildArity(int minimum)
        => new()
        {
            ["minimum"] = minimum,
            ["maximum"] = 1,
        };
}

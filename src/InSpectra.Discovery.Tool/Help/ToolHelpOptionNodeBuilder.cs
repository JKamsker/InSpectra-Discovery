namespace InSpectra.Discovery.Tool.Help;

using System.Text.Json.Nodes;

internal sealed class ToolHelpOptionNodeBuilder
{
    public JsonArray? Build(ToolHelpDocument? helpDocument)
    {
        if (helpDocument?.Options.Count is not > 0)
        {
            return null;
        }

        var options = new JsonArray();
        foreach (var item in helpDocument.Options)
        {
            var signature = ToolHelpOptionSignatureSupport.Parse(item.Key);
            if (signature.PrimaryName is null)
            {
                continue;
            }

            var inferredArgumentRequired = ToolHelpOptionDescriptionInference.StartsWithRequiredPrefix(item.Description);
            var hasExplicitArgument = signature.ArgumentName is not null;
            var hasNonBooleanDefault = ToolHelpOptionDescriptionInference.HasNonBooleanDefault(item.Description ?? string.Empty);
            var argumentName = signature.ArgumentName
                ?? ToolHelpOptionDescriptionInference.InferArgumentName(signature, item.Description);
            var argumentRequired = argumentName is not null
                && (hasExplicitArgument
                    ? !hasNonBooleanDefault && (signature.ArgumentRequired || inferredArgumentRequired)
                    : inferredArgumentRequired);
            var description = ToolHelpOptionDescriptionInference.StartsWithRequiredPrefix(item.Description)
                ? ToolHelpOptionDescriptionInference.TrimLeadingRequiredPrefix(item.Description)
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


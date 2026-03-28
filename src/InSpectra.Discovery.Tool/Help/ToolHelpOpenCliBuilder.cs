using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

internal sealed partial class ToolHelpOpenCliBuilder
{
    private readonly ToolHelpCommandTreeBuilder _commandTreeBuilder = new();

    public JsonObject Build(
        string commandName,
        string packageVersion,
        IReadOnlyDictionary<string, ToolHelpDocument> helpDocuments)
    {
        helpDocuments.TryGetValue(string.Empty, out var rootHelp);
        var rootCommands = new JsonArray(_commandTreeBuilder
            .Build(helpDocuments)
            .Select(node => BuildCommandNode(commandName, node, helpDocuments))
            .ToArray());

        return new JsonObject
        {
            ["opencli"] = "0.1-draft",
            ["info"] = new JsonObject
            {
                ["title"] = rootHelp?.Title ?? commandName,
                ["version"] = rootHelp?.Version ?? packageVersion,
                ["description"] = rootHelp?.ApplicationDescription ?? rootHelp?.CommandDescription,
            },
            ["x-inspectra"] = new JsonObject
            {
                ["artifactSource"] = "crawled-from-help",
                ["generator"] = "InSpectra.Discovery",
                ["helpDocumentCount"] = helpDocuments.Count,
            },
            ["options"] = BuildOptions(rootHelp),
            ["arguments"] = BuildArguments(commandName, string.Empty, rootHelp),
            ["commands"] = rootCommands,
        };
    }

    private JsonObject BuildCommandNode(
        string commandName,
        ToolHelpCommandNode commandNode,
        IReadOnlyDictionary<string, ToolHelpDocument> helpDocuments)
    {
        helpDocuments.TryGetValue(commandNode.FullName, out var helpDocument);
        var node = new JsonObject
        {
            ["name"] = commandNode.DisplayName,
            ["description"] = helpDocument?.CommandDescription ?? commandNode.Description,
            ["hidden"] = false,
        };

        var options = BuildOptions(helpDocument);
        if (options is not null)
        {
            node["options"] = options;
        }

        var arguments = BuildArguments(commandName, commandNode.FullName, helpDocument);
        if (arguments is not null)
        {
            node["arguments"] = arguments;
        }

        if (commandNode.Children.Count > 0)
        {
            node["commands"] = new JsonArray(commandNode.Children
                .Select(child => BuildCommandNode(commandName, child, helpDocuments))
                .ToArray());
        }

        return node;
    }

    private JsonArray? BuildOptions(ToolHelpDocument? helpDocument)
    {
        if (helpDocument?.Options.Count is not > 0)
        {
            return null;
        }

        var options = new JsonArray();
        foreach (var item in helpDocument.Options)
        {
            var signature = ParseOptionSignature(item.Key);
            if (signature.PrimaryName is null)
            {
                continue;
            }

            var node = new JsonObject
            {
                ["name"] = signature.PrimaryName,
                ["required"] = item.IsRequired,
                ["description"] = item.Description,
                ["recursive"] = false,
                ["hidden"] = false,
            };

            if (signature.Aliases.Count > 0)
            {
                node["aliases"] = new JsonArray(signature.Aliases.Select(alias => JsonValue.Create(alias)).ToArray());
            }

            if (signature.ArgumentName is not null)
            {
                node["arguments"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = signature.ArgumentName.ToUpperInvariant(),
                        ["required"] = signature.ArgumentRequired,
                        ["arity"] = BuildArity(signature.ArgumentRequired ? 1 : 0),
                    },
                };
            }

            options.Add(node);
        }

        return options.Count > 0 ? options : null;
    }

    private JsonArray? BuildArguments(string commandName, string commandPath, ToolHelpDocument? helpDocument)
    {
        if (helpDocument is null)
        {
            return null;
        }

        var arguments = helpDocument.Arguments.Count > 0
            ? helpDocument.Arguments
            : ExtractUsageArguments(commandName, commandPath, helpDocument.UsageLines);

        if (arguments.Count == 0)
        {
            return null;
        }

        var array = new JsonArray();
        foreach (var argument in arguments)
        {
            array.Add(new JsonObject
            {
                ["name"] = NormalizeArgumentName(argument.Key),
                ["required"] = argument.IsRequired,
                ["description"] = argument.Description,
                ["hidden"] = false,
                ["arity"] = BuildArity(argument.IsRequired ? 1 : 0),
            });
        }

        return array.Count > 0 ? array : null;
    }

    private static IReadOnlyList<ToolHelpItem> ExtractUsageArguments(
        string commandName,
        string commandPath,
        IReadOnlyList<string> usageLines)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var arguments = new List<ToolHelpItem>();

        foreach (var line in usageLines)
        {
            foreach (Match match in UsageArgumentRegex().Matches(line))
            {
                var value = match.Groups["name"].Value.Trim();
                if (string.Equals(value, "command", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(value, "options", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!seen.Add(value))
                {
                    continue;
                }

                arguments.Add(new ToolHelpItem(
                    Key: value,
                    IsRequired: !match.Value.StartsWith("[", StringComparison.Ordinal),
                    Description: null));
            }
        }

        return arguments;
    }

    private static OptionSignature ParseOptionSignature(string key)
    {
        var aliases = new List<string>();
        var placeholders = UsageArgumentRegex().Matches(key)
            .Select(match => match.Groups["name"].Value.Trim())
            .Where(value => value.Length > 0)
            .ToArray();

        foreach (var segment in key.Split(new[] { '|', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var token = OptionTokenRegex().Match(segment).Value;
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            aliases.Add(token);
        }

        var primary = aliases
            .OrderByDescending(name => name.StartsWith("--", StringComparison.Ordinal) || name.StartsWith("/", StringComparison.Ordinal))
            .ThenByDescending(name => name.Length)
            .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        return new OptionSignature(
            PrimaryName: primary,
            Aliases: aliases
                .Where(alias => !string.Equals(alias, primary, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            ArgumentName: placeholders.FirstOrDefault(),
            ArgumentRequired: !key.Contains("[", StringComparison.Ordinal));
    }

    private static JsonObject BuildArity(int minimum)
        => new()
        {
            ["minimum"] = minimum,
            ["maximum"] = 1,
        };

    private static string NormalizeArgumentName(string key)
        => key.Trim().TrimStart('[', '<').TrimEnd(']', '>').ToUpperInvariant();

    [GeneratedRegex(@"(?<option>(?:--?|/)[A-Za-z0-9\?\-]+)", RegexOptions.Compiled)]
    private static partial Regex OptionTokenRegex();

    [GeneratedRegex(@"(?<all>\[?<(?<name>[^>]+)>\]?)", RegexOptions.Compiled)]
    private static partial Regex UsageArgumentRegex();

    private sealed record OptionSignature(
        string? PrimaryName,
        IReadOnlyList<string> Aliases,
        string? ArgumentName,
        bool ArgumentRequired);
}

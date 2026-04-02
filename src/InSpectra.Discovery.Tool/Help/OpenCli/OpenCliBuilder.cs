namespace InSpectra.Discovery.Tool.Help.OpenCli;

using InSpectra.Discovery.Tool.OpenCli.Documents;

using InSpectra.Discovery.Tool.Help.Documents;

using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

internal sealed partial class OpenCliBuilder
{
    private readonly CommandTreeBuilder _commandTreeBuilder = new();
    private readonly OptionNodeBuilder _optionBuilder = new();
    private readonly ArgumentNodeBuilder _argumentBuilder = new();

    public JsonObject Build(
        string commandName,
        string packageVersion,
        IReadOnlyDictionary<string, Document> helpDocuments)
    {
        var expandedHelpDocuments = EmbeddedCommandDocumentExpansionSupport.Expand(commandName, helpDocuments);
        expandedHelpDocuments.TryGetValue(string.Empty, out var rootHelp);
        var rootCommands = new JsonArray(_commandTreeBuilder
            .Build(commandName, expandedHelpDocuments)
            .Select(node => BuildCommandNode(commandName, node, expandedHelpDocuments))
            .ToArray());
        var document = new JsonObject
        {
            ["opencli"] = "0.1-draft",
            ["info"] = BuildInfo(commandName, packageVersion, rootHelp),
            ["x-inspectra"] = new JsonObject
            {
                ["artifactSource"] = "crawled-from-help",
                ["generator"] = "InSpectra.Discovery",
                ["helpDocumentCount"] = expandedHelpDocuments.Count,
            },
            ["commands"] = rootCommands,
        };

        AddIfPresent(document, "options", _optionBuilder.Build(commandName, string.Empty, rootHelp));
        AddIfPresent(document, "arguments", _argumentBuilder.Build(commandName, string.Empty, rootHelp));
        return OpenCliDocumentSanitizer.Sanitize(document);
    }

    private JsonObject BuildCommandNode(
        string commandName,
        CommandNode commandNode,
        IReadOnlyDictionary<string, Document> helpDocuments)
    {
        helpDocuments.TryGetValue(commandNode.FullName, out var helpDocument);
        helpDocument ??= UsagePrototypeDocumentSupport.Create(commandName, commandNode.FullName, helpDocuments);
        var node = new JsonObject
        {
            ["name"] = commandNode.DisplayName,
            ["hidden"] = false,
        };

        AddIfPresent(node, "description", helpDocument?.CommandDescription ?? commandNode.Description);
        AddIfPresent(node, "options", _optionBuilder.Build(commandName, commandNode.FullName, helpDocument));
        AddIfPresent(node, "arguments", _argumentBuilder.Build(commandName, commandNode.FullName, helpDocument));

        if (commandNode.Children.Count > 0)
        {
            node["commands"] = new JsonArray(commandNode.Children
                .Select(child => BuildCommandNode(commandName, child, helpDocuments))
                .ToArray());
        }

        return node;
    }

    private static JsonObject BuildInfo(string commandName, string packageVersion, Document? rootHelp)
    {
        var parsedTitle = rootHelp?.Title;
        var parsedDescription = rootHelp?.CommandDescription ?? rootHelp?.ApplicationDescription;
        var cleanedParsedTitle = OpenCliDocumentTitleCleaner.CleanTitle(parsedTitle ?? string.Empty);
        var title = !string.IsNullOrWhiteSpace(cleanedParsedTitle)
            && !OpenCliDocumentPublishabilityInspector.LooksLikeNonPublishableTitle(cleanedParsedTitle)
                ? cleanedParsedTitle
                : commandName;
        var description = parsedDescription;

        if (!string.IsNullOrWhiteSpace(parsedTitle)
            && LooksLikeDescriptionNotTitle(parsedTitle, commandName)
            && string.IsNullOrWhiteSpace(parsedDescription))
        {
            title = commandName;
            description = parsedTitle;
        }

        var info = new JsonObject
        {
            ["title"] = title,
            ["version"] = string.IsNullOrWhiteSpace(packageVersion) ? rootHelp?.Version : packageVersion,
        };

        AddIfPresent(info, "description", description);
        return info;
    }

    private static bool LooksLikeDescriptionNotTitle(string title, string commandName)
    {
        var words = title.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 3)
        {
            return false;
        }

        if (title.IndexOf(commandName, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return false;
        }

        return DescriptionLikeTitleRegex().IsMatch(title);
    }

    private static void AddIfPresent(JsonObject target, string propertyName, JsonNode? value)
    {
        if (value is not null)
        {
            target[propertyName] = value;
        }
    }

    private static void AddIfPresent(JsonObject target, string propertyName, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            target[propertyName] = value;
        }
    }

    [GeneratedRegex(@"^(?:Handle|Manage|Deploy|Generate|Create|Build|Run|Pack|Detect|Scaffold|Determine|Upload|Download|Install|Automagic|Convert|Transform|Publish|Update|Open|Execute|Launch|Parse|Analyze|Check|Validate|Scan|Watch|Monitor|Collect|Extract|Import|Export|Apply|Process|Send|Resolve|Configure|Migrate|Synchronize|Sync|Format|Serve|Clean|Remove|Delete|Compile|Inspect|Aggregate|Map|Push|Copy|Start|Stop|Test|Verify)\w*\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex DescriptionLikeTitleRegex();
}

using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

internal static partial class OpenCliDocumentSanitizer
{
    private static readonly HashSet<string> EmptyOptionalArrayProperties = new(StringComparer.Ordinal)
    {
        "acceptedValues",
        "aliases",
        "arguments",
        "examples",
        "metadata",
        "options",
    };

    public static JsonObject Sanitize(JsonObject document)
    {
        SanitizeInfoTitle(document);
        SanitizeNode(document, arrayContext: null);
        return document;
    }

    public static void ApplyNuGetMetadata(JsonObject document, string? nugetTitle, string? nugetDescription)
    {
        if (document["info"] is not JsonObject info)
        {
            return;
        }

        var inspectra = document["x-inspectra"] as JsonObject;
        var cliTitle = info["title"]?.GetValue<string>();
        var cliDescription = info["description"]?.GetValue<string>();

        if (!string.IsNullOrWhiteSpace(nugetTitle))
        {
            var cleaned = OpenCliDocumentTitleCleaner.CleanTitle(nugetTitle);
            if (!string.IsNullOrWhiteSpace(cleaned))
            {
                info["title"] = cleaned;
            }
        }

        if (!string.IsNullOrWhiteSpace(nugetDescription))
        {
            info["description"] = nugetDescription.Trim();
        }

        if (inspectra is not null)
        {
            if (!string.IsNullOrWhiteSpace(cliTitle)
                && !string.Equals(cliTitle, info["title"]?.GetValue<string>(), StringComparison.Ordinal))
            {
                inspectra["cliParsedTitle"] = cliTitle;
            }

            if (!string.IsNullOrWhiteSpace(cliDescription)
                && !string.Equals(cliDescription, info["description"]?.GetValue<string>(), StringComparison.Ordinal))
            {
                inspectra["cliParsedDescription"] = cliDescription;
            }
        }
    }

    public static JsonObject EnsureArtifactSource(JsonObject document, string artifactSource)
    {
        if (string.IsNullOrWhiteSpace(artifactSource))
        {
            return document;
        }

        var inspectra = document["x-inspectra"] as JsonObject;
        if (inspectra is null)
        {
            inspectra = new JsonObject();
            document["x-inspectra"] = inspectra;
        }

        if (string.IsNullOrWhiteSpace(inspectra["artifactSource"]?.GetValue<string>()))
        {
            inspectra["artifactSource"] = artifactSource;
        }

        return document;
    }

    private static void SanitizeNode(JsonNode node, string? arrayContext)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj.ToArray())
            {
                if (string.Equals(arrayContext, "options", StringComparison.Ordinal)
                    && string.Equals(property.Key, "required", StringComparison.Ordinal))
                {
                    obj.Remove(property.Key);
                    continue;
                }

                if (property.Value is null)
                {
                    obj.Remove(property.Key);
                    continue;
                }

                var childArrayContext = property.Value is JsonArray ? property.Key : arrayContext;
                SanitizeNode(property.Value, childArrayContext);

                if (ShouldRemoveProperty(property.Key, property.Value))
                {
                    obj.Remove(property.Key);
                }
            }

            ScrubSandboxPaths(obj);

            if (string.Equals(arrayContext, "options", StringComparison.Ordinal))
            {
                OpenCliOptionSanitizer.NormalizeOptionObject(obj);
            }

            if (obj["options"] is JsonArray options)
            {
                OpenCliOptionSanitizer.DeduplicateSafeOptionCollisions(options);
                if (ShouldRemoveProperty("options", options))
                {
                    obj.Remove("options");
                }
            }

            return;
        }

        if (node is not JsonArray array)
        {
            return;
        }

        for (var index = array.Count - 1; index >= 0; index--)
        {
            if (array[index] is null)
            {
                array.RemoveAt(index);
                continue;
            }

            SanitizeNode(array[index]!, arrayContext);
        }
    }

    private static bool ShouldRemoveProperty(string propertyName, JsonNode value)
    {
        if (value is JsonArray array)
        {
            return array.Count == 0 && EmptyOptionalArrayProperties.Contains(propertyName);
        }

        if (value is JsonObject obj)
        {
            return obj.Count == 0 && string.Equals(propertyName, "x-inspectra", StringComparison.Ordinal);
        }

        if (value is not JsonValue jsonValue
            || !string.Equals(propertyName, "description", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            return string.IsNullOrWhiteSpace(jsonValue.GetValue<string>());
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static void ScrubSandboxPaths(JsonObject obj)
    {
        foreach (var property in obj.ToArray())
        {
            if (property.Value is JsonValue value
                && value.TryGetValue<string>(out var text)
                && text.Contains("/tmp/inspectra-", StringComparison.OrdinalIgnoreCase))
            {
                var scrubbed = SandboxPathRegex().Replace(text, "<path>");
                if (string.IsNullOrWhiteSpace(scrubbed) || string.Equals(scrubbed, "<path>", StringComparison.Ordinal))
                {
                    obj.Remove(property.Key);
                }
                else
                {
                    obj[property.Key] = scrubbed;
                }
            }
        }
    }

    private static void SanitizeInfoTitle(JsonObject document)
    {
        if (document["info"] is not JsonObject info)
        {
            return;
        }

        var title = info["title"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        var cleaned = OpenCliDocumentTitleCleaner.CleanTitle(title);
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            info.Remove("title");
        }
        else if (!string.Equals(cleaned, title, StringComparison.Ordinal))
        {
            info["title"] = cleaned;
        }
    }

    [GeneratedRegex(@"/tmp/inspectra-[^\s""'\]}>)]+", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex SandboxPathRegex();
}

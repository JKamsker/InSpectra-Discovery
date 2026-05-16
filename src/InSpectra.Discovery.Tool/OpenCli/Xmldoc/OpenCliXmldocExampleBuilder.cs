namespace InSpectra.Discovery.Tool.OpenCli.Xmldoc;

using System.Text.Json.Nodes;
using System.Xml.Linq;

internal static class OpenCliXmldocExampleBuilder
{
    public static JsonArray ConvertExamples(
        XElement commandNode,
        IReadOnlyList<string> commandPath,
        bool treatDefaultCommandAsParent = false)
    {
        var commandName = OpenCliXmldocSupport.GetAttributeValue(commandNode, "Name") ?? string.Empty;
        if (string.Equals(commandName, "__default_command", StringComparison.Ordinal) && !treatDefaultCommandAsParent)
        {
            return [];
        }

        var tokens = OpenCliXmldocSupport.GetElements(
                OpenCliXmldocSupport.GetElements(commandNode, "Examples").FirstOrDefault(),
                "Example")
            .Select(example => OpenCliXmldocSupport.GetAttributeValue(example, "commandLine")
                ?? OpenCliXmldocSupport.GetAttributeValue(example, "CommandLine"))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToArray();
        if (tokens.Length == 0)
        {
            return [];
        }

        var startSequence = treatDefaultCommandAsParent
            ? commandPath.ToArray()
            : commandPath.Count > 0 ? commandPath.ToArray() : [commandName];
        var examples = new JsonArray();
        var index = 0;

        while (index < tokens.Length)
        {
            var parts = new List<string>();
            if (StartsWithSequence(tokens, index, startSequence))
            {
                parts.AddRange(startSequence);
                index += startSequence.Length;
            }
            else
            {
                parts.Add(tokens[index]);
                index++;
            }

            while (index < tokens.Length && !StartsWithSequence(tokens, index, startSequence))
            {
                parts.Add(tokens[index]);
                index++;
            }

            var example = string.Join(" ", parts).Trim();
            if (!string.IsNullOrWhiteSpace(example))
            {
                examples.Add(example);
            }
        }

        return examples;
    }

    private static bool StartsWithSequence(IReadOnlyList<string> tokens, int index, IReadOnlyList<string> sequence)
    {
        if (sequence.Count == 0 || index + sequence.Count > tokens.Count)
        {
            return false;
        }

        for (var offset = 0; offset < sequence.Count; offset++)
        {
            if (!string.Equals(tokens[index + offset], sequence[offset], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}


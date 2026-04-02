namespace InSpectra.Discovery.Tool.Help.OpenCli;

using InSpectra.Discovery.Tool.Help.Documents;
using InSpectra.Discovery.Tool.Help.Inference.Usage;

internal static class UsagePrototypeDocumentSupport
{
    public static Document? Create(
        string rootCommandName,
        string commandPath,
        IReadOnlyDictionary<string, Document> helpDocuments)
    {
        var prototypes = helpDocuments.Values
            .SelectMany(document => UsagePrototypeSupport.ExtractLeafCommandPrototypes(
                rootCommandName,
                commandPath,
                document.UsageLines))
            .ToArray();
        if (prototypes.Length == 0)
        {
            return null;
        }

        var description = prototypes
            .Select(prototype => prototype.Description)
            .Where(description => !string.IsNullOrWhiteSpace(description))
            .OrderByDescending(description => description!.Length)
            .FirstOrDefault();
        return new Document(
            Title: null,
            Version: null,
            ApplicationDescription: null,
            CommandDescription: description,
            UsageLines: prototypes.Select(prototype => prototype.Prototype).ToArray(),
            Arguments: [],
            Options: [],
            Commands: []);
    }
}

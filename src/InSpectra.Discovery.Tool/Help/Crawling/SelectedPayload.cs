namespace InSpectra.Discovery.Tool.Help.Crawling;

using InSpectra.Discovery.Tool.Help.Documents;


internal sealed record SelectedPayload(Document? Document, string? Payload, bool IsTerminalNonHelp);

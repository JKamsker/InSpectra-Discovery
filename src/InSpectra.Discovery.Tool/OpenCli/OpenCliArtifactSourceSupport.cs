internal static class OpenCliArtifactSourceSupport
{
    public static string? InferClassification(string? artifactSource)
        => artifactSource switch
        {
            "tool-output" => "json-ready",
            "crawled-from-help" => "help-crawl",
            "crawled-from-clifx-help" => "clifx-crawl",
            "synthesized-from-xmldoc" => "xmldoc-synthesized",
            _ => null,
        };

    public static string? InferArtifactSource(string? analysisMode)
        => analysisMode switch
        {
            "native" => "tool-output",
            "help" => "crawled-from-help",
            "clifx" => "crawled-from-clifx-help",
            "xmldoc" => "synthesized-from-xmldoc",
            _ => null,
        };
}

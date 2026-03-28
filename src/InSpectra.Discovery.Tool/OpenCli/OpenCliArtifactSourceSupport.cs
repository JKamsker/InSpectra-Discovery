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

    public static string? InferAnalysisMode(string? artifactSource)
        => artifactSource switch
        {
            "tool-output" => "native",
            "crawled-from-help" => "help",
            "crawled-from-clifx-help" => "clifx",
            "synthesized-from-xmldoc" => "xmldoc",
            _ => null,
        };

    public static string? InferAnalysisModeFromClassification(string? classification)
        => classification switch
        {
            "json-ready" => "native",
            "json-ready-with-nonzero-exit" => "native",
            "help-crawl" => "help",
            "clifx-crawl" => "clifx",
            "xmldoc-synthesized" => "xmldoc",
            _ => null,
        };
}

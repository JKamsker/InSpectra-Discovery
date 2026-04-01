namespace InSpectra.Discovery.Tool.OpenCli.Artifacts;

internal static class OpenCliArtifactSourceSupport
{
    public static string? InferClassification(string? artifactSource)
        => artifactSource switch
        {
            "tool-output" => "json-ready",
            "startup-hook" => "startup-hook",
            "crawled-from-help" => "help-crawl",
            "crawled-from-clifx-help" => "clifx-crawl",
            "static-analysis" => "static-crawl",
            "synthesized-from-xmldoc" => "xmldoc-synthesized",
            _ => null,
        };

    public static string? InferArtifactSource(string? analysisMode)
        => analysisMode switch
        {
            "native" => "tool-output",
            "hook" => "startup-hook",
            "help" => "crawled-from-help",
            "clifx" => "crawled-from-clifx-help",
            "static" => "static-analysis",
            "xmldoc" => "synthesized-from-xmldoc",
            _ => null,
        };

    public static string? InferAnalysisMode(string? artifactSource)
        => artifactSource switch
        {
            "tool-output" => "native",
            "startup-hook" => "hook",
            "crawled-from-help" => "help",
            "crawled-from-clifx-help" => "clifx",
            "static-analysis" => "static",
            "synthesized-from-xmldoc" => "xmldoc",
            _ => null,
        };

    public static string? InferAnalysisModeFromClassification(string? classification)
        => classification switch
        {
            "json-ready" => "native",
            "json-ready-with-nonzero-exit" => "native",
            "startup-hook" => "hook",
            "help-crawl" => "help",
            "clifx-crawl" => "clifx",
            "static-crawl" => "static",
            "xmldoc-synthesized" => "xmldoc",
            _ => null,
        };
}


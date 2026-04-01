namespace InSpectra.Discovery.Tool.Analysis.CliFx.Execution;

using InSpectra.Discovery.Tool.Analysis.CliFx.Crawling;
using InSpectra.Discovery.Tool.Infrastructure.Commands;

internal sealed class CliFxRuntimeCompatibilityDetector
{
    public DotnetRuntimeIssue? Detect(CliFxCaptureSummary capture)
        => DotnetRuntimeCompatibilitySupport.DetectMissingFramework(
            capture.Command,
            capture.Stdout,
            capture.Stderr);
}


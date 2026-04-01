namespace InSpectra.Discovery.Tool.Analysis.Auto.Selection;

using InSpectra.Discovery.Tool.Frameworks;

using InSpectra.Discovery.Tool.Analysis.Tools;

internal static class AutoModeSupport
{
    public static IReadOnlyList<AutoAnalysisAttempt> BuildAttemptPlan(ToolDescriptor descriptor)
    {
        if (string.Equals(descriptor.PreferredAnalysisMode, "help", StringComparison.OrdinalIgnoreCase))
        {
            return [new AutoAnalysisAttempt("help", null)];
        }

        var attempts = new List<AutoAnalysisAttempt>();
        foreach (var provider in CliFrameworkProviderRegistry.ResolveAnalysisProviders(descriptor.CliFramework))
        {
            if (provider.SupportsCliFxAnalysis)
            {
                attempts.Add(new AutoAnalysisAttempt("clifx", provider.Name));
            }

            if (provider.SupportsHookAnalysis)
            {
                attempts.Add(new AutoAnalysisAttempt("hook", provider.Name));
            }

            if (provider.StaticAnalysisAdapter is not null)
            {
                attempts.Add(new AutoAnalysisAttempt("static", provider.Name));
            }
        }

        if (attempts.Count == 0)
        {
            return [new AutoAnalysisAttempt("help", null)];
        }

        attempts.Add(new AutoAnalysisAttempt("help", null));
        return attempts;
    }

    public static string ResolveFallbackMode(ToolDescriptor descriptor)
    {
        var attempts = BuildAttemptPlan(descriptor);
        return attempts.Count == 0 ? "help" : attempts[0].Mode;
    }
}

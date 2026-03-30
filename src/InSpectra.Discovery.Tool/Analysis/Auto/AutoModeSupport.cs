namespace InSpectra.Discovery.Tool.Analysis.Auto;

internal static class AutoModeSupport
{
    public static string ResolveFallbackMode(ToolDescriptor descriptor)
    {
        if (string.Equals(descriptor.PreferredAnalysisMode, "clifx", StringComparison.OrdinalIgnoreCase))
        {
            return "clifx";
        }

        if (string.Equals(descriptor.PreferredAnalysisMode, "static", StringComparison.OrdinalIgnoreCase))
        {
            return "static";
        }

        if (string.Equals(descriptor.PreferredAnalysisMode, "help", StringComparison.OrdinalIgnoreCase))
        {
            return "help";
        }

        if (CliFrameworkProviderRegistry.HasCliFxAnalysisSupport(descriptor.CliFramework))
        {
            return "clifx";
        }

        if (CliFrameworkProviderRegistry.HasStaticAnalysisSupport(descriptor.CliFramework))
        {
            return "static";
        }

        return "help";
    }
}

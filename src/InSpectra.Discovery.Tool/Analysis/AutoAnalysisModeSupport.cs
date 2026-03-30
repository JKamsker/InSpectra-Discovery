internal static class AutoAnalysisModeSupport
{
    public static string ResolveFallbackMode(ToolAnalysisDescriptor descriptor)
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

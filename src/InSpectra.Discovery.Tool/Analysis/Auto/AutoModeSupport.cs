namespace InSpectra.Discovery.Tool.Analysis.Auto;

internal static class AutoModeSupport
{
    public static string ResolveFallbackMode(ToolDescriptor descriptor)
    {
        // Hook analysis is strictly better than static for System.CommandLine tools,
        // so upgrade "static" to "hook" when the framework supports it.
        if (CliFrameworkProviderRegistry.HasHookAnalysisSupport(descriptor.CliFramework))
        {
            if (string.IsNullOrWhiteSpace(descriptor.PreferredAnalysisMode)
                || string.Equals(descriptor.PreferredAnalysisMode, "static", StringComparison.OrdinalIgnoreCase)
                || string.Equals(descriptor.PreferredAnalysisMode, "hook", StringComparison.OrdinalIgnoreCase))
            {
                return "hook";
            }
        }

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



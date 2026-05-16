internal static class CommandLineParser
{
    public static CliCommandRequest Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return new HelpCommandRequest(HelpTopic.Root);
        }

        if (IsHelpToken(args[0]))
        {
            return new HelpCommandRequest(HelpTopic.Root);
        }

        if (IsVersionToken(args[0]))
        {
            return new VersionCommandRequest();
        }

        if (args[0].StartsWith('-'))
        {
            return ParseLegacyIndexBuild(args);
        }

        return args[0] switch
        {
            "index" => ParseIndex(args[1..]),
            "filter" => ParseFilter(args[1..]),
            _ => throw new CliUsageException(
                $"Unknown command '{args[0]}'.",
                HelpTopic.Root,
                ContainsJson(args)),
        };
    }

    private static CliCommandRequest ParseLegacyIndexBuild(string[] args)
    {
        if (args.Any(IsHelpToken))
        {
            return new HelpCommandRequest(HelpTopic.IndexBuild);
        }

        return new IndexBuildCommandRequest(BootstrapOptions.Parse(args));
    }

    private static CliCommandRequest ParseIndex(string[] args)
    {
        if (args.Length == 0 || IsHelpToken(args[0]))
        {
            return new HelpCommandRequest(HelpTopic.IndexBuild);
        }

        if (!string.Equals(args[0], "build", StringComparison.OrdinalIgnoreCase))
        {
            throw new CliUsageException(
                $"Unknown command 'index {args[0]}'.",
                HelpTopic.IndexBuild,
                ContainsJson(args));
        }

        var commandArgs = args[1..];
        if (commandArgs.Any(IsHelpToken))
        {
            return new HelpCommandRequest(HelpTopic.IndexBuild);
        }

        return new IndexBuildCommandRequest(BootstrapOptions.Parse(commandArgs));
    }

    private static CliCommandRequest ParseFilter(string[] args)
    {
        if (args.Length == 0 || IsHelpToken(args[0]))
        {
            return new HelpCommandRequest(HelpTopic.FilterSpectreConsole);
        }

        if (!string.Equals(args[0], "spectre-console", StringComparison.OrdinalIgnoreCase))
        {
            throw new CliUsageException(
                $"Unknown command 'filter {args[0]}'.",
                HelpTopic.FilterSpectreConsole,
                ContainsJson(args));
        }

        var commandArgs = args[1..];
        if (commandArgs.Any(IsHelpToken))
        {
            return new HelpCommandRequest(HelpTopic.FilterSpectreConsole);
        }

        return new FilterSpectreConsoleCommandRequest(SpectreConsoleFilterOptions.Parse(commandArgs));
    }

    private static bool ContainsJson(IEnumerable<string> args)
        => args.Any(arg => string.Equals(arg, "--json", StringComparison.OrdinalIgnoreCase));

    private static bool IsHelpToken(string arg)
        => string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase)
            || string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase);

    private static bool IsVersionToken(string arg)
        => string.Equals(arg, "--version", StringComparison.OrdinalIgnoreCase)
            || string.Equals(arg, "-V", StringComparison.OrdinalIgnoreCase);
}

internal enum HelpTopic
{
    Root,
    IndexBuild,
    Filter,
    FilterSpectreConsole,
    FilterSpectreConsoleCli,
}

internal abstract record CliCommandRequest(bool JsonRequested);

internal sealed record HelpCommandRequest(HelpTopic Topic) : CliCommandRequest(false);

internal sealed record VersionCommandRequest() : CliCommandRequest(false);

internal sealed record IndexBuildCommandRequest(BootstrapOptions Options) : CliCommandRequest(Options.Json);

internal sealed record FilterSpectreConsoleCommandRequest(SpectreConsoleFilterOptions Options) : CliCommandRequest(Options.Json);

internal sealed class CliUsageException : Exception
{
    public CliUsageException(string message, HelpTopic topic, bool jsonRequested)
        : base(message)
    {
        Topic = topic;
        JsonRequested = jsonRequested;
    }

    public HelpTopic Topic { get; }

    public bool JsonRequested { get; }
}

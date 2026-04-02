namespace InSpectra.Discovery.Tool.Help.Crawling;


internal sealed record CaptureSummary(
    string Command,
    string? HelpInvocation,
    bool Parsed,
    bool TerminalNonHelp,
    bool TimedOut,
    int? ExitCode,
    string? Stdout,
    string? Stderr,
    bool OutputLimitExceeded = false);

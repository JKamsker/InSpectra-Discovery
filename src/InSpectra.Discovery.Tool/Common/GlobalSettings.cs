using Spectre.Console.Cli;

internal abstract class GlobalSettings : CommandSettings
{
    [CommandOption("--json")]
    public bool Json { get; set; }

    [CommandOption("--repo-root <PATH>")]
    public string? RepoRoot { get; set; }

    [CommandOption("-v|--verbose")]
    public bool Verbose { get; set; }

    [CommandOption("--no-color")]
    public bool NoColor { get; set; }
}

using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

internal sealed class CatalogDeltaDiscoverCommand : AsyncCommand<CatalogDeltaDiscoverCommand.Settings>
{
    private readonly CatalogCommandService _service = new();

    public sealed class Settings : GlobalSettings
    {
        [CommandOption("--current <PATH>")]
        [DefaultValue(IndexDeltaOptions.DefaultCurrentSnapshotPath)]
        public string CurrentSnapshotPath { get; set; } = IndexDeltaOptions.DefaultCurrentSnapshotPath;

        [CommandOption("--output <PATH>")]
        [DefaultValue(IndexDeltaOptions.DefaultDeltaOutputPath)]
        public string DeltaOutputPath { get; set; } = IndexDeltaOptions.DefaultDeltaOutputPath;

        [CommandOption("--cursor <PATH>")]
        [DefaultValue(IndexDeltaOptions.DefaultCursorStatePath)]
        public string CursorStatePath { get; set; } = IndexDeltaOptions.DefaultCursorStatePath;

        [CommandOption("--service-index <URL>")]
        [DefaultValue(BootstrapOptions.DefaultServiceIndexUrl)]
        public string ServiceIndexUrl { get; set; } = BootstrapOptions.DefaultServiceIndexUrl;

        [CommandOption("--concurrency <NUMBER>")]
        [DefaultValue(12)]
        public int Concurrency { get; set; } = 12;

        [CommandOption("--overlap-minutes <NUMBER>")]
        [DefaultValue(30)]
        public int OverlapMinutes { get; set; } = 30;

        [CommandOption("--seed-cursor-utc <TIMESTAMP>")]
        public DateTimeOffset? SeedCursorUtc { get; set; }

        public override ValidationResult Validate()
            => Concurrency > 0 && OverlapMinutes >= 0
                ? ValidationResult.Success()
                : ValidationResult.Error("`--concurrency` must be positive and `--overlap-minutes` must be non-negative.");
    }

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        => _service.RunDeltaDiscoverAsync(
            new IndexDeltaOptions
            {
                Json = settings.Json,
                CurrentSnapshotPath = settings.CurrentSnapshotPath,
                DeltaOutputPath = settings.DeltaOutputPath,
                CursorStatePath = settings.CursorStatePath,
                ServiceIndexUrl = settings.ServiceIndexUrl,
                Concurrency = settings.Concurrency,
                OverlapMinutes = settings.OverlapMinutes,
                SeedCursorUtc = settings.SeedCursorUtc?.ToUniversalTime(),
            },
            cancellationToken);
}

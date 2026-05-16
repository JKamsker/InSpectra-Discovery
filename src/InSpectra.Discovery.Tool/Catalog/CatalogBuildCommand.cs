namespace InSpectra.Discovery.Tool.Catalog;

using InSpectra.Discovery.Tool.Catalog.Indexing;

using InSpectra.Discovery.Tool.Infrastructure.Settings;

using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

internal sealed class CatalogBuildCommand : AsyncCommand<CatalogBuildCommand.Settings>
{
    private readonly CatalogCommandService _service;

    public CatalogBuildCommand(CatalogCommandService service)
    {
        _service = service;
    }

    public sealed class Settings : GlobalSettings
    {
        [CommandOption("--output <PATH>")]
        [DefaultValue(BootstrapOptions.DefaultOutputPath)]
        public string OutputPath { get; set; } = BootstrapOptions.DefaultOutputPath;

        [CommandOption("--prefix-alphabet <CHARS>")]
        [DefaultValue(BootstrapOptions.DefaultPrefixAlphabet)]
        public string PrefixAlphabet { get; set; } = BootstrapOptions.DefaultPrefixAlphabet;

        [CommandOption("--service-index <URL>")]
        [DefaultValue(BootstrapOptions.DefaultServiceIndexUrl)]
        public string ServiceIndexUrl { get; set; } = BootstrapOptions.DefaultServiceIndexUrl;

        [CommandOption("--page-size <NUMBER>")]
        [DefaultValue(1000)]
        public int PageSize { get; set; } = 1000;

        [CommandOption("--concurrency <NUMBER>")]
        [DefaultValue(12)]
        public int MetadataConcurrency { get; set; } = 12;

        public override ValidationResult Validate()
            => PageSize > 0 && MetadataConcurrency > 0
                ? ValidationResult.Success()
                : ValidationResult.Error("`--page-size` and `--concurrency` must be positive integers.");
    }

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        => _service.RunBuildAsync(
            new BootstrapOptions
            {
                Json = settings.Json,
                OutputPath = settings.OutputPath,
                PrefixAlphabet = settings.PrefixAlphabet,
                ServiceIndexUrl = settings.ServiceIndexUrl,
                PageSize = settings.PageSize,
                MetadataConcurrency = settings.MetadataConcurrency,
            },
            cancellationToken);
}



using System.Net;
using System.Text.Json;

var exitCode = await RunAsync(args);
return exitCode;

static async Task<int> RunAsync(string[] args)
{
    using var cancellationSource = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        cancellationSource.Cancel();
    };

    var output = new CommandOutput(Console.Out, Console.Error);
    var jsonRequested = args.Any(arg => string.Equals(arg, "--json", StringComparison.OrdinalIgnoreCase));

    try
    {
        var request = CommandLineParser.Parse(args);
        switch (request)
        {
            case HelpCommandRequest help:
                output.WriteHelp(help.Topic);
                return 0;
            case VersionCommandRequest:
                output.WriteVersion();
                return 0;
        }

        using var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
        };

        using var httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(90),
        };

        var apiClient = new NuGetApiClient(httpClient);

        return request switch
        {
            IndexBuildCommandRequest build => await RunIndexBuildAsync(apiClient, output, build.Options, cancellationSource.Token),
            IndexDeltaCommandRequest delta => await RunIndexDeltaAsync(apiClient, output, delta.Options, cancellationSource.Token),
            FilterSpectreConsoleCommandRequest filter => await RunSpectreFilterAsync(apiClient, output, filter.Options, cancellationSource.Token),
            _ => throw new InvalidOperationException($"Unsupported command type '{request.GetType().Name}'."),
        };
    }
    catch (CliUsageException ex)
    {
        return await output.WriteUsageErrorAsync(ex, cancellationSource.Token);
    }
    catch (OperationCanceledException)
    {
        return await output.WriteErrorAsync("canceled", "Operation canceled.", 10, jsonRequested, cancellationSource.Token);
    }
    catch (FileNotFoundException ex)
    {
        return await output.WriteErrorAsync("not-found", ex.Message, 5, jsonRequested, cancellationSource.Token);
    }
    catch (Exception ex)
    {
        return await output.WriteErrorAsync("error", ex.Message, 1, jsonRequested, cancellationSource.Token, ex);
    }
}

static async Task<int> RunIndexBuildAsync(
    NuGetApiClient apiClient,
    CommandOutput output,
    BootstrapOptions options,
    CancellationToken cancellationToken)
{
    var bootstrapper = new CurrentDotnetToolIndexBootstrapper(apiClient);
    var snapshot = await bootstrapper.RunAsync(
        options,
        options.Json ? null : output.WriteProgress,
        cancellationToken);

    var outputPath = Path.GetFullPath(options.OutputPath);
    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

    await using var outputStream = File.Create(outputPath);
    await JsonSerializer.SerializeAsync(outputStream, snapshot, JsonOptions.Default, cancellationToken);

    return await output.WriteSuccessAsync(
        new IndexBuildCommandSummary(
            Command: "index build",
            OutputPath: outputPath,
            PackageCount: snapshot.Packages.Count,
            SortOrder: snapshot.Source.SortOrder),
        [
            new SummaryRow("Command", "index build"),
            new SummaryRow("Packages", snapshot.Packages.Count.ToString()),
            new SummaryRow("Sort order", snapshot.Source.SortOrder),
            new SummaryRow("Output", outputPath),
        ],
        options.Json,
        cancellationToken);
}

static async Task<int> RunSpectreFilterAsync(
    NuGetApiClient apiClient,
    CommandOutput output,
    SpectreConsoleFilterOptions options,
    CancellationToken cancellationToken)
{
    var filter = new SpectreConsoleCatalogFilter(apiClient);
    var snapshot = await filter.RunAsync(
        options,
        options.Json ? null : output.WriteProgress,
        cancellationToken);

    var outputPath = Path.GetFullPath(options.OutputPath);
    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

    await using var outputStream = File.Create(outputPath);
    await JsonSerializer.SerializeAsync(outputStream, snapshot, JsonOptions.Default, cancellationToken);

    return await output.WriteSuccessAsync(
        new SpectreConsoleFilterCommandSummary(
            Command: options.CommandName,
            InputPath: snapshot.InputPath,
            OutputPath: outputPath,
            ScannedPackageCount: snapshot.ScannedPackageCount,
            MatchedPackageCount: snapshot.PackageCount),
        [
            new SummaryRow("Command", options.CommandName),
            new SummaryRow("Input", snapshot.InputPath),
            new SummaryRow("Scanned", snapshot.ScannedPackageCount.ToString()),
            new SummaryRow("Matched", snapshot.PackageCount.ToString()),
            new SummaryRow("Output", outputPath),
        ],
        options.Json,
        cancellationToken);
}

static async Task<int> RunIndexDeltaAsync(
    NuGetApiClient apiClient,
    CommandOutput output,
    IndexDeltaOptions options,
    CancellationToken cancellationToken)
{
    var discoverer = new DotnetToolCatalogDeltaDiscoverer(apiClient);
    var computation = await discoverer.RunAsync(
        options,
        options.Json ? null : output.WriteProgress,
        cancellationToken);

    var currentSnapshotPath = Path.GetFullPath(options.CurrentSnapshotPath);
    var deltaOutputPath = Path.GetFullPath(options.DeltaOutputPath);
    var cursorStatePath = Path.GetFullPath(options.CursorStatePath);

    await WriteJsonFileAsync(currentSnapshotPath, computation.UpdatedCurrentSnapshot, cancellationToken);
    await WriteJsonFileAsync(deltaOutputPath, computation.Delta, cancellationToken);
    await WriteJsonFileAsync(cursorStatePath, computation.CursorState, cancellationToken);

    return await output.WriteSuccessAsync(
        new IndexDeltaCommandSummary(
            Command: "index delta",
            CurrentSnapshotPath: currentSnapshotPath,
            DeltaOutputPath: deltaOutputPath,
            CursorStatePath: cursorStatePath,
            CatalogLeafCount: computation.Delta.CatalogLeafCount,
            AffectedPackageCount: computation.Delta.AffectedPackageCount,
            ChangedPackageCount: computation.Delta.ChangedPackageCount,
            CursorStartUtc: computation.Delta.CursorStartUtc,
            CursorEndUtc: computation.Delta.CursorEndUtc),
        [
            new SummaryRow("Command", "index delta"),
            new SummaryRow("Cursor start", computation.Delta.CursorStartUtc.ToString("O")),
            new SummaryRow("Cursor end", computation.Delta.CursorEndUtc.ToString("O")),
            new SummaryRow("Catalog leaves", computation.Delta.CatalogLeafCount.ToString()),
            new SummaryRow("Affected packages", computation.Delta.AffectedPackageCount.ToString()),
            new SummaryRow("Changed packages", computation.Delta.ChangedPackageCount.ToString()),
            new SummaryRow("Current snapshot", currentSnapshotPath),
            new SummaryRow("Delta output", deltaOutputPath),
            new SummaryRow("Cursor state", cursorStatePath),
        ],
        options.Json,
        cancellationToken);
}

static async Task WriteJsonFileAsync<T>(string outputPath, T value, CancellationToken cancellationToken)
{
    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
    await using var outputStream = File.Create(outputPath);
    await JsonSerializer.SerializeAsync(outputStream, value, JsonOptions.Default, cancellationToken);
}

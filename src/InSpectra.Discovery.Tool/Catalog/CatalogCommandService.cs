using System.Text.Json;

internal sealed class CatalogCommandService
{
    public async Task<int> RunBuildAsync(BootstrapOptions options, CancellationToken cancellationToken)
    {
        var output = Runtime.CreateOutput();
        using var scope = Runtime.CreateNuGetApiClientScope();
        var bootstrapper = new CurrentDotnetToolIndexBootstrapper(scope.Client);
        var snapshot = await bootstrapper.RunAsync(
            options,
            options.Json ? null : output.WriteProgress,
            cancellationToken);

        var outputPath = Path.GetFullPath(options.OutputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        await using var outputStream = File.Create(outputPath);
        await JsonSerializer.SerializeAsync(outputStream, snapshot, JsonOptions.RepositoryFiles, cancellationToken);

        return await output.WriteSuccessAsync(
            new IndexBuildCommandSummary(
                Command: "catalog build",
                OutputPath: outputPath,
                PackageCount: snapshot.Packages.Count,
                SortOrder: snapshot.Source.SortOrder),
            [
                new SummaryRow("Command", "catalog build"),
                new SummaryRow("Packages", snapshot.Packages.Count.ToString()),
                new SummaryRow("Sort order", snapshot.Source.SortOrder),
                new SummaryRow("Output", outputPath),
            ],
            options.Json,
            cancellationToken);
    }

    public async Task<int> RunDeltaDiscoverAsync(IndexDeltaOptions options, CancellationToken cancellationToken)
    {
        var output = Runtime.CreateOutput();
        using var scope = Runtime.CreateNuGetApiClientScope();
        var discoverer = new DotnetToolCatalogDeltaDiscoverer(scope.Client);
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
                Command: "catalog delta discover",
                CurrentSnapshotPath: currentSnapshotPath,
                DeltaOutputPath: deltaOutputPath,
                CursorStatePath: cursorStatePath,
                CatalogLeafCount: computation.Delta.CatalogLeafCount,
                AffectedPackageCount: computation.Delta.AffectedPackageCount,
                ChangedPackageCount: computation.Delta.ChangedPackageCount,
                CursorStartUtc: computation.Delta.CursorStartUtc,
                CursorEndUtc: computation.Delta.CursorEndUtc),
            [
                new SummaryRow("Command", "catalog delta discover"),
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

    public async Task<int> RunDeltaQueueSpectreCliAsync(IndexDeltaSpectreConsoleCliOptions options, CancellationToken cancellationToken)
    {
        var output = Runtime.CreateOutput();
        using var scope = Runtime.CreateNuGetApiClientScope();
        var builder = new SpectreConsoleCliDeltaQueueBuilder(scope.Client);
        var computation = await builder.RunAsync(
            options,
            options.Json ? null : output.WriteProgress,
            cancellationToken);

        var outputDeltaPath = Path.GetFullPath(options.OutputDeltaPath);
        var queueOutputPath = Path.GetFullPath(options.QueueOutputPath);

        await WriteJsonFileAsync(outputDeltaPath, computation.Delta, cancellationToken);
        await WriteJsonFileAsync(queueOutputPath, computation.Queue, cancellationToken);

        return await output.WriteSuccessAsync(
            new IndexDeltaSpectreConsoleCliCommandSummary(
                Command: "catalog delta queue-spectre-cli",
                InputDeltaPath: Path.GetFullPath(options.InputDeltaPath),
                OutputDeltaPath: outputDeltaPath,
                QueueOutputPath: queueOutputPath,
                ScannedChangeCount: computation.Delta.ScannedChangeCount,
                MatchedPackageCount: computation.Delta.PackageCount,
                QueueCount: computation.Queue.ItemCount),
            [
                new SummaryRow("Command", "catalog delta queue-spectre-cli"),
                new SummaryRow("Input delta", Path.GetFullPath(options.InputDeltaPath)),
                new SummaryRow("Scanned changes", computation.Delta.ScannedChangeCount.ToString()),
                new SummaryRow("Matched subset", computation.Delta.PackageCount.ToString()),
                new SummaryRow("Queued current", computation.Queue.ItemCount.ToString()),
                new SummaryRow("Output delta", outputDeltaPath),
                new SummaryRow("Queue output", queueOutputPath),
            ],
            options.Json,
            cancellationToken);
    }

    public async Task<int> RunDeltaQueueAllToolsAsync(IndexDeltaAllToolsOptions options, CancellationToken cancellationToken)
    {
        var output = Runtime.CreateOutput();
        var builder = new DotnetToolDeltaQueueBuilder();
        var computation = await builder.RunAsync(
            options,
            options.Json ? null : output.WriteProgress,
            cancellationToken);

        var outputDeltaPath = Path.GetFullPath(options.OutputDeltaPath);
        var queueOutputPath = Path.GetFullPath(options.QueueOutputPath);

        await WriteJsonFileAsync(outputDeltaPath, computation.Delta, cancellationToken);
        await WriteJsonFileAsync(queueOutputPath, computation.Queue, cancellationToken);

        return await output.WriteSuccessAsync(
            new IndexDeltaAllToolsCommandSummary(
                "catalog delta queue-all-tools",
                Path.GetFullPath(options.InputDeltaPath),
                outputDeltaPath,
                queueOutputPath,
                computation.Delta.ScannedChangeCount,
                computation.Delta.PackageCount,
                computation.Queue.ItemCount),
            [
                new SummaryRow("Command", "catalog delta queue-all-tools"),
                new SummaryRow("Input delta", Path.GetFullPath(options.InputDeltaPath)),
                new SummaryRow("Scanned changes", computation.Delta.ScannedChangeCount.ToString()),
                new SummaryRow("Queued current", computation.Queue.ItemCount.ToString()),
                new SummaryRow("Output delta", outputDeltaPath),
                new SummaryRow("Queue output", queueOutputPath),
            ],
            options.Json,
            cancellationToken);
    }

    public async Task<int> RunFilterAsync(SpectreConsoleFilterOptions options, CancellationToken cancellationToken)
    {
        var output = Runtime.CreateOutput();
        using var scope = Runtime.CreateNuGetApiClientScope();
        var filter = new SpectreConsoleCatalogFilter(scope.Client);
        var snapshot = await filter.RunAsync(
            options,
            options.Json ? null : output.WriteProgress,
            cancellationToken);

        var outputPath = Path.GetFullPath(options.OutputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        await using var outputStream = File.Create(outputPath);
        await JsonSerializer.SerializeAsync(outputStream, snapshot, JsonOptions.RepositoryFiles, cancellationToken);

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

    public async Task<int> RunCliFxFilterAsync(CliFxFilterOptions options, CancellationToken cancellationToken)
    {
        var output = Runtime.CreateOutput();
        using var scope = Runtime.CreateNuGetApiClientScope();
        var filter = new CliFxCatalogFilter(scope.Client);
        var snapshot = await filter.RunAsync(
            options,
            options.Json ? null : output.WriteProgress,
            cancellationToken);

        var outputPath = Path.GetFullPath(options.OutputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        await using var outputStream = File.Create(outputPath);
        await JsonSerializer.SerializeAsync(outputStream, snapshot, JsonOptions.RepositoryFiles, cancellationToken);

        return await output.WriteSuccessAsync(
            new
            {
                command = CliFxFilterOptions.CommandName,
                inputPath = snapshot.InputPath,
                outputPath,
                scannedPackageCount = snapshot.ScannedPackageCount,
                matchedPackageCount = snapshot.PackageCount,
            },
            [
                new SummaryRow("Command", CliFxFilterOptions.CommandName),
                new SummaryRow("Input", snapshot.InputPath),
                new SummaryRow("Scanned", snapshot.ScannedPackageCount.ToString()),
                new SummaryRow("Matched", snapshot.PackageCount.ToString()),
                new SummaryRow("Output", outputPath),
            ],
            options.Json,
            cancellationToken);
    }

    private static async Task WriteJsonFileAsync<T>(string outputPath, T value, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await using var outputStream = File.Create(outputPath);
        await JsonSerializer.SerializeAsync(outputStream, value, JsonOptions.RepositoryFiles, cancellationToken);
    }
}

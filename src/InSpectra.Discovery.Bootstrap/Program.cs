using System.Net;
using System.Text.Json;

var exitCode = await RunAsync(args);
return exitCode;

static async Task<int> RunAsync(string[] args)
{
    BootstrapOptions options;

    try
    {
        options = BootstrapOptions.Parse(args);
    }
    catch (ArgumentException ex)
    {
        Console.Error.WriteLine(ex.Message);
        BootstrapOptions.WriteUsage(Console.Error);
        return 1;
    }

    using var cancellationSource = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        cancellationSource.Cancel();
    };

    using var handler = new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.All,
    };

    using var httpClient = new HttpClient(handler)
    {
        Timeout = TimeSpan.FromSeconds(90),
    };

    var apiClient = new NuGetApiClient(httpClient);
    var bootstrapper = new CurrentDotnetToolIndexBootstrapper(apiClient);

    try
    {
        var snapshot = await bootstrapper.RunAsync(options, cancellationSource.Token);
        var outputPath = Path.GetFullPath(options.OutputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        await using var outputStream = File.Create(outputPath);
        await JsonSerializer.SerializeAsync(outputStream, snapshot, JsonOptions.Default, cancellationSource.Token);
        Console.WriteLine($"Wrote {snapshot.Packages.Count} packages to {outputPath}");
        return 0;
    }
    catch (OperationCanceledException)
    {
        Console.Error.WriteLine("Bootstrap canceled.");
        return 1;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex.ToString());
        return 1;
    }
}

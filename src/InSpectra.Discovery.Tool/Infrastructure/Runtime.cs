namespace InSpectra.Discovery.Tool.Infrastructure;

using System.Net;

internal static class Runtime
{
    private static readonly CancellationTokenSource CancellationSource = new();
    private static int _initialized;

    public static CancellationToken CancellationToken => CancellationSource.Token;

    public static void Initialize()
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 1)
        {
            return;
        }

        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            CancellationSource.Cancel();
        };
    }

    public static CommandOutput CreateOutput()
        => new(Console.Out, Console.Error);

    public static NuGetApiClientScope CreateNuGetApiClientScope()
        => new();
}

internal sealed class NuGetApiClientScope : IDisposable
{
    private readonly HttpClientHandler _handler;
    private readonly HttpClient _httpClient;

    public NuGetApiClientScope()
    {
        _handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
        };

        _httpClient = new HttpClient(_handler)
        {
            Timeout = TimeSpan.FromSeconds(90),
        };

        Client = new NuGetApiClient(_httpClient);
    }

    public NuGetApiClient Client { get; }

    public void Dispose()
    {
        _httpClient.Dispose();
        _handler.Dispose();
    }
}

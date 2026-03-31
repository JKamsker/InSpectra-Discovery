namespace InSpectra.Discovery.Tool.App.Composition;

using InSpectra.Discovery.Tool.Infrastructure.Host;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Runtime.Initialize();
        var output = Runtime.CreateOutput();
        var jsonRequested = args.Any(arg => string.Equals(arg, "--json", StringComparison.OrdinalIgnoreCase));

        try
        {
            return await CliApplication.Create().RunAsync(args);
        }
        catch (OperationCanceledException)
        {
            return await output.WriteErrorAsync("canceled", "Operation canceled.", 10, jsonRequested, Runtime.CancellationToken);
        }
        catch (FileNotFoundException ex)
        {
            return await output.WriteErrorAsync("not-found", ex.Message, 5, jsonRequested, Runtime.CancellationToken);
        }
        catch (Exception ex)
        {
            return await output.WriteErrorAsync("error", ex.Message, 1, jsonRequested, Runtime.CancellationToken, ex);
        }
    }
}



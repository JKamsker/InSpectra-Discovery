namespace InSpectra.Discovery.Tool.App.Machine;

using InSpectra.Discovery.Tool.Infrastructure.Json;

using System.Text.Json;

internal sealed class CommandOutput
{
    private readonly TextWriter _stdout;
    private readonly TextWriter _stderr;

    public CommandOutput(TextWriter stdout, TextWriter stderr)
    {
        _stdout = stdout;
        _stderr = stderr;
    }

    public void WriteProgress(string message)
        => _stderr.WriteLine(message);

    public async Task<int> WriteSuccessAsync<T>(
        T data,
        IReadOnlyList<SummaryRow> rows,
        bool json,
        CancellationToken cancellationToken)
    {
        if (json)
        {
            await WriteJsonAsync(new MachineEnvelope<T>(
                Ok: true,
                Data: data,
                Error: null,
                Meta: new MachineMeta(1)), cancellationToken);

            return 0;
        }

        WriteSummary(rows);
        return 0;
    }

    public async Task<int> WriteErrorAsync(
        string kind,
        string message,
        int exitCode,
        bool json,
        CancellationToken cancellationToken,
        Exception? exception = null)
    {
        if (json)
        {
            await WriteJsonAsync(new MachineEnvelope<object>(
                Ok: false,
                Data: null,
                Error: new MachineError(kind, message),
                Meta: new MachineMeta(1)), cancellationToken);

            return exitCode;
        }

        _stderr.WriteLine(message);
        if (exception is not null)
        {
            _stderr.WriteLine(exception.ToString());
        }

        return exitCode;
    }

    private void WriteSummary(IReadOnlyList<SummaryRow> rows)
    {
        var keyWidth = rows.Max(row => row.Key.Length);
        foreach (var row in rows)
        {
            _stdout.WriteLine($"{row.Key.PadRight(keyWidth)} : {row.Value}");
        }
    }

    private static async Task WriteJsonAsync<T>(MachineEnvelope<T> envelope, CancellationToken cancellationToken)
    {
        await using var stdout = Console.OpenStandardOutput();
        await JsonSerializer.SerializeAsync(stdout, envelope, JsonOptions.Default, cancellationToken);
        await stdout.WriteAsync(new byte[] { (byte)'\n' }, cancellationToken);
    }
}


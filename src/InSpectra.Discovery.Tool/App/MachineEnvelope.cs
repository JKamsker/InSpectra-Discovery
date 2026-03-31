namespace InSpectra.Discovery.Tool.App;

internal sealed record MachineEnvelope<T>(
    bool Ok,
    T? Data,
    MachineError? Error,
    MachineMeta Meta);

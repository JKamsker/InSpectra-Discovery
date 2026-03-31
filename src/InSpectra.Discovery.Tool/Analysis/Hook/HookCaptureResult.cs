namespace InSpectra.Discovery.Tool.Analysis.Hook;

using System.Text.Json.Serialization;

internal sealed class HookCaptureResult
{
    [JsonPropertyName("captureVersion")]
    public int CaptureVersion { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("systemCommandLineVersion")]
    public string? SystemCommandLineVersion { get; set; }

    [JsonPropertyName("patchTarget")]
    public string? PatchTarget { get; set; }

    [JsonPropertyName("root")]
    public HookCapturedCommand? Root { get; set; }
}

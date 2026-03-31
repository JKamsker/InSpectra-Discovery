namespace InSpectra.Discovery.Tool.Analysis.Hook;

using System.Text.Json.Serialization;

internal sealed class HookCapturedCommand
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("isHidden")]
    public bool IsHidden { get; set; }

    [JsonPropertyName("aliases")]
    public List<string> Aliases { get; set; } = [];

    [JsonPropertyName("options")]
    public List<HookCapturedOption> Options { get; set; } = [];

    [JsonPropertyName("arguments")]
    public List<HookCapturedArgument> Arguments { get; set; } = [];

    [JsonPropertyName("subcommands")]
    public List<HookCapturedCommand> Subcommands { get; set; } = [];
}

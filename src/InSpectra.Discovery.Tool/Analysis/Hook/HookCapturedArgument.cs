namespace InSpectra.Discovery.Tool.Analysis.Hook;

using System.Text.Json.Serialization;

internal sealed class HookCapturedArgument
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("isHidden")]
    public bool IsHidden { get; set; }

    [JsonPropertyName("minArity")]
    public int MinArity { get; set; }

    [JsonPropertyName("maxArity")]
    public int MaxArity { get; set; }

    [JsonPropertyName("hasDefaultValue")]
    public bool HasDefaultValue { get; set; }

    [JsonPropertyName("defaultValue")]
    public string? DefaultValue { get; set; }

    [JsonPropertyName("allowedValues")]
    public List<string>? AllowedValues { get; set; }

    [JsonPropertyName("valueType")]
    public string? ValueType { get; set; }
}

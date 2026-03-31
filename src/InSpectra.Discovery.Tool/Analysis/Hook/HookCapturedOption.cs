namespace InSpectra.Discovery.Tool.Analysis.Hook;

using System.Text.Json.Serialization;

internal sealed class HookCapturedOption
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("isRequired")]
    public bool IsRequired { get; set; }

    [JsonPropertyName("isHidden")]
    public bool IsHidden { get; set; }

    [JsonPropertyName("aliases")]
    public List<string> Aliases { get; set; } = [];

    [JsonPropertyName("minArity")]
    public int MinArity { get; set; }

    [JsonPropertyName("maxArity")]
    public int MaxArity { get; set; }

    [JsonPropertyName("valueType")]
    public string? ValueType { get; set; }

    [JsonPropertyName("recursive")]
    public bool Recursive { get; set; }

    [JsonPropertyName("argumentName")]
    public string? ArgumentName { get; set; }

    [JsonPropertyName("hasDefaultValue")]
    public bool HasDefaultValue { get; set; }

    [JsonPropertyName("defaultValue")]
    public string? DefaultValue { get; set; }

    [JsonPropertyName("allowedValues")]
    public List<string>? AllowedValues { get; set; }
}

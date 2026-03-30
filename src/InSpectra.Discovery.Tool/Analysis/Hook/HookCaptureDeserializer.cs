namespace InSpectra.Discovery.Tool.Analysis.Hook;

using System.Text.Json;
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

internal static class HookCaptureDeserializer
{
    public static HookCaptureResult? Deserialize(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<HookCaptureResult>(json);
        }
        catch
        {
            return null;
        }
    }
}

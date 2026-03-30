using System.Text.Json.Serialization;

internal sealed class CaptureResult
{
    [JsonPropertyName("captureVersion")]
    public int CaptureVersion { get; set; } = 1;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "ok";

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("systemCommandLineVersion")]
    public string? SystemCommandLineVersion { get; set; }

    [JsonPropertyName("patchTarget")]
    public string? PatchTarget { get; set; }

    [JsonPropertyName("root")]
    public CapturedCommand? Root { get; set; }
}

internal sealed class CapturedCommand
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
    public List<CapturedOption> Options { get; set; } = [];

    [JsonPropertyName("arguments")]
    public List<CapturedArgument> Arguments { get; set; } = [];

    [JsonPropertyName("subcommands")]
    public List<CapturedCommand> Subcommands { get; set; } = [];
}

internal sealed class CapturedOption
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
}

internal sealed class CapturedArgument
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

    [JsonPropertyName("valueType")]
    public string? ValueType { get; set; }
}

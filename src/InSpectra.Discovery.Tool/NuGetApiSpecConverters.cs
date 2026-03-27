using System.Text.Json;
using System.Text.Json.Serialization;

internal sealed class NuGetTypeValueJsonConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.StartArray => ReadFirstStringFromArray(ref reader),
            _ => throw new JsonException($"Expected a string or array but found {reader.TokenType}."),
        };
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value);
    }

    private static string ReadFirstStringFromArray(ref Utf8JsonReader reader)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var value = item.GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        throw new JsonException("Expected an array containing at least one non-empty string.");
    }
}

internal sealed class NuGetRepositoryJsonConverter : JsonConverter<CatalogRepositorySpec?>
{
    public override CatalogRepositorySpec? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.String => ReadRepositoryString(ref reader),
            JsonTokenType.StartObject => ReadRepositoryObject(ref reader, options),
            _ => throw new JsonException($"Expected repository to be null, a string, or an object but found {reader.TokenType}."),
        };
    }

    public override void Write(Utf8JsonWriter writer, CatalogRepositorySpec? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        JsonSerializer.Serialize(
            writer,
            new CatalogRepositoryObjectSpec(value.Type, value.Url, value.Commit),
            options);
    }

    private static CatalogRepositorySpec? ReadRepositoryString(ref Utf8JsonReader reader)
    {
        var value = reader.GetString();
        return string.IsNullOrWhiteSpace(value)
            ? null
            : new CatalogRepositorySpec(Type: null, Url: value, Commit: null);
    }

    private static CatalogRepositorySpec ReadRepositoryObject(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var payload = document.RootElement.Deserialize<CatalogRepositoryObjectSpec>(options)
            ?? throw new JsonException("Repository object payload was null.");

        return new CatalogRepositorySpec(payload.Type, payload.Url, payload.Commit);
    }
}

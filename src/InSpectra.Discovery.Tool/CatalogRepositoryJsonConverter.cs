using System.Text.Json;
using System.Text.Json.Serialization;

internal sealed class CatalogRepositoryJsonConverter : JsonConverter<CatalogRepository?>
{
    public override CatalogRepository? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            return string.IsNullOrWhiteSpace(value) ? null : new CatalogRepository(null, value, null);
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException($"Unexpected token '{reader.TokenType}' while reading CatalogRepository.");
        }

        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        return new CatalogRepository(
            Type: root.TryGetProperty("type", out var type) ? type.GetString() : null,
            Url: root.TryGetProperty("url", out var url) ? url.GetString() : null,
            Commit: root.TryGetProperty("commit", out var commit) ? commit.GetString() : null);
    }

    public override void Write(Utf8JsonWriter writer, CatalogRepository? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        if (!string.IsNullOrWhiteSpace(value.Type))
        {
            writer.WriteString("type", value.Type);
        }

        if (!string.IsNullOrWhiteSpace(value.Url))
        {
            writer.WriteString("url", value.Url);
        }

        if (!string.IsNullOrWhiteSpace(value.Commit))
        {
            writer.WriteString("commit", value.Commit);
        }

        writer.WriteEndObject();
    }
}

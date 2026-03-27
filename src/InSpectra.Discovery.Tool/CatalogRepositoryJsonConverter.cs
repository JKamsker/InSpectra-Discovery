using System.Text.Json;
using System.Text.Json.Serialization;

internal sealed class CatalogRepositoryJsonConverter : JsonConverter<CatalogRepository?>
{
    public override CatalogRepository? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;

            case JsonTokenType.String:
                var value = reader.GetString();
                return string.IsNullOrWhiteSpace(value)
                    ? null
                    : new CatalogRepository(
                        Type: null,
                        Url: value,
                        Commit: null);

            case JsonTokenType.StartObject:
                using (var document = JsonDocument.ParseValue(ref reader))
                {
                    var root = document.RootElement;
                    return new CatalogRepository(
                        Type: GetOptionalString(root, "type"),
                        Url: GetOptionalString(root, "url"),
                        Commit: GetOptionalString(root, "commit"));
                }

            default:
                throw new JsonException($"Expected repository to be null, a string, or an object but found {reader.TokenType}.");
        }
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

    private static string? GetOptionalString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => property.GetString(),
            _ => throw new JsonException($"Expected repository.{propertyName} to be a string but found {property.ValueKind}."),
        };
    }
}

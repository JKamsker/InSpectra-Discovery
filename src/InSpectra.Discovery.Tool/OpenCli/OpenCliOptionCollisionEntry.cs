using System.Text.Json.Nodes;

internal sealed record OpenCliOptionCollisionEntry(JsonObject Option, IReadOnlySet<string> Tokens);

namespace InSpectra.Discovery.Tool.OpenCli.Options;

using System.Text.Json.Nodes;

internal static class OpenCliOptionSanitizer
{
    public static void NormalizeOptionObject(JsonObject option)
        => OpenCliOptionDescriptionSupport.NormalizeOptionObject(option);

    public static void DeduplicateSafeOptionCollisions(JsonArray options)
    {
        OpenCliOptionCollisionResolver.DeduplicateSafeOptionCollisions(options);
        OpenCliOptionDuplicatePrimaryNameResolver.Resolve(options);
        OpenCliOptionAliasConflictResolver.RemoveConflictingAliases(options);
    }
}

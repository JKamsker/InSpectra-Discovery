namespace InSpectra.Discovery.Tool.OpenCli;

using System.Text.Json.Nodes;

internal static partial class OpenCliDocumentPublishabilityInspector
{
    public static bool HasPublishableSurface(JsonObject document)
        => HasPublishableSurfaceCore(document);

    public static bool LooksLikeInventoryOnlyCommandShellDocument(JsonObject document)
        => LooksLikeInventoryOnlyCommandShellDocumentCore(document);

    public static bool ContainsErrorText(JsonObject document)
        => ContainsErrorTextCore(document);

    public static int CountTotalCommands(JsonObject node)
        => CountTotalCommandsCore(node);

    public static bool ContainsBoxDrawingCommandNames(JsonObject node)
        => ContainsBoxDrawingCommandNamesCore(node);

    public static bool LooksLikeNonPublishableTitle(string? title)
        => LooksLikeNonPublishableTitleCore(title);

    public static bool LooksLikeNonPublishableDescription(string? description)
        => LooksLikeNonPublishableDescriptionCore(description);
}


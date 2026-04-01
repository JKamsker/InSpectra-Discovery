namespace InSpectra.Discovery.Tool.StaticAnalysis.OpenCli;

using InSpectra.Discovery.Tool.StaticAnalysis.Models;

internal static class StaticAnalysisCommandPublishabilitySupport
{
    private static readonly HashSet<string> CoconaInfrastructureCommandNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "buildwebhost",
        "configureappconfiguration",
        "configurecontainer",
        "configurehostconfiguration",
        "configurelogging",
        "configureservices",
        "createinstance",
        "createasyncscope",
        "createhostbuilder",
        "createwebhostbuilder",
        "createscope",
        "dispatchasync",
        "getserviceorcreateinstance",
        "isservice",
        "runasync",
        "startasync",
        "stopasync",
    };

    private static readonly HashSet<string> CoconaContextualInfrastructureCommandNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "build",
        "dispose",
        "run",
    };

    public static IReadOnlyDictionary<string, StaticCommandDefinition> FilterPublishableCommands(
        string framework,
        IReadOnlyDictionary<string, StaticCommandDefinition> staticCommands)
    {
        if (!string.Equals(framework, "Cocona", StringComparison.OrdinalIgnoreCase))
        {
            return staticCommands;
        }

        var suspectCommandCount = staticCommands
            .Where(static pair => !string.IsNullOrWhiteSpace(pair.Key))
            .Count(pair => IsIntrinsicInfrastructureCommand(pair.Key)
                || HasOnlyInfrastructureMembers(pair.Value));
        var filtered = new Dictionary<string, StaticCommandDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in staticCommands)
        {
            if (!ShouldExcludeCoconaCommand(pair.Key, pair.Value, suspectCommandCount))
            {
                filtered[pair.Key] = pair.Value;
            }
        }

        return filtered;
    }

    private static bool ShouldExcludeCoconaCommand(string key, StaticCommandDefinition definition, int suspectCommandCount)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        var leafName = key.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? key;
        if (IsIntrinsicInfrastructureCommand(leafName) || HasOnlyInfrastructureMembers(definition))
        {
            return true;
        }

        return suspectCommandCount >= 3 && CoconaContextualInfrastructureCommandNames.Contains(leafName);
    }

    private static bool IsIntrinsicInfrastructureCommand(string name)
        => CoconaInfrastructureCommandNames.Contains(name);

    private static bool HasOnlyInfrastructureMembers(StaticCommandDefinition definition)
    {
        var memberTypes = definition.Options
            .Select(static option => option.ClrType)
            .Concat(definition.Values.Select(static value => value.ClrType))
            .Where(static clrType => !string.IsNullOrWhiteSpace(clrType))
            .Cast<string>()
            .ToArray();

        return memberTypes.Length > 0 && memberTypes.All(LooksLikeInfrastructureType);
    }

    private static bool LooksLikeInfrastructureType(string clrType)
        => clrType.Contains("Microsoft.Extensions.", StringComparison.Ordinal)
            || clrType.Contains("Microsoft.AspNetCore.", StringComparison.Ordinal)
            || clrType.Contains("Cocona.", StringComparison.Ordinal)
            || string.Equals(clrType, "System.Type", StringComparison.Ordinal)
            || string.Equals(clrType, "System.Object[]", StringComparison.Ordinal)
            || string.Equals(clrType, "System.IServiceProvider", StringComparison.Ordinal)
            || string.Equals(clrType, "System.IServiceScope", StringComparison.Ordinal)
            || string.Equals(clrType, "System.IAsyncDisposable", StringComparison.Ordinal);
}

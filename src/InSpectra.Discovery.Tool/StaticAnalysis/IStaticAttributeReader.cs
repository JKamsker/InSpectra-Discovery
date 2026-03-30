namespace InSpectra.Discovery.Tool.StaticAnalysis;

internal interface IStaticAttributeReader
{
    IReadOnlyDictionary<string, StaticCommandDefinition> Read(IReadOnlyList<ScannedModule> modules);
}


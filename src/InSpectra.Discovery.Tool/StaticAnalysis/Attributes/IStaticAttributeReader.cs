namespace InSpectra.Discovery.Tool.StaticAnalysis.Attributes;

using InSpectra.Discovery.Tool.StaticAnalysis.Models;

using InSpectra.Discovery.Tool.StaticAnalysis.Inspection;

internal interface IStaticAttributeReader
{
    IReadOnlyDictionary<string, StaticCommandDefinition> Read(IReadOnlyList<ScannedModule> modules);
}


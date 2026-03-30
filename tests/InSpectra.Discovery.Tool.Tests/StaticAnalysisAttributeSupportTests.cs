namespace InSpectra.Discovery.Tool.Tests;

using dnlib.DotNet;
using Xunit;

public sealed class StaticAnalysisAttributeSupportTests
{
    [Fact]
    public void FindAttribute_And_ArgumentReaders_Return_Expected_Values()
    {
        using var module = ModuleDefMD.Load(typeof(StaticAnalysisAttributeSupportTests).Assembly.Location);
        var typeDef = GetTypeDef(module, typeof(StaticAnalysisAttributeSupportSampleBase));
        var attribute = StaticAnalysisAttributeSupport.FindAttribute(
            typeDef.CustomAttributes,
            ["Missing.Attribute", typeof(StaticAnalysisSampleAttribute).FullName!]);

        Assert.NotNull(attribute);
        Assert.Equal("root", StaticAnalysisAttributeSupport.GetConstructorArgumentString(attribute, 0));
        Assert.Equal("command", StaticAnalysisAttributeSupport.GetNamedArgumentString(attribute, nameof(StaticAnalysisSampleAttribute.Description)));
        Assert.Equal(3, StaticAnalysisAttributeSupport.GetNamedArgumentInt(attribute, nameof(StaticAnalysisSampleAttribute.Order)));
        Assert.True(StaticAnalysisAttributeSupport.GetNamedArgumentBool(attribute, nameof(StaticAnalysisSampleAttribute.Enabled)));
        Assert.Equal("7", StaticAnalysisAttributeSupport.GetNamedArgumentValueAsString(attribute, nameof(StaticAnalysisSampleAttribute.Count)));
        Assert.Equal(new[] { "alpha", "b" }, StaticAnalysisAttributeSupport.GetNamedArgumentStrings(attribute, nameof(StaticAnalysisSampleAttribute.Aliases)));
    }

    [Fact]
    public void GetAttributeString_And_ConstructorArgumentInt_Read_Constructor_Payloads()
    {
        using var module = ModuleDefMD.Load(typeof(StaticAnalysisAttributeSupportTests).Assembly.Location);
        var typeDef = GetTypeDef(module, typeof(StaticAnalysisAttributeSupportSample));
        var property = typeDef.Properties.First(candidate => string.Equals(candidate.Name, nameof(StaticAnalysisAttributeSupportSample.Value), StringComparison.Ordinal));

        var description = StaticAnalysisAttributeSupport.GetAttributeString(property.CustomAttributes, typeof(StaticAnalysisSampleAttribute).FullName!);
        var indexAttribute = StaticAnalysisAttributeSupport.FindAttribute(property.CustomAttributes, typeof(StaticAnalysisIndexedSampleAttribute).FullName!);

        Assert.Equal("derived-value", description);
        Assert.Equal(5, StaticAnalysisAttributeSupport.GetConstructorArgumentInt(indexAttribute, 0, fallback: -1));
    }

    [Fact]
    public void HierarchySupport_Enumerates_Base_Properties_Before_Derived_Properties()
    {
        using var module = ModuleDefMD.Load(typeof(StaticAnalysisAttributeSupportTests).Assembly.Location);
        var typeDef = GetTypeDef(module, typeof(StaticAnalysisAttributeSupportSample));

        var propertyNames = StaticAnalysisHierarchySupport.GetPropertiesFromHierarchy(typeDef)
            .Select(property => property.Name.String)
            .ToArray();

        Assert.Equal(
            new[] { nameof(StaticAnalysisAttributeSupportSampleBase.BaseValue), nameof(StaticAnalysisAttributeSupportSample.Value) },
            propertyNames);
    }

    [Fact]
    public void CommandDefinitionSupport_Upserts_More_Detailed_Definitions()
    {
        var commands = new Dictionary<string, StaticCommandDefinition>(StringComparer.OrdinalIgnoreCase);
        var sparseDefinition = new StaticCommandDefinition(
            Name: "sample",
            Description: null,
            IsDefault: false,
            IsHidden: false,
            Values: [],
            Options: []);
        var richerDefinition = new StaticCommandDefinition(
            Name: "sample",
            Description: "description",
            IsDefault: false,
            IsHidden: false,
            Values: [new StaticValueDefinition(0, "value", true, false, "System.String", null, null, [])],
            Options: []);

        StaticCommandDefinitionSupport.UpsertBest(commands, sparseDefinition);
        StaticCommandDefinitionSupport.UpsertBest(commands, richerDefinition);

        Assert.Same(richerDefinition, commands["sample"]);
    }

    private static TypeDef GetTypeDef(ModuleDefMD module, Type type)
        => module.GetTypes().First(candidate => string.Equals(candidate.FullName, type.FullName, StringComparison.Ordinal));
}

[AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
internal sealed class StaticAnalysisSampleAttribute : Attribute
{
    public StaticAnalysisSampleAttribute(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public string? Description { get; set; }

    public int Order { get; set; }

    public bool Enabled { get; set; }

    public int Count { get; set; }

    public string[] Aliases { get; set; } = [];
}

[AttributeUsage(AttributeTargets.All)]
internal sealed class StaticAnalysisIndexedSampleAttribute : Attribute
{
    public StaticAnalysisIndexedSampleAttribute(int index)
    {
        Index = index;
    }

    public int Index { get; }
}

[StaticAnalysisSample("root", Description = "command", Order = 3, Enabled = true, Count = 7, Aliases = ["alpha", "b"])]
internal class StaticAnalysisAttributeSupportSampleBase
{
    [StaticAnalysisSample("base-value")]
    public string BaseValue { get; set; } = string.Empty;
}

internal sealed class StaticAnalysisAttributeSupportSample : StaticAnalysisAttributeSupportSampleBase
{
    [StaticAnalysisSample("derived-value", Description = "option")]
    [StaticAnalysisIndexedSample(5)]
    public int Value { get; set; }

    [StaticAnalysisSample("run", Description = "invoke")]
    public void Run()
    {
    }
}


namespace InSpectra.Discovery.Tool.Tests;

using dnlib.DotNet;
using Xunit;

public sealed class StaticAnalysisTypeSupportTests
{
    [Fact]
    public void GetClrTypeName_Formats_Generic_Nullable_Signatures()
    {
        using var module = ModuleDefMD.Load(typeof(StaticAnalysisTypeSupportTests).Assembly.Location);
        var typeDef = GetTypeDef(module, typeof(StaticAnalysisTypeSampleTypes));
        var propertyType = GetPropertyType(typeDef, nameof(StaticAnalysisTypeSampleTypes.Map));

        var clrTypeName = StaticAnalysisTypeSupport.GetClrTypeName(propertyType);

        Assert.Equal("System.Collections.Generic.Dictionary<System.String, System.Nullable<System.Int32>>", clrTypeName);
    }

    [Fact]
    public void GetAcceptedValues_Returns_Enum_Names_For_Nullable_Enums()
    {
        using var module = ModuleDefMD.Load(typeof(StaticAnalysisTypeSupportTests).Assembly.Location);
        var typeDef = GetTypeDef(module, typeof(StaticAnalysisTypeSampleTypes));
        var propertyType = GetPropertyType(typeDef, nameof(StaticAnalysisTypeSampleTypes.Mode));

        var acceptedValues = StaticAnalysisTypeSupport.GetAcceptedValues(propertyType);

        Assert.Equal(["Basic", "Advanced"], acceptedValues);
    }

    [Fact]
    public void IsSequenceType_Detects_Collections_But_Not_Strings()
    {
        using var module = ModuleDefMD.Load(typeof(StaticAnalysisTypeSupportTests).Assembly.Location);
        var typeDef = GetTypeDef(module, typeof(StaticAnalysisTypeSampleTypes));

        var collectionType = GetPropertyType(typeDef, nameof(StaticAnalysisTypeSampleTypes.Paths));
        var stringType = GetPropertyType(typeDef, nameof(StaticAnalysisTypeSampleTypes.Name));

        Assert.True(StaticAnalysisTypeSupport.IsSequenceType(collectionType));
        Assert.False(StaticAnalysisTypeSupport.IsSequenceType(stringType));
    }

    [Fact]
    public void IsBoolType_Detects_Nullable_Boolean()
    {
        using var module = ModuleDefMD.Load(typeof(StaticAnalysisTypeSupportTests).Assembly.Location);
        var typeDef = GetTypeDef(module, typeof(StaticAnalysisTypeSampleTypes));
        var propertyType = GetPropertyType(typeDef, nameof(StaticAnalysisTypeSampleTypes.Enabled));

        Assert.True(StaticAnalysisTypeSupport.IsBoolType(propertyType));
    }

    private static TypeDef GetTypeDef(ModuleDefMD module, Type type)
        => module.GetTypes().First(candidate => string.Equals(candidate.FullName, type.FullName, StringComparison.Ordinal));

    private static TypeSig? GetPropertyType(TypeDef typeDef, string propertyName)
        => typeDef.Properties.First(property => string.Equals(property.Name, propertyName, StringComparison.Ordinal)).PropertySig?.RetType;
}

internal sealed class StaticAnalysisTypeSampleTypes
{
    public List<string> Paths { get; init; } = [];

    public string Name { get; init; } = string.Empty;

    public bool? Enabled { get; init; }

    public StaticAnalysisTypeSampleMode? Mode { get; init; }

    public Dictionary<string, int?> Map { get; init; } = new();
}

internal enum StaticAnalysisTypeSampleMode
{
    Basic,
    Advanced,
}


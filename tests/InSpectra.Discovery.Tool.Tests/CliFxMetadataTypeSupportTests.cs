namespace InSpectra.Discovery.Tool.Tests;

using System.Reflection;
using Xunit;

public sealed class CliFxMetadataTypeSupportTests
{
    [Fact]
    public void GetClrTypeName_Formats_Nullable_And_Generic_Types()
    {
        var clrTypeName = CliFxMetadataTypeSupport.GetClrTypeName(typeof(Dictionary<string, int?>));

        Assert.Equal("System.Collections.Generic.Dictionary<System.String, System.Nullable<System.Int32>>", clrTypeName);
    }

    [Fact]
    public void GetAcceptedValues_Returns_Enum_Names_For_Nullable_Enums()
    {
        var acceptedValues = CliFxMetadataTypeSupport.GetAcceptedValues(typeof(SampleMode?));

        Assert.Equal(["Basic", "Advanced"], acceptedValues);
    }

    [Fact]
    public void IsSequence_Returns_True_For_Array_Properties_Without_Converter()
    {
        var property = typeof(SampleCommand).GetProperty(nameof(SampleCommand.Paths), BindingFlags.Instance | BindingFlags.Public)!;
        var attribute = property.CustomAttributes.First();

        var isSequence = CliFxMetadataTypeSupport.IsSequence(property, attribute);

        Assert.True(isSequence);
    }

    private enum SampleMode
    {
        Basic,
        Advanced,
    }

    private sealed class SampleCommand
    {
        [System.ComponentModel.Description("sample")]
        public string[] Paths { get; init; } = [];
    }
}


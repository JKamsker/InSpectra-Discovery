namespace InSpectra.Discovery.Tool.Tests;

using System.Text.Json.Nodes;
using System.Xml.Linq;
using Xunit;

public sealed class OpenCliDocumentSynthesizerTests
{
    [Fact]
    public void ConvertFromXmldoc_Hoists_Root_Default_Inputs_And_Omits_Root_Default_Command()
    {
        var xml = XDocument.Parse(
            """
            <Model>
              <Command Name="__default_command">
                <Description>Sample root description</Description>
                <Parameters>
                  <Option Long="verbose">
                    <Description>Verbose output</Description>
                  </Option>
                  <Argument Name="path" Required="false">
                    <Description>Input path</Description>
                  </Argument>
                </Parameters>
              </Command>
              <Command Name="serve">
                <Description>Serve content</Description>
              </Command>
            </Model>
            """);

        var document = OpenCliDocumentSynthesizer.ConvertFromXmldoc(xml, "sample", "1.2.3");

        Assert.Equal("Sample root description", document["info"]?["description"]?.GetValue<string>());
        Assert.Contains(document["options"]!.AsArray(), option =>
            string.Equals(option?["name"]?.GetValue<string>(), "--verbose", StringComparison.Ordinal));
        Assert.Contains(document["arguments"]!.AsArray(), argument =>
            string.Equals(argument?["name"]?.GetValue<string>(), "path", StringComparison.Ordinal));
        Assert.DoesNotContain(document["commands"]!.AsArray(), command =>
            string.Equals(command?["name"]?.GetValue<string>(), "__default_command", StringComparison.Ordinal));
        Assert.Contains(document["commands"]!.AsArray(), command =>
            string.Equals(command?["name"]?.GetValue<string>(), "serve", StringComparison.Ordinal));
    }

    [Fact]
    public void ConvertFromXmldoc_Omits_Unsupported_OptionRequired_And_Uses_Vector_Arity()
    {
        var xml = XDocument.Parse(
            """
            <Model>
              <Command Name="pack">
                <Parameters>
                  <Option Long="item" Kind="vector" ClrType="System.String" Value="ITEM">
                    <Description>Items</Description>
                  </Option>
                  <Argument Name="files" Required="false" Kind="vector" ClrType="System.String">
                    <Description>Files to pack</Description>
                  </Argument>
                </Parameters>
              </Command>
            </Model>
            """);

        var document = OpenCliDocumentSynthesizer.ConvertFromXmldoc(xml, "sample", "1.2.3");
        var command = document["commands"]![0]!.AsObject();
        var option = command["options"]![0]!.AsObject();
        var optionArgument = option["arguments"]![0]!.AsObject();
        var argument = command["arguments"]![0]!.AsObject();

        Assert.False(option.ContainsKey("required"));
        Assert.Equal(1, optionArgument["arity"]!["minimum"]!.GetValue<int>());
        Assert.False(optionArgument["arity"]!.AsObject().ContainsKey("maximum"));
        Assert.Equal(0, argument["arity"]!["minimum"]!.GetValue<int>());
        Assert.False(argument["arity"]!.AsObject().ContainsKey("maximum"));
    }

    [Fact]
    public void ConvertFromXmldoc_Hoists_Nested_Default_Command_Inputs_And_Omits_Empty_Examples()
    {
        var xml = XDocument.Parse(
            """
            <Model>
              <Command Name="config">
                <Command Name="">
                  <Parameters>
                    <Option Long="force">
                      <Description>Force config</Description>
                    </Option>
                  </Parameters>
                </Command>
              </Command>
            </Model>
            """);

        var document = OpenCliDocumentSynthesizer.ConvertFromXmldoc(xml, "sample", "1.2.3");
        var config = document["commands"]![0]!.AsObject();

        Assert.False(config.ContainsKey("commands"));
        Assert.Contains(config["options"]!.AsArray(), option =>
            string.Equals(option?["name"]?.GetValue<string>(), "--force", StringComparison.Ordinal));
        Assert.False(config.ContainsKey("examples"));
    }

    [Fact]
    public void ConvertFromXmldoc_Normalizes_Display_Argument_Names()
    {
        var xml = XDocument.Parse(
            """
            <Model>
              <Command Name="generate">
                <Parameters>
                  <Argument Name="URL or input file" Required="false" Kind="scalar" ClrType="System.String">
                    <Description>Input source</Description>
                  </Argument>
                </Parameters>
              </Command>
            </Model>
            """);

        var document = OpenCliDocumentSynthesizer.ConvertFromXmldoc(xml, "sample", "1.2.3");
        var command = document["commands"]![0]!.AsObject();
        var argument = command["arguments"]![0]!.AsObject();

        Assert.Equal("url-or-input-file", argument["name"]!.GetValue<string>());
    }
}


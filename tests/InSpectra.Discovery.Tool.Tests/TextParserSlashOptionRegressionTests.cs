namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Help.OpenCli;
using InSpectra.Discovery.Tool.Help.Parsing;
using System.Text.Json.Nodes;
using Xunit;

public sealed class TextParserSlashOptionRegressionTests
{
    [Fact]
    public void Parse_Preserves_Slash_Style_Option_Block_For_Mgcb()
    {
        var parser = new TextParser();
        var document = parser.Parse(
            """
            MonoGame Content Builder: v3.8.5.0
            Builds optimized game content for MonoGame projects.

            Usage: mgcb <Options>

            Options:
              /@, /@:<responseFile>             Read a text response
                                                  file with additional
                                                  command line options
                                                  and switches.
              /b, /build:<sourceFile>           Build the content source
                                                  file using the
                                                  previously set
                                                  switches and options.
              /c, /clean                        Delete all previously
                                                  built content and
                                                  intermediate files.
                  /compress                     Compress the XNB files
                                                  for smaller file
                                                  sizes.
                  /config:<string>              The optional build
                                                  config string from the
                                                  build system.
                  /copy:<sourceFile>            Copy the content source
                                                  file verbatim to the
                                                  output directory.
              /h, /help                         Displays this help.
              /i, /importer:<className>         Defines the class name
                                                  of the content
                                                  importer for reading
                                                  source content.
              /I, /incremental                  Skip cleaning files not
                                                  included in the
                                                  current build.
              /n, /intermediateDir:<path>       The directory where all
                                                  intermediate files are
                                                  written.
              /d, /launchdebugger               Launch the debugger
                                                  before building
                                                  content.
              /o, /outputDir:<path>             The directory where all
                                                  content is written.
              /t, /platform:<targetPlatform>    Set the target platform
                                                  for this build.
                                                  Defaults to Windows
                                                  desktop DirectX.
              /p, /processor:<className>        Defines the class name
                                                  of the content
                                                  processor for
                                                  processing imported
                                                  content.
              /P, /processorParam:<name=value>  Defines a parameter name
                                                  and value to set on a
                                                  content processor.
              /g, /profile:<graphicsProfile>    Set the target graphics
                                                  profile for this
                                                  build.  Defaults to
                                                  HiDef.
              /q, /quiet                        Only output content
                                                  build errors.
              /r, /rebuild                      Forces a full rebuild of
                                                  all content.
              /f, /reference:<assembly>         Adds an assembly
                                                  reference for
                                                  resolving content
                                                  importers, processors,
                                                  and writers.
              /a, /waitfordebugger              Wait for debugger to
                                                  attach before building
                                                  content.
              /w, /workingDir:<directoryPath>   The working directory
                                                  where all source
                                                  content is located.
            Build started 31.03.2026 22:26:48

            Build 0 succeeded, 0 failed.

            Time elapsed 00:00:00.03.
            """);

        var optionKeys = document.Options.Select(option => option.Key).ToArray();
        Assert.Equal(
            new[]
            {
                "/b, /build:<sourceFile>",
                "/c, /clean",
                "/compress",
                "/config:<string>",
                "/copy:<sourceFile>",
                "/h, /help",
                "/i, /importer:<className>",
                "/I, /incremental",
                "/n, /intermediateDir:<path>",
                "/d, /launchdebugger",
                "/o, /outputDir:<path>",
                "/t, /platform:<targetPlatform>",
                "/p, /processor:<className>",
                "/P, /processorParam:<name=value>",
                "/g, /profile:<graphicsProfile>",
                "/q, /quiet",
                "/r, /rebuild",
                "/f, /reference:<assembly>",
                "/a, /waitfordebugger",
                "/w, /workingDir:<directoryPath>",
            },
            optionKeys);
    }

    [Fact]
    public void OpenCliBuilder_Preserves_Parsed_Slash_Options_For_Mgcb()
    {
        var parser = new TextParser();
        var helpDocument = parser.Parse(
            """
            MonoGame Content Builder: v3.8.5.0
            Builds optimized game content for MonoGame projects.

            Usage: mgcb <Options>

            Options:
              /b, /build:<sourceFile>           Build the content source
                                                  file using the
                                                  previously set
                                                  switches and options.
              /c, /clean                        Delete all previously
                                                  built content and
                                                  intermediate files.
                  /compress                     Compress the XNB files
                                                  for smaller file
                                                  sizes.
              /h, /help                         Displays this help.
            """);

        var openCli = new OpenCliBuilder().Build(
            "mgcb",
            "3.8.5-preview.3",
            new Dictionary<string, InSpectra.Discovery.Tool.Help.Documents.Document>(StringComparer.OrdinalIgnoreCase)
            {
                [""] = helpDocument,
            });

        var options = Assert.IsType<JsonArray>(openCli["options"]);
        Assert.Equal(
            new[]
            {
                "/build",
                "/clean",
                "/compress",
                "/help",
            },
            options
                .OfType<JsonObject>()
                .Select(option => option["name"]!.GetValue<string>())
                .ToArray());
    }
}

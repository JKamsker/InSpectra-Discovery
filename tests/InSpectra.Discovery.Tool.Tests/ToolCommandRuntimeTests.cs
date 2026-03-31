namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Infrastructure.Commands;

using Xunit;

public sealed class CommandRuntimeTests
{
    [Fact]
    public void NormalizeConsoleText_Strips_Ansi_Control_Codes_And_Bom()
    {
        var normalized = CommandRuntime.NormalizeConsoleText("\uFEFF\u001b[31merror\u001b[0m");

        Assert.Equal("error", normalized);
    }

    [Fact]
    public void ResolveInstalledCommandPath_Prefers_Existing_Command_File()
    {
        var runtime = new CommandRuntime();
        var tempRoot = Path.Combine(Path.GetTempPath(), "inspectra-runtime-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var commandPath = Path.Combine(tempRoot, "demo.cmd");
            File.WriteAllText(commandPath, "@echo off");

            var resolvedPath = runtime.ResolveInstalledCommandPath(tempRoot, "demo");

            Assert.Equal(commandPath, resolvedPath);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}



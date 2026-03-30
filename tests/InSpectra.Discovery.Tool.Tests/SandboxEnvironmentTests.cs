using Xunit;

public sealed class SandboxEnvironmentTests
{
    [Fact]
    public void ToolCommandRuntime_CreateSandboxEnvironment_Disables_GlobalToolPathMutation_And_Only_Returns_Directories()
    {
        var runtime = new ToolCommandRuntime();
        var environment = runtime.CreateSandboxEnvironment(@"C:\temp\inspectra-test");

        Assert.Equal("0", environment.Values["DOTNET_ADD_GLOBAL_TOOLS_TO_PATH"]);
        Assert.Equal("0", environment.Values["DOTNET_GENERATE_ASPNET_CERTIFICATE"]);
        Assert.Equal("1", environment.Values["DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE"]);
        Assert.Equal(environment.Values["HOME"], environment.Values["USERPROFILE"]);
        Assert.Equal(environment.Values["XDG_CONFIG_HOME"], environment.Values["APPDATA"]);
        Assert.Equal(environment.Values["XDG_DATA_HOME"], environment.Values["LOCALAPPDATA"]);
        Assert.Equal(environment.Values["TMPDIR"], environment.Values["TMP"]);
        Assert.Equal(environment.Values["TMPDIR"], environment.Values["TEMP"]);
        Assert.DoesNotContain("0", environment.Directories);
        Assert.DoesNotContain("1", environment.Directories);
        Assert.DoesNotContain("dumb", environment.Directories);
        Assert.Contains(environment.Values["DOTNET_CLI_HOME"], environment.Directories);
        Assert.Contains(environment.Values["TMPDIR"], environment.Directories);
    }

    [Fact]
    public void AnalysisRuntimeSupport_CreateSandboxEnvironment_Disables_GlobalToolPathMutation()
    {
        var environment = AnalysisRuntimeSupport.CreateSandboxEnvironment(@"C:\temp\inspectra-test");

        Assert.Equal("0", environment.Values["DOTNET_ADD_GLOBAL_TOOLS_TO_PATH"]);
        Assert.Equal("0", environment.Values["DOTNET_GENERATE_ASPNET_CERTIFICATE"]);
        Assert.Equal("1", environment.Values["DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE"]);
        Assert.Equal(environment.Values["HOME"], environment.Values["USERPROFILE"]);
        Assert.Equal(environment.Values["XDG_CONFIG_HOME"], environment.Values["APPDATA"]);
        Assert.Equal(environment.Values["XDG_DATA_HOME"], environment.Values["LOCALAPPDATA"]);
        Assert.Equal(environment.Values["TMPDIR"], environment.Values["TMP"]);
        Assert.Equal(environment.Values["TMPDIR"], environment.Values["TEMP"]);
    }
}

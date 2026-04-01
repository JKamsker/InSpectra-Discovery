using Xunit;

public sealed class HookAssemblySelectionSupportTests
{
    [Fact]
    public void ShouldPatch_Rejects_Framework_Assembly_Outside_Preferred_Directory()
    {
        var shouldPatch = HookAssemblySelectionSupport.ShouldPatch(
            assemblyName: "System.CommandLine",
            assemblyLocation: @"C:\dotnet\sdk\10.0.100\System.CommandLine.dll",
            cliFramework: HookCliFrameworkSupport.SystemCommandLine,
            preferredFrameworkDirectory: @"C:\tools\demo");

        Assert.False(shouldPatch);
    }

    [Fact]
    public void ShouldPatch_Accepts_Framework_Assembly_Inside_Preferred_Directory()
    {
        var shouldPatch = HookAssemblySelectionSupport.ShouldPatch(
            assemblyName: "System.CommandLine",
            assemblyLocation: @"C:\tools\demo\System.CommandLine.dll",
            cliFramework: HookCliFrameworkSupport.SystemCommandLine,
            preferredFrameworkDirectory: @"C:\tools\demo");

        Assert.True(shouldPatch);
    }

    [Fact]
    public void ShouldPatch_Accepts_Matching_Assembly_When_No_Preferred_Directory_Is_Set()
    {
        var shouldPatch = HookAssemblySelectionSupport.ShouldPatch(
            assemblyName: "System.CommandLine",
            assemblyLocation: @"C:\dotnet\sdk\10.0.100\System.CommandLine.dll",
            cliFramework: HookCliFrameworkSupport.SystemCommandLine,
            preferredFrameworkDirectory: null);

        Assert.True(shouldPatch);
    }
}

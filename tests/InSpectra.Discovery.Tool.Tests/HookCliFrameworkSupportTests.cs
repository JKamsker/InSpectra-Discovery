using System.Reflection;
using System.Reflection.Emit;

using Xunit;

public sealed class HookCliFrameworkSupportTests
{
    [Fact]
    public void GetExpectedAssemblyName_Maps_CommandLineParser_To_CommandLine()
    {
        var assemblyName = HookCliFrameworkSupport.GetExpectedAssemblyName(HookCliFrameworkSupport.CommandLineParser);

        Assert.Equal("CommandLine", assemblyName);
    }

    [Fact]
    public void MatchesExpectedAssembly_Recognizes_CommandLine_Assembly_For_CommandLineParser()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("CommandLine"), AssemblyBuilderAccess.Run);

        var matches = HookCliFrameworkSupport.MatchesExpectedAssembly(assembly, HookCliFrameworkSupport.CommandLineParser);

        Assert.True(matches);
    }
}

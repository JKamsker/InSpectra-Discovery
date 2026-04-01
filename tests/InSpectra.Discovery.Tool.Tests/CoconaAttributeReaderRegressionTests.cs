namespace InSpectra.Discovery.Tool.Tests
{
    using InSpectra.Discovery.Tool.StaticAnalysis.Attributes;
    using InSpectra.Discovery.Tool.StaticAnalysis.Inspection;

    using dnlib.DotNet;
    using Xunit;

    public sealed class CoconaAttributeReaderRegressionTests
    {
        [Fact]
        public void Read_Ignores_Framework_And_Infrastructure_Methods_While_Keeping_User_Commands()
        {
            using var module = ModuleDefMD.Load(
                typeof(CoconaAttributeReaderRegressionTests).Assembly.Location,
                new ModuleCreationOptions { TryToLoadPdbFromDisk = false });

            var reader = new CoconaAttributeReader();
            var commands = reader.Read([new ScannedModule(module.Location!, module)]);

            Assert.True(commands.ContainsKey("coconaexamplegenerate"));
            Assert.True(commands.ContainsKey("coconaexampleserve"));
            Assert.False(commands.ContainsKey("coconaframeworkconfigureappconfiguration"));
            Assert.False(commands.ContainsKey("coconainfrastructureapply"));
            Assert.False(commands.ContainsKey("coconaframeworkrunasync"));
        }
    }
}

namespace InSpectra.Discovery.Tool.Tests.CoconaFixtures
{
    public sealed class CoconaExampleCommands
    {
        public void CoconaExampleGenerate(string outputPath)
        {
        }

        public void CoconaExampleServe(string path, global::Microsoft.Extensions.Logging.ILogger logger)
        {
        }
    }

    public sealed class CoconaInfrastructureLookingCommands
    {
        public void CoconaInfrastructureApply(
            global::Microsoft.Extensions.Configuration.ConfigurationManager configuration,
            global::Microsoft.Extensions.Hosting.HostBuilder hostBuilder)
        {
        }
    }
}

namespace Cocona
{
    public sealed class FrameworkLikeBuilder
    {
        public void CoconaFrameworkConfigureAppConfiguration(global::System.Action<global::Microsoft.Extensions.Configuration.IConfigurationBuilder> configureDelegate)
        {
        }

        public global::System.Threading.Tasks.Task CoconaFrameworkRunAsync()
            => global::System.Threading.Tasks.Task.CompletedTask;
    }
}

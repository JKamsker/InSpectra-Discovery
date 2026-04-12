namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Analysis.Bridge;
using InSpectra.Lib.Composition;
using InSpectra.Lib.Contracts.Providers;
using Microsoft.Extensions.DependencyInjection;

internal static class TestBridgeFactory
{
    public static LibAnalysisBridge CreateBridge()
    {
        var services = new ServiceCollection();
        services.AddInSpectraEngine();
        var provider = services.BuildServiceProvider();
        return new LibAnalysisBridge(
            provider.GetRequiredService<IPackageCliToolInstaller>(),
            provider.GetRequiredService<IAcquisitionAnalysisDispatcher>());
    }
}

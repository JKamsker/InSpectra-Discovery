namespace InSpectra.Discovery.Tool.App;

using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

internal sealed class TypeResolver(ServiceProvider provider) : ITypeResolver, IDisposable
{
    public object? Resolve(Type? type)
        => type is null ? null : provider.GetService(type);

    public void Dispose()
        => provider.Dispose();
}

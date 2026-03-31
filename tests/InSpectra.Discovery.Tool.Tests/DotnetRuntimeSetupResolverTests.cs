namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.NuGet;
using InSpectra.Discovery.Tool.Queue.Planning;

using System.IO.Compression;
using System.Net;
using System.Text.Json.Nodes;
using Xunit;

public sealed class DotnetRuntimeSetupResolverTests
{
    [Fact]
    public void TryResolveFromCatalog_UsesLegacyNetCoreRuntimeChannel()
    {
        var catalogLeaf = CreateCatalogLeaf(
            "tools/netcoreapp3.1/any/Sample.Tool.dll");

        var plan = DotnetRuntimeSetupResolver.TryResolveFromCatalog(catalogLeaf, "ubuntu-latest");

        Assert.NotNull(plan);
        Assert.Equal("runtime-only", plan!.Mode);
        var requirement = Assert.Single(plan.RequiredRuntimes);
        Assert.Equal("Microsoft.NETCore.App", requirement.Name);
        Assert.Equal("3.1", requirement.Channel);
        Assert.Equal("dotnet", requirement.Runtime);
    }

    [Fact]
    public void TryResolveFromCatalog_PicksHighestCompatibleToolFramework()
    {
        var catalogLeaf = CreateCatalogLeaf(
            "tools/net6.0/any/Sample.Tool.dll",
            "tools/net8.0/any/Sample.Tool.dll");

        var plan = DotnetRuntimeSetupResolver.TryResolveFromCatalog(catalogLeaf, "ubuntu-latest");

        Assert.NotNull(plan);
        var requirement = Assert.Single(plan!.RequiredRuntimes);
        Assert.Equal("8.0", requirement.Channel);
    }

    [Fact]
    public void TryResolveFromCatalog_UsesWindowsDesktopRuntimeForWindowsTarget()
    {
        var catalogLeaf = CreateCatalogLeaf(
            "tools/net8.0-windows/any/Sample.Tool.dll");

        var plan = DotnetRuntimeSetupResolver.TryResolveFromCatalog(catalogLeaf, "windows-latest");

        Assert.NotNull(plan);
        var requirement = Assert.Single(plan!.RequiredRuntimes);
        Assert.Equal("Microsoft.WindowsDesktop.App", requirement.Name);
        Assert.Equal("windowsdesktop", requirement.Runtime);
        Assert.Equal("8.0", requirement.Channel);
    }

    [Fact]
    public void TryReadRuntimeRequirements_ParsesFrameworkArray()
    {
        var document = JsonNode.Parse(
            """
            {
              "runtimeOptions": {
                "frameworks": [
                  {
                    "name": "Microsoft.NETCore.App",
                    "version": "7.0.0"
                  },
                  {
                    "name": "Microsoft.AspNetCore.App",
                    "version": "7.0.0"
                  }
                ]
              }
            }
            """)?.AsObject();

        var success = DotnetRuntimeSetupResolver.TryReadRuntimeRequirements(document, out var requirements, out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.Equal(2, requirements.Count);
        Assert.Contains(requirements, requirement => requirement.Runtime == "dotnet" && requirement.Channel == "7.0");
        Assert.Contains(requirements, requirement => requirement.Runtime == "aspnetcore" && requirement.Channel == "7.0");
    }

    [Fact]
    public async Task ResolveForPlanItemAsync_UsesArchiveInspectionForLegacyTargets()
    {
        var item = new JsonObject
        {
            ["packageContentUrl"] = "https://nuget.test/sample.tool.1.0.0.nupkg",
        };

        var catalogLeaf = CreateCatalogLeaf(
            "tools/net7.0/any/Sample.Tool.dll",
            "tools/net7.0/any/Sample.Tool.runtimeconfig.json");

        using var httpClient = new HttpClient(new BinaryStubHttpMessageHandler(
            new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                [item["packageContentUrl"]!.GetValue<string>()] = CreatePackageArchiveBytes(
                    ("tools/net7.0/any/Sample.Tool.dll", string.Empty),
                    (
                        "tools/net7.0/any/Sample.Tool.runtimeconfig.json",
                        """
                        {
                          "runtimeOptions": {
                            "frameworks": [
                              {
                                "name": "Microsoft.NETCore.App",
                                "version": "7.0.0"
                              },
                              {
                                "name": "Microsoft.AspNetCore.App",
                                "version": "7.0.0"
                              }
                            ]
                          }
                        }
                        """)),
            }));
        var client = new NuGetApiClient(httpClient);

        var plan = await DotnetRuntimeSetupResolver.ResolveForPlanItemAsync(
            item,
            catalogLeaf,
            "ubuntu-latest",
            client,
            CancellationToken.None);

        Assert.Equal("runtime-only", plan.Mode);
        Assert.Equal("archive-runtimeconfig", plan.Source);
        Assert.Contains(plan.RequiredRuntimes, requirement => requirement.Runtime == "dotnet" && requirement.Channel == "7.0");
        Assert.Contains(plan.RequiredRuntimes, requirement => requirement.Runtime == "aspnetcore" && requirement.Channel == "7.0");
    }

    private static CatalogLeaf CreateCatalogLeaf(params string[] paths)
        => new(
            "https://nuget.test/catalog/sample.tool.1.0.0.json",
            Title: null,
            Description: null,
            ProjectUrl: null,
            Repository: null,
            paths.Select(path => new CatalogPackageEntry(path, Path.GetFileName(path))).ToArray(),
            DependencyGroups: null,
            PackageTypes: null);

    private static byte[] CreatePackageArchiveBytes(params (string Path, string Content)[] entries)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in entries)
            {
                var archiveEntry = archive.CreateEntry(entry.Path);
                if (string.IsNullOrEmpty(entry.Content))
                {
                    continue;
                }

                using var writer = new StreamWriter(archiveEntry.Open());
                writer.Write(entry.Content);
            }
        }

        return stream.ToArray();
    }

    private sealed class BinaryStubHttpMessageHandler : HttpMessageHandler
    {
        private readonly IReadOnlyDictionary<string, byte[]> _responses;

        public BinaryStubHttpMessageHandler(IReadOnlyDictionary<string, byte[]> responses)
        {
            _responses = responses;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri?.ToString() ?? throw new InvalidOperationException("Request URI is missing.");
            if (!_responses.TryGetValue(uri, out var content))
            {
                throw new InvalidOperationException($"Unexpected request URI '{uri}'.");
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(content),
            });
        }
    }
}

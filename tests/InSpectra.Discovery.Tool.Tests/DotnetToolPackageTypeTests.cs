using System.Text.Json;
using Xunit;

public sealed class DotnetToolPackageTypeTests
{
    [Fact]
    public void IsDotnetTool_AcceptsSingleObjectPackageType()
    {
        using var document = JsonDocument.Parse("""{ "name": "DotnetTool", "version": "0.0" }""");
        var leaf = new CatalogLeaf(
            Id: "https://nuget.test/catalog/example.json",
            Title: null,
            Description: null,
            ProjectUrl: null,
            Repository: null,
            PackageEntries: null,
            DependencyGroups: null,
            PackageTypes: document.RootElement.Clone());

        var result = DotnetToolPackageType.IsDotnetTool(leaf);

        Assert.True(result);
    }

    [Fact]
    public void IsDotnetTool_AcceptsArrayPackageTypes()
    {
        using var document = JsonDocument.Parse("""[{ "name": "Dependency" }, { "name": "DotnetTool" }]""");
        var leaf = new CatalogLeaf(
            Id: "https://nuget.test/catalog/example.json",
            Title: null,
            Description: null,
            ProjectUrl: null,
            Repository: null,
            PackageEntries: null,
            DependencyGroups: null,
            PackageTypes: document.RootElement.Clone());

        var result = DotnetToolPackageType.IsDotnetTool(leaf);

        Assert.True(result);
    }
}

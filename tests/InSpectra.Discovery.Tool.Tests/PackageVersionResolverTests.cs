namespace InSpectra.Discovery.Tool.Tests;

using Xunit;

public sealed class PackageVersionResolverTests
{
    [Fact]
    public async Task ResolveAsync_Allows_Registration_Page_Items_With_String_CatalogEntry()
    {
        const string serviceIndexUrl = "https://api.nuget.org/v3/index.json";
        const string registrationIndexUrl = "https://nuget.test/registration/cute/index.json";
        const string registrationPageUrl = "https://nuget.test/registration/cute/page/2.14.0/2.15.0.json";
        const string registrationLeafUrl = "https://nuget.test/registration/cute/2.15.0.json";
        const string catalogLeafUrl = "https://nuget.test/catalog/cute.2.15.0.json";

        using var httpClient = new HttpClient(new StubHttpMessageHandler(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [serviceIndexUrl] = """
                {
                  "resources": [
                    {
                      "@id": "https://nuget.test/registration",
                      "@type": "RegistrationsBaseUrl/3.6.0"
                    }
                  ]
                }
                """,
            [registrationIndexUrl] = """
                {
                  "@id": "https://nuget.test/registration/cute/index.json",
                  "items": [
                    {
                      "@id": "https://nuget.test/registration/cute/page/2.14.0/2.15.0.json",
                      "count": 2
                    }
                  ]
                }
                """,
            [registrationPageUrl] = """
                {
                  "items": [
                    {
                      "@id": "https://nuget.test/registration/cute/2.15.0.json",
                      "commitTimeStamp": "2026-03-27T13:29:43.8550432+01:00",
                      "catalogEntry": "https://nuget.test/catalog/cute.2.15.0.json",
                      "packageContent": "https://nuget.test/flat/cute.2.15.0.nupkg"
                    }
                  ]
                }
                """,
            [registrationLeafUrl] = """
                {
                  "@id": "https://nuget.test/registration/cute/2.15.0.json",
                  "catalogEntry": "https://nuget.test/catalog/cute.2.15.0.json",
                  "listed": true,
                  "packageContent": "https://nuget.test/flat/cute.2.15.0.nupkg",
                  "published": "2026-03-27T13:24:43.773+01:00"
                }
                """,
            [catalogLeafUrl] = """
                {
                  "@id": "https://nuget.test/catalog/cute.2.15.0.json",
                  "projectUrl": "https://github.com/andresharpe/cute",
                  "repository": {
                    "type": "git",
                    "url": "https://github.com/andresharpe/cute.git"
                  },
                  "packageEntries": [],
                  "dependencyGroups": []
                }
                """,
        }));
        var client = new NuGetApiClient(httpClient);

        var (leaf, catalogLeaf) = await PackageVersionResolver.ResolveAsync(client, "cute", "2.15.0", CancellationToken.None);

        Assert.Equal(registrationLeafUrl, leaf.Id);
        Assert.Equal(catalogLeafUrl, leaf.CatalogEntryUrl);
        Assert.Equal("https://github.com/andresharpe/cute", catalogLeaf.ProjectUrl);
    }

    [Theory]
    [InlineData("https://github.com/example/tool.git", "https://github.com/example/tool")]
    [InlineData("https://github.com/example/tool", "https://github.com/example/tool")]
    [InlineData(" https://github.com/example/tool.git ", "https://github.com/example/tool")]
    [InlineData("not-a-url.git", "not-a-url.git")]
    public void NormalizeRepositoryUrl_NormalizesExpectedForms(string input, string expected)
    {
        Assert.Equal(expected, PackageVersionResolver.NormalizeRepositoryUrl(input));
    }

    [Fact]
    public void NormalizeRepositoryUrl_ReturnsNullForWhitespace()
    {
        Assert.Null(PackageVersionResolver.NormalizeRepositoryUrl("   "));
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly IReadOnlyDictionary<string, string> _responses;

        public StubHttpMessageHandler(IReadOnlyDictionary<string, string> responses)
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

            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }
}


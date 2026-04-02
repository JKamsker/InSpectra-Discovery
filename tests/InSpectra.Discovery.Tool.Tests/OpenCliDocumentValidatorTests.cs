namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Infrastructure.Paths;
using InSpectra.Discovery.Tool.OpenCli.Documents;

using System.Text.Json.Nodes;
using Xunit;

public sealed class OpenCliDocumentValidatorTests
{
    [Fact]
    public void TryLoadValidDocument_Rejects_Missing_OpenCli_Marker()
    {
        using var tempDirectory = new TemporaryDirectory();
        var artifactPath = Path.Combine(tempDirectory.Path, "opencli.json");
        RepositoryPathResolver.WriteJsonFile(
            artifactPath,
            new JsonObject
            {
                ["info"] = new JsonObject
                {
                    ["title"] = "sample",
                },
            });

        var valid = OpenCliDocumentValidator.TryLoadValidDocument(artifactPath, out _, out var reason);

        Assert.False(valid);
        Assert.Equal("OpenCLI artifact is missing the root 'opencli' marker.", reason);
    }

    [Fact]
    public void TryLoadValidDocument_Accepts_Minimal_OpenCli_With_Surface()
    {
        using var tempDirectory = new TemporaryDirectory();
        var artifactPath = Path.Combine(tempDirectory.Path, "opencli.json");
        RepositoryPathResolver.WriteJsonFile(
            artifactPath,
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["options"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = "--verbose",
                    },
                },
            });

        var valid = OpenCliDocumentValidator.TryLoadValidDocument(artifactPath, out var document, out var reason);

        Assert.True(valid);
        Assert.NotNull(document);
        Assert.Null(reason);
    }

    [Fact]
    public void TryLoadValidDocument_Rejects_NonObject_Command_Entries()
    {
        using var tempDirectory = new TemporaryDirectory();
        var artifactPath = Path.Combine(tempDirectory.Path, "opencli.json");
        RepositoryPathResolver.WriteJsonFile(
            artifactPath,
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["commands"] = new JsonArray
                {
                    JsonValue.Create("serve"),
                },
            });

        var valid = OpenCliDocumentValidator.TryLoadValidDocument(artifactPath, out _, out var reason);

        Assert.False(valid);
        Assert.Equal("OpenCLI artifact has a non-object entry at '$.commands[0]'.", reason);
    }

    [Fact]
    public void TryLoadValidDocument_Rejects_NonString_Example_Entries()
    {
        using var tempDirectory = new TemporaryDirectory();
        var artifactPath = Path.Combine(tempDirectory.Path, "opencli.json");
        RepositoryPathResolver.WriteJsonFile(
            artifactPath,
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["commands"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = "serve",
                        ["examples"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["command"] = "serve",
                            },
                        },
                    },
                },
            });

        var valid = OpenCliDocumentValidator.TryLoadValidDocument(artifactPath, out _, out var reason);

        Assert.False(valid);
        Assert.Equal("OpenCLI artifact has a non-string entry at '$.commands[0].examples[0]'.", reason);
    }

    [Fact]
    public void TryLoadValidDocument_Rejects_Null_Arrays()
    {
        using var tempDirectory = new TemporaryDirectory();
        var artifactPath = Path.Combine(tempDirectory.Path, "opencli.json");
        RepositoryPathResolver.WriteJsonFile(
            artifactPath,
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["arguments"] = null,
                ["options"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = "--verbose",
                    },
                },
            });

        var valid = OpenCliDocumentValidator.TryLoadValidDocument(artifactPath, out _, out var reason);

        Assert.False(valid);
        Assert.Equal("OpenCLI artifact has a null 'arguments' property at '$'.", reason);
    }

    [Fact]
    public void TryLoadValidDocument_Rejects_Empty_Surface()
    {
        using var tempDirectory = new TemporaryDirectory();
        var artifactPath = Path.Combine(tempDirectory.Path, "opencli.json");
        RepositoryPathResolver.WriteJsonFile(
            artifactPath,
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["info"] = new JsonObject
                {
                    ["title"] = "demo",
                    ["version"] = "1.0.0",
                },
                ["commands"] = new JsonArray(),
            });

        var valid = OpenCliDocumentValidator.TryLoadValidDocument(artifactPath, out _, out var reason);

        Assert.False(valid);
        Assert.Equal("OpenCLI artifact does not expose any commands, options, or arguments.", reason);
    }

    [Fact]
    public void TryLoadValidDocument_Rejects_Default_Command_Nodes()
    {
        using var tempDirectory = new TemporaryDirectory();
        var artifactPath = Path.Combine(tempDirectory.Path, "opencli.json");
        RepositoryPathResolver.WriteJsonFile(
            artifactPath,
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["commands"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = "__default_command",
                    },
                },
            });

        var valid = OpenCliDocumentValidator.TryLoadValidDocument(artifactPath, out _, out var reason);

        Assert.False(valid);
        Assert.Equal("OpenCLI artifact contains a '__default_command' node at '$.commands[0]'.", reason);
    }

    [Fact]
    public void TryLoadValidDocument_Rejects_NonPublishable_Command_Names()
    {
        using var tempDirectory = new TemporaryDirectory();
        var artifactPath = Path.Combine(tempDirectory.Path, "opencli.json");
        RepositoryPathResolver.WriteJsonFile(
            artifactPath,
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["commands"] = new JsonArray
                {
                    CreateLeafCommand("."),
                },
            });

        var valid = OpenCliDocumentValidator.TryLoadValidDocument(artifactPath, out _, out var reason);

        Assert.False(valid);
        Assert.Equal("OpenCLI artifact has a non-publishable command name '.' at '$.commands[0]'.", reason);
    }

    [Fact]
    public void TryLoadValidDocument_Rejects_AngleBracket_Command_Names()
    {
        using var tempDirectory = new TemporaryDirectory();
        var artifactPath = Path.Combine(tempDirectory.Path, "opencli.json");
        RepositoryPathResolver.WriteJsonFile(
            artifactPath,
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["commands"] = new JsonArray
                {
                    CreateLeafCommand("<clone>$"),
                },
            });

        var valid = OpenCliDocumentValidator.TryLoadValidDocument(artifactPath, out _, out var reason);

        Assert.False(valid);
        Assert.Equal("OpenCLI artifact has a non-publishable command name '<clone>$' at '$.commands[0]'.", reason);
    }

    [Fact]
    public void TryLoadValidDocument_Rejects_NonPublishable_Argument_Names()
    {
        using var tempDirectory = new TemporaryDirectory();
        var artifactPath = Path.Combine(tempDirectory.Path, "opencli.json");
        RepositoryPathResolver.WriteJsonFile(
            artifactPath,
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["commands"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = "serve",
                        ["options"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["name"] = "--project",
                                ["arguments"] = new JsonArray
                                {
                                    new JsonObject
                                    {
                                        ["name"] = "#=ZFSB8$OO=",
                                    },
                                },
                            },
                        },
                    },
                },
            });

        var valid = OpenCliDocumentValidator.TryLoadValidDocument(artifactPath, out _, out var reason);

        Assert.False(valid);
        Assert.Equal("OpenCLI artifact has a non-publishable argument name '#=ZFSB8$OO=' at '$.commands[0].options[0].arguments[0]'.", reason);
    }

    [Fact]
    public void TryLoadValidDocument_Rejects_NonPublishable_Option_Names()
    {
        using var tempDirectory = new TemporaryDirectory();
        var artifactPath = Path.Combine(tempDirectory.Path, "opencli.json");
        RepositoryPathResolver.WriteJsonFile(
            artifactPath,
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["options"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = "⎼⎼⎼⎼⎼⎼⎼⎼⎼⎼⎼⎼⎼⎼⎼⎼⎼⎼",
                    },
                },
            });

        var valid = OpenCliDocumentValidator.TryLoadValidDocument(artifactPath, out _, out var reason);

        Assert.False(valid);
        Assert.Equal("OpenCLI artifact has a non-publishable option name '⎼⎼⎼⎼⎼⎼⎼⎼⎼⎼⎼⎼⎼⎼⎼⎼⎼⎼' at '$.options[0]'.", reason);
    }

    [Fact]
    public void TryLoadValidDocument_Rejects_Short_Separator_Option_Names()
    {
        using var tempDirectory = new TemporaryDirectory();
        var artifactPath = Path.Combine(tempDirectory.Path, "opencli.json");
        RepositoryPathResolver.WriteJsonFile(
            artifactPath,
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["options"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = "---",
                    },
                },
            });

        var valid = OpenCliDocumentValidator.TryLoadValidDocument(artifactPath, out _, out var reason);

        Assert.False(valid);
        Assert.Equal("OpenCLI artifact has a non-publishable option name '---' at '$.options[0]'.", reason);
    }

    [Fact]
    public void TryLoadValidDocument_Rejects_Environment_Snippet_Command_Names()
    {
        using var tempDirectory = new TemporaryDirectory();
        var artifactPath = Path.Combine(tempDirectory.Path, "opencli.json");
        RepositoryPathResolver.WriteJsonFile(
            artifactPath,
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["commands"] = new JsonArray
                {
                    CreateLeafCommand("\"UV_PYTHON_DOWNLOADS=never\"]"),
                },
            });

        var valid = OpenCliDocumentValidator.TryLoadValidDocument(artifactPath, out _, out var reason);

        Assert.False(valid);
        Assert.Equal("OpenCLI artifact has a non-publishable command name '\"UV_PYTHON_DOWNLOADS=never\"]' at '$.commands[0]'.", reason);
    }

    [Fact]
    public void TryLoadValidDocument_Accepts_Directive_Style_Command_Names()
    {
        using var tempDirectory = new TemporaryDirectory();
        var artifactPath = Path.Combine(tempDirectory.Path, "opencli.json");
        RepositoryPathResolver.WriteJsonFile(
            artifactPath,
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["commands"] = new JsonArray
                {
                    CreateLeafCommand("#!who"),
                },
            });

        var valid = OpenCliDocumentValidator.TryLoadValidDocument(artifactPath, out var document, out var reason);

        Assert.True(valid);
        Assert.NotNull(document);
        Assert.Null(reason);
    }

    [Fact]
    public void TryLoadValidDocument_Accepts_Command_Name_Repeated_Up_To_Three_Times_In_One_Path()
    {
        using var tempDirectory = new TemporaryDirectory();
        var artifactPath = Path.Combine(tempDirectory.Path, "opencli.json");
        RepositoryPathResolver.WriteJsonFile(
            artifactPath,
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["commands"] = new JsonArray
                {
                    CreateCommandPathNode(["command", "input", "command", "input", "command", "leaf"]),
                },
            });

        var valid = OpenCliDocumentValidator.TryLoadValidDocument(artifactPath, out var document, out var reason);

        Assert.True(valid);
        Assert.NotNull(document);
        Assert.Null(reason);
    }

    [Fact]
    public void TryLoadValidDocument_Rejects_Command_Name_Repeated_More_Than_Three_Times_In_One_Path()
    {
        using var tempDirectory = new TemporaryDirectory();
        var artifactPath = Path.Combine(tempDirectory.Path, "opencli.json");
        RepositoryPathResolver.WriteJsonFile(
            artifactPath,
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["commands"] = new JsonArray
                {
                    CreateCommandPathNode(["command", "input", "command", "input", "command", "input", "command"]),
                },
            });

        var valid = OpenCliDocumentValidator.TryLoadValidDocument(artifactPath, out _, out var reason);

        Assert.False(valid);
        Assert.Contains("OpenCLI artifact repeats command name 'command' more than 3 times", reason);
        Assert.Contains("$.commands[0].commands[0].commands[0].commands[0]", reason);
    }

    [Fact]
    public void TryLoadValidDocument_Rejects_Duplicate_Option_Tokens()
    {
        using var tempDirectory = new TemporaryDirectory();
        var artifactPath = Path.Combine(tempDirectory.Path, "opencli.json");
        RepositoryPathResolver.WriteJsonFile(
            artifactPath,
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["options"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = "--help",
                        ["aliases"] = new JsonArray
                        {
                            "-h",
                        },
                    },
                    new JsonObject
                    {
                        ["name"] = "-h",
                    },
                },
            });

        var valid = OpenCliDocumentValidator.TryLoadValidDocument(artifactPath, out _, out var reason);

        Assert.False(valid);
        Assert.Equal("OpenCLI artifact has a duplicate option token '-h' at '$.options[1]' colliding with '$.options[0]'.", reason);
    }

    [Fact]
    public void TryLoadValidDocument_Allows_Case_Distinct_Option_Tokens()
    {
        using var tempDirectory = new TemporaryDirectory();
        var artifactPath = Path.Combine(tempDirectory.Path, "opencli.json");
        RepositoryPathResolver.WriteJsonFile(
            artifactPath,
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["options"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = "--name",
                        ["aliases"] = new JsonArray
                        {
                            "-n",
                        },
                    },
                    new JsonObject
                    {
                        ["name"] = "--namespace",
                        ["aliases"] = new JsonArray
                        {
                            "-N",
                        },
                    },
                },
            });

        var valid = OpenCliDocumentValidator.TryLoadValidDocument(artifactPath, out _, out var reason);

        Assert.True(valid);
        Assert.Null(reason);
    }

    [Fact]
    public void TryLoadValidDocument_Rejects_NonPublishable_Info_Text()
    {
        using var tempDirectory = new TemporaryDirectory();
        var artifactPath = Path.Combine(tempDirectory.Path, "opencli.json");
        RepositoryPathResolver.WriteJsonFile(
            artifactPath,
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["info"] = new JsonObject
                {
                    ["title"] = "No embedding provider configured. Defaulting to LocalONNX which requires native ONNX runtime. If this fails, set EmbeddingProvider to OpenAI via: demo --config set EmbeddingProvider=OpenAI",
                },
                ["options"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = "--verbose",
                    },
                },
            });

        var valid = OpenCliDocumentValidator.TryLoadValidDocument(artifactPath, out _, out var reason);

        Assert.False(valid);
        Assert.Equal("OpenCLI artifact has a non-publishable 'info.title' value.", reason);
    }

    [Fact]
    public void TryLoadValidDocument_Rejects_StartupHook_Dotnet_Host_Captures()
    {
        using var tempDirectory = new TemporaryDirectory();
        var artifactPath = Path.Combine(tempDirectory.Path, "opencli.json");
        RepositoryPathResolver.WriteJsonFile(
            artifactPath,
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["info"] = new JsonObject
                {
                    ["title"] = "dotnet",
                    ["version"] = "1.1.5",
                },
                ["x-inspectra"] = new JsonObject
                {
                    ["artifactSource"] = "startup-hook",
                },
                ["commands"] = new JsonArray
                {
                    CreateLeafCommand("add"),
                    CreateLeafCommand("build"),
                    CreateLeafCommand("clean"),
                    CreateLeafCommand("nuget"),
                    CreateLeafCommand("restore"),
                    CreateLeafCommand("run"),
                    CreateLeafCommand("test"),
                    CreateLeafCommand("tool"),
                },
            });

        var valid = OpenCliDocumentValidator.TryLoadValidDocument(artifactPath, out _, out var reason);

        Assert.False(valid);
        Assert.Equal("OpenCLI artifact looks like a startup-hook capture of the dotnet host instead of the installed tool.", reason);
    }

    [Fact]
    public void TryValidateDocument_Rejects_Implausibly_Large_Artifacts()
    {
        var document = new JsonObject
        {
            ["opencli"] = "0.1-draft",
            ["info"] = new JsonObject
            {
                ["title"] = "demo",
                ["description"] = new string('a', (int)OpenCliDocumentValidator.MaxArtifactSizeBytes),
            },
            ["options"] = new JsonArray
            {
                new JsonObject
                {
                    ["name"] = "--verbose",
                },
            },
        };

        var valid = OpenCliDocumentValidator.TryValidateDocument(document, out var reason);

        Assert.False(valid);
        Assert.Equal("OpenCLI artifact is implausibly large (2 MB).", reason);
    }

    private static JsonObject CreateCommandPathNode(IReadOnlyList<string> commandNames, int index = 0)
    {
        var node = new JsonObject
        {
            ["name"] = commandNames[index],
            ["hidden"] = false,
        };

        if (index == commandNames.Count - 1)
        {
            node["options"] = new JsonArray
            {
                new JsonObject
                {
                    ["name"] = "--verbose",
                },
            };
            return node;
        }

        node["commands"] = new JsonArray
        {
            CreateCommandPathNode(commandNames, index + 1),
        };
        return node;
    }

    private static JsonObject CreateLeafCommand(string name)
        => new()
        {
            ["name"] = name,
            ["hidden"] = false,
            ["options"] = new JsonArray
            {
                new JsonObject
                {
                    ["name"] = "--help",
                },
            },
        };

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"inspectra-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}

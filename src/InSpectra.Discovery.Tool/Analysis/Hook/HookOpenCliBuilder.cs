namespace InSpectra.Discovery.Tool.Analysis.Hook;

using InSpectra.Discovery.Tool.OpenCli.Documents;

using System.Text.Json.Nodes;

internal static class HookOpenCliBuilder
{
    public static JsonObject Build(string commandName, string version, HookCaptureResult capture)
    {
        var root = capture.Root!;
        var document = new JsonObject
        {
            ["opencli"] = "0.1-draft",
            ["info"] = new JsonObject
            {
                ["title"] = root.Name ?? commandName,
                ["version"] = version,
                ["description"] = root.Description,
            },
            ["x-inspectra"] = new JsonObject
            {
                ["artifactSource"] = "startup-hook",
                ["generator"] = "InSpectra.Discovery",
                ["hookCapture"] = new JsonObject
                {
                    ["cliFramework"] = capture.CliFramework,
                    ["frameworkVersion"] = capture.FrameworkVersion,
                    ["systemCommandLineVersion"] = capture.SystemCommandLineVersion,
                    ["patchTarget"] = capture.PatchTarget,
                },
            },
        };

        // Root-level options.
        var rootOptions = BuildOptions(root.Options);
        if (rootOptions.Count > 0)
            document["options"] = rootOptions;

        // Root-level arguments.
        var rootArguments = BuildArguments(root.Arguments);
        if (rootArguments.Count > 0)
            document["arguments"] = rootArguments;

        // Subcommands.
        var commands = BuildCommands(root.Subcommands);
        if (commands.Count > 0)
            document["commands"] = commands;

        return OpenCliDocumentSanitizer.Sanitize(document);
    }

    private static JsonArray BuildCommands(List<HookCapturedCommand> subcommands)
    {
        var array = new JsonArray();
        foreach (var cmd in subcommands)
        {
            if (cmd.Name is null) continue;

            var node = new JsonObject
            {
                ["name"] = cmd.Name,
            };

            if (!string.IsNullOrWhiteSpace(cmd.Description))
                node["description"] = cmd.Description;
            if (cmd.IsHidden)
                node["hidden"] = true;

            var aliases = BuildAliases(cmd.Aliases, cmd.Name);
            if (aliases.Count > 0)
                node["aliases"] = aliases;

            var options = BuildOptions(cmd.Options);
            if (options.Count > 0)
                node["options"] = options;

            var arguments = BuildArguments(cmd.Arguments);
            if (arguments.Count > 0)
                node["arguments"] = arguments;

            var nested = BuildCommands(cmd.Subcommands);
            if (nested.Count > 0)
                node["commands"] = nested;

            array.Add(node);
        }
        return array;
    }

    private static JsonArray BuildOptions(List<HookCapturedOption> options)
    {
        var array = new JsonArray();
        foreach (var opt in options)
        {
            if (opt.Name is null) continue;

            var node = new JsonObject
            {
                ["name"] = opt.Name,
                ["recursive"] = opt.Recursive,
                ["hidden"] = opt.IsHidden,
            };

            if (!string.IsNullOrWhiteSpace(opt.Description))
            {
                // Append default value to description (matching help-crawl format).
                var desc = opt.Description;
                if (opt.HasDefaultValue && opt.DefaultValue is not null)
                    desc += $" [default: {opt.DefaultValue}]";
                node["description"] = desc;
            }

            if (opt.IsRequired)
                node["required"] = true;

            var aliases = BuildAliases(opt.Aliases, opt.Name);
            if (aliases.Count > 0)
                node["aliases"] = aliases;

            // Arguments sub-node for the option's value.
            if (opt.ValueType is not null && opt.ValueType is not "Void")
            {
                var argNode = new JsonObject();

                // Argument name (e.g., "SERVER", "COUNT") — matches help-crawl output.
                var argName = opt.ArgumentName;
                if (!string.IsNullOrWhiteSpace(argName))
                    argNode["name"] = argName.ToUpperInvariant();

                // Boolean options are flags — arity 0..1, not required.
                var isFlag = opt.ValueType == "Boolean";
                argNode["required"] = !isFlag && opt.MinArity > 0;

                argNode["arity"] = new JsonObject
                {
                    ["minimum"] = opt.MinArity,
                    ["maximum"] = opt.MaxArity,
                };

                if (opt.ValueType is not null)
                    argNode["type"] = opt.ValueType;

                if (opt.AllowedValues is { Count: > 0 })
                {
                    var valuesArray = new JsonArray();
                    foreach (var v in opt.AllowedValues) valuesArray.Add(v);
                    argNode["allowedValues"] = valuesArray;
                }

                node["arguments"] = new JsonArray { argNode };
            }

            array.Add(node);
        }
        return array;
    }

    private static JsonArray BuildArguments(List<HookCapturedArgument> arguments)
    {
        var array = new JsonArray();
        foreach (var arg in arguments)
        {
            var node = new JsonObject
            {
                ["name"] = arg.Name ?? "value",
            };

            if (!string.IsNullOrWhiteSpace(arg.Description))
            {
                var desc = arg.Description;
                if (arg.HasDefaultValue && arg.DefaultValue is not null)
                    desc += $" [default: {arg.DefaultValue}]";
                node["description"] = desc;
            }

            if (arg.IsHidden)
                node["hidden"] = true;

            node["required"] = arg.MinArity > 0;

            if (arg.MinArity > 0 || arg.MaxArity > 0)
            {
                node["arity"] = new JsonObject
                {
                    ["minimum"] = arg.MinArity,
                    ["maximum"] = arg.MaxArity,
                };
            }

            if (arg.ValueType is not null)
                node["type"] = arg.ValueType;

            if (arg.AllowedValues is { Count: > 0 })
            {
                var valuesArray = new JsonArray();
                foreach (var v in arg.AllowedValues) valuesArray.Add(v);
                node["allowedValues"] = valuesArray;
            }

            array.Add(node);
        }
        return array;
    }

    private static JsonArray BuildAliases(List<string> aliases, string primaryName)
    {
        var array = new JsonArray();
        foreach (var alias in aliases)
        {
            if (!string.Equals(alias, primaryName, StringComparison.Ordinal))
                array.Add(alias);
        }
        return array;
    }
}


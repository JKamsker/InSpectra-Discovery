# NuGet Tool CLI Framework Usage Report

Generated: 2026-03-27T21:58:06.759Z

Scope: top 100 and top 1000 `DotnetTool` packages by `totalDownloads`, using the latest package version exposed by the NuGet search endpoint.

Method: classify each tool by scanning the latest `.nupkg` central directory for known CLI framework DLLs.

Limitation: the NuGet search API reported 6,797 `DotnetTool` packages, but only 4,000 were retrievable through paged search results. The rankings below are based on that retrievable set.

## Key Findings

- Top 100: 60 tools matched a known CLI framework; 40 were `Unknown/Custom`.
- Top 1000: 652 tools matched a known CLI framework; 348 were `Unknown/Custom`.
- Dominant frameworks in the top 1000: `System.CommandLine` (227), `CommandLineParser` (209), `McMaster.Extensions.CommandLineUtils` (88), and `Spectre.Console.Cli` (64).
- `CliFx` appears in 4 of the top 1000 scanned tools.

## Framework Ranking: Top 100

| Rank | Framework | Tools | Share of Top 100 |
| --- | --- | --- | --- |
| 1 | System.CommandLine | 29 | 29.0% |
| 2 | CommandLineParser | 10 | 10.0% |
| 3 | McMaster.Extensions.CommandLineUtils | 10 | 10.0% |
| 4 | Spectre.Console.Cli | 9 | 9.0% |
| 5 | Argu | 3 | 3.0% |
| 6 | Mono.Options / NDesk.Options | 2 | 2.0% |
| 7 | CliFx | 1 | 1.0% |
| 8 | Microsoft.Extensions.CommandLineUtils | 1 | 1.0% |
| 9 | PowerArgs | 1 | 1.0% |

## Framework Ranking: Top 1000

| Rank | Framework | Tools | Share of Top 1000 |
| --- | --- | --- | --- |
| 1 | System.CommandLine | 227 | 22.7% |
| 2 | CommandLineParser | 209 | 20.9% |
| 3 | McMaster.Extensions.CommandLineUtils | 88 | 8.8% |
| 4 | Spectre.Console.Cli | 64 | 6.4% |
| 5 | Mono.Options / NDesk.Options | 28 | 2.8% |
| 6 | Argu | 27 | 2.7% |
| 7 | Microsoft.Extensions.CommandLineUtils | 20 | 2.0% |
| 8 | PowerArgs | 8 | 0.8% |
| 9 | Cocona | 7 | 0.7% |
| 10 | ConsoleAppFramework | 6 | 0.6% |
| 11 | ManyConsole | 6 | 0.6% |
| 12 | CliFx | 4 | 0.4% |
| 13 | CommandDotNet | 2 | 0.2% |
| 14 | DocoptNet | 2 | 0.2% |
| 15 | Oakton | 1 | 0.1% |

## CliFx Tools in the Top 1000

| Rank | Package | Downloads | Detected Frameworks |
| --- | --- | --- | --- |
| 28 | Husky | 7,235,681 | CliFx |
| 223 | ZeroQL.Cli | 256,605 | CliFx |
| 365 | WildernessLabs.Meadow.CLI | 107,367 | CliFx |
| 835 | Chickensoft.GodotEnv | 29,205 | CliFx |

## Artifacts

- `nuget-tool-cli-framework-top1000.csv`: per-tool ranking, version, download count, and detected frameworks for the top 1000 scanned tools.
- `nuget-tool-cli-framework-top1000.json`: machine-readable summary plus the full classified top-1000 dataset.

## Framework Examples

### Argu

| Rank | Package | Downloads |
| --- | --- | --- |
| 15 | Paket | 15,123,061 |
| 52 | fantomas | 2,686,952 |
| 97 | fantomas-tool | 1,062,621 |
| 116 | csharp-ls | 797,619 |
| 131 | smite-cli | 643,737 |

### CliFx

| Rank | Package | Downloads |
| --- | --- | --- |
| 28 | Husky | 7,235,681 |
| 223 | ZeroQL.Cli | 256,605 |
| 365 | WildernessLabs.Meadow.CLI | 107,367 |
| 835 | Chickensoft.GodotEnv | 29,205 |

### Cocona

| Rank | Package | Downloads |
| --- | --- | --- |
| 103 | Libplanet.Tools | 921,891 |
| 132 | Credfeto.Package.Push | 629,979 |
| 362 | mslack | 108,232 |
| 397 | Credfeto.Tsql.Formatter.Cmd | 92,514 |
| 480 | htmlc | 67,618 |

### CommandDotNet

| Rank | Package | Downloads |
| --- | --- | --- |
| 483 | Squidex.CLI | 67,125 |
| 976 | Amusoft.VisualStudio.TemplateGenerator.CommandLine | 23,779 |

### CommandLineParser

| Rank | Package | Downloads |
| --- | --- | --- |
| 21 | JetBrains.ReSharper.GlobalTools | 12,373,082 |
| 53 | dotnet-project-licenses | 2,599,805 |
| 56 | snapx | 2,510,514 |
| 61 | Microsoft.CST.DevSkim.CLI | 2,291,604 |
| 66 | Credfeto.ChangeLog.Cmd | 1,966,527 |

### ConsoleAppFramework

| Rank | Package | Downloads |
| --- | --- | --- |
| 135 | MessagePack.Generator | 618,670 |
| 162 | MasterMemory.Generator | 444,249 |
| 497 | MagicOnion.Generator | 63,399 |
| 571 | Ulid-Cli | 51,253 |
| 795 | trxlog2html | 30,905 |

### DocoptNet

| Rank | Package | Downloads |
| --- | --- | --- |
| 122 | coveralls.net | 728,746 |
| 527 | Tyrannoport | 55,501 |

### ManyConsole

| Rank | Package | Downloads |
| --- | --- | --- |
| 300 | ReGitLint | 158,110 |
| 308 | NVika | 151,723 |
| 549 | NuSight | 52,698 |
| 671 | SIL.Machine.Morphology.HermitCrab.Tool | 40,373 |
| 794 | AMorrison.Mutant | 30,951 |

### McMaster.Extensions.CommandLineUtils

| Rank | Package | Downloads |
| --- | --- | --- |
| 7 | dotnet-serve | 37,883,026 |
| 33 | dotnet-script | 6,514,790 |
| 35 | dotnet-outdated-tool | 5,653,350 |
| 36 | dotnet-stryker | 5,649,506 |
| 62 | StrawberryShake.Tools | 2,207,042 |

### Microsoft.Extensions.CommandLineUtils

| Rank | Package | Downloads |
| --- | --- | --- |
| 61 | Microsoft.CST.DevSkim.CLI | 2,291,604 |
| 134 | dotnet-version-cli | 620,618 |
| 136 | SourceLink | 596,116 |
| 171 | dotnet-ildasm | 386,894 |
| 177 | NuGetKeyVaultSignTool | 367,954 |

### Mono.Options / NDesk.Options

| Rank | Package | Downloads |
| --- | --- | --- |
| 65 | Pickles.CommandLine | 1,969,774 |
| 89 | security-scan | 1,235,884 |
| 130 | dotnet-xscgen | 664,913 |
| 147 | altcover.global | 531,270 |
| 175 | apkdiff | 371,093 |

### Oakton

| Rank | Package | Downloads |
| --- | --- | --- |
| 922 | dotnet-nswagen | 25,646 |

### PowerArgs

| Rank | Package | Downloads |
| --- | --- | --- |
| 74 | Microsoft.Sbom.DotNetTool | 1,668,253 |
| 298 | DependenSee | 161,726 |
| 500 | Microsoft.Artifacts.CredentialProvider.NuGet.Tool | 63,001 |
| 504 | PullRequestReleaseNotes.Tool | 62,840 |
| 781 | automatica-cli | 31,875 |

### Spectre.Console.Cli

| Rank | Package | Downloads |
| --- | --- | --- |
| 1 | Cake.Tool | 153,269,359 |
| 23 | Refitter | 11,253,402 |
| 38 | docfx | 4,506,077 |
| 42 | Rapicgen | 3,394,864 |
| 64 | AWS.Deploy.Tools | 2,042,864 |

### System.CommandLine

| Rank | Package | Downloads |
| --- | --- | --- |
| 6 | dotnet-dump | 59,650,228 |
| 10 | Microsoft.dotnet-interactive | 35,167,722 |
| 11 | dotnet-coverage | 34,913,363 |
| 12 | coverlet.console | 16,997,973 |
| 13 | dotnet-trace | 15,857,772 |

## Notes

- A tool can match multiple frameworks if it ships multiple parser/CLI assemblies.
- `Unknown/Custom` means none of the currently configured framework DLL signatures were present in the shipped package contents.
- This report uses package contents rather than NuGet dependency metadata, because many tools bundle the framework assembly directly.

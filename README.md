# InSpectra-Discovery

> **Companion repository for [InSpectra](https://github.com/JKamsker/InSpectra).**
> This repo handles the automated discovery, filtering, and analysis of .NET CLI tools, with scheduled all-tool discovery that prefers native Spectre.Console.Cli OpenCLI extraction and falls back to generic help-based indexing for other CLI frameworks. The resulting index feeds the main InSpectra project.

## What it does

InSpectra-Discovery is an automated pipeline that:

1. **Discovers** all dotnet-tool packages published on NuGet via the V3 catalog API.
2. **Filters** packages into analysis queues, including scheduled all-tool discovery and manual/research-driven framework batches.
3. **Analyzes** each tool by installing it in a sandbox, preferring native `--opencli` / XML documentation when a `Spectre.Console.Cli` tool supports it, and otherwise falling back to a generic recursive `--help` crawl.
4. **Maintains** a versioned index of analyzed tools with metadata, CLI structure, and documentation artifacts.
5. **Runs continuously** via GitHub Actions workflows that detect new/updated packages and queue them for analysis.

## Pipeline overview

```mermaid
flowchart TD
    subgraph Discovery["Discovery (scheduled 5x daily)"]
        NuGet[(NuGet V3 Catalog)]
        Tool["catalog build / catalog delta discover"]
        NuGet -->|poll catalog| Tool
        Tool --> Snapshot["dotnet-tools snapshot"]
    end

    subgraph Filtering
        Snapshot --> Filter["catalog delta queue-all-tools"]
        Filter -->|"changed current
dotnet tools"| Filtered["Scheduled analysis queue"]
    end

    subgraph Analysis["Analysis (GitHub Actions)"]
        Filtered --> Queue["Analysis queue"]
        Queue --> Dispatch["dispatch-discovery-queue-analysis"]
        Dispatch -->|batch slices| Analyze["analyze-untrusted-batch"]
        Analyze -->|"install tool
prefer native Spectre OpenCLI/XMLDoc
fallback to recursive --help crawl"| Results["Analysis results"]
    end

    subgraph Promotion
        Results --> Promote["promote-untrusted-analysis-results"]
        Promote --> Index["index/"]
    end

    subgraph Artifacts["Index artifacts (per package/version)"]
        Index --> metadata.json
        Index --> opencli.json
        Index --> xmldoc.xml
        Index --> all.json["all.json (manifest)"]
    end

    all.json --->|feeds| InSpectra["InSpectra (main repo)"]

    style Discovery fill:#1a3a4a,stroke:#4aa3c5,color:#e0e0e0
    style Filtering fill:#3a2a10,stroke:#c98a2e,color:#e0e0e0
    style Analysis fill:#3a1525,stroke:#c44a6c,color:#e0e0e0
    style Promotion fill:#1a3a1a,stroke:#4a9a4a,color:#e0e0e0
    style Artifacts fill:#2a1a3a,stroke:#8a5aaa,color:#e0e0e0
    style InSpectra fill:#3a3510,stroke:#c9a82e,color:#e0e0e0,stroke-width:2px
```

## Repository structure

```
src/InSpectra.Discovery.Tool/        # .NET 8 tool/CLI (discovery, analysis, promotion)
scripts/                             # Legacy/manual PowerShell helpers
.github/workflows/                   # CI/CD pipelines (scheduled discovery, batch analysis)
docs/Plans/                          # Reusable checked-in analysis plans
index/                               # Output: analyzed tool index (all.json + per-package artifacts)
state/                               # Persistent state (catalog cursors, queues, deltas)
tests/                               # xUnit tests
```

## How it works

### Discovery

The discovery tool enumerates NuGet's autocomplete and registration APIs to build a snapshot of all dotnet-tool packages, enriched with download counts:

```bash
dotnet run --project src/InSpectra.Discovery.Tool -- catalog build --concurrency 16
```

Incremental updates use the NuGet catalog cursor to detect only new or changed packages:

```bash
dotnet run --project src/InSpectra.Discovery.Tool -- catalog delta discover
```

### Filtering

Scheduled discovery now queues all changed current dotnet tools for analysis:

```bash
dotnet run --project src/InSpectra.Discovery.Tool -- catalog delta queue-all-tools
```

To converge the repository toward full current-version coverage, the scheduler can also build a backlog queue of latest versions that are still missing analysis, retryable, or stuck in legacy native-analysis states:

```bash
dotnet run --project src/InSpectra.Discovery.Tool -- queue backfill-current-analysis --current-snapshot state/discovery/dotnet-tools.current.json --output state/discovery/dotnet-tools.current-backfill.queue.json --take 100
```

Focused research workflows can still filter to packages that depend on `Spectre.Console.Cli`:

```bash
dotnet run --project src/InSpectra.Discovery.Tool -- catalog filter spectre-console-cli --concurrency 16
```

### Analysis

Discovered tools are analyzed via the discovery CLI. The scheduled path prefers native Spectre OpenCLI/XMLDoc extraction, falls back to dedicated `CliFx` analysis when that framework is detected, and otherwise uses generic help crawling for other well-behaving tools:

```powershell
dotnet run --project src/InSpectra.Discovery.Tool -- analysis run-auto --package-id JellyfinCli --version 0.1.16 --output-root artifacts/analysis/jellyfincli --batch-id manual
```

For non-Spectre tools that do not expose native `--opencli`, the checked-in help batch can run generic-help and `CliFx` representatives and emit a promotion-ready `expected.json`:

```powershell
dotnet run --project src/InSpectra.Discovery.Tool -- analysis run-help-batch --plan docs/Plans/validated-generic-help-frameworks.json --output-root artifacts/help-batches/validated-frameworks --source help-index-batch
dotnet run --project src/InSpectra.Discovery.Tool -- promotion apply-untrusted --download-root artifacts/help-batches/validated-frameworks
```

The sample plan in [docs/Plans/validated-generic-help-frameworks.json](docs/Plans/validated-generic-help-frameworks.json) covers validated representatives for `CliFx`, `Argu`, `McMaster.Extensions.CommandLineUtils`, `Spectre.Console.Cli`, `Cocona`, `DocoptNet`, `System.CommandLine`, `CommandLineParser`, `Mono.Options / NDesk.Options`, `Microsoft.Extensions.CommandLineUtils`, `ConsoleAppFramework`, `CommandDotNet`, and `PowerArgs`.

Items can declare `"analysisMode": "help"`, `"analysisMode": "clifx"`, or `"analysisMode": "native"`. `run-help-batch` executes the `help` and `clifx` items, and records the others in the plan's `skipped` array. The checked-in plan uses that for `Cake.Tool`, which stays indexed through the richer native OpenCLI/XMLDoc path instead of being downgraded to a help-only partial entry, while `Husky` now exercises the dedicated `CliFx` analyzer path.

Crawler-based analysis now always emits `crawl.json`, even when the parser fails to synthesize a usable `opencli.json`, so raw help captures remain available for later parser fixes without re-running the tool.

### Output artifacts

Each analyzed tool produces versioned artifacts under `index/packages/{packageId}/{version}/`:

| File | Description |
|---|---|
| `metadata.json` | Package info, detection results, introspection status, timing data |
| `opencli.json` | Parsed CLI command structure |
| `crawl.json` | Raw recursive help-crawl captures preserved for re-parsing and parser debugging |
| `xmldoc.xml` | Extracted XML documentation |

A global manifest at `index/all.json` lists all indexed packages with their latest status.

## CI/CD

| Workflow | Schedule | Purpose |
|---|---|---|
| `discover-dotnet-tool-updates` | 5x daily | Polls NuGet catalog for new/updated tools |
| `dispatch-discovery-queue-analysis` | On demand | Slices the analysis queue into batches |
| `analyze-untrusted-batch` | On demand | Runs sandboxed analysis on queued tools |
| `promote-untrusted-analysis-results` | On demand | Promotes successful results into the main index |
| `index-unindexed-nuget-tools` | On demand | Builds the current unindexed backlog, analyzes 250-item batches in parallel, and promotes by merging the batch plans/artifacts |
| `queue-indexed-metadata-backfill` | On demand | Backfills historical versions for indexed packages |

## Prerequisites

- .NET 10.0 SDK
- PowerShell 7+ for legacy/manual scripts only

## Building and testing

```bash
dotnet build InSpectra.Discovery.sln
dotnet test
```

## License

See the main [InSpectra](https://github.com/JKamsker/InSpectra) repository for license information.

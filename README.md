# InSpectra-Discovery

> **Companion repository for [InSpectra](https://github.com/JKamsker/InSpectra).**
> This repo handles the automated discovery, filtering, and analysis of .NET CLI tools that use [Spectre.Console](https://spectreconsole.net/). The resulting index feeds the main InSpectra project.

## What it does

InSpectra-Discovery is an automated pipeline that:

1. **Discovers** all dotnet-tool packages published on NuGet via the V3 catalog API.
2. **Filters** packages down to those that depend on `Spectre.Console.Cli`.
3. **Analyzes** each tool by installing it in a sandbox, extracting its CLI structure (`--opencli`) and XML documentation.
4. **Maintains** a versioned index of analyzed tools with metadata, CLI structure, and documentation artifacts.
5. **Runs continuously** via GitHub Actions workflows that detect new/updated packages and queue them for analysis.

## Repository structure

```
src/InSpectra.Discovery.Bootstrap/   # .NET 8 console app (discovery & filtering)
scripts/                             # PowerShell automation (analysis, promotion, backfill)
.github/workflows/                   # CI/CD pipelines (scheduled discovery, batch analysis)
index/                               # Output: analyzed tool index (all.json + per-package artifacts)
state/                               # Persistent state (catalog cursors, queues, deltas)
tests/                               # xUnit tests
```

## How it works

### Discovery

The bootstrap app enumerates NuGet's autocomplete and registration APIs to build a snapshot of all dotnet-tool packages, enriched with download counts:

```bash
dotnet run --project src/InSpectra.Discovery.Bootstrap -- index build --concurrency 16
```

Incremental updates use the NuGet catalog cursor to detect only new or changed packages:

```bash
dotnet run --project src/InSpectra.Discovery.Bootstrap -- index delta
```

### Filtering

Packages are filtered to those that depend on `Spectre.Console.Cli`, inspecting NuPkg archives for dependency evidence:

```bash
dotnet run --project src/InSpectra.Discovery.Bootstrap -- filter spectre-console-cli --concurrency 16
```

### Analysis

Discovered tools are analyzed via PowerShell scripts that install each tool, extract its CLI structure, and parse XML documentation:

```powershell
# Single trusted tool
pwsh -File scripts/Invoke-TrustedToolEvaluation.ps1 -PackageId JellyfinCli -Version 0.1.16 -Source workflow-dispatch -Trusted

# Batch untrusted analysis
pwsh -File scripts/Invoke-UntrustedToolAnalysis.ps1 -BatchPlanPath config/untrusted-batches/batch.json
```

### Output artifacts

Each analyzed tool produces versioned artifacts under `index/packages/{packageId}/{version}/`:

| File | Description |
|---|---|
| `metadata.json` | Package info, detection results, introspection status, timing data |
| `opencli.json` | Parsed CLI command structure |
| `xmldoc.xml` | Extracted XML documentation |

A global manifest at `index/all.json` lists all indexed packages with their latest status.

## CI/CD

| Workflow | Schedule | Purpose |
|---|---|---|
| `discover-dotnet-tool-updates` | 5x daily | Polls NuGet catalog for new/updated tools |
| `dispatch-discovery-queue-analysis` | On demand | Slices the analysis queue into batches |
| `analyze-untrusted-batch` | On demand | Runs sandboxed analysis on queued tools |
| `promote-untrusted-analysis-results` | On demand | Promotes successful results into the main index |
| `queue-indexed-metadata-backfill` | On demand | Backfills historical versions for indexed packages |

## Prerequisites

- .NET 8.0 SDK
- PowerShell 7+

## Building and testing

```bash
dotnet build InSpectra.Discovery.sln
dotnet test
```

## License

See the main [InSpectra](https://github.com/JKamsker/InSpectra) repository for license information.

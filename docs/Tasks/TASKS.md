- [x] Verify the supported NuGet API for an initial dotnet-tool index bootstrap.
- [x] Implement a bootstrap tool that enumerates current DotnetTool packages from NuGet's V3 autocomplete and registration resources and writes a JSON index.
- [x] Run the bootstrap locally and record the output paths/usage.
- [x] Enrich the snapshot with `totalDownloads` from NuGet search metadata and sort the package list descending by downloads.
- [x] Add a catalog-based `filter spectre-console` command that reads the ranked index and writes a filtered JSON file with Spectre evidence.
- [x] Add a trusted single-package GitHub Actions workflow for JellyfinCli that evaluates the tool and opens a PR with queue/generated/index files.

Command:
`dotnet run --project src/InSpectra.Discovery.Bootstrap -- index build --concurrency 16`

Output:
`artifacts/index/dotnet-tools.current.json`

Spectre filter:
`dotnet run --project src/InSpectra.Discovery.Bootstrap -- filter spectre-console --concurrency 16`

Filtered output:
`artifacts/index/dotnet-tools.spectre-console.json`

Trusted evaluator:
`pwsh -File scripts/Invoke-TrustedToolEvaluation.ps1 -PackageId JellyfinCli -Version 0.1.16 -Source workflow-dispatch -Trusted`

Workflow:
`.github/workflows/evaluate-trusted-jellyfincli.yml`

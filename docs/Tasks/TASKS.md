- [x] Verify the supported NuGet API for an initial dotnet-tool index bootstrap.
- [x] Implement a bootstrap tool that enumerates current DotnetTool packages from NuGet's V3 autocomplete and registration resources and writes a JSON index.
- [x] Run the bootstrap locally and record the output paths/usage.
- [x] Enrich the snapshot with `totalDownloads` from NuGet search metadata and sort the package list descending by downloads.
- [x] Add a catalog-based `filter spectre-console` command that reads the ranked index and writes a filtered JSON file with Spectre evidence.
- [x] Add a trusted single-package GitHub Actions workflow for JellyfinCli that evaluates the tool and opens a PR with versioned package index files.
- [x] Add a pilot untrusted analysis pipeline with two 10-item batches, artifact-only analysis, retry state, and a promotion workflow.

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

Versioned outputs:
`index/packages/jellyfincli/0.1.16/{metadata.json,opencli.json,xmldoc.xml}`

Latest alias:
`index/packages/jellyfincli/latest/{metadata.json,opencli.json,xmldoc.xml}`

Pilot untrusted batches:
`config/untrusted-batches/pilot-batch-01.json`
`config/untrusted-batches/pilot-batch-02.json`

Untrusted analysis workflow:
`.github/workflows/analyze-untrusted-batch.yml`

Promotion workflow:
`.github/workflows/promote-untrusted-analysis-results.yml`

- [x] Verify the supported NuGet API for an initial dotnet-tool index bootstrap.
- [x] Implement a bootstrap tool that enumerates current DotnetTool packages from NuGet's V3 autocomplete and registration resources and writes a JSON index.
- [x] Run the bootstrap locally and record the output paths/usage.
- [x] Enrich the snapshot with `totalDownloads` from NuGet search metadata and sort the package list descending by downloads.
- [x] Add a catalog-based `filter spectre-console` command that reads the ranked index and writes a filtered JSON file with Spectre evidence.
- [x] Add a stricter `filter spectre-console-cli` command and use its snapshot as the primary source for untrusted Spectre CLI batches.
- [x] Enrich the Spectre CLI snapshot with resolved Spectre package versions from `*.deps.json` and assembly/file version metadata from `Spectre.Console*.dll`.
- [x] Add a trusted single-package JellyfinCli evaluator path for manual use.
- [x] Add a pilot untrusted analysis pipeline, retry state, and a promotion workflow.
- [x] Make the untrusted analyzer classify `opencli` and `xmldoc` independently, tolerate ANSI/noisy output, and stop requeueing deterministic contract/auth/config failures.
- [x] Add a catalog-cursor `index delta` command and a workflow that opens a PR for dotnet-tool additions or latest-version changes since the last discovery cursor.
- [x] Narrow each broad discovery delta to changed `Spectre.Console.Cli` tools only, and emit a queue JSON that can feed later analysis runs directly.

Command:
`dotnet run --project src/InSpectra.Discovery.Tool -- catalog build --concurrency 16`

Output:
`artifacts/index/dotnet-tools.current.json`

Spectre filter:
`dotnet run --project src/InSpectra.Discovery.Tool -- catalog filter spectre-console --concurrency 16`

Filtered output:
`artifacts/index/dotnet-tools.spectre-console.json`

Spectre CLI filter:
`dotnet run --project src/InSpectra.Discovery.Tool -- catalog filter spectre-console-cli --concurrency 16`

Primary source snapshot:
`artifacts/index/dotnet-tools.spectre-console-cli.json`

Version evidence:
`packages[*].detection.packageInspection`

Trusted evaluator:
`pwsh -File scripts/Invoke-TrustedToolEvaluation.ps1 -PackageId JellyfinCli -Version 0.1.16 -Source workflow-dispatch -Trusted`

Versioned outputs:
`index/packages/jellyfincli/0.1.16/{metadata.json,opencli.json,xmldoc.xml}`

Latest alias:
`index/packages/jellyfincli/latest/{metadata.json,opencli.json,xmldoc.xml}`

Queue dispatch planner:
`inspectra-discovery queue dispatch-plan`

Untrusted analysis workflow:
`.github/workflows/analyze-untrusted-batch.yml`

Promotion workflow:
`.github/workflows/promote-untrusted-analysis-results.yml`

Delta discovery workflow:
`.github/workflows/discover-dotnet-tool-updates.yml`

Narrowed Spectre CLI delta:
`state/discovery/dotnet-tools.spectre-console-cli.delta.json`

Action queue:
`state/discovery/dotnet-tools.spectre-console-cli.queue.json`

## In simple terms

You want a living index of **NuGet .NET tools that are built on Spectre.Console**, plus the metadata each tool can expose through commands like `cli opencli` and `cli xmldoc`. The goal is to discover candidates with as little external traffic as possible, then only execute the tools that are likely matches, and record the results in a GitHub repo through reviewable PRs. The key constraint is that the **published package** is what matters most, because .NET tool packages are expected to include `DotnetToolSettings.xml` and the assets they need to run, while .NET tools themselves run in **full trust** and should be treated as untrusted code. ([GitHub][1])

## Approach

Use a **two-lane pipeline**:

1. **Discovery lane**
   Enumerate all `DotnetTool` packages from NuGet using the catalog, keep a cursor so updates are incremental, and do cheap pre-analysis on the published `.nupkg` before any install. The catalog is the right feed for “all packages over time,” and NuGet’s package content endpoint is the supported way to fetch `.nupkg` files or `.nuspec` files. ([Microsoft Learn][2])

2. **Execution lane**
   For packages that look like Spectre-based tools, GitHub Actions installs the tool into an isolated `--tool-path`, runs your extraction commands, uploads the normalized result as an artifact, and then a separate privileged workflow turns that artifact into a PR. GitHub documents `--tool-path` for custom install locations, workflow artifacts for passing files between jobs, and `workflow_run` for a second workflow that can have write access even if the first one did not. GitHub also warns that `workflow_run` must treat artifacts from earlier workflows cautiously. ([Microsoft Learn][3])

That gives you a clean split:

* **cheap discovery first**
* **package inspection before install**
* **execution only for probable matches**
* **write access only in the promotion step**

## Detailed plan

### 1) Use a GitHub repo as the control plane

Make the repo the source of truth for:

* the queue of packages to analyze
* the generated metadata
* the assembled index
* the audit trail of what changed and why

A simple layout:

```text
/queue/<package-id>/<version>.json
/generated/<package-id>/<version>.json
/index/packages/<package-id>.json
/index/all.json
/logs/<date>/<package-id>-<version>.json
```

What each area means:

* `queue/`: small request records saying “analyze this package version”
* `generated/`: raw normalized outputs from sandbox execution
* `index/`: rolled-up views used by your site, API, or search
* `logs/`: optional debug trail for failures and reruns

The important idea is that the repo stores **requests** separately from **results**.

### 2) Discovery program: find the universe of candidate tools

The discovery program should not install anything. Its job is to build and maintain the queue.

Recommended flow:

1. Read NuGet’s service index.
2. Find the **Catalog** resource.
3. Continue from the saved cursor.
4. Process catalog leaves newer than the cursor.
5. Keep only package versions whose package type is `DotnetTool`.
6. For each kept package/version, enqueue it unless it is already known. NuGet’s guide for querying all published packages explicitly recommends the catalog and a cursor-based reader, starting from the service index and processing catalog pages and leaves newer than your cursor. ([Microsoft Learn][2])

For each candidate, store at least:

* package id
* version
* listed/unlisted
* publish/update timestamp
* package URL
* optional project/repo URL if you can find it
* discovery reason
* status: `new`, `queued`, `analyzed`, `failed`, `ignored`

### 3) Cheap prefilter: inspect the package before install

For each queued package, fetch the `.nupkg` from NuGet’s package content endpoint. NuGet documents the package content endpoint as the standard V3 resource for downloading package content, and those URLs support both `GET` and `HEAD`. ([Microsoft Learn][4])

This is your main low-load filter.

Inside the package, inspect:

* `DotnetToolSettings.xml`
* `*.deps.json`
* `*.runtimeconfig.json`
* assembly filenames under `/tools/.../any/...`

The .NET SDK tool package format says:

* the package type is `DotnetTool`
* there is a `DotnetToolSettings.xml` for each tool asset set
* the command name in that file is what the user types
* each asset set should contain the dependencies the tool requires to run. ([GitHub][1])

Your Spectre detection logic should be staged:

**Strong positive**

* `.deps.json` references `Spectre.Console`
* `.deps.json` references `Spectre.Console.Cli`
* packaged DLL names include `Spectre.Console*.dll`

**Weak positive**

* entry assembly references `Spectre.Console*`
* command or help text hints at a Spectre-style CLI

**Negative**

* no Spectre evidence in package contents

**Ambiguous**

* package shape is unusual
* assets are missing or packed oddly
* package looks like a wrapper or launcher

Only `strong positive` and `ambiguous` should proceed to install.

### 4) Optional GitHub repo check as a hint, not a gate

You mentioned searching GitHub for `Spectre.Console` in `*.csproj` or `Directory.Packages.props`. That is useful, but I would keep it **optional** and non-authoritative.

Use it for:

* prioritizing analysis
* enriching metadata
* finding the likely project path
* reducing unnecessary installs for borderline packages

Do not use it to skip package inspection entirely. The repo is just a hint; the package is what will actually be installed and executed.

A practical rule:

* if package scan is strongly positive, ignore repo
* if package scan is ambiguous, repo check can break the tie
* if package scan is negative, repo check is not enough by itself to promote the package

### 5) Queue records: keep them small and immutable

Each queue file should be enough to analyze one package version.

Example shape:

```json
{
  "packageId": "cake.tool",
  "version": "5.1.0",
  "discoveredAt": "2026-03-25T10:00:00Z",
  "source": "nuget-catalog",
  "repoUrl": "https://github.com/cake-build/cake",
  "prefilter": {
    "packageScan": "strong-positive",
    "repoHint": "unknown"
  },
  "status": "queued"
}
```

Why immutable queue files help:

* reruns are easy
* failures are reproducible
* review stays simple
* discovery and execution stay decoupled

### 6) Analysis workflow: unprivileged, isolated, no repo writes

This workflow should trigger from:

* `repository_dispatch` from your discovery process
* optionally `workflow_dispatch` for manual reruns
* optionally `push` to `queue/**` if you want queue files to be the trigger

GitHub documents `repository_dispatch` as the event for triggering workflows from activity that happens outside GitHub, and the workflow file must exist on the default branch for it to trigger. ([GitHub Docs][5])

This analysis workflow should have:

* `permissions: contents: read`
* no secrets
* no PR creation
* no branch pushing

GitHub recommends using the `permissions` key to give `GITHUB_TOKEN` the minimum required access, and specifically calls out least-privilege as good security practice. ([GitHub Docs][6])

The steps:

1. Resolve queue record.
2. Download `.nupkg`.
3. Re-run the package scan.
4. If negative, emit a “not Spectre” result and stop.
5. If positive or ambiguous:

   * install with `dotnet tool install <id> --version <v> --tool-path <tempdir>`
   * determine command name from `DotnetToolSettings.xml`
   * run:

     * `<fullpath-command> cli opencli`
     * `<fullpath-command> cli xmldoc`
6. Normalize stdout, stderr, exit code, and timing.
7. Upload result JSON as an artifact.

The .NET docs explicitly support installing into a custom location with `--tool-path`, and note that you can invoke the tool from that directory or by full path. They also warn that .NET tools run in full trust, which is why this workflow should stay unprivileged. ([Microsoft Learn][3])

Suggested normalized result:

```json
{
  "packageId": "cake.tool",
  "version": "5.1.0",
  "command": "dotnet-cake",
  "classification": "spectre-positive",
  "detection": {
    "method": "deps-json",
    "evidence": ["Spectre.Console", "Spectre.Console.Cli"]
  },
  "install": {
    "status": "ok"
  },
  "opencli": {
    "status": "ok",
    "exitCode": 0,
    "stdout": "...",
    "stderr": ""
  },
  "xmldoc": {
    "status": "ok",
    "exitCode": 0,
    "stdout": "...",
    "stderr": ""
  },
  "analyzedAt": "2026-03-25T10:10:00Z"
}
```

### 7) Promotion workflow: privileged, but never executes tools

The second workflow should trigger on `workflow_run` after the analysis workflow completes.

GitHub’s workflow docs state that a workflow started by `workflow_run` can access secrets and write tokens even if the previous workflow could not. The same docs also warn that running untrusted code via `workflow_run` patterns can create security problems, and the secure-use guidance says workflows triggered on `workflow_run` should treat artifacts from earlier workflows with caution and must not check out untrusted code. ([GitHub Docs][5])

So this workflow should:

1. Read the triggering run metadata.
2. Download the artifact from that run.
3. Validate the artifact strictly against a schema.
4. Confirm package id/version matches an existing queue record.
5. Write files to `generated/` and rebuild the rolled-up `index/`.
6. Open a PR.

It should **not**:

* reinstall the tool
* rerun the tool
* execute anything from the artifact
* check out untrusted code from other repos or forks

Artifacts are the right handoff mechanism here because GitHub artifacts are designed to persist files after a job completes and share them with later jobs and workflows. GitHub’s `workflow_run` docs also show using the triggering workflow’s payload and REST API to fetch artifacts from the earlier run. ([GitHub Docs][7])

### 8) Configure GitHub permissions deliberately

For repository settings, you likely want:

* default `GITHUB_TOKEN` permissions restricted
* allow PR creation only for the promotion workflow

GitHub’s org settings docs say new organizations default `GITHUB_TOKEN` to read access for `contents` and `packages`, and that workflows are not allowed to create or approve pull requests by default unless you enable that setting. ([GitHub Docs][8])

Workflow-level guidance:

**Analysis workflow**

```yaml
permissions:
  contents: read
```

**Promotion workflow**

```yaml
permissions:
  contents: write
  pull-requests: write
```

### 9) Control volume with matrices and concurrency

When discovery finds a batch, analyze them with a matrix, but keep parallelism modest.

GitHub Actions supports matrix jobs with `max-parallel`, and it supports `concurrency` keys so only one workflow or job in a concurrency group runs at a time. ([GitHub Docs][9])

That lets you do things like:

* max 4 or 8 tool analyses at once
* never analyze the same package/version twice simultaneously
* cancel superseded reruns

A good concurrency key is:

```yaml
concurrency:
  group: toolscan-${{ matrix.packageId }}-${{ matrix.version }}
  cancel-in-progress: false
```

### 10) Build the index in two layers

Keep both raw and rolled-up data.

**Raw per version**

* every command output
* detection evidence
* install results
* failures

**Rolled up per package**

* latest successful version
* latest analyzed version
* command name
* Spectre confidence
* opencli/xmldoc availability
* timestamps

This is useful because the raw layer is your audit trail, and the rolled-up layer is what your UI or consumers will likely read.

### 11) Failure handling and retries

Not every tool will install or behave.

Classify failures separately:

* `not-a-match`
* `install-failed`
* `command-missing`
* `opencli-failed`
* `xmldoc-failed`
* `timeout`
* `schema-invalid`
* `promotion-rejected`

That matters because “not Spectre-based” is not the same as “Spectre-based but broken.”

Retry rules I’d use:

* automatic retry once for network/download issues
* no automatic retry for clear negatives
* manual rerun for timeouts and ambiguous package scans

### 12) Incremental update cycle

The normal steady-state loop should be:

1. discovery advances the NuGet catalog cursor
2. new queue items are created
3. analysis runs only for new package versions
4. promotion writes results via PR
5. merged PR updates the repo index
6. next discovery run continues from the last cursor

That keeps load low because you are no longer re-scanning the full universe every time. NuGet’s catalog guidance is built around exactly this cursor-based model. ([Microsoft Learn][2])

## Recommended version 1

For the first working version, I would keep scope narrow:

* latest stable version only
* no historical backfill yet
* package-first detection
* GitHub repo check only when package scan is ambiguous
* analysis and promotion as two separate workflows
* one PR per batch, not per package

That gets you to a useful system quickly without overbuilding.

## Recommended version 2

Once v1 is stable, add:

* historical versions for matched packages
* GitHub repo enrichment
* richer schema validation
* a website or API over `index/`
* scheduled reanalysis when extraction logic changes

## The core principle

The design can be summarized like this:

* **NuGet catalog** tells you what tools exist. ([Microsoft Learn][2])
* **The `.nupkg`** tells you what was actually shipped. ([GitHub][1])
* **The sandboxed analysis workflow** tells you what the tool really exposes at runtime, and it must stay unprivileged because .NET tools run in full trust. ([Microsoft Learn][3])
* **The promotion workflow** turns validated artifacts into repo changes and PRs, with write access kept separate from untrusted execution. ([GitHub Docs][5])

That is the plan I would build.

Next, I can turn this into a concrete repo skeleton with the workflow files and JSON schemas.

[1]: https://github.com/dotnet/sdk/blob/main/documentation/general/tool-nuget-package-format.md "sdk/documentation/general/tool-nuget-package-format.md at main · dotnet/sdk · GitHub"
[2]: https://learn.microsoft.com/en-us/nuget/guides/api/query-for-all-published-packages "Query for all packages published to nuget.org | Microsoft Learn"
[3]: https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools ".NET tools - .NET CLI | Microsoft Learn"
[4]: https://learn.microsoft.com/en-us/nuget/api/package-base-address-resource "Package Content, NuGet API | Microsoft Learn"
[5]: https://docs.github.com/actions/using-workflows/events-that-trigger-workflows "Events that trigger workflows - GitHub Docs"
[6]: https://docs.github.com/en/actions/tutorials/authenticate-with-github_token "Use GITHUB_TOKEN for authentication in workflows - GitHub Docs"
[7]: https://docs.github.com/en/actions/concepts/workflows-and-actions/workflow-artifacts "Workflow artifacts - GitHub Docs"
[8]: https://docs.github.com/en/organizations/managing-organization-settings/disabling-or-limiting-github-actions-for-your-organization "Disabling or limiting GitHub Actions for your organization - GitHub Docs"
[9]: https://docs.github.com/actions/writing-workflows/choosing-what-your-workflow-does/running-variations-of-jobs-in-a-workflow?utm_source=chatgpt.com "Running variations of jobs in a workflow"

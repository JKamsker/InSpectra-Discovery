- [x] Verify the supported NuGet API for an initial dotnet-tool index bootstrap.
- [x] Implement a bootstrap tool that enumerates current DotnetTool packages from NuGet's V3 autocomplete and registration resources and writes a JSON index.
- [x] Run the bootstrap locally and record the output paths/usage.

Command:
`dotnet run --project src/InSpectra.Discovery.Bootstrap -- --concurrency 16`

Output:
`artifacts/index/dotnet-tools.current.json`

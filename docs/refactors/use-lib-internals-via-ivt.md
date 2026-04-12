# Refactor: Replace duplicated code via InternalsVisibleTo

## Summary

Discovery still contains ~1,650 lines of code that is duplicated 1:1 inside
InSpectra.Lib's internals. Adding a single `InternalsVisibleTo` attribute in
Lib would let Discovery consume these types directly — zero new public surface.

```csharp
// In InSpectra.Lib AssemblyInfo or csproj
[assembly: InternalsVisibleTo("InSpectra.Discovery.Tool")]
```

## Candidates

### 1. Frameworks/ (3 files, 255 lines) — VERY HIGH confidence

Discovery's `CliFrameworkProviderRegistry`, `CliFrameworkProvider`, and
`CliFrameworkReferenceProbe` are identical to Lib's versions in
`Tooling/FrameworkDetection/`. Lib's version is a superset (supports
dynamic registration).

**Delete:** `src/InSpectra.Discovery.Tool/Frameworks/`  
**Replace with:** `using InSpectra.Lib.Tooling.FrameworkDetection;`  
**Callers to update:** ~8 files (ToolDescriptorResolver, AutoModeSupport,
UntrustedCommandService, AutoResultSupport, PromotionPlanItemMergeSupport,
PackageArchiveCliFrameworkReferenceSupport, HelpCrawlArtifactRegenerationService)

### 2. Packages/ (10 files, 615 lines) — HIGH confidence

Discovery's package inspection code (`PackageArchiveInspector`,
`PackageToolCommandInspector`, `DotnetToolPackageLayoutReader`, PE inspection,
framework reference detection) is 90% identical to Lib's
`Tooling/Packages/` namespace.

**Delete:** `src/InSpectra.Discovery.Tool/Packages/`  
**Replace with:** `using InSpectra.Lib.Tooling.Packages;`  
**Gap:** `DotnetToolSettingsReader` may need to be moved into Lib first if
it doesn't already exist there.  
**Callers to update:** NonSpectreBootstrapSupport, ToolDescriptorResolver,
catalog filtering inspectors

### 3. Infrastructure/Commands/ (7 files, 787 lines) — HIGH confidence

Discovery's `CommandRuntime`, `CommandProcessSupport`,
`CommandSandboxEnvironmentSupport`, `ProcessOutputCaptureSupport`,
`CommandInstallationSupport`, `InstalledDotnetToolCommandSupport`, and
`DotnetRuntimeCompatibilitySupport` overlap heavily with Lib's
`Tooling/Process/` and `Execution/Process/` namespaces.

**Delete:** most of `src/InSpectra.Discovery.Tool/Infrastructure/Commands/`  
**Replace with:** Lib's `CommandRuntime`, `IProcessRunner`, sandbox utilities  
**Callers to update:** NonSpectreExecutionSupport, mode services (HelpService,
CliFxService, StaticService, HookService — they each `new CommandRuntime()`)  
**Note:** `DotnetRuntimeCompatibilitySupport` may be Discovery-specific. Verify
before deleting.

### 4. NonSpectre/ partial (~200 lines reducible) — MEDIUM confidence

If Lib exposed its internal `NonSpectreResultSupport` and
`NonSpectreExecutionSupport` equivalents, Discovery's scaffold could shrink.
The records (`NonSpectreInstalledToolAnalysisRequest`, etc.) are
Discovery-specific and should stay, but the result formatting and temp-dir
management could delegate to Lib.

**Reducible:** ~200 of 511 lines  
**More architectural** than the other candidates — lower priority.

## Total impact

~1,650 lines deletable, ~28 files removed, with a single
`InternalsVisibleTo` attribute as the only Lib change.

## Risks

- Tight coupling to Lib internals. If Lib refactors `Tooling/Process/` or
  `Tooling/FrameworkDetection/`, Discovery breaks at compile time.
- Acceptable since both repos are maintained together and Discovery is the
  only IVT consumer.
- Alternative: expose a small set of public interfaces instead. Higher
  maintenance cost but looser coupling.

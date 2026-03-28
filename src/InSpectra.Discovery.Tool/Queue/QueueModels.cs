internal sealed record QueueDispatchBatch(
    string BatchId,
    string QueuePath,
    int Offset,
    int Take,
    string TargetBranch,
    string StateBranch,
    int ItemCount);

internal sealed record QueueDispatchPlan(
    int SchemaVersion,
    DateTimeOffset GeneratedAt,
    string QueuePath,
    string TargetBranch,
    string StateBranch,
    int QueueItemCount,
    int BatchSize,
    int BatchCount,
    IReadOnlyList<QueueDispatchBatch> Batches);

internal sealed record IndexedMetadataBackfillQueueItem(
    string PackageId,
    string Version,
    long? TotalDownloads,
    string PackageUrl,
    string? PackageContentUrl,
    string? RegistrationLeafUrl,
    string? CatalogEntryUrl,
    string? PublishedAt,
    object? Listed,
    string BackfillKind,
    string RunsOn,
    string RunnerReason,
    IReadOnlyList<string> RequiredFrameworks,
    IReadOnlyList<string> ToolRids,
    IReadOnlyList<string> RuntimeRids,
    string? InspectionError,
    string RunnerHintSource);

internal sealed record IndexedMetadataBackfillQueue(
    int SchemaVersion,
    DateTimeOffset GeneratedAtUtc,
    string Filter,
    string SourceIndexPath,
    string? SourceGeneratedAtUtc,
    string SourceCurrentSnapshotPath,
    int IndexedPackageCount,
    int IndexedVersionCount,
    int ItemCount,
    string BatchPrefix,
    bool ForceReanalyze,
    bool SkipRunnerInspection,
    int SkippedCount,
    IReadOnlyList<object> Skipped,
    IReadOnlyList<IndexedMetadataBackfillQueueItem> Items);

internal sealed record UntrustedBatchPlanItem(
    string PackageId,
    string Version,
    long? TotalDownloads,
    string? PackageUrl,
    string? PackageContentUrl,
    string? CatalogEntryUrl,
    string? Command,
    string? CliFramework,
    string? AnalysisMode,
    string? AnalysisReason,
    int Attempt,
    string ArtifactName,
    string RunsOn,
    string RunnerReason,
    IReadOnlyList<string> RequiredFrameworks,
    IReadOnlyList<string> ToolRids,
    IReadOnlyList<string> RuntimeRids,
    string? InspectionError);

internal sealed record UntrustedBatchPlan(
    int SchemaVersion,
    string BatchId,
    DateTimeOffset GeneratedAt,
    string SourceManifestPath,
    string SourceSnapshotPath,
    string TargetBranch,
    bool ForceReanalyze,
    int SelectedCount,
    int SkippedCount,
    IReadOnlyList<UntrustedBatchPlanItem> Items,
    IReadOnlyList<object> Skipped);

internal sealed record RunnerSelection(
    string RunsOn,
    string Reason,
    IReadOnlyList<string> RequiredFrameworks,
    IReadOnlyList<string> ToolRids,
    IReadOnlyList<string> RuntimeRids,
    string? InspectionError,
    string HintSource);

internal sealed record LegacyTerminalNegativeQueueComputation(
    int CurrentPackageCount,
    int EligibleLegacyNegativeCount,
    DotnetToolQueueSnapshot Queue);

internal sealed record CurrentAnalysisBackfillQueueComputation(
    int CurrentPackageCount,
    int EligiblePackageCount,
    int MissingCount,
    int LegacyTerminalNegativeCount,
    int LegacyTerminalFailureCount,
    int RetryableCount,
    DotnetToolQueueSnapshot Queue);

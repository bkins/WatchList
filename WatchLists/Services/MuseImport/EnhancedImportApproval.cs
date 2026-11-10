namespace WatchLists.Services.MuseImport;

public sealed record EnhancedImportApproval(EnhancedImportRow Row, IReadOnlySet<RecommendationField> AcceptedConflicts);

namespace Annap.CoffeeQrOrdering.Web.Services;

public enum DataAuditLevel
{
    Pass,
    Warn,
    Fail
}

public sealed record DataAuditFinding(DataAuditLevel Level, string Message);

public sealed record DataAuditCategoryRow(
    string Name,
    int SortOrder,
    int TotalItems,
    int AvailableItems,
    int ArchivedItems);

public sealed record DataAuditSpecialtyRow(
    string CatalogKey,
    string Name,
    bool IsAvailable,
    bool IsArchived,
    bool IsSignature);

public sealed record DataAuditMediaRow(
    string Name,
    string? ImageUrl,
    bool? ImageFileExists,
    string? DetailPosterImagePath,
    bool? PosterFileExists,
    bool HasMissingFile);

public sealed record DataAuditPairingSample(
    string DrinkName,
    Guid DrinkId,
    int BakeryPoolCount,
    int PairingsReturned,
    IReadOnlyList<string> PairingNames);

public sealed record DataAuditDiagnostics(
    int TotalCategories,
    int TotalMenuItems,
    int BakeryCategoryCount,
    int BakeryItemCount,
    int PosterFileCount);

public sealed record ProductionDataAuditReport(
    DataAuditDiagnostics Diagnostics,
    IReadOnlyList<DataAuditCategoryRow> Categories,
    bool BakeryCategoryExists,
    string? BakeryCategoryName,
    int BakeryItemCount,
    IReadOnlyList<string> BakeryItemNames,
    int SpecialtyPoolCount,
    IReadOnlyList<DataAuditSpecialtyRow> SpecialtyCoffees,
    IReadOnlyList<DataAuditMediaRow> MediaRows,
    int MissingManagedImagePaths,
    int MissingManagedPosterPaths,
    DataAuditPairingSample? PairingSample,
    string DatabaseHost,
    string DatabaseName,
    string? AppUrlPublicBaseUrl,
    string? PublicBaseUrlOverride,
    bool RenderDeploymentDetected,
    DataAuditLevel OverallLevel,
    IReadOnlyList<DataAuditFinding> Findings)
{
    public static ProductionDataAuditReport Failed(
        string message,
        string databaseHost = "(unknown)",
        string databaseName = "(unknown)") =>
        new(
            new DataAuditDiagnostics(0, 0, 0, 0, 0),
            Array.Empty<DataAuditCategoryRow>(),
            false,
            null,
            0,
            Array.Empty<string>(),
            0,
            Array.Empty<DataAuditSpecialtyRow>(),
            Array.Empty<DataAuditMediaRow>(),
            0,
            0,
            null,
            databaseHost,
            databaseName,
            null,
            null,
            InfrastructureEnvironment.IsRenderDeployment,
            DataAuditLevel.Fail,
            [new DataAuditFinding(DataAuditLevel.Fail, message)]);
}

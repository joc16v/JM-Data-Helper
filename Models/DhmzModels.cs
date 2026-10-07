namespace JMDataHelper.Models;

public sealed record WarningRegion(
    string Id,
    string Name,
    string Color,
    int Level);

public sealed record DhmzWarningsResponse(
    bool Ok,
    int Day,
    string SourceUrl,
    DateTimeOffset FetchedAt,
    IReadOnlyList<WarningRegion> Counties,
    IReadOnlyList<WarningRegion> Sea);

using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Services.Patient;

public interface IBedRegistryService
{
    Task<IReadOnlyDictionary<Guid, int>> CountBedsByWardAsync(
        IReadOnlyCollection<Guid> wardIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RegisteredBed>> ListBedsInWardsAsync(
        IReadOnlyCollection<Guid> wardIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RegisteredBed>> ListBedsByIdAsync(
        IReadOnlyCollection<Guid> bedIds, CancellationToken cancellationToken = default);

    Task<RegisteredBed?> FindBedAsync(Guid bedId, CancellationToken cancellationToken = default);
}

public readonly record struct RegisteredBed(
    Guid Id,
    Guid WardId,
    string BedNumber,
    bool HasIsolation,
    BedCondition Condition,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

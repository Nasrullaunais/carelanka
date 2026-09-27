using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Equipment;

namespace CareLanka.Api.Services.Equipment;

public interface IWarningService
{
    Task<PagedResult<Warning>> ListAsync(WarningQuery query, CancellationToken cancellationToken = default);

    Task<Warning> AcknowledgeAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Marks a resolved or dismissed warning done, which takes it off the list for good.</summary>
    Task ClearAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Checks stock, expiry and maintenance against fixed rules, raises a warning for
    /// each problem found and closes the ones whose problem has gone.</summary>
    Task<WarningSweepResult> SweepAsync(CancellationToken cancellationToken = default);
}

public sealed record WarningQuery(
    WarningStatus? Status,
    WarningSeverity? Severity,
    WarningType? Type,
    int Page,
    int PageSize);

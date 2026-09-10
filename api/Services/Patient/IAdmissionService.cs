using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Patient;
using AdmissionEntity = CareLanka.Api.Data.Entities.Patient.Admission;
using AdmissionResponse = CareLanka.Api.DTOs.Patient.Admission;

namespace CareLanka.Api.Services.Patient;

/// <summary>
/// One row per hospital visit. For an emergency the row exists before the patient arrives,
/// which is why an admission starts at <c>awaiting_bed</c> with no arrival time.
/// </summary>
public interface IAdmissionService
{
    Task<PagedResult<AdmissionSummary>> ListAsync(
        IReadOnlyCollection<AdmissionStatus>? statuses,
        AdmissionCategory? category,
        AdmissionSource? source,
        bool? detailsComplete,
        string? search,
        int page,
        int pageSize,
        AdmissionSortField sortBy,
        SortDirection sortDir,
        CancellationToken cancellationToken = default);

    /// <summary>Throws NotFoundException when there is no such admission.</summary>
    Task<AdmissionDetail> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);

    Task<AdmissionResponse> CreateAsync(
        CreateAdmissionRequest request, CancellationToken cancellationToken = default);

    /// <summary>Fills in what was missing at registration and recalculates completeness server-side.</summary>
    Task<AdmissionResponse> CompleteDetailsAsync(
        Guid id, CompleteDetailsRequest request, CancellationToken cancellationToken = default);

    /// <summary>Null when there is no such admission. For internal lookups — use GetByIdAsync to answer a request.</summary>
    Task<AdmissionEntity?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Throws NotFoundException when there is no such admission.</summary>
    Task<AdmissionEntity> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

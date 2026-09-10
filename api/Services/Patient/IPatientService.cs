using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Patient;
using PatientEntity = CareLanka.Api.Data.Entities.Patient.Patient;
using PatientResponse = CareLanka.Api.DTOs.Patient.Patient;

namespace CareLanka.Api.Services.Patient;

/// <summary>
/// The patient register. One row per human being, reused across visits — never one row per
/// admission, which is the single most damaging data problem this component can have.
/// </summary>
public interface IPatientService
{
    Task<PagedResult<PatientSummary>> ListAsync(
        string? search,
        int page,
        int pageSize,
        PatientSortField sortBy,
        SortDirection sortDir,
        CancellationToken cancellationToken = default);

    /// <summary>Throws NotFoundException when there is no such active patient.</summary>
    Task<PatientDetail> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PatientResponse> CreateAsync(
        CreatePatientRequest request, CancellationToken cancellationToken = default);

    Task<PatientResponse> UpdateAsync(
        Guid id, UpdatePatientRequest request, CancellationToken cancellationToken = default);

    /// <summary>A miss is a normal answer, not an error — the result says found = false.</summary>
    Task<PatientLookupResult> LookupByNicAsync(
        string nic, CancellationToken cancellationToken = default);

    Task LinkAccountAsync(
        Guid id, Guid userAccountId, CancellationToken cancellationToken = default);

    /// <summary>Null when there is no such active patient. For internal lookups — use GetByIdAsync to answer a request.</summary>
    Task<PatientEntity?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Throws NotFoundException when there is no such active patient.</summary>
    Task<PatientEntity> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Patient;
using PatientEntity = CareLanka.Api.Data.Entities.Patient.Patient;
using PatientResponse = CareLanka.Api.DTOs.Patient.Patient;

namespace CareLanka.Api.Services.Patient;

public interface IPatientService
{
    Task<PagedResult<PatientSummary>> ListAsync(
        string? search,
        int page,
        int pageSize,
        PatientSortField sortBy,
        SortDirection sortDir,
        CancellationToken cancellationToken = default);

    Task<PatientDetail> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PatientResponse> CreateAsync(
        CreatePatientRequest request, CancellationToken cancellationToken = default);

    Task<PatientResponse> UpdateAsync(
        Guid id, UpdatePatientRequest request, CancellationToken cancellationToken = default);

    Task<PatientLookupResult> LookupByNicAsync(
        string nic, CancellationToken cancellationToken = default);

    Task LinkAccountAsync(
        Guid id, Guid userAccountId, CancellationToken cancellationToken = default);

    Task<PatientEntity?> FindByUserAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);

    Task<PatientEntity?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PatientEntity> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

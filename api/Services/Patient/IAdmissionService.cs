using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Patient;
using AdmissionEntity = CareLanka.Api.Data.Entities.Patient.Admission;
using AdmissionResponse = CareLanka.Api.DTOs.Patient.Admission;

namespace CareLanka.Api.Services.Patient;

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

    Task<AdmissionDetail> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);

    Task<AdmissionResponse> CreateAsync(
        CreateAdmissionRequest request, CancellationToken cancellationToken = default);

    Task<AdmissionResponse> CompleteDetailsAsync(
        Guid id, CompleteDetailsRequest request, CancellationToken cancellationToken = default);

    Task<AdmissionResponse> MarkArrivedAsync(Guid id, CancellationToken cancellationToken = default);

    Task<AdmissionResponse> CompleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<AdmissionResponse> CancelAsync(
        Guid id, CancelAdmissionRequest request, CancellationToken cancellationToken = default);

    Task<AdmissionEntity?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<AdmissionEntity> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

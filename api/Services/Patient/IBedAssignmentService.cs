using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Patient;

namespace CareLanka.Api.Services.Patient;

public interface IBedAssignmentService
{
    Task<PagedResult<AdmissionBed>> ListAvailabilityAsync(
        Guid? wardId,
        WardType? wardType,
        BedAvailabilityFilter availability,
        bool? needsIsolation,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<DTOs.Patient.BedAssignment> AssignManuallyAsync(
        Guid admissionId, AssignBedRequest request, CancellationToken cancellationToken = default);

    Task<DTOs.Patient.BedAssignment> CorrectBedAsync(
        Guid admissionId, CorrectBedRequest request, CancellationToken cancellationToken = default);

    Task<BedOccupancyStatus> GetBedOccupancyAsync(
        Guid bedId, CancellationToken cancellationToken = default);
}

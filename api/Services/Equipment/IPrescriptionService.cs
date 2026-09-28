using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Equipment;

namespace CareLanka.Api.Services.Equipment;

public interface IPrescriptionService
{
    Task<IReadOnlyList<MyPrescription>> ListMineAsync(CancellationToken cancellationToken = default);

    Task<MyPrescription> UploadMineAsync(
        UploadPrescriptionRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Prescription>> ListAsync(
        PrescriptionStatus status, CancellationToken cancellationToken = default);

    /// <summary>A doctor writing one directly for a patient they looked up, rather than a
    /// patient photographing a paper one.</summary>
    Task<Prescription> CreateAsync(
        CreatePrescriptionRequest request, CancellationToken cancellationToken = default);

    Task<LabReportFile> GetFileAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Prescription> MarkReadyAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Prescription> MarkDeliveredAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Prescription> RejectAsync(
        Guid id, string reason, CancellationToken cancellationToken = default);
}

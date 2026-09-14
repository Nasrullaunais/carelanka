using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Equipment;

namespace CareLanka.Api.Services.Equipment;

public interface ILabReportService
{
    Task<PagedResult<LabReport>> ListForPatientAsync(
        Guid patientId, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<LabReport> UploadAsync(
        UploadLabReportRequest request, CancellationToken cancellationToken = default);

    /// <summary>Throws NotFoundException when there is no such report.</summary>
    Task<LabReportFile> GetFileAsync(Guid id, CancellationToken cancellationToken = default);
}

public record LabReportFile(byte[] Content, string ContentType, string FileName);

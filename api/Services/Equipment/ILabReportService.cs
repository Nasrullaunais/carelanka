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

    /// <summary>
    /// Same file, scoped to one patient - for the patient's own self-service read, where the
    /// caller has no staff reader policy and must never be handed somebody else's report by
    /// guessing an id. Throws NotFoundException for a wrong id and for a right id that is not
    /// this patient's, identically, so neither answer confirms the other id exists.
    /// </summary>
    Task<LabReportFile> GetFileForPatientAsync(
        Guid id, Guid patientId, CancellationToken cancellationToken = default);
}

public record LabReportFile(byte[] Content, string ContentType, string FileName);

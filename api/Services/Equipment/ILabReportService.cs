using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Equipment;

namespace CareLanka.Api.Services.Equipment;

public interface ILabReportService
{
    /// <summary>
    /// One patient's results, newest first. Metadata only - the bytes are a separate request,
    /// so opening a patient does not pull every PDF they have ever had down the wire.
    /// </summary>
    Task<PagedResult<LabReport>> ListForPatientAsync(
        Guid patientId, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Files a finished report against a patient. Rejects a patient who does not exist, an
    /// empty file, one over the size limit, and anything that is not a PDF or an image.
    /// </summary>
    Task<LabReport> UploadAsync(
        UploadLabReportRequest request, CancellationToken cancellationToken = default);

    /// <summary>The file itself. Throws NotFoundException when there is no such report.</summary>
    Task<LabReportFile> GetFileAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Who the laboratory can file against right now, by ward. The answer to "show me the
    /// patients, do not make me type a NIC": a lab holds a rack of specimens from one ward and
    /// works down it.
    /// </summary>
    /// <param name="wardName">Null for every current visit, including those holding no bed.</param>
    Task<PagedResult<LabPatient>> ListPatientsAsync(
        string? wardName, int page, int pageSize, CancellationToken cancellationToken = default);
}

/// <summary>The stored bytes and what is needed to serve them as the document they are.</summary>
public record LabReportFile(byte[] Content, string ContentType, string FileName);

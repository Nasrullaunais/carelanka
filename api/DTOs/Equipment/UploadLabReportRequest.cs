using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Equipment;

/// <summary>
/// A multipart form, not JSON, because it carries the scanned report itself. The fields are
/// form fields and <see cref="File"/> is the upload.
/// </summary>
public class UploadLabReportRequest
{
    /// <summary>Which patient the result belongs to. Looked up by code, name or NIC on the lab's screen.</summary>
    [Required]
    public Guid PatientId { get; set; }

    /// <summary>What was tested, in the lab's own words.</summary>
    [Required]
    [StringLength(120, MinimumLength = 2)]
    public string TestName { get; set; } = string.Empty;

    /// <summary>The lab's short summary, if they wrote one. Never a substitute for the file.</summary>
    [StringLength(1000)]
    public string? Summary { get; set; }

    /// <summary>The report. A PDF or a photograph of one, at most 10 MB.</summary>
    [Required]
    public IFormFile File { get; set; } = null!;
}

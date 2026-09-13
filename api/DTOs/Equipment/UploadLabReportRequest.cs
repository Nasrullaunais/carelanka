using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Equipment;

// A multipart form rather than JSON, because it carries the scanned report itself.
public class UploadLabReportRequest
{
    [Required]
    public Guid PatientId { get; set; }

    [Required]
    [StringLength(120, MinimumLength = 2)]
    public string TestName { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Summary { get; set; }

    [Required]
    public IFormFile File { get; set; } = null!;
}

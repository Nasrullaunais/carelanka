using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Equipment;

// Metadata only. The bytes are a separate request, so a list of thirty reports does not carry
// thirty PDFs.
public class LabReport
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public Guid PatientId { get; set; }

    [Required]
    public string TestName { get; set; } = string.Empty;

    public string? Summary { get; set; }

    [Required]
    public string FileName { get; set; } = string.Empty;

    [Required]
    public string ContentType { get; set; } = string.Empty;

    [Required]
    public int ByteSize { get; set; }

    [Required]
    public Guid UploadedByStaffId { get; set; }

    [Required]
    public DateTimeOffset CreatedAt { get; set; }
}

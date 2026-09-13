using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Equipment;

/// <summary>One finished laboratory result. Metadata only: the file itself is a separate request, so a list of thirty reports does not carry thirty PDFs.</summary>
public class LabReport
{
    [Required]
    public Guid Id { get; set; }

    /// <summary>Patient Management owns the patient; this is the id and nothing more.</summary>
    [Required]
    public Guid PatientId { get; set; }

    [Required]
    public string TestName { get; set; } = string.Empty;

    public string? Summary { get; set; }

    [Required]
    public string FileName { get; set; } = string.Empty;

    [Required]
    public string ContentType { get; set; } = string.Empty;

    /// <summary>So a ward sees how big it is before opening it on ward wifi.</summary>
    [Required]
    public int ByteSize { get; set; }

    /// <summary>Staff Management owns the person; this is the id and nothing more.</summary>
    [Required]
    public Guid UploadedByStaffId { get; set; }

    /// <summary>When the lab filed it. A report is never edited, so there is no updated_at.</summary>
    [Required]
    public DateTimeOffset CreatedAt { get; set; }
}

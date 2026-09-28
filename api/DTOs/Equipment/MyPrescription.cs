using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Equipment;

// What a patient sees of their own prescription. No staff ids, same reasoning as MyLabReport.
public class MyPrescription
{
    [Required]
    public Guid Id { get; set; }

    public string? Note { get; set; }

    public string? FileName { get; set; }

    public string? ContentType { get; set; }

    public int? ByteSize { get; set; }

    /// <summary>What the doctor typed directly, when this one has no photographed file.</summary>
    public string? Body { get; set; }

    [Required]
    public PrescriptionStatus Status { get; set; }

    public DateOnly? TokenDate { get; set; }

    public int? TokenNumber { get; set; }

    public DateTimeOffset? ReadyAt { get; set; }

    public DateTimeOffset? DeliveredAt { get; set; }

    public string? RejectionReason { get; set; }

    [Required]
    public DateTimeOffset CreatedAt { get; set; }
}

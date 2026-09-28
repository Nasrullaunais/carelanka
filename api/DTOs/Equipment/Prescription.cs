using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Equipment;

// The pharmacy's view: who sent it, so the medicine can be handed to the right person.
public class Prescription
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public Guid PatientId { get; set; }

    [Required]
    public string PatientCode { get; set; } = string.Empty;

    [Required]
    public string PatientName { get; set; } = string.Empty;

    public string? Note { get; set; }

    public string? FileName { get; set; }

    public string? ContentType { get; set; }

    public int? ByteSize { get; set; }

    /// <summary>What a doctor typed directly, in place of a photographed file.</summary>
    public string? Body { get; set; }

    /// <summary>Set only when a doctor wrote this one directly; null for a patient's own upload.</summary>
    public Guid? PrescribedByStaffId { get; set; }

    [Required]
    public PrescriptionStatus Status { get; set; }

    public DateOnly? TokenDate { get; set; }

    public int? TokenNumber { get; set; }

    public DateTimeOffset? ReadyAt { get; set; }

    public DateTimeOffset? DeliveredAt { get; set; }

    public string? RejectionReason { get; set; }

    [Required]
    public DateTimeOffset CreatedAt { get; set; }

    [Required]
    public DateTimeOffset UpdatedAt { get; set; }
}

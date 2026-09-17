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

    [Required]
    public string FileName { get; set; } = string.Empty;

    [Required]
    public string ContentType { get; set; } = string.Empty;

    [Required]
    public int ByteSize { get; set; }

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

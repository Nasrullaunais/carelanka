using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Equipment;

// What a patient sees of their own prescription. No staff ids, same reasoning as MyLabReport.
public class MyPrescription
{
    [Required]
    public Guid Id { get; set; }

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
}

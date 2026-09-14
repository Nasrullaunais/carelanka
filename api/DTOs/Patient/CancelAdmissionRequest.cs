using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

public class CancelAdmissionRequest
{
    [Required]
    [EnumDataType(typeof(CancelReason))]
    public CancelReason? Reason { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }
}

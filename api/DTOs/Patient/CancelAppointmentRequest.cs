using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Patient;

public class CancelAppointmentRequest
{
    /// <summary>
    /// Required, because the patient reads it. It is also where the desk names
    /// a time the clinic can actually see them.
    /// </summary>
    [Required]
    [MinLength(1)]
    [MaxLength(300)]
    public string Reason { get; set; } = null!;
}

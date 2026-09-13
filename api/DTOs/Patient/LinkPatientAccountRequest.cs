using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Patient;

public class LinkPatientAccountRequest
{
    [Required]
    public Guid UserAccountId { get; set; }
}

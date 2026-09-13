using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Patient;

public class AdmissionDetail : Admission
{
    [Required]
    public IReadOnlyList<BedAssignment> BedAssignments { get; set; } = Array.Empty<BedAssignment>();

    public Discharge? Discharge { get; set; }

    public Bill? Bill { get; set; }
}

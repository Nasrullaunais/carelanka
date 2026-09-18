using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Patient;

public class BedWorkflowAccepted
{
    [Required]
    public Guid WorkflowId { get; set; }

    /// <summary>
    /// Null when the run was started from an NIC or patient code and the agent has not resolved
    /// the patient yet. Populated once it has.
    /// </summary>
    public Guid? AdmissionId { get; set; }

    [Required]
    public string Status { get; set; } = null!;

    [Required]
    public string PollUrl { get; set; } = null!;
}

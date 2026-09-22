namespace CareLanka.Api.DTOs.Patient;

public sealed class BedWorkflowAccepted
{
    public Guid WorkflowId { get; set; }

    public Guid? AdmissionId { get; set; }

    public BedWorkflowStatus Status { get; set; }

    public string PollUrl { get; set; } = string.Empty;
}

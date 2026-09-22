using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Emergency;

public class DispatchProposal : AuditedEntity
{
    public Guid WorkflowId { get; set; }
    public Guid EmergencyCallId { get; set; }
    public EmergencyCall EmergencyCall { get; set; } = null!;
    public CallPriority CallPriority { get; set; }
    public DispatchProposalStatus Status { get; set; }
    public DispatchOutcome? Outcome { get; set; }
    public bool IsDiversion { get; set; }
    public bool AllowDiversion { get; set; }
    public string? ExcludeAmbulanceIdsJson { get; set; }
    public string? Rationale { get; set; }

    public Guid? ProposedAmbulanceId { get; set; }
    public int? EstimatedMinutesToScene { get; set; }

    public Guid? SourceDispatchId { get; set; }
    public Guid? SourceCallId { get; set; }
    public CallPriority? SourceCallPriority { get; set; }
    public string? SourceCallAddressLabel { get; set; }
    public DispatchStatus? SourceDispatchStatus { get; set; }
    public int? SourceCallWaitingMinutesSoFar { get; set; }
    public int? SourceCallAdditionalWaitMinutes { get; set; }
    public Guid? ReplacementAmbulanceId { get; set; }
    public int? MinutesSavedForThisCall { get; set; }

    public Guid? ResultingDispatchId { get; set; }
    public string? PreAdmissionSentJson { get; set; }

    public Guid? ReviewedByStaffMemberId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }
    public DispatchRejectionReason? RejectionReason { get; set; }
}

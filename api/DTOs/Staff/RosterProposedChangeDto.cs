using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Staff;

public class RosterProposedChangeDto
{
    public Guid Id { get; set; }

    public int Sequence { get; set; }

    public RosterProposedChangeType ChangeType { get; set; }

    public Guid? TargetAllocationId { get; set; }

    public Guid? ProposedStaffMemberId { get; set; }

    public Guid? StaffMemberId
    {
        get => ProposedStaffMemberId;
        set => ProposedStaffMemberId = value;
    }

    public string? ProposedStaffName { get; set; }

    public string? StaffName
    {
        get => ProposedStaffName;
        set => ProposedStaffName = value;
    }

    public Guid? ProposedShiftId { get; set; }

    public Guid? ToShiftId
    {
        get => ProposedShiftId;
        set => ProposedShiftId = value;
    }

    public Guid? FromShiftId { get; set; }

    public string? FromWardName { get; set; }

    public string? ToWardName { get; set; }

    public string? Rationale { get; set; }

    public ProposedChangeValidationStatus ValidationStatus { get; set; }

    public string? ValidationMessage { get; set; }

    public DateTimeOffset? AppliedAt { get; set; }

    public Guid? AppliedEntityId { get; set; }
}

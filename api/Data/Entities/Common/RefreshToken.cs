using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Common;

// Deviates from entity_diagram.md (recorded there in Rev 2.7): two nullable foreign keys plus
// PrincipalType, because common-spec.yaml says refresh works for staff and patients alike.
public class RefreshToken : Entity
{
    public PrincipalType PrincipalType { get; set; }

    public Guid? StaffMemberId { get; set; }

    public StaffMember? StaffMember { get; set; }

    public Guid? PatientAccountId { get; set; }

    public PatientAccount? PatientAccount { get; set; }

    public string TokenHash { get; set; } = null!;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public string? RevokedReason { get; set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    public Guid PrincipalId => PrincipalType == PrincipalType.Staff
        ? StaffMemberId!.Value
        : PatientAccountId!.Value;
}

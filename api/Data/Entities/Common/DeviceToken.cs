using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Common;

public class DeviceToken : AuditedEntity
{
    public Guid? StaffMemberId { get; set; }

    public StaffMember? StaffMember { get; set; }

    public Guid? PatientAccountId { get; set; }

    public PatientAccount? PatientAccount { get; set; }

    public string Token { get; set; } = null!;

    public DevicePlatform Platform { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }
}

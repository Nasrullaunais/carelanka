namespace CareLanka.Api.Data.Entities.Common;

/// <summary>
/// Persisted so a session can be revoked server-side — a lost phone or a staff member who
/// has left. Append-only apart from RevokedAt, which is why it stays on Entity.
/// The raw token never reaches the database; only its SHA-256 hash does.
/// </summary>
public class RefreshToken : Entity
{
    public required Guid StaffMemberId { get; set; }
    public StaffMember? StaffMember { get; set; }

    public required string TokenHash { get; set; }
    public required DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    public bool IsUsable(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;
}

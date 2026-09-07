using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Common;

/// <summary>
/// A session, stored so it can be ended server-side. The access token is stateless and
/// short-lived; this row is what <c>POST /auth/logout</c> actually kills.
/// <para>
/// <strong>Only the hash is stored.</strong> If the database leaks, the rows are not
/// usable credentials.
/// </para>
/// <para>
/// <strong>Deviation from <c>docs/entity_diagram.md</c>, recorded there in Rev 2.7:</strong>
/// the diagram gives this table a non-null <c>StaffMemberId</c>, which predates
/// <see cref="PatientAccount"/>. <c>specs/common-spec.yaml</c> says refresh works for both
/// identities, and the spec wins. So the row carries a <see cref="PrincipalType"/> and
/// exactly one of the two nullable foreign keys, enforced by a CHECK constraint — a
/// nullable FK each way keeps referential integrity that a bare <c>principal_id</c> Guid
/// would throw away.
/// </para>
/// </summary>
public class RefreshToken : Entity
{
    /// <summary>Which table <see cref="StaffMemberId"/> / <see cref="PatientAccountId"/> is in.</summary>
    public PrincipalType PrincipalType { get; set; }

    public Guid? StaffMemberId { get; set; }

    public StaffMember? StaffMember { get; set; }

    public Guid? PatientAccountId { get; set; }

    public PatientAccount? PatientAccount { get; set; }

    /// <summary>SHA-256 of the token the client holds. The token itself is never persisted.</summary>
    public string TokenHash { get; set; } = null!;

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// The one post-insert mutation, which is why this stays on <see cref="Entity"/> rather
    /// than gaining an <c>UpdatedAt</c> that would duplicate it.
    /// </summary>
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>Why the row died — rotated, logged out, or reuse detected. Read during an incident.</summary>
    public string? RevokedReason { get; set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    /// <summary>The principal this session belongs to, whichever table it is in.</summary>
    public Guid PrincipalId => PrincipalType == PrincipalType.Staff
        ? StaffMemberId!.Value
        : PatientAccountId!.Value;
}

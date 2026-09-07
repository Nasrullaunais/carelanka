namespace CareLanka.Api.Common.Auth;

/// <summary>
/// The claim names in a CareLanka token. Short, and the same four in every token.
/// </summary>
public static class CareLankaClaims
{
    /// <summary>The principal's id — a <c>StaffMember.Id</c> or a <c>PatientAccount.Id</c>.</summary>
    public const string Subject = "sub";

    /// <summary>One <c>PrincipalRole</c> value.</summary>
    public const string Role = "role";

    /// <summary>
    /// <c>staff</c> or <c>patient</c> — <strong>which table <c>sub</c> is in.</strong>
    /// <para>
    /// The claim it is easy to leave out and expensive to add back. <c>sub</c> alone is
    /// ambiguous: staff ids and patient-account ids are both GUIDs from different tables.
    /// An endpoint that trusts <c>sub</c> without checking this looks a patient id up in
    /// <c>staff_members</c>, finds nothing, and either 500s or silently treats the request
    /// as unauthenticated staff.
    /// </para>
    /// </summary>
    public const string PrincipalType = "typ";

    /// <summary>Token id, for traceability.</summary>
    public const string TokenId = "jti";
}

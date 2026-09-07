namespace CareLanka.Api.Data.Enums;

/// <summary>
/// Which table the JWT <c>sub</c> claim points at. Carried in the token as <c>typ</c> and
/// on every <c>refresh_tokens</c> row.
/// <para>
/// <c>sub</c> alone is ambiguous: staff ids and patient-account ids are both GUIDs from
/// different tables. An endpoint that trusts <c>sub</c> without checking <c>typ</c> looks a
/// patient id up in <c>staff_members</c>, finds nothing, and either 500s or silently treats
/// the request as unauthenticated staff.
/// </para>
/// </summary>
public enum PrincipalType
{
    Staff,
    Patient
}

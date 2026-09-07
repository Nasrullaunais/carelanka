namespace CareLanka.Api.Common.Errors;

/// <summary>
/// The machine-readable half of an error. Clients branch on this, never on the text, so
/// Sinhala or Tamil later is a resource-file change rather than a code change.
/// <para>
/// <strong>Prefixes are reserved per component and must not collide:</strong>
/// <c>cl_err_</c> shared (this file, group-owned), then <c>cl_emg_</c> Emergency,
/// <c>cl_stf_</c> Staff, <c>cl_equ_</c> Equipment, <c>cl_pat_</c> Patient. Add your own
/// codes in your own enum with your own prefix; do not extend this one.
/// </para>
/// The wire string is in <see cref="MessageCodes"/> and the human text in
/// <c>ErrorMessages.resx</c>, keyed by that same string.
/// </summary>
public enum MessageCode
{
    /// <summary>Validation failed. Accompanies a <c>ValidationProblemDetails</c>.</summary>
    ValidationFailed,

    /// <summary>
    /// Wrong password, unknown account, or deactivated account — deliberately
    /// indistinguishable, so login cannot be used to discover which emails exist.
    /// </summary>
    InvalidCredentials,

    /// <summary>No token, or a token that does not validate.</summary>
    NotAuthenticated,

    /// <summary>Authenticated, but this role may not do this.</summary>
    Forbidden,

    /// <summary>Generic not found. Parameters: entity type, id.</summary>
    NotFound,

    /// <summary>Generic conflict.</summary>
    Conflict,

    /// <summary>Not a legal status change. Parameters: entity type, from, to.</summary>
    IllegalTransition,

    /// <summary>Rate limited.</summary>
    TooManyRequests,

    /// <summary>Anything unhandled. The response carries no internal detail.</summary>
    Unexpected,

    /// <summary>Refresh token unknown, expired, or already used.</summary>
    RefreshTokenInvalid,

    /// <summary>That phone number already has an active patient account.</summary>
    PhoneNumberAlreadyRegistered
}

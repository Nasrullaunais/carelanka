namespace CareLanka.Api.Common.Messages;

/// <summary>
/// A stable, machine-readable code on every error response, in extensions["code"].
/// Clients branch on the code, never on the message text — that is what keeps the text
/// translatable into Sinhala and Tamil without touching any client.
///
/// Prefixes: cl_err_ shared · cl_emg_ Emergency · cl_stf_ Staff · cl_equ_ Equipment
///           cl_pat_ Patient. Add your component's codes to your own region.
/// Codes are permanent. Never renumber one; add a new one and stop using the old.
/// </summary>
public enum MessageCode
{
    // ---- Shared (cl_err_) -----------------------------------------------------
    cl_err_000_unexpected,
    cl_err_001_not_found,
    cl_err_002_validation_failed,
    cl_err_003_forbidden,
    cl_err_004_conflict,
    cl_err_005_illegal_transition,
    cl_err_006_bad_request,

    // ---- Auth (cl_err_1xx) ----------------------------------------------------
    cl_err_100_invalid_credentials,
    cl_err_101_account_inactive,
    cl_err_102_refresh_token_invalid,
    cl_err_103_email_already_registered,

    // ---- Emergency (cl_emg_) --------------------------------------------------

    // ---- Staff (cl_stf_) ------------------------------------------------------

    // ---- Equipment (cl_equ_) --------------------------------------------------

    // ---- Patient (cl_pat_) ----------------------------------------------------
}

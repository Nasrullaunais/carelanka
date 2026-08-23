namespace CareLanka.Api.Common.Messages;

/// <summary>
/// Human text for each MessageCode, parameterised with {0}, {1} and so on.
///
/// This is deliberately one flat dictionary rather than a .resx: a .resx is a single XML
/// file that four people editing on the same day would conflict on constantly. When we
/// actually add Sinhala and Tamil, this becomes the English resource file and nothing
/// outside this class changes.
///
/// A missing code is not an exception — it falls back to the code itself, so a forgotten
/// entry shows up as an ugly response rather than a 500.
/// </summary>
public static class MessageCatalog
{
    private static readonly Dictionary<MessageCode, string> Text = new()
    {
        [MessageCode.cl_err_000_unexpected] = "Something went wrong. Please try again.",
        [MessageCode.cl_err_001_not_found] = "{0} {1} was not found.",
        [MessageCode.cl_err_002_validation_failed] = "One or more fields are invalid.",
        [MessageCode.cl_err_003_forbidden] = "You do not have permission to do that.",
        [MessageCode.cl_err_004_conflict] = "{0}",
        [MessageCode.cl_err_005_illegal_transition] = "{0} {1} cannot move from {2} to {3}.",
        [MessageCode.cl_err_006_bad_request] = "{0}",

        [MessageCode.cl_err_100_invalid_credentials] = "Email or password is incorrect.",
        [MessageCode.cl_err_101_account_inactive] = "This account is no longer active.",
        [MessageCode.cl_err_102_refresh_token_invalid] = "Your session has expired. Please sign in again.",
        [MessageCode.cl_err_103_email_already_registered] = "{0} is already registered."
    };

    public static string Format(MessageCode code, params object?[] args)
    {
        if (!Text.TryGetValue(code, out var template)) return code.ToString();
        return args.Length == 0 ? template : string.Format(template, args);
    }
}

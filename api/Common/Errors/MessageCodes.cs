using System.Globalization;
using System.Resources;

namespace CareLanka.Api.Common.Errors;

/// <summary>
/// Turns a <see cref="MessageCode"/> into the string clients see in
/// <c>ProblemDetails.extensions["code"]</c>, and into human text.
/// <para>
/// The mapping is written out rather than derived from the enum name, because the wire
/// string is a published contract: <c>specs/common-spec.yaml</c> pins
/// <c>cl_err_401</c> by name. Renaming a C# enum member must not silently rename a code
/// four clients are branching on.
/// </para>
/// </summary>
public static class MessageCodes
{
    private static readonly ResourceManager Resources =
        new("CareLanka.Api.Common.Errors.ErrorMessages", typeof(MessageCodes).Assembly);

    private static readonly IReadOnlyDictionary<MessageCode, string> Wire =
        new Dictionary<MessageCode, string>
        {
            [MessageCode.ValidationFailed] = "cl_err_400",
            [MessageCode.InvalidCredentials] = "cl_err_401",
            [MessageCode.NotAuthenticated] = "cl_err_401_missing",
            [MessageCode.Forbidden] = "cl_err_403",
            [MessageCode.NotFound] = "cl_err_404",
            [MessageCode.Conflict] = "cl_err_409",
            [MessageCode.IllegalTransition] = "cl_err_409_transition",
            [MessageCode.TooManyRequests] = "cl_err_429",
            [MessageCode.Unexpected] = "cl_err_500",
            [MessageCode.RefreshTokenInvalid] = "cl_err_001",
            [MessageCode.PhoneNumberAlreadyRegistered] = "cl_err_002"
        };

    /// <summary>The published string, e.g. <c>cl_err_401</c>.</summary>
    public static string ToWire(this MessageCode code) => Wire[code];

    /// <summary>
    /// The human text for a code, with <c>{0}</c>-style parameters filled in. Falls back to
    /// the code itself if the resource is missing, so a forgotten entry is visible rather
    /// than blank.
    /// </summary>
    public static string ToText(this MessageCode code, params object?[] args)
    {
        var template = Resources.GetString(code.ToWire(), CultureInfo.CurrentUICulture);

        if (template is null)
        {
            return code.ToWire();
        }

        return args.Length == 0
            ? template
            : string.Format(CultureInfo.CurrentUICulture, template, args);
    }
}

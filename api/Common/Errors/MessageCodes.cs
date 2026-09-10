using System.Globalization;
using System.Resources;

namespace CareLanka.Api.Common.Errors;

public static class MessageCodes
{
    private static readonly ResourceManager Resources =
        new("CareLanka.Api.Common.Errors.ErrorMessages", typeof(MessageCodes).Assembly);

    // Written out rather than derived from the enum name: these strings are a published
    // contract, so renaming a C# member must not rename a code clients branch on.
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
            [MessageCode.PhoneNumberAlreadyRegistered] = "cl_err_002",
            [MessageCode.WardNameTaken] = "cl_pat_001",
            [MessageCode.PatientNicTaken] = "cl_pat_002",
            [MessageCode.PatientAlreadyHasAccount] = "cl_pat_003",
            [MessageCode.AccountAlreadyLinked] = "cl_pat_004",
            [MessageCode.TempReferenceExhausted] = "cl_pat_005",
            [MessageCode.PatientHasOpenAdmission] = "cl_pat_006",
            [MessageCode.DispatchIdRequired] = "cl_pat_007",
            [MessageCode.CategoryStaffNotFound] = "cl_pat_008"
        };

    public static string ToWire(this MessageCode code) => Wire[code];

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

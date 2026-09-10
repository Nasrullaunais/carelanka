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
            [MessageCode.BedNumberTaken] = "cl_equ_001",
            [MessageCode.AssetTagTaken] = "cl_equ_002",
            [MessageCode.BedOccupied] = "cl_equ_003",
            [MessageCode.CategoryNameTaken] = "cl_equ_004",
            [MessageCode.SerialNumberTaken] = "cl_equ_005",
            [MessageCode.EquipmentNotAvailable] = "cl_equ_006",
            [MessageCode.EquipmentNotAssigned] = "cl_equ_007",
            [MessageCode.PharmacyCategoryNameTaken] = "cl_equ_008",
            [MessageCode.PharmacyItemNameTaken] = "cl_equ_009",
            [MessageCode.InsufficientStock] = "cl_equ_010",
            [MessageCode.AdjustmentNeedsNote] = "cl_equ_011",
            [MessageCode.WardNameTaken] = "cl_pat_001"
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

using System.Globalization;
using System.Resources;

namespace CareLanka.Api.Common.Errors;

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
            [MessageCode.UsernameAlreadyTaken] = "cl_err_002",
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
            [MessageCode.MaintenanceNotCompletable] = "cl_equ_012",
            [MessageCode.EquipmentAwaitingRepair] = "cl_equ_013",
            [MessageCode.LabReportFileEmpty] = "cl_equ_014",
            [MessageCode.LabReportFileTooLarge] = "cl_equ_015",
            [MessageCode.LabReportFileType] = "cl_equ_016",
            [MessageCode.ConfirmationCodeIncorrect] = "cl_equ_017",
            [MessageCode.EquipmentAwaitingConfirmation] = "cl_equ_018",
            [MessageCode.EquipmentNotAwaitingConfirmation] = "cl_equ_019",
            [MessageCode.PrescriptionWrongStatus] = "cl_equ_020",
            [MessageCode.PrescriptionFileEmpty] = "cl_equ_021",
            [MessageCode.PrescriptionFileTooLarge] = "cl_equ_022",
            [MessageCode.PrescriptionFileType] = "cl_equ_023",
            [MessageCode.EquipmentRetireNeedsCode] = "cl_equ_024",
            [MessageCode.EquipmentRemoveNeedsRetired] = "cl_equ_025",
            [MessageCode.PharmacyReceiveIsABatch] = "cl_equ_026",
            [MessageCode.PharmacyRemoveNeedsEmpty] = "cl_equ_027",
            [MessageCode.WarningClosed] = "cl_equ_028",
            [MessageCode.WarningNotResolved] = "cl_equ_029",
            [MessageCode.EquipmentCategoryInUse] = "cl_equ_030",
            [MessageCode.WardNameTaken] = "cl_pat_001",
            [MessageCode.PatientNicTaken] = "cl_pat_002",
            [MessageCode.PatientAlreadyHasAccount] = "cl_pat_003",
            [MessageCode.AccountAlreadyLinked] = "cl_pat_004",
            [MessageCode.TempReferenceExhausted] = "cl_pat_005",
            [MessageCode.PatientHasOpenAdmission] = "cl_pat_006",
            [MessageCode.DispatchIdRequired] = "cl_pat_007",
            [MessageCode.CategoryStaffNotFound] = "cl_pat_008",
            [MessageCode.PatientHasOpenAppointment] = "cl_pat_009",
            [MessageCode.AppointmentInThePast] = "cl_pat_010",
            [MessageCode.CareLevelNeedsDutyManager] = "cl_pat_011",
            [MessageCode.BedNeedsDutyManager] = "cl_pat_012",
            [MessageCode.BedDowngradeNeedsDutyManager] = "cl_pat_013",
            [MessageCode.BedAlreadyClaimed] = "cl_pat_014",
            [MessageCode.BedOutOfService] = "cl_pat_015",
            [MessageCode.BedWardTooAcute] = "cl_pat_016",
            [MessageCode.BedWardGenderPolicy] = "cl_pat_017",
            [MessageCode.BedNeedsIsolation] = "cl_pat_018",
            [MessageCode.BedWardNotInService] = "cl_pat_019",
            [MessageCode.VisitNeedsDischargeNotComplete] = "cl_pat_020",
            [MessageCode.VisitNeedsNoBed] = "cl_pat_021",
            [MessageCode.ChecklistItemWrongRole] = "cl_pat_022",
            [MessageCode.DischargeChecklistIncomplete] = "cl_pat_023",
            [MessageCode.BillingTickedBySettlingOnly] = "cl_pat_025",
            [MessageCode.BillAlreadySettled] = "cl_pat_026",
            [MessageCode.BillLineNotRemovable] = "cl_pat_027",
            [MessageCode.BedNotAssigned] = "cl_pat_028",
            [MessageCode.BedAlreadyTheirs] = "cl_pat_029",
            [MessageCode.BedWardPediatricAdult] = "cl_pat_030",
            [MessageCode.AmbulanceRegistrationTaken] = "cl_emg_001",
            [MessageCode.AmbulanceHasActiveDispatch] = "cl_emg_002",
            [MessageCode.StaffNotAmbulanceCrew] = "cl_emg_003",
            [MessageCode.CrewMemberAlreadyAssigned] = "cl_emg_004",
            [MessageCode.AmbulanceNotEligible] = "cl_emg_005",
            [MessageCode.CallNotAwaitingDispatch] = "cl_emg_006",
            [MessageCode.NicLinkedToAnotherAccount] = "cl_pat_031",
            [MessageCode.NicDoesNotMatchYourRecord] = "cl_pat_032",
            [MessageCode.AccountHasNoPatientRecord] = "cl_pat_033",
            [MessageCode.NoCurrentAdmission] = "cl_pat_034",
            [MessageCode.AppointmentBilledOnItsAdmission] = "cl_pat_035",
            [MessageCode.NoBillRaised] = "cl_pat_036",
            [MessageCode.PatientCodeNotClaimable] = "cl_pat_037",
            // cl_pat_038 is reserved for the care advisory agent's entry point (build/patient.md
            // step 15). Left free rather than reused, because a retired or reassigned code is
            // how a client ends up branching on the wrong thing.
            [MessageCode.BedSuggestionNotPossible] = "cl_pat_039"
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

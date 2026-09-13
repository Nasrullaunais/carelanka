namespace CareLanka.Api.Common.Errors;

public enum MessageCode
{
    ValidationFailed,

    InvalidCredentials,

    NotAuthenticated,

    Forbidden,

    NotFound,

    Conflict,

    IllegalTransition,

    TooManyRequests,

    Unexpected,

    RefreshTokenInvalid,

    PhoneNumberAlreadyRegistered,

    BedNumberTaken,

    AssetTagTaken,

    BedOccupied,

    CategoryNameTaken,

    SerialNumberTaken,

    EquipmentNotAvailable,

    EquipmentNotAssigned,

    EquipmentAwaitingRepair,

    LabReportFileEmpty,

    LabReportFileTooLarge,

    LabReportFileType,

    PharmacyCategoryNameTaken,

    PharmacyItemNameTaken,

    InsufficientStock,

    AdjustmentNeedsNote,

    MaintenanceNotCompletable,

    WardNameTaken,

    PatientNicTaken,

    PatientAlreadyHasAccount,

    AccountAlreadyLinked,

    TempReferenceExhausted,

    PatientHasOpenAdmission,

    DispatchIdRequired,

    CategoryStaffNotFound,

    PatientHasOpenAppointment,

    AppointmentInThePast,

    CareLevelNeedsDutyManager,

    BedNeedsDutyManager,

    BedDowngradeNeedsDutyManager,

    BedAlreadyClaimed,

    BedOutOfService,

    BedWardTooAcute,

    BedWardGenderPolicy,

    BedNeedsIsolation,

    BedWardNotInService,

    VisitNeedsDischargeNotComplete,

    VisitNeedsNoBed,

    ChecklistItemWrongRole,

    DischargeChecklistIncomplete,

    BillingTickedBySettlingOnly,

    BillAlreadySettled,

    BillLineNotRemovable,

    BedNotAssigned,

    BedAlreadyTheirs,

    BedWardPediatricAdult,

    AmbulanceRegistrationTaken,

    AmbulanceHasActiveDispatch,

    NicLinkedToAnotherAccount,

    NicDoesNotMatchYourRecord,

    AccountHasNoPatientRecord,

    NoCurrentAdmission
}

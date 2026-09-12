namespace CareLanka.Api.Common.Errors;

public enum MessageCode
{
    ValidationFailed,

    // Also used for an unknown account and a deactivated one, so login cannot be used
    // to discover which emails exist.
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

    // Equipment

    BedNumberTaken,

    AssetTagTaken,

    BedOccupied,

    CategoryNameTaken,

    SerialNumberTaken,

    EquipmentNotAvailable,

    EquipmentNotAssigned,

    PharmacyCategoryNameTaken,

    PharmacyItemNameTaken,

    InsufficientStock,

    AdjustmentNeedsNote,

    MaintenanceNotCompletable,

    // Patient Management — cl_pat_*
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

    // Manual bed assignment. One code per hard rule, because "that bed will not work" tells a
    // nurse nothing about which other bed might.
    BedNeedsDutyManager,

    BedDowngradeNeedsDutyManager,

    BedAlreadyClaimed,

    BedOutOfService,

    BedWardTooAcute,

    BedWardGenderPolicy,

    BedNeedsIsolation,

    BedWardNotInService,

    // A visit that needs a bed cannot be finished with /complete. That is a discharge, and
    // discharge has a checklist, an approver and a bed to give back.
    VisitNeedsDischargeNotComplete,

    VisitNeedsNoBed,

    // Discharge, step 7.
    ChecklistItemWrongRole,

    DischargeChecklistIncomplete,

    // Billing. billing_settled is not a box anyone ticks by hand - settling the bill is what
    // writes it, so the money and the checklist cannot disagree.
    BillingTickedBySettlingOnly,

    BillAlreadySettled,

    BillLineNotRemovable,

    // Correcting a bed that was chosen by mistake.
    BedNotAssigned,

    BedAlreadyTheirs,

    // H6. A children's ward, and a patient who is not a child - or whose age nobody recorded.
    BedWardPediatricAdult
}

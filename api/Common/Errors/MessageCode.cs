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

    UsernameAlreadyTaken,

    CurrentPasswordIncorrect,

    PasswordChangeRequired,

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

    ConfirmationCodeIncorrect,

    EquipmentAwaitingConfirmation,

    EquipmentNotAwaitingConfirmation,

    PrescriptionWrongStatus,

    PrescriptionFileEmpty,

    PrescriptionFileTooLarge,

    PrescriptionFileType,

    EquipmentRetireNeedsCode,

    EquipmentRemoveNeedsRetired,

    PharmacyReceiveIsABatch,

    PharmacyRemoveNeedsEmpty,

    WarningClosed,

    WarningNotResolved,

    EquipmentCategoryInUse,

    ReorderSuggestionAlreadyRunning,

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

    PatientHasOpenAppointment,

    AppointmentInThePast,

    BedNeedsDutyManager,

    BedDowngradeNeedsDutyManager,

    BedAlreadyClaimed,

    BedOutOfService,

    BedWardTooAcute,

    BedWardGenderPolicy,

    BedNeedsIsolation,

    BedWardNotInService,

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

    StaffNotAmbulanceCrew,

    CrewMemberAlreadyAssigned,

    AmbulanceNotEligible,

    CallNotAwaitingDispatch,

    DispatchProposalConflict,

    DispatchProposalNotConfirmable,

    DispatchProposalNotApprovable,

    CallAlreadyDispatched,

    CallNotCancellable,

    CallHasNoLiveDispatch,

    CancellationAlreadyRequested,

    CancellationNotPending,

    AmbulanceNotRetired,

    AmbulanceRetired,

    RunPastPickup,

    AddressSearchUnavailable,

    CallClosed,

    NicLinkedToAnotherAccount,

    NicDoesNotMatchYourRecord,

    AccountHasNoPatientRecord,

    NoCurrentAdmission,

    AppointmentBilledOnItsAdmission,

    NoBillRaised,

    PatientCodeNotClaimable,

    NotCurrentlyAdmittedForCareQuery,

    CareRecommendationNotPendingReview,

    PreAdmissionAlreadyExists,

    AdmissionAlreadyClassified,

    AdmissionNotYetClassified,

    BillNotOpenForStay,

    AppointmentNotBillable,

    DischargeChecklistNotOnWard,

    BedWardWrongKind,

    MaternityNeedsFemalePatient,

    CareAgentStillDrafting,

    TooManyCareQueries,

    NicHasHospitalRecord,

    PatientIdentityLocked,

    PatientHasNoAppAccount,

    NicBirthYearImpossible,

    NicOldFormatAfter2000,

    NicBirthYearMismatch,

    PrescriptionHasNoFile
}

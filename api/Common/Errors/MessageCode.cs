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

    // Patient Management — cl_pat_*
    WardNameTaken,

    PatientNicTaken,

    PatientAlreadyHasAccount,

    AccountAlreadyLinked,

    TempReferenceExhausted,

    PatientHasOpenAdmission,

    DispatchIdRequired,

    CategoryStaffNotFound
}

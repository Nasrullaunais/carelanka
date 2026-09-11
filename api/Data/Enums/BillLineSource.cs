namespace CareLanka.Api.Data.Enums;

// Where a line on a bill came from. The difference is not cosmetic: preparing a bill again
// replaces every generated line and leaves every typed one alone, and only a typed one can be
// removed.
public enum BillLineSource
{
    // The one-off fee for opening the visit, priced from the care level.
    AdmissionFee,

    // A bed, for as many days as the patient was actually in it. One line per BedAssignment,
    // so a mid-stay transfer bills each ward at its own rate.
    BedStay,

    // Somebody at the desk typed it. Everything clinical is here, because no table in this
    // component records a treatment, a scan or a drug against an admission - see
    // patient-management-plan.md 6.5.
    Manual
}
